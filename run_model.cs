using Azure;
using Azure.AI.OpenAI;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using System.Text.Json;
using static System.Environment;
using System.Linq;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Ensure local dev config is loaded even if environment isn't set to Development
builder.Configuration
    .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

// Add logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

// CORS for local testing and static UI
builder.Services.AddCors(o =>
{
    o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

// Serve static files from wwwroot
builder.Services.AddDirectoryBrowser();

var app = builder.Build();

app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapPost("/api/chat", async (ChatRequest req, ILogger<Program> logger) =>
{
    logger.LogInformation("Received chat request with deployment: {Deployment}", req.deployment ?? "default");
    
    // Validate input
    if (string.IsNullOrWhiteSpace(req.prompt))
    {
        logger.LogWarning("Chat request rejected: empty prompt");
        return Results.BadRequest(new { error = "Prompt is required and cannot be empty." });
    }
    
    if (req.prompt.Length > 100000)
    {
        logger.LogWarning("Chat request rejected: prompt too long ({Length} chars)", req.prompt.Length);
        return Results.BadRequest(new { error = "Prompt exceeds maximum length of 10,000 characters." });
    }
    
    // Resolve configuration: prefer non-empty env vars, then appsettings
    var cfg = app.Configuration;
    var envEndpoint = GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")?.Trim();
    var envKey = GetEnvironmentVariable("AZURE_OPENAI_API_KEY")?.Trim();
    var envDeployment = GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT")?.Trim();

    string? endpoint = !string.IsNullOrWhiteSpace(envEndpoint) ? envEndpoint : cfg["AzureOpenAI:Endpoint"];
    string? key = !string.IsNullOrWhiteSpace(envKey) ? envKey : cfg["AzureOpenAI:ApiKey"];
    string deployment = !string.IsNullOrWhiteSpace(req.deployment)
        ? req.deployment!
        : (!string.IsNullOrWhiteSpace(envDeployment) ? envDeployment : (cfg["AzureOpenAI:Deployment"] ?? "gpt-4.1"));

    var missing = new List<string>();
    if (string.IsNullOrWhiteSpace(endpoint)) missing.Add("Endpoint");
    if (string.IsNullOrWhiteSpace(key)) missing.Add("ApiKey");
    if (missing.Count > 0)
    {
        logger.LogError("Missing Azure OpenAI configuration: {MissingFields}", string.Join(", ", missing));
        return Results.Problem($"Missing Azure OpenAI configuration: {string.Join(", ", missing)}.", statusCode: 500);
    }

    logger.LogDebug("Using deployment: {Deployment}", deployment);

    // Normalize endpoint to base resource URL expected by AzureOpenAIClient
    // At this point endpoint and key are guaranteed to be non-null due to validation above
    Uri endpointUri;
    try
    {
        endpointUri = new Uri(endpoint!);
        
        // Validate that the URI has a proper scheme and host
        if (!endpointUri.IsAbsoluteUri)
        {
            logger.LogError("Azure OpenAI endpoint is not an absolute URI: {Endpoint}", endpoint);
            return Results.Problem("Azure OpenAI endpoint must be an absolute URI (e.g., https://your-resource.openai.azure.com/).", statusCode: 500);
        }
        
        if (endpointUri.Scheme != "https" && endpointUri.Scheme != "http")
        {
            logger.LogError("Azure OpenAI endpoint has invalid scheme: {Scheme}", endpointUri.Scheme);
            return Results.Problem("Azure OpenAI endpoint must use http or https scheme.", statusCode: 500);
        }
        
        if (string.IsNullOrWhiteSpace(endpointUri.Host))
        {
            logger.LogError("Azure OpenAI endpoint has no host: {Endpoint}", endpoint);
            return Results.Problem("Azure OpenAI endpoint must have a valid host.", statusCode: 500);
        }
    }
    catch (UriFormatException ex)
    {
        logger.LogError(ex, "Invalid Azure OpenAI endpoint URL format: {Endpoint}", endpoint);
        return Results.Problem($"Invalid Azure OpenAI endpoint URL format: {ex.Message}", statusCode: 500);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Unexpected error parsing Azure OpenAI endpoint: {Endpoint}", endpoint);
        return Results.Problem($"Error parsing Azure OpenAI endpoint: {ex.Message}", statusCode: 500);
    }
    
    var baseEndpoint = new Uri(endpointUri.GetLeftPart(UriPartial.Authority) + "/");
    logger.LogDebug("Normalized endpoint to: {BaseEndpoint}", baseEndpoint);

    // Prefer env var overrides, then config, otherwise sane defaults
    float temperature = 0.7f;
    int maxTokens = 800;

    var envTemp = GetEnvironmentVariable("OPENAI_TEMPERATURE");
    var envMax = GetEnvironmentVariable("MAX_OUTPUT_TOKENS");

    if (!string.IsNullOrWhiteSpace(envTemp))
    {
        if (float.TryParse(envTemp, out var t) && t >= 0f && t <= 2f)
        {
            temperature = t;
            logger.LogDebug("Using temperature from env: {Temperature}", temperature);
        }
        else
        {
            logger.LogWarning("Invalid OPENAI_TEMPERATURE value '{Value}', must be between 0 and 2. Using default: {Default}", envTemp, temperature);
        }
    }
    else if (float.TryParse(cfg["AzureOpenAI:Temperature"], out var tc) && tc >= 0f && tc <= 2f)
    {
        temperature = tc;
        logger.LogDebug("Using temperature from config: {Temperature}", temperature);
    }

    if (!string.IsNullOrWhiteSpace(envMax))
    {
        if (int.TryParse(envMax, out var mt) && mt > 0 && mt <= 4096)
        {
            maxTokens = mt;
            logger.LogDebug("Using max tokens from env: {MaxTokens}", maxTokens);
        }
        else
        {
            logger.LogWarning("Invalid MAX_OUTPUT_TOKENS value '{Value}', must be between 1 and 4096. Using default: {Default}", envMax, maxTokens);
        }
    }
    else if (int.TryParse(cfg["AzureOpenAI:MaxOutputTokens"], out var mc) && mc > 0 && mc <= 4096)
    {
        maxTokens = mc;
        logger.LogDebug("Using max tokens from config: {MaxTokens}", maxTokens);
    }

    var credential = new AzureKeyCredential(key!);
    AzureOpenAIClient azureClient;
    ChatClient chatClient;
    
    try
    {
        azureClient = new AzureOpenAIClient(baseEndpoint, credential);
        chatClient = azureClient.GetChatClient(deployment);
        logger.LogDebug("Successfully created Azure OpenAI client");
    }
    catch (ArgumentException ex)
    {
        logger.LogError(ex, "Invalid argument when creating Azure OpenAI client");
        return Results.Problem($"Configuration error: {ex.Message}", statusCode: 500);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to create Azure OpenAI client");
        return Results.Problem($"Failed to initialize Azure OpenAI client: {ex.Message}", statusCode: 500);
    }

    var messages = new List<ChatMessage>();
    if (!string.IsNullOrWhiteSpace(req.system))
    {
        messages.Add(new SystemChatMessage(req.system!));
        logger.LogDebug("Added system message");
    }
    messages.Add(new UserChatMessage(req.prompt));

    var options = new ChatCompletionOptions
    {
        Temperature = temperature,
        MaxOutputTokenCount = maxTokens,
        TopP = 0.95f,
        FrequencyPenalty = 0f,
        PresencePenalty = 0f,
    };

    logger.LogInformation("Calling Azure OpenAI with temperature={Temperature}, maxTokens={MaxTokens}", temperature, maxTokens);

    try
    {
        ChatCompletion completion = await chatClient.CompleteChatAsync(messages, options);

        if (completion == null)
        {
            logger.LogError("Received null completion from Azure OpenAI");
            return Results.Problem("Received null response from Azure OpenAI.", statusCode: 500);
        }

        var parts = completion.Content;
        if (parts == null)
        {
            logger.LogWarning("Completion content is null");
            return Results.Ok(new ChatResponse(string.Empty, completion));
        }
        
        string text = string.Join("\n\n", parts.Select(p => p.Text).Where(t => !string.IsNullOrWhiteSpace(t)));
        
        logger.LogInformation("Successfully completed chat request. Response length: {Length} chars", text.Length);
        
        return Results.Ok(new ChatResponse(text, completion!));
    }
    catch (RequestFailedException ex)
    {
        // Azure-specific exception with status code and error details
        logger.LogError(ex, "Azure OpenAI request failed with status {Status}: {Message}", ex.Status, ex.Message);
        
        var statusCode = ex.Status switch
        {
            401 => 500, // Authentication error - don't expose to client
            429 => 429, // Rate limit
            >= 400 and < 500 => 400, // Client error
            _ => 500 // Server error
        };
        
        return Results.Problem(
            detail: $"Azure OpenAI request failed: {ex.Message}",
            statusCode: statusCode,
            title: "Chat Request Failed"
        );
    }
    catch (OperationCanceledException ex)
    {
        logger.LogWarning(ex, "Chat request was cancelled");
        return Results.Problem("Request was cancelled or timed out.", statusCode: 408);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Unexpected error during chat completion: {ExceptionType}", ex.GetType().Name);
        return Results.Problem($"Unexpected error: {ex.GetType().Name} - {ex.Message}", statusCode: 500);
    }
});

// Health check
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
app.MapHealthChecks("/health");

app.Run();

// DTOs (must appear after top-level statements)
public record ChatRequest(string? system, string prompt, string? deployment);
public record ChatResponse(string text, object raw);
