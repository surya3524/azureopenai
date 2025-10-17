using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenAI.Chat;
using System.Text.Json;
using static System.Environment;
using System.Linq;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

// Ensure local dev config is loaded even if environment isn't set to Development
builder.Configuration
    .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

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
var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("ApiLogger");

app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapPost("/api/chat", async (ChatRequest req) =>
{
    // Resolve configuration: prefer non-empty env vars, then appsettings
    var cfg = app.Configuration;
    var envEndpoint = GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")?.Trim();
    var envKey = GetEnvironmentVariable("AZURE_OPENAI_API_KEY")?.Trim();
    var envDeployment = GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT")?.Trim();
    var envUseEntraId = GetEnvironmentVariable("USE_ENTRA_ID")?.Trim();

    // Prefer env vars, then config
    string endpoint = !string.IsNullOrWhiteSpace(envEndpoint)
        ? envEndpoint
        : cfg["AzureOpenAI:Endpoint"] ?? "https://rs-sm-az-openai.openai.azure.com/";
    
    string? key = !string.IsNullOrWhiteSpace(envKey)
        ? envKey
        : cfg["AzureOpenAI:ApiKey"];
    
    string deployment = !string.IsNullOrWhiteSpace(req.deployment)
        ? req.deployment!
        : (!string.IsNullOrWhiteSpace(envDeployment)
            ? envDeployment
            : (cfg["AzureOpenAI:Deployment"] ?? "gpt-4.1"));

    // Determine authentication mode: if USE_ENTRA_ID=true or no API key provided, use Entra ID
    bool useEntraId = (!string.IsNullOrWhiteSpace(envUseEntraId) && envUseEntraId.Equals("true", StringComparison.OrdinalIgnoreCase))
                      || string.IsNullOrWhiteSpace(key);

    if (string.IsNullOrWhiteSpace(endpoint))
    {
        return Results.Problem("Missing Azure OpenAI Endpoint configuration.", statusCode: 500);
    }

    // Normalize endpoint to base resource URL expected by AzureOpenAIClient
    Uri endpointUri;
    try
    {
        endpointUri = new Uri(endpoint);
    }
    catch
    {
        return Results.Problem("Invalid Azure OpenAI endpoint URL.", statusCode: 500);
    }

    AzureOpenAIClient azureClient;
    
    if (useEntraId)
    {
        logger.LogInformation("Using Entra ID (DefaultAzureCredential) for authentication");
        var credential = new DefaultAzureCredential();
        azureClient = new AzureOpenAIClient(endpointUri, credential);
    }
    else
    {
        logger.LogInformation("Using API Key for authentication");
        var credential = new AzureKeyCredential(key!);
        azureClient = new AzureOpenAIClient(endpointUri, credential);
    }

    var chatClient = azureClient.GetChatClient(deployment);

    var messages = new List<ChatMessage>();
    if (!string.IsNullOrWhiteSpace(req.system))
    {
        messages.Add(new SystemChatMessage(req.system!));
    }
    messages.Add(new UserChatMessage(req.prompt));

    // Prefer env var overrides, then config, otherwise sane defaults
    float temperature = 0.7f;
    int maxTokens = 800;

    var envTemp = GetEnvironmentVariable("OPENAI_TEMPERATURE");
    var envMax = GetEnvironmentVariable("MAX_OUTPUT_TOKENS");

    if (!string.IsNullOrWhiteSpace(envTemp) && float.TryParse(envTemp, out var t))
    {
        temperature = t;
    }
    else if (float.TryParse(cfg["AzureOpenAI:Temperature"], out var tc))
    {
        temperature = tc;
    }

    if (!string.IsNullOrWhiteSpace(envMax) && int.TryParse(envMax, out var mt))
    {
        maxTokens = mt;
    }
    else if (int.TryParse(cfg["AzureOpenAI:MaxOutputTokens"], out var mc))
    {
        maxTokens = mc;
    }

    var options = new ChatCompletionOptions
    {
        Temperature = temperature,
        MaxOutputTokenCount = maxTokens,
        TopP = 0.95f,
        FrequencyPenalty = 0f,
        PresencePenalty = 0f,
    };

    logger.LogInformation("API Call: /api/chat");
    logger.LogInformation("  Endpoint: {Endpoint}", endpointUri);
    logger.LogInformation("  Authentication: {AuthType}", useEntraId ? "Entra ID" : "API Key");
    logger.LogInformation("  Temperature: {Temperature}", temperature);
    logger.LogInformation("  MaxTokens: {MaxTokens}", maxTokens);
    logger.LogInformation("  Deployment: {Deployment}", deployment);

    try
    {
        ChatCompletion completion = await chatClient.CompleteChatAsync(messages, options);

        var parts = completion.Content; // IEnumerable<ChatMessageContentPart>
        string text = string.Join("\n\n", parts.Select(p => p.Text).Where(t => !string.IsNullOrWhiteSpace(t)));

        return Results.Ok(new ChatResponse(text, completion!));
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Chat API error");
        return Results.Problem($"Chat error: {ex.Message}", statusCode: 500);
    }
});

// Health check
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
app.MapHealthChecks("/health");

app.Run();

// DTOs (must appear after top-level statements)
public record ChatRequest(string? system, string prompt, string? deployment);
public record ChatResponse(string text, object raw);
