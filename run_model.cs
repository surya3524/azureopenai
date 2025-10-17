using Azure;
using Azure.AI.OpenAI;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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

app.MapPost("/api/chat", async (ChatRequest req) =>
{
    // Resolve configuration: prefer non-empty env vars, then appsettings
    var cfg = app.Configuration;
    var envEndpoint = GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")?.Trim();
    var envKey = GetEnvironmentVariable("AZURE_OPENAI_API_KEY")?.Trim();
    var envDeployment = GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT")?.Trim();

    // Prefer env vars, then config, then local debug fallback
    string endpoint = !string.IsNullOrWhiteSpace(envEndpoint)
        ? envEndpoint
        : cfg["AzureOpenAI:Endpoint"] ?? "https://localhost:1234/openai";
    string key = !string.IsNullOrWhiteSpace(envKey)
        ? envKey
        : cfg["AzureOpenAI:ApiKey"] ?? "local-debug-key";
    string deployment = !string.IsNullOrWhiteSpace(req.deployment)
        ? req.deployment!
        : (!string.IsNullOrWhiteSpace(envDeployment)
            ? envDeployment
            : (cfg["AzureOpenAI:Deployment"] ?? "gpt-4.1"));

    var missing = new List<string>();
    if (string.IsNullOrWhiteSpace(endpoint)) missing.Add("Endpoint");
    if (string.IsNullOrWhiteSpace(key)) missing.Add("ApiKey");
    if (missing.Count > 0)
    {
        return Results.Problem($"Missing Azure OpenAI configuration: {string.Join(", ", missing)}.", statusCode: 500);
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
    var baseEndpoint = new Uri(endpointUri.GetLeftPart(UriPartial.Authority) + "/");

    var credential = new AzureKeyCredential(key);
    var azureClient = new AzureOpenAIClient(baseEndpoint, credential);
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

    try
    {
        ChatCompletion completion = await chatClient.CompleteChatAsync(messages, options);

        var parts = completion.Content; // IEnumerable<ChatMessageContentPart>
        string text = string.Join("\n\n", parts.Select(p => p.Text).Where(t => !string.IsNullOrWhiteSpace(t)));

        return Results.Ok(new ChatResponse(text, completion!));
    }
    catch (Exception ex)
    {
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
