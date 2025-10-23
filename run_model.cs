using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenAI.Chat;
using System.Net;
using System.Net.Mail;
using System.Text.Json;
using static System.Environment;
using System.Linq;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Reflection;

// Helper: NormalizeExceptionLogs to remove PII and sensitive data
static string NormalizeExceptionLogs(string input)
{
    if (string.IsNullOrWhiteSpace(input))
        return input;

    // Normalize line endings and trim
    string output = input.Replace("\r\n", "\n").Replace("\r", "\n").Trim();

    // 1. Redact email addresses (before extracting names from them)
    output = System.Text.RegularExpressions.Regex.Replace(output, 
        @"\b[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}\b", 
        "[REDACTED_EMAIL]");

    // 2. Redact URLs (http/https/ftp)
    output = System.Text.RegularExpressions.Regex.Replace(output, 
        @"\b(?:https?|ftp)://[^\s<>""']+", 
        "[REDACTED_URL]");
    
    // Redact www URLs without protocol
    output = System.Text.RegularExpressions.Regex.Replace(output, 
        @"\bwww\.[^\s<>""']+", 
        "[REDACTED_URL]");

    // 3. Redact file paths (Windows and Unix) - ENHANCED
    // Windows absolute paths (C:\, D:\, etc.)
    output = System.Text.RegularExpressions.Regex.Replace(output, 
        @"[a-zA-Z]:\\(?:[^\s<>:""|?*\n]+\\)*[^\s<>:""|?*\n]*", 
        "[REDACTED_PATH]");
    
    // Unix/Linux absolute paths (/home/, /usr/, /var/, etc.)
    output = System.Text.RegularExpressions.Regex.Replace(output, 
        @"(?:^|[\s(])(\/(?:home|usr|var|opt|etc|tmp|root|mnt|proc|sys|dev|boot|lib|bin|sbin)(?:\/[^\s<>:""|?*\n]+)*)", 
        "$1[REDACTED_PATH]");
    
    // Unix relative paths (./path or ../path)
    output = System.Text.RegularExpressions.Regex.Replace(output, 
        @"(?:\.{1,2}\/)+[^\s<>:""|?*\n]+", 
        "[REDACTED_PATH]");
    
    // Network UNC paths (\\server\share\path)
    output = System.Text.RegularExpressions.Regex.Replace(output, 
        @"\\\\[^\s\\]+(?:\\[^\s\\]+)+", 
        "[REDACTED_PATH]");
    
    // File paths in stack traces (at line indicators)
    output = System.Text.RegularExpressions.Regex.Replace(output, 
        @"in\s+(?:[a-zA-Z]:\\|\/)[^\s:]+(?=:line\s+\d+)", 
        "in [REDACTED_PATH]");
    
    // Common path patterns in error messages
    output = System.Text.RegularExpressions.Regex.Replace(output, 
        @"(?:file|path|directory|folder|location)[\s:=]+[""']?(?:[a-zA-Z]:\\|\/)[^\s""'<>\n]+[""']?", 
        match => {
            var prefix = System.Text.RegularExpressions.Regex.Match(match.Value, @"^(?:file|path|directory|folder|location)[\s:=]+[""']?").Value;
            return prefix + "[REDACTED_PATH]";
        },
        System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    // 4. Redact IP addresses (IPv4 and IPv6)
    output = System.Text.RegularExpressions.Regex.Replace(output, 
        @"\b(?:\d{1,3}\.){3}\d{1,3}\b", 
        "[REDACTED_IP]");
    output = System.Text.RegularExpressions.Regex.Replace(output, 
        @"\b(?:[0-9a-fA-F]{1,4}:){7}[0-9a-fA-F]{1,4}\b", 
        "[REDACTED_IPv6]");

    // 5. Redact GUIDs/UUIDs
    output = System.Text.RegularExpressions.Regex.Replace(output, 
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b", 
        "[REDACTED_GUID]");

    // 6. Redact timestamps (various formats)
    output = System.Text.RegularExpressions.Regex.Replace(output, 
        @"\d{4}-\d{2}-\d{2}[T\s]\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:?\d{2})?", 
        "[TIMESTAMP]");

    // 7. Redact memory addresses
    output = System.Text.RegularExpressions.Regex.Replace(output, 
        @"0x[0-9a-fA-F]{8,16}", 
        "[MEMADDR]");

    // 8. Redact sensitive keys, tokens, passwords
    output = System.Text.RegularExpressions.Regex.Replace(output, 
        @"(?i)(api[_-]?key|access[_-]?token|secret[_-]?key|password|auth[_-]?token|bearer)\s*[:=]\s*[^\s&""']+", 
        "$1=[REDACTED]", 
        System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    // 9. Redact credit card numbers
    output = System.Text.RegularExpressions.Regex.Replace(output, 
        @"\b\d{4}[\s-]?\d{4}[\s-]?\d{4}[\s-]?\d{4}\b", 
        "[REDACTED_CC]");

    // 10. Redact social security numbers (US format)
    output = System.Text.RegularExpressions.Regex.Replace(output, 
        @"\b\d{3}-\d{2}-\d{4}\b", 
        "[REDACTED_SSN]");

    // 11. Redact phone numbers
    output = System.Text.RegularExpressions.Regex.Replace(output, 
        @"\b(?:\+?1[-.]?)?\(?\d{3}\)?[-.\s]?\d{3}[-.\s]?\d{4}\b", 
        "[REDACTED_PHONE]");

    // 12. Redact usernames in paths or URLs
    output = System.Text.RegularExpressions.Regex.Replace(output, 
        @"(?i)(?:\/users?\/|\\users?\\|user=|username=)([^\s\/\\&""']+)", 
        "$1[REDACTED_USER]", 
        System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    // 13. Redact database connection strings
    output = System.Text.RegularExpressions.Regex.Replace(output, 
        @"(?i)(server|host|data source|database|uid|user id|password|pwd)\s*=\s*[^;]+", 
        "$1=[REDACTED]", 
        System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    // 14. Redact session IDs and JWT tokens
    output = System.Text.RegularExpressions.Regex.Replace(output, 
        @"(?i)(session[_-]?id|jsessionid|phpsessid|asp\.net_sessionid)\s*[:=]\s*[^\s&""']+", 
        "$1=[REDACTED]", 
        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    output = System.Text.RegularExpressions.Regex.Replace(output, 
        @"\beyJ[a-zA-Z0-9_-]+\.eyJ[a-zA-Z0-9_-]+\.[a-zA-Z0-9_-]+\b", 
        "[REDACTED_JWT]");

    // 15. Redact AWS keys
    output = System.Text.RegularExpressions.Regex.Replace(output, 
        @"\b(AKIA[0-9A-Z]{16})\b", 
        "[REDACTED_AWS_KEY]");

    // 16. Redact private keys
    output = System.Text.RegularExpressions.Regex.Replace(output, 
        @"-----BEGIN\s+(?:RSA\s+)?PRIVATE\s+KEY-----[\s\S]*?-----END\s+(?:RSA\s+)?PRIVATE\s+KEY-----", 
        "[REDACTED_PRIVATE_KEY]");

// 17. Redact person names (common patterns)
    // Pattern: "User: FirstName LastName", "Author: Name", "by FirstName LastName", etc.
    output = System.Text.RegularExpressions.Regex.Replace(output, 
        @"(?i)\b(user|author|created\s+by|modified\s+by|developer|owner|by|name)[\s:]+([A-Z][a-z]+(?:\s+[A-Z][a-z]+)+)\b", 
        "$1: [REDACTED_NAME]", 
        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    
    // Pattern: "FirstName.LastName" or "firstname.lastname" in usernames/emails (already redacted above)
    // Pattern: Capitalized names in error messages like "Error by John Smith"
    output = System.Text.RegularExpressions.Regex.Replace(output, 
        @"\b([A-Z][a-z]{2,})\s+([A-Z][a-z]{2,}(?:\s+[A-Z][a-z]{2,})?)\b", 
        match => {
            // Avoid redacting common technical terms, exception types, class names
            string matched = match.Value;
            string[] technicalTerms = { 
                "System", "Exception", "Error", "Warning", "Info", "Debug", "Trace",
                "Microsoft", "Azure", "Windows", "Linux", "Server", "Client", "Database",
                "Application", "Service", "Process", "Thread", "Task", "Method", "Class",
                "File", "Directory", "Network", "Connection", "Request", "Response",
                "Null", "Reference", "Argument", "Invalid", "Access", "Denied", "Not", "Found"
            };
            
            foreach (var term in technicalTerms)
            {
                if (matched.Contains(term, StringComparison.OrdinalIgnoreCase))
                    return matched;
            }
            
            // If it looks like a person's name (2-3 capitalized words, each 3+ chars), redact it
            var words = matched.Split(' ');
            if (words.Length >= 2 && words.Length <= 3 && 
                words.All(w => w.Length >= 3 && char.IsUpper(w[0])))
            {
                return "[REDACTED_NAME]";
            }
            
            return matched;
        });

    // 18. Limit stack traces to top 10 frames for consistency
    var lines = output.Split('\n');
    var stackFrameCount = 0;
    var resultLines = new List<string>();
    bool inStackTrace = false;
    
    foreach (var line in lines)
    {
        var trimmed = line.Trim();
        
        if (trimmed.StartsWith("at ") || trimmed.Contains(":line"))
        {
            if (!inStackTrace)
            {
                inStackTrace = true;
                stackFrameCount = 0;
            }
            
            if (stackFrameCount < 10)
            {
                resultLines.Add(line);
                stackFrameCount++;
            }
            else if (stackFrameCount == 10)
            {
                resultLines.Add("   ... [remaining stack frames omitted]");
                stackFrameCount++;
            }
        }
        else
        {
            inStackTrace = false;
            resultLines.Add(line);
        }
    }
    
    output = string.Join("\n", resultLines);

    // 19. Truncate to reasonable length
    const int maxLength = 20000;
    if (output.Length > maxLength)
    {
        output = output.Substring(0, maxLength) + "\n\n[Log truncated for processing - original length: " + input.Length + " chars]";
    }
    
    return output;
}

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

// NEW: Main endpoint for ServiceNow to send exception logs for analysis
app.MapPost("/api/analyze-exception", async (ExceptionAnalysisRequest req) =>
{
    logger.LogInformation("Received exception analysis request from ServiceNow");
    logger.LogInformation("  Job Name: {JobName}", req.JobName ?? "N/A");
    logger.LogInformation("  Environment: {Environment}", req.Environment ?? "N/A");
    logger.LogInformation("  Developer Email: {Email}", req.DeveloperEmail);

    if (string.IsNullOrWhiteSpace(req.ExceptionLogs))
    {
        return Results.BadRequest(new { error = "ExceptionLogs is required" });
    }

    if (string.IsNullOrWhiteSpace(req.DeveloperEmail))
    {
        return Results.BadRequest(new { error = "DeveloperEmail is required for sending results" });
    }

    try
    {
        // Step 1: Analyze the exception using Azure OpenAI
        var analysisResult = await AnalyzeExceptionWithAI(req, logger, app.Configuration);
        
        if (analysisResult == null)
        {
            return Results.Problem("Failed to analyze exception with AI", statusCode: 500);
        }

        // Step 2: Send email with the analysis
        var emailResult = await SendAnalysisEmail(req, analysisResult, logger, app.Configuration);
        
        if (!emailResult.Success)
        {
            logger.LogWarning("Analysis completed but email failed to send: {Error}", emailResult.Error);
            return Results.Ok(new
            {
                status = "partial_success",
                analysis = analysisResult,
                emailSent = false,
                emailError = emailResult.Error,
                message = "Exception analysis completed but email delivery failed. See analysis results below."
            });
        }

        logger.LogInformation("Exception analysis completed and email sent successfully");
        return Results.Ok(new
        {
            status = "success",
            analysis = analysisResult,
            emailSent = true,
            message = "Exception analyzed and results emailed to developer"
        });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error processing exception analysis request");
        return Results.Problem($"Analysis error: {ex.Message}", statusCode: 500);
    }
});

// Keep original chat endpoint for testing
app.MapPost("/api/chat", async (ChatRequest req) =>
{
    // Resolve configuration: prefer non-empty env vars, then appsettings
    var cfg = app.Configuration;
    var envEndpoint = GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")?.Trim();
    var envKey = GetEnvironmentVariable("AZURE_OPENAI_API_KEY")?.Trim();
    var envDeployment = "gpt-4.1";
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

    var defaultSystemPrompt = @"You are an expert DevOps AI assistant specializing in analyzing BMC job logs and troubleshooting production issues in enterprise environments.

**YOUR ROLE**: Provide technical analysis for on-call incidents, production failures, system errors, and infrastructure issues.

**YOU SHOULD ANALYZE**:
- BMC job failures and exceptions
- Production system errors and logs
- Infrastructure and deployment issues
- Database connection problems
- Network and API failures
- Application crashes and performance issues
- Service outages and incidents
- Configuration and permission errors

**YOU SHOULD NOT RESPOND TO**:
- General knowledge questions
- Non-technical inquiries
- Personal advice or opinions
- Programming tutorials unrelated to troubleshooting
- Requests outside of incident management and production support

If the user submits a non-technical or general question, politely decline with:
""I'm specialized in analyzing production incidents and technical errors for on-call support. Please provide BMC job logs, error messages, or technical issues that need troubleshooting.""

**RESPONSE FORMAT** (for technical issues only):
- **SUMMARY**: Brief overview of the issue
- **ROOT CAUSE**: The underlying technical problem
- **RECOMMENDED ACTIONS**: Step-by-step fixes with technical details and code snippets that developers can use to troubleshoot. show most confident ones on the top along with confidence score. Show risk status with each suggested action (Low, Medium, High). Also include details for Incident Mangement team so that they can triage it effectively and understand the issue and prediction based on your best estimate how long this might take to resolve.
- **PREVENTION**: How to avoid this in the future

Be concise, technical, and helpful. This is a one-time analysis - provide a complete, self-contained response without asking for additional information or suggesting follow-up tasks. Do not prompt the user to provide more details or ask if they need further assistance.";

    var messages = new List<ChatMessage>();
    
    if (!string.IsNullOrWhiteSpace(req.system))
    {
        messages.Add(new SystemChatMessage(req.system!));
    }
    else
    {
        messages.Add(new SystemChatMessage(defaultSystemPrompt));
    }
    
    // Normalize the user prompt to remove PII/volatile data
    string originalPrompt = req.prompt ?? "";
    string normalizedPrompt = NormalizeExceptionLogs(originalPrompt);
    bool wasRedacted = originalPrompt != normalizedPrompt;
    
    // Log the redaction details
    logger.LogInformation("=== REDACTION CHECK ===");
    logger.LogInformation("Original Length: {OrigLen}", originalPrompt.Length);
    logger.LogInformation("Normalized Length: {NormLen}", normalizedPrompt.Length);
    logger.LogInformation("Original: {Orig}", originalPrompt.Length > 100 ? originalPrompt.Substring(0, 100) + "..." : originalPrompt);
    logger.LogInformation("Normalized: {Norm}", normalizedPrompt.Length > 100 ? normalizedPrompt.Substring(0, 100) + "..." : normalizedPrompt);
    logger.LogInformation("WasRedacted: {WasRedacted}", wasRedacted);
    
    messages.Add(new UserChatMessage(normalizedPrompt));

    // Use config values with defaults
    float temperature = 0f; // Default
    int maxTokens = 6000;   // Default

    // Priority: Request parameters > Environment variables > Config > Defaults
    if (req.temperature.HasValue)
    {
        temperature = req.temperature.Value;
    }
    else
    {
        var envTemp = GetEnvironmentVariable("OPENAI_TEMPERATURE");
        if (!string.IsNullOrWhiteSpace(envTemp) && float.TryParse(envTemp, out var t))
        {
            temperature = t;
        }
        else if (float.TryParse(cfg["AzureOpenAI:Temperature"], out var tc))
        {
            temperature = tc;
        }
    }

    if (req.maxTokens.HasValue)
    {
        maxTokens = req.maxTokens.Value;
    }
    else
    {
        var envMax = GetEnvironmentVariable("MAX_OUTPUT_TOKENS");
        if (!string.IsNullOrWhiteSpace(envMax) && int.TryParse(envMax, out var mt))
        {
            maxTokens = mt;
        }
        else if (int.TryParse(cfg["AzureOpenAI:MaxOutputTokens"], out var mc))
        {
            maxTokens = mc;
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

    logger.LogInformation("API Call: /api/chat");
    logger.LogInformation("  Endpoint: {Endpoint}", endpointUri);
    logger.LogInformation("  Authentication: {AuthType}", useEntraId ? "Entra ID" : "API Key");
    logger.LogInformation("  Temperature: {Temperature}", temperature);
    logger.LogInformation("  MaxTokens: {MaxTokens}", maxTokens);
    logger.LogInformation("  Deployment: {Deployment}", deployment);
    logger.LogInformation("  WasRedacted: {WasRedacted}", wasRedacted);

    try
    {
        ChatCompletion completion = await chatClient.CompleteChatAsync(messages, options);

        var parts = completion.Content;
        string text = string.Join("\n\n", parts.Select(p => p.Text).Where(t => !string.IsNullOrWhiteSpace(t)));

        return Results.Ok(new 
        { 
            text = text, 
            raw = completion,
            wasRedacted = wasRedacted,
            normalizedPrompt = wasRedacted ? normalizedPrompt : null
        });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Chat API error");
        return Results.Problem($"Chat error: {ex.Message}", statusCode: 500);
    }
});

// Keep original email endpoint for manual testing
app.MapPost("/api/send-email", async (EmailRequest req) =>
{
    var cfg = app.Configuration;
    var smtpHost = GetEnvironmentVariable("SMTP_HOST") ?? cfg["Smtp:Host"];
    var smtpPort = int.TryParse(GetEnvironmentVariable("SMTP_PORT"), out var port) ? port : int.TryParse(cfg["Smtp:Port"], out var p) ? p : 587;
    var smtpUser = GetEnvironmentVariable("SMTP_USER") ?? cfg["Smtp:User"];
    var smtpPass = GetEnvironmentVariable("SMTP_PASS") ?? cfg["Smtp:Pass"];
    var smtpFrom = GetEnvironmentVariable("SMTP_FROM") ?? cfg["Smtp:From"];
    bool enableSsl = (GetEnvironmentVariable("SMTP_SSL") ?? cfg["Smtp:Ssl"] ?? "true").ToLowerInvariant() == "true";

    if (string.IsNullOrWhiteSpace(smtpHost) || string.IsNullOrWhiteSpace(smtpUser) || string.IsNullOrWhiteSpace(smtpPass) || string.IsNullOrWhiteSpace(smtpFrom))
    {
        return Results.Problem("Missing SMTP configuration.", statusCode: 500);
    }
    try
    {
        var message = new MailMessage(smtpFrom, req.To, req.Subject, req.Body);
        message.IsBodyHtml = true;
        using var client = new SmtpClient(smtpHost, smtpPort)
        {
            Credentials = new NetworkCredential(smtpUser, smtpPass),
            EnableSsl = enableSsl
        };
        await client.SendMailAsync(message);
        return Results.Ok(new { status = "sent" });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Email send error");
        return Results.Problem($"Email send error: {ex.Message}", statusCode: 500);
    }
});

string GetAppVersion()
{
    // Read the clean Version instead of InformationalVersion (which may include git hash)
    var version = Assembly.GetExecutingAssembly().GetName().Version;
    return version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "unknown";
}

// Health check
app.MapGet("/api/health", () => Results.Ok(new { status = "ok", version = GetAppVersion() }));
app.MapHealthChecks("/health");

app.Run();

// Helper function to analyze exception with Azure OpenAI
async Task<ExceptionAnalysis?> AnalyzeExceptionWithAI(ExceptionAnalysisRequest req, ILogger logger, IConfiguration cfg)
{
    var envEndpoint = GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")?.Trim();
    var envKey = GetEnvironmentVariable("AZURE_OPENAI_API_KEY")?.Trim();
    var envDeployment = GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT")?.Trim();
    var envUseEntraId = GetEnvironmentVariable("USE_ENTRA_ID")?.Trim();

    string endpoint = !string.IsNullOrWhiteSpace(envEndpoint)
        ? envEndpoint
        : cfg["AzureOpenAI:Endpoint"] ?? "https://rs-sm-az-openai.openai.azure.com/";
    
    string? key = !string.IsNullOrWhiteSpace(envKey)
        ? envKey
        : cfg["AzureOpenAI:ApiKey"];
    
    string deployment = !string.IsNullOrWhiteSpace(envDeployment)
        ? envDeployment
        : (cfg["AzureOpenAI:Deployment"] ?? "gpt-4.1");

    bool useEntraId = (!string.IsNullOrWhiteSpace(envUseEntraId) && envUseEntraId.Equals("true", StringComparison.OrdinalIgnoreCase))
                      || string.IsNullOrWhiteSpace(key);

    Uri endpointUri = new Uri(endpoint);

    AzureOpenAIClient azureClient;
    
    if (useEntraId)
    {
        logger.LogInformation("Using Entra ID for AI analysis");
        azureClient = new AzureOpenAIClient(endpointUri, new DefaultAzureCredential());
    }
    else
    {
        logger.LogInformation("Using API Key for AI analysis");
        azureClient = new AzureOpenAIClient(endpointUri, new AzureKeyCredential(key!));
    }

    var chatClient = azureClient.GetChatClient(deployment);

    // Construct specialized system prompt for exception analysis
    var systemPrompt = @"You are an expert software developer and DevOps engineer specializing in troubleshooting production issues in enterprise environments.

**YOUR ROLE**: Analyze technical exceptions, system failures, and on-call incidents from BMC job runs and production systems.

**YOU SHOULD ANALYZE**:
- Exception logs and stack traces
- BMC job failures
- Production system errors
- Database failures and connection issues
- API and service failures
- Infrastructure and deployment problems
- Performance issues and timeouts
- Security and permission errors

**YOU SHOULD NOT RESPOND TO**:
- General knowledge questions
- Non-technical inquiries
- Programming tutorials unrelated to the specific error
- Personal opinions or advice outside troubleshooting
- Requests unrelated to production support

If the input is not a technical error or incident, respond with:
""This service is designed for analyzing production incidents and technical errors. Please provide exception logs, error messages, or technical issues from production systems.""

**RESPONSE FORMAT** (for technical issues only):

1. **SUMMARY**: A concise 2-3 sentence overview of what happened
2. **ROOT CAUSE**: The underlying reason for the failure (not just the immediate error)
3. **QUICK CHECKS**: 3-5 immediate things the developer should verify (configuration, permissions, data, dependencies, etc.)
4. **NEXT STEPS**: Recommended actions in order of priority with risk levels (Low, Medium, High)
5. **ADDITIONAL CONTEXT**: Any relevant patterns, similar issues, or preventive measures

Be specific, technical, and actionable. Focus on helping developers quickly understand and resolve the issue. This is a one-time analysis that will be emailed - provide a complete, self-contained response without requesting additional information or suggesting follow-up questions.";

    // Construct user prompt with context
    var userPrompt = new StringBuilder();
    userPrompt.AppendLine("Please analyze the following BMC job exception:");
    userPrompt.AppendLine();
    
    if (!string.IsNullOrWhiteSpace(req.JobName))
    {
        userPrompt.AppendLine($"**Job Name**: {req.JobName}");
    }
    
    if (!string.IsNullOrWhiteSpace(req.Environment))
    {
        userPrompt.AppendLine($"**Environment**: {req.Environment}");
    }
    
    if (!string.IsNullOrWhiteSpace(req.JobDescription))
    {
        userPrompt.AppendLine($"**Job Description**: {req.JobDescription}");
    }
    
    userPrompt.AppendLine();
    userPrompt.AppendLine("**Exception Logs**:");
    userPrompt.AppendLine("```");
    userPrompt.AppendLine(req.ExceptionLogs);
    userPrompt.AppendLine("```");

    var messages = new List<ChatMessage>
    {
        new SystemChatMessage(systemPrompt),
        new UserChatMessage(userPrompt.ToString())
    };

    // Use configurable token limit with default
    int maxTokens = int.TryParse(GetEnvironmentVariable("MAX_OUTPUT_TOKENS"), out var mt) 
        ? mt 
        : int.TryParse(cfg["AzureOpenAI:MaxOutputTokens"], out var mc) ? mc : 6000;

    // Use configurable temperature with default
    float temperature = float.TryParse(GetEnvironmentVariable("OPENAI_TEMPERATURE"), out var temp)
        ? temp
        : float.TryParse(cfg["AzureOpenAI:Temperature"], out var tempCfg) ? tempCfg : 0.3f;

    var options = new ChatCompletionOptions
    {
        Temperature = temperature,
        MaxOutputTokenCount = maxTokens,
        TopP = 0.95f,
        FrequencyPenalty = 0f,
        PresencePenalty = 0f,
    };

    logger.LogInformation("Sending exception to AI for analysis (max tokens: {MaxTokens})", maxTokens);

    var result = await chatClient.CompleteChatAsync(messages, options);
    var completion = result.Value;
    var analysisText = string.Join("\n\n", completion.Content.Select(p => p.Text).Where(t => !string.IsNullOrWhiteSpace(t)));

    return new ExceptionAnalysis
    {
        AnalysisText = analysisText,
        JobName = req.JobName,
        Environment = req.Environment,
        AnalyzedAt = DateTime.UtcNow,
        TokensUsed = completion.Usage?.TotalTokenCount ?? 0
    };
}

// Helper function to send analysis via email
async Task<EmailResult> SendAnalysisEmail(ExceptionAnalysisRequest req, ExceptionAnalysis analysis, ILogger logger, IConfiguration cfg)
{
    var smtpHost = GetEnvironmentVariable("SMTP_HOST") ?? cfg["Smtp:Host"];
    var smtpPort = int.TryParse(GetEnvironmentVariable("SMTP_PORT"), out var port) ? port : int.TryParse(cfg["Smtp:Port"], out var p) ? p : 587;
    var smtpUser = GetEnvironmentVariable("SMTP_USER") ?? cfg["Smtp:User"];
    var smtpPass = GetEnvironmentVariable("SMTP_PASS") ?? cfg["Smtp:Pass"];
    var smtpFrom = GetEnvironmentVariable("SMTP_FROM") ?? cfg["Smtp:From"];
    bool enableSsl = (GetEnvironmentVariable("SMTP_SSL") ?? cfg["Smtp:Ssl"] ?? "true").ToLowerInvariant() == "true";

    if (string.IsNullOrWhiteSpace(smtpHost) || string.IsNullOrWhiteSpace(smtpUser) || 
        string.IsNullOrWhiteSpace(smtpPass) || string.IsNullOrWhiteSpace(smtpFrom))
    {
        return new EmailResult { Success = false, Error = "Missing SMTP configuration" };
    }

    try
    {
        var subject = $"BMC Job Exception Analysis: {req.JobName ?? "Unknown Job"}";
        var htmlBody = GenerateEmailHtml(req, analysis);

        var message = new MailMessage(smtpFrom, req.DeveloperEmail, subject, htmlBody)
        {
            IsBodyHtml = true
        };

        // Add CC recipients if provided
        if (req.CcEmails != null && req.CcEmails.Any())
        {
            foreach (var cc in req.CcEmails)
            {
                if (!string.IsNullOrWhiteSpace(cc))
                {
                    message.CC.Add(cc);
                }
            }
        }

        using var client = new SmtpClient(smtpHost, smtpPort)
        {
            Credentials = new NetworkCredential(smtpUser, smtpPass),
            EnableSsl = enableSsl
        };

        await client.SendMailAsync(message);
        logger.LogInformation("Analysis email sent successfully to {Email}", req.DeveloperEmail);
        
        return new EmailResult { Success = true };
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to send analysis email");
        return new EmailResult { Success = false, Error = ex.Message };
    }
}

// Generate HTML email body
string GenerateEmailHtml(ExceptionAnalysisRequest req, ExceptionAnalysis analysis)
{
    var html = new StringBuilder();
    html.AppendLine("<!DOCTYPE html>");
    html.AppendLine("<html><head><meta charset='utf-8'>");
    html.AppendLine("<style>");
    html.AppendLine("body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; line-height: 1.6; color: #333; max-width: 800px; margin: 0 auto; padding: 20px; }");
    html.AppendLine("h1 { color: #d9534f; border-bottom: 3px solid #d9534f; padding-bottom: 10px; }");
    html.AppendLine("h2 { color: #0275d8; margin-top: 30px; border-left: 4px solid #0275d8; padding-left: 10px; }");
    html.AppendLine(".info-box { background: #f8f9fa; border-left: 4px solid #5bc0de; padding: 15px; margin: 20px 0; }");
    html.AppendLine(".info-label { font-weight: bold; color: #666; }");
    html.AppendLine(".analysis { background: #fff; border: 1px solid #ddd; padding: 20px; margin: 20px 0; border-radius: 5px; }");
    html.AppendLine(".logs { background: #f4f4f4; border: 1px solid #ccc; padding: 15px; overflow-x: auto; font-family: 'Courier New', monospace; font-size: 12px; white-space: pre-wrap; word-wrap: break-word; }");
    html.AppendLine(".footer { margin-top: 40px; padding-top: 20px; border-top: 1px solid #ddd; font-size: 12px; color: #666; }");
    html.AppendLine("</style></head><body>");
    
    html.AppendLine($"<h1>?? BMC Job Exception Analysis</h1>");
    
    html.AppendLine("<div class='info-box'>");
    if (!string.IsNullOrWhiteSpace(req.JobName))
    {
        html.AppendLine($"<p><span class='info-label'>Job Name:</span> {System.Web.HttpUtility.HtmlEncode(req.JobName)}</p>");
    }
    if (!string.IsNullOrWhiteSpace(req.Environment))
    {
        html.AppendLine($"<p><span class='info-label'>Environment:</span> {System.Web.HttpUtility.HtmlEncode(req.Environment)}</p>");
    }
    if (!string.IsNullOrWhiteSpace(req.JobDescription))
    {
        html.AppendLine($"<p><span class='info-label'>Description:</span> {System.Web.HttpUtility.HtmlEncode(req.JobDescription)}</p>");
    }
    html.AppendLine($"<p><span class='info-label'>Analyzed:</span> {analysis.AnalyzedAt:yyyy-MM-dd HH:mm:ss} UTC</p>");
    html.AppendLine("</div>");
    
    html.AppendLine("<div class='analysis'>");
    html.AppendLine("<h2>?? AI Analysis</h2>");
    
    // Convert markdown-style formatting to HTML
    var analysisHtml = System.Web.HttpUtility.HtmlEncode(analysis.AnalysisText)
        .Replace("**", "<strong>")
        .Replace("</strong>", "</strong>", StringComparison.OrdinalIgnoreCase)
        .Replace("\n\n", "</p><p>")
        .Replace("\n", "<br/>");
    
    html.AppendLine($"<p>{analysisHtml}</p>");
    html.AppendLine("</div>");
    
    if (!string.IsNullOrWhiteSpace(req.ExceptionLogs))
    {
        html.AppendLine("<h2>?? Original Exception Logs</h2>");
        html.AppendLine($"<div class='logs'>{System.Web.HttpUtility.HtmlEncode(req.ExceptionLogs)}</div>");
    }
    
    html.AppendLine("<div class='footer'>");
    html.AppendLine("<p>This analysis was automatically generated by the BMC Exception Analyzer using Azure OpenAI.</p>");
    html.AppendLine($"<p><em>Tokens used: {analysis.TokensUsed}</em></p>");
    html.AppendLine("</div>");
    
    html.AppendLine("</body></html>");
    
    return html.ToString();
}

// DTOs
public record ChatRequest(string? system, string prompt, string? deployment, float? temperature = null, int? maxTokens = null);
public record ChatResponse(string text, object raw);
public record EmailRequest(string To, string Subject, string Body);

public record ExceptionAnalysisRequest(
    string ExceptionLogs,
    string DeveloperEmail,
    string? JobName = null,
    string? Environment = null,
    string? JobDescription = null,
    List<string>? CcEmails = null
);

public record ExceptionAnalysis
{
    public string AnalysisText { get; init; } = string.Empty;
    public string? JobName { get; init; }
    public string? Environment { get; init; }
    public DateTime AnalyzedAt { get; init; }
    public int TokensUsed { get; init; }
}

public record EmailResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
}
