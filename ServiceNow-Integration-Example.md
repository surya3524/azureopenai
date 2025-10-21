# ServiceNow Integration Guide

## Overview
This API analyzes BMC job exception logs using Azure OpenAI and automatically emails the analysis to developers.

## Main Endpoint

### POST `/api/analyze-exception`

Analyzes exception logs from BMC jobs and emails the results to the developer.

#### Request Body (JSON)

```json
{
  "exceptionLogs": "System.NullReferenceException: Object reference not set to an instance of an object.\n   at MyApp.DataProcessor.Process(String data) in C:\\Projects\\MyApp\\DataProcessor.cs:line 42\n   at MyApp.Jobs.BMCJob.Execute() in C:\\Projects\\MyApp\\Jobs\\BMCJob.cs:line 88",
  "developerEmail": "developer@company.com",
  "jobName": "Daily Data Sync Job",
  "environment": "Production",
  "jobDescription": "Synchronizes customer data between CRM and warehouse systems every night at 2 AM",
  "ccEmails": ["team-lead@company.com", "devops@company.com"]
}
```

#### Required Fields
- `exceptionLogs` (string): The full exception stack trace and error logs from the BMC job
- `developerEmail` (string): Email address where the analysis should be sent

#### Optional Fields
- `jobName` (string): Name of the BMC job that failed
- `environment` (string): Environment where the job ran (e.g., Production, Staging, QA)
- `jobDescription` (string): Brief description of what the job does - helps AI provide better context
- `ccEmails` (array of strings): Additional email addresses to CC on the analysis

#### Response (Success)

```json
{
  "status": "success",
  "analysis": {
    "analysisText": "**SUMMARY**\nThe job failed due to a NullReferenceException...",
    "jobName": "Daily Data Sync Job",
    "environment": "Production",
    "analyzedAt": "2024-01-15T22:30:00Z",
    "tokensUsed": 1247
  },
  "emailSent": true,
  "message": "Exception analyzed and results emailed to developer"
}
```

#### Response (Partial Success - Analysis completed but email failed)

```json
{
  "status": "partial_success",
  "analysis": {
    "analysisText": "...",
    "jobName": "Daily Data Sync Job",
    "environment": "Production",
    "analyzedAt": "2024-01-15T22:30:00Z",
    "tokensUsed": 1247
  },
  "emailSent": false,
  "emailError": "SMTP authentication failed",
  "message": "Exception analysis completed but email delivery failed. See analysis results below."
}
```

## ServiceNow Workflow Example

### 1. Capture Exception in ServiceNow
When a BMC job fails, ServiceNow should capture:
- The complete exception stack trace
- Job metadata (name, environment, description)
- Developer assignment from the job configuration

### 2. Call the Analysis API
```javascript
// ServiceNow Script (Business Rule or Workflow)
var request = new sn_ws.RESTMessageV2();
request.setEndpoint('https://your-api-url.com/api/analyze-exception');
request.setHttpMethod('POST');
request.setRequestHeader('Content-Type', 'application/json');

var requestBody = {
    exceptionLogs: current.exception_logs.toString(),
    developerEmail: current.assigned_developer.email.toString(),
    jobName: current.job_name.toString(),
    environment: current.environment.toString(),
    jobDescription: current.job_description.toString(),
    ccEmails: [current.team_lead.email.toString()]
};

request.setRequestBody(JSON.stringify(requestBody));

var response = request.execute();
var httpStatus = response.getStatusCode();
var responseBody = response.getBody();

gs.info('Exception Analysis API Response: ' + responseBody);

if (httpStatus == 200) {
    var result = JSON.parse(responseBody);
    if (result.status === 'success') {
        current.work_notes = 'AI analysis completed and emailed to developer';
        current.ai_analysis = result.analysis.analysisText;
        current.update();
    }
}
```

### 3. Developer Receives Email
The developer receives a professionally formatted HTML email containing:
- **Job Information**: Name, environment, description, timestamp
- **AI Analysis**: 
  - Summary of what happened
  - Root cause analysis
  - Quick checks to perform
  - Next steps (prioritized)
  - Additional context and preventive measures
- **Original Exception Logs**: Full stack trace for reference

## Email Output Format

The email will include structured sections:

1. **SUMMARY**: 2-3 sentence overview
2. **ROOT CAUSE**: Why the failure occurred
3. **QUICK CHECKS**: 3-5 immediate verification steps
   - Configuration settings
   - Permissions
   - Data availability
   - Dependencies
4. **NEXT STEPS**: Prioritized action items
5. **ADDITIONAL CONTEXT**: Patterns, similar issues, prevention tips

## Testing the API

### Using cURL
```bash
curl -X POST https://your-api-url.com/api/analyze-exception \
  -H "Content-Type: application/json" \
  -d '{
    "exceptionLogs": "System.Data.SqlClient.SqlException: Cannot open database \"CustomerDB\" requested by the login. The login failed.\nLogin failed for user \"NT AUTHORITY\\NETWORK SERVICE\".",
    "developerEmail": "dev@company.com",
    "jobName": "Customer Import Job",
    "environment": "Production"
  }'
```

### Using PowerShell
```powershell
$body = @{
    exceptionLogs = "System.IO.FileNotFoundException: Could not find file 'C:\Data\import.csv'..."
    developerEmail = "dev@company.com"
    jobName = "File Import Job"
    environment = "Staging"
} | ConvertTo-Json

Invoke-RestMethod -Uri "https://your-api-url.com/api/analyze-exception" `
    -Method Post `
    -Body $body `
    -ContentType "application/json"
```

## Configuration

### Environment Variables (Recommended for Production)
```
AZURE_OPENAI_ENDPOINT=https://your-openai.openai.azure.com/
AZURE_OPENAI_API_KEY=your-api-key
AZURE_OPENAI_DEPLOYMENT=gpt-4
MAX_OUTPUT_TOKENS=2000

SMTP_HOST=smtp.gmail.com
SMTP_PORT=587
SMTP_USER=your-email@gmail.com
SMTP_PASS=your-app-password
SMTP_FROM=your-email@gmail.com
SMTP_SSL=true
```

### appsettings.Development.json (For Local Testing)
See existing configuration file.

## Best Practices

1. **Include Complete Stack Traces**: More context = better analysis
2. **Add Job Description**: Helps AI understand the job's purpose and dependencies
3. **Use Correct Environment Labels**: Production, Staging, QA, Development
4. **Set CC Appropriately**: Include team leads for critical production failures
5. **Store Analysis in ServiceNow**: Save the AI analysis text back to the incident/ticket

## Health Check
```bash
curl https://your-api-url.com/api/health
```

Returns: `{"status":"ok"}`
