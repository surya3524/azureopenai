# Troubleshooting Azure OpenAI 404 Errors

## Error: HTTP 404 (Resource not found)

This error occurs when the Azure OpenAI deployment cannot be found. Here's how to fix it:

## Step 1: Check Your Configuration

Run the diagnostic endpoint:
```bash
curl http://localhost:5173/api/config-check
```

This will show your current configuration and highlight any issues.

## Step 2: Verify Endpoint Format

### ? WRONG (Too Specific)
```json
{
  "Endpoint": "https://rs-sm-az-openai.openai.azure.com/openai/deployments/gpt-4.1/chat/completions?api-version=2025-01-01-preview"
}
```

### ? CORRECT (Base URL Only)
```json
{
  "Endpoint": "https://rs-sm-az-openai.openai.azure.com/"
}
```

**Why**: The SDK automatically constructs the full URL. Providing the full path causes double-construction and results in a 404.

## Step 3: Verify Deployment Name

### Find Your Actual Deployment Name

#### Option A: Azure Portal
1. Go to https://portal.azure.com
2. Navigate to your Azure OpenAI resource: `rs-sm-az-openai`
3. Click **Model deployments** (left menu)
4. Look for your deployment name (e.g., `gpt-4o-mini`, `gpt-4-turbo`, `oncallbuddy-gpt4o-mini`)

#### Option B: Azure CLI
```bash
az cognitiveservices account deployment list \
  --name rs-sm-az-openai \
  --resource-group <your-resource-group>
```

#### Option C: REST API
```bash
curl "https://rs-sm-az-openai.openai.azure.com/openai/deployments?api-version=2024-10-01-preview" \
  -H "api-key: YOUR_API_KEY"
```

### Update Configuration

Once you find the actual deployment name, update `appsettings.Development.json`:

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://rs-sm-az-openai.openai.azure.com/",
    "Deployment": "YOUR_ACTUAL_DEPLOYMENT_NAME",  // ? Use the exact name from Azure
    "ApiKey": "your-key",
    "Temperature": "0",
    "MaxOutputTokens": "2000"
  }
}
```

## Step 4: Common Deployment Names

Based on your config, try one of these:

```bash
# Test with potential deployment names
export AZURE_OPENAI_DEPLOYMENT="oncallbuddy-gpt4o-mini"
dotnet run

# OR
export AZURE_OPENAI_DEPLOYMENT="gpt-4o-mini"
dotnet run

# OR
export AZURE_OPENAI_DEPLOYMENT="gpt-4"
dotnet run
```

## Step 5: Test with Curl

Once you have the right deployment name, test directly:

```bash
curl "https://rs-sm-az-openai.openai.azure.com/openai/deployments/YOUR_DEPLOYMENT_NAME/chat/completions?api-version=2024-10-01-preview" \
  -H "Content-Type: application/json" \
  -H "api-key: 7w39wbuKV2FS6O7C9tImlcj0sgRrlrJX4UsC4C4RMe3pELYpKMDAJQQJ99BJACYeBjFXJ3w3AAABACOGEwJe" \
  -d '{
    "messages": [{"role": "user", "content": "Hello"}],
    "max_tokens": 50
  }'
```

**Expected**: Should return a JSON response with the AI's reply.
**If 404**: Deployment name is wrong.
**If 401**: API key is wrong.
**If 429**: Rate limit exceeded.

## Step 6: Environment Variable Override

For quick testing, override via environment variables (these take precedence):

```bash
export AZURE_OPENAI_ENDPOINT="https://rs-sm-az-openai.openai.azure.com/"
export AZURE_OPENAI_DEPLOYMENT="oncallbuddy-gpt4o-mini"  # Or whatever works
export AZURE_OPENAI_API_KEY="7w39wbuKV2FS6O7C9tImlcj0sgRrlrJX4UsC4C4RMe3pELYpKMDAJQQJ99BJACYeBjFXJ3w3AAABACOGEwJe"

dotnet run --project example.csproj
```

## Step 7: Check API Version

If the deployment exists but still 404s, the API version might be too new. Try an older one:

### Update in code (temporary test)
In `run_model.cs`, after creating the client, you can't easily change API version with current SDK.

### Use REST API to test
```bash
# Try different API versions
for version in "2024-10-01-preview" "2024-08-01-preview" "2024-06-01"; do
  echo "Testing $version..."
  curl -s "https://rs-sm-az-openai.openai.azure.com/openai/deployments/YOUR_DEPLOYMENT/chat/completions?api-version=$version" \
    -H "api-key: YOUR_KEY" \
    -d '{"messages":[{"role":"user","content":"Hi"}]}' | jq .error.code
done
```

## Step 8: Verify Permissions

If using Entra ID (Managed Identity):

```bash
# Check if you have the right role assignments
az role assignment list \
  --assignee <your-managed-identity-object-id> \
  --scope /subscriptions/<sub-id>/resourceGroups/<rg>/providers/Microsoft.CognitiveServices/accounts/rs-sm-az-openai
```

Required role: **Cognitive Services OpenAI User** or **Cognitive Services OpenAI Contributor**

## Quick Fix Checklist

- [ ] Endpoint is base URL only (ends with `/`)
- [ ] Deployment name matches Azure Portal exactly (case-sensitive)
- [ ] API key is correct and not expired
- [ ] Resource name is correct: `rs-sm-az-openai`
- [ ] Resource is in the same region as expected
- [ ] Deployment is not stopped/disabled in Azure Portal
- [ ] No typos in configuration

## Example Working Configuration

Based on your resource `rs-sm-az-openai`, here's a working example:

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://rs-sm-az-openai.openai.azure.com/",
    "Deployment": "gpt-4o-mini",  // ? Verify this exact name in Azure Portal
    "ApiKey": "7w39wbuKV2FS6O7C9tImlcj0sgRrlrJX4UsC4C4RMe3pELYpKMDAJQQJ99BJACYeBjFXJ3w3AAABACOGEwJe",
    "Temperature": "0",
    "MaxOutputTokens": "2000"
  }
}
```

## Still Not Working?

### Enable Detailed Logging
```bash
export AZURE_LOG_LEVEL=verbose
dotnet run
```

### Check Application Logs
Look for the exact URL being called:
```bash
dotnet run | grep -i "endpoint\|deployment\|url"
```

### Contact Support
If none of the above works, check:
1. Azure OpenAI resource is provisioned correctly
2. Region supports the model you're trying to use
3. Quota/limits haven't been exceeded

## Common Mistakes

| Mistake | Symptom | Fix |
|---------|---------|-----|
| Full URL in Endpoint | 404 | Use base URL only |
| Wrong deployment name | 404 | Check Azure Portal |
| Expired API key | 401 | Generate new key |
| Wrong resource name | 404 | Verify in Azure |
| Deployment stopped | 404 | Start deployment in Portal |
| Wrong region endpoint | 404/timeout | Check resource region |

## Need More Help?

Run diagnostics:
```bash
curl http://localhost:5173/api/config-check | jq .
```

Check health:
```bash
curl http://localhost:5173/api/health
```

Test with simple chat:
```bash
curl -X POST http://localhost:5173/api/chat \
  -H "Content-Type: application/json" \
  -d '{"prompt":"Hello, are you working?","system":null,"deployment":null}'
```
