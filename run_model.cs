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

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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
    var endpoint = GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
    var key = GetEnvironmentVariable("AZURE_OPENAI_API_KEY");
    var deployment = req.deployment ?? GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT") ?? "gpt-4.1";

    if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(key))
    {
        return Results.Problem("Missing AZURE_OPENAI_ENDPOINT or AZURE_OPENAI_API_KEY env vars.", statusCode: 500);
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

    var options = new ChatCompletionOptions
    {
        Temperature = 0.7f,
        MaxOutputTokenCount = 800,
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

app.Run();

// DTOs (must appear after top-level statements)
public record ChatRequest(string? system, string prompt, string? deployment);
public record ChatResponse(string text, object raw);
