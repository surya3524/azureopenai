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
    string endpoint;
        string key;
        string deployment;
        
        // Prefer config file in Development; env vars otherwise
        if (app.Environment.IsDevelopment())
        {
            var cfg = app.Configuration;
            endpoint = cfg["AzureOpenAI:Endpoint"];
            key = cfg["AzureOpenAI:ApiKey"];
            deployment = req.deployment ?? cfg["AzureOpenAI:Deployment"] ?? "gpt-4.1";
        }
        else
        {
            endpoint = GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
            key = GetEnvironmentVariable("AZURE_OPENAI_API_KEY");
            deployment = req.deployment ?? GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT") ?? "gpt-4.1";
        }
        
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(key))
        {
            return Results.Problem("Missing Azure OpenAI configuration.", statusCode: 500);
        }
        
        var credential = new AzureKeyCredential(key);
        var azureClient = new AzureOpenAIClient(new Uri(endpoint), credential);
        var chatClient = azureClient.GetChatClient(deployment);
        
        var messages = new List<ChatMessage>();
        if (!string.IsNullOrWhiteSpace(req.system))
        {
            messages.Add(new SystemChatMessage(req.system!));
        }
        messages.Add(new UserChatMessage(req.prompt));
        
        // Allow overriding options from config in Development; env vars otherwise
        float temperature = 0.7f;
        int maxTokens = 800;
        
        if (app.Environment.IsDevelopment())
        {
            float.TryParse(app.Configuration["AzureOpenAI:Temperature"], out temperature);
            int.TryParse(app.Configuration["AzureOpenAI:MaxOutputTokens"], out maxTokens);
        }
        else
        {
            if (float.TryParse(GetEnvironmentVariable("OPENAI_TEMPERATURE"), out var t))
            {
                temperature = t;
            }
            if (int.TryParse(GetEnvironmentVariable("MAX_OUTPUT_TOKENS"), out var mt))
            {
                maxTokens = mt;
            }
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

    // Extract assistant text for convenience from content parts
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
