# BMC Exception Analyzer API

An intelligent API that analyzes BMC job exception logs using Azure OpenAI and automatically emails actionable insights to developers.

## ?? Purpose

This service helps development teams quickly understand and resolve production issues by:
1. **Receiving exception logs** from ServiceNow when BMC jobs fail
2. **Analyzing the root cause** using Azure OpenAI (GPT-4)
3. **Providing actionable recommendations** including quick checks and next steps
4. **Automatically emailing results** to the assigned developer

## ?? Features

- **Smart Exception Analysis**: AI-powered analysis that goes beyond the immediate error to identify root causes
- **Structured Output**: Organized sections including Summary, Root Cause, Quick Checks, Next Steps, and Additional Context
- **Professional Email Delivery**: HTML-formatted emails with clear sections and formatting
- **ServiceNow Integration Ready**: JSON API designed for easy integration with ServiceNow workflows
- **Configurable**: Support for environment variables and configuration files
- **Multiple Authentication**: Supports both Azure OpenAI API key and Entra ID authentication

## ?? Requirements

- .NET 9.0
- Azure OpenAI account with GPT-4 deployment
- SMTP server access (Gmail, Office365, or other)

## ?? Configuration

### Option 1: Environment Variables (Recommended for Production)

```bash
# Azure OpenAI Configuration
AZURE_OPENAI_ENDPOINT=https://your-instance.openai.azure.com/
AZURE_OPENAI_API_KEY=your-api-key-here
AZURE_OPENAI_DEPLOYMENT=gpt-4
MAX_OUTPUT_TOKENS=2000

# SMTP Configuration
SMTP_HOST=smtp.gmail.com
SMTP_PORT=587
SMTP_USER=your-email@gmail.com
SMTP_PASS=your-app-password
SMTP_FROM=your-email@gmail.com
SMTP_SSL=true

# Optional: Use Entra ID instead of API key
USE_ENTRA_ID=false
```

### Option 2: appsettings.Development.json

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://your-instance.openai.azure.com/",
    "ApiKey": "your-api-key",
    "Deployment": "gpt-4",
    "Temperature": "0.3",
    "MaxOutputTokens": "2000"
  },
  "Smtp": {
    "Host": "smtp.gmail.com",
    "Port": "587",
    "User": "your-email@gmail.com",
    "Pass": "your-app-password",
    "From": "your-email@gmail.com",
    "Ssl": "true"
  }
}
```

### Gmail Setup

If using Gmail:
1. Enable 2-factor authentication on your Google account
2. Generate an App Password:
   - Go to Google Account Settings
   - Security ? 2-Step Verification ? App passwords
   - Generate password for "Mail"
3. Use the generated app password (format: `xxxx xxxx xxxx xxxx`)
4. Set `SMTP_HOST=smtp.gmail.com` and `SMTP_PORT=587`

## ?? Running the Application

```bash
# Restore dependencies
dotnet restore

# Run the application
dotnet run

# The API will start on http://localhost:5000
```

## ?? API Endpoints

### Main Endpoint: Analyze Exception

**POST** `/api/analyze-exception`

Analyzes BMC job exception logs and emails the results.

**Request Body:**
```json
{
  "exceptionLogs": "Full stack trace here...",
  "developerEmail": "dev@company.com",
  "jobName": "Daily Data Sync Job",
  "environment": "Production",
  "jobDescription": "Brief description of what the job does",
  "ccEmails": ["team-lead@company.com"]
}
```

**Required Fields:**
- `exceptionLogs`: The complete exception stack trace and logs
- `developerEmail`: Email address to receive the analysis

**Optional Fields:**
- `jobName`: Name of the failed job
- `environment`: Environment (Production, Staging, QA, etc.)
- `jobDescription`: What the job does (helps AI provide better context)
- `ccEmails`: Array of additional email addresses to CC

**Response (Success):**
```json
{
  "status": "success",
  "analysis": {
    "analysisText": "Detailed AI analysis...",
    "jobName": "Daily Data Sync Job",
    "environment": "Production",
    "analyzedAt": "2024-01-15T22:30:00Z",
    "tokensUsed": 1247
  },
  "emailSent": true,
  "message": "Exception analyzed and results emailed to developer"
}
```

### Other Endpoints

- **GET** `/api/health` - Health check endpoint
- **POST** `/api/chat` - Direct chat with Azure OpenAI (for testing)
- **POST** `/api/send-email` - Send email directly (for testing)

## ?? Testing

### Quick Test with PowerShell

```powershell
.\test-api.ps1
```

This will send the sample exception from `test-request-example.json` to the API.

### Manual Test with cURL

```bash
curl -X POST http://localhost:5000/api/analyze-exception \
  -H "Content-Type: application/json" \
  -d @test-request-example.json
```

### Test with Postman

1. Import the API endpoint: `POST http://localhost:5000/api/analyze-exception`
2. Set Content-Type: `application/json`
3. Use the body from `test-request-example.json`
4. Send the request

## ?? Email Output

Developers receive a professionally formatted HTML email containing:

### ?? Job Information Section
- Job Name
- Environment
- Description
- Analysis Timestamp

### ?? AI Analysis Section
- **SUMMARY**: Concise overview of what happened
- **ROOT CAUSE**: The underlying reason for the failure
- **QUICK CHECKS**: 3-5 immediate things to verify
  - Configuration settings
  - Permissions and credentials
  - Data availability
  - Dependencies and services
- **NEXT STEPS**: Prioritized action items
- **ADDITIONAL CONTEXT**: Patterns, prevention tips, related issues

### ?? Original Logs
- Complete exception stack trace for reference

## ?? ServiceNow Integration

See `ServiceNow-Integration-Example.md` for detailed integration guide including:
- ServiceNow workflow setup
- JavaScript code examples
- Business rule configuration
- Best practices

### Quick Integration Example

```javascript
// ServiceNow Business Rule
var request = new sn_ws.RESTMessageV2();
request.setEndpoint('https://your-api.com/api/analyze-exception');
request.setHttpMethod('POST');
request.setRequestHeader('Content-Type', 'application/json');

var body = {
    exceptionLogs: current.exception_logs.toString(),
    developerEmail: current.assigned_developer.email.toString(),
    jobName: current.job_name.toString(),
    environment: current.environment.toString(),
    jobDescription: current.job_description.toString()
};

request.setRequestBody(JSON.stringify(body));
var response = request.execute();

if (response.getStatusCode() == 200) {
    var result = JSON.parse(response.getBody());
    current.work_notes = 'AI analysis sent to ' + current.assigned_developer.email;
    current.ai_analysis = result.analysis.analysisText;
    current.update();
}
```

## ?? AI Prompt Engineering

The API uses a specialized system prompt that instructs the AI to:
- Act as an expert software developer and DevOps engineer
- Focus on root cause analysis, not just symptoms
- Provide actionable, prioritized recommendations
- Consider production context and urgency
- Use a structured output format

The prompt is optimized for:
- **Lower temperature (0.3)**: More focused, consistent analysis
- **Higher token limit (1500-2000)**: Detailed, comprehensive responses
- **Contextual information**: Job description helps AI understand dependencies

## ?? Monitoring & Logging

The application logs:
- All incoming analysis requests with job details
- AI analysis parameters (deployment, tokens, authentication method)
- Email delivery status
- Errors and exceptions

View logs in the console or configure your preferred logging provider.

## ?? Security Best Practices

1. **Use Environment Variables** for production secrets (API keys, passwords)
2. **Enable HTTPS** when deploying to production
3. **Rotate SMTP passwords** regularly
4. **Use Entra ID** authentication for Azure OpenAI when possible
5. **Implement API authentication** for the /api/analyze-exception endpoint
6. **Rate limiting** to prevent abuse
7. **Input validation** to prevent injection attacks

## ?? Deployment

### Deploy to Azure App Service

```bash
# Publish the application
dotnet publish -c Release -o ./publish

# Deploy to Azure (requires Azure CLI)
az webapp up --name your-app-name --resource-group your-rg
```

### Configure App Settings in Azure

Add environment variables in Azure Portal:
- Configuration ? Application Settings
- Add all SMTP and Azure OpenAI settings

## ?? Customization

### Modify AI Analysis Prompt

Edit the `systemPrompt` in the `AnalyzeExceptionWithAI` function to:
- Add company-specific guidelines
- Include additional analysis sections
- Change the tone or format
- Add specific technologies or frameworks

### Customize Email Template

Edit the `GenerateEmailHtml` function to:
- Change colors and styling
- Add company branding
- Include additional sections
- Link to internal documentation

### Adjust Token Limits

- Increase `MaxOutputTokens` for more detailed analysis
- Decrease for faster, more concise responses
- Balance between detail and cost

## ?? Cost Considerations

- GPT-4 tokens are metered by Azure OpenAI
- Typical analysis uses 1000-2000 tokens
- Monitor usage in Azure Portal
- Consider using GPT-3.5-turbo for lower costs with slightly less detailed analysis

## ?? Troubleshooting

### Email Not Sending
- Verify SMTP credentials are correct
- Check app password is generated (for Gmail)
- Ensure port 587 is not blocked by firewall
- Try with `EnableSsl = true`

### AI Analysis Fails
- Verify Azure OpenAI endpoint is correct
- Check API key is valid
- Ensure deployment name matches your Azure OpenAI deployment
- Review token limits

### ServiceNow Integration Issues
- Verify API endpoint is accessible from ServiceNow
- Check JSON format matches schema
- Review ServiceNow script logs
- Test with Postman first

## ?? Additional Resources

- `ServiceNow-Integration-Example.md` - Detailed ServiceNow setup guide
- `test-request-example.json` - Sample exception for testing
- `test-api.ps1` - PowerShell test script

## ?? License

MIT License - Feel free to use and modify for your organization.

## ?? Contributing

Improvements welcome! Consider adding:
- Authentication/authorization
- Database logging of analyses
- Metrics and dashboards
- Support for other ticketing systems
- Multi-language support
