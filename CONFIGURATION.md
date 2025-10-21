# Azure OpenAI Configuration Guide

This application supports **two authentication methods**:
1. **API Key Authentication** (default)
2. **Entra ID Authentication** (Managed Identity / DefaultAzureCredential)

---

## Required Configuration Keys

### Option 1: API Key Authentication (Recommended for Development)

Set these environment variables or add them to `appsettings.Development.json`:

| Configuration Key | Environment Variable | Required | Example Value |
|-------------------|---------------------|----------|---------------|
| Endpoint | `AZURE_OPENAI_ENDPOINT` | **Yes** | `https://rs-sm-az-openai.openai.azure.com/` |
| API Key | `AZURE_OPENAI_API_KEY` | **Yes** | `7w39wbuKV2FS...` |
| Deployment | `AZURE_OPENAI_DEPLOYMENT` | **Yes** | `gpt-4.1` |
| Temperature | `OPENAI_TEMPERATURE` | No | `0.7` |
| Max Tokens | `MAX_OUTPUT_TOKENS` | No | `800` |

#### Example: Set Environment Variables (macOS/Linux)

```bash
export AZURE_OPENAI_ENDPOINT="https://rs-sm-az-openai.openai.azure.com/"
export AZURE_OPENAI_API_KEY="your-api-key-here"
export AZURE_OPENAI_DEPLOYMENT="gpt-4.1"
export OPENAI_TEMPERATURE="0.7"
export MAX_OUTPUT_TOKENS="800"
```

#### Example: Set Environment Variables (Windows PowerShell)

```powershell
$env:AZURE_OPENAI_ENDPOINT="https://rs-sm-az-openai.openai.azure.com/"
$env:AZURE_OPENAI_API_KEY="your-api-key-here"
$env:AZURE_OPENAI_DEPLOYMENT="gpt-4.1"
$env:OPENAI_TEMPERATURE="0.7"
$env:MAX_OUTPUT_TOKENS="800"
```

#### Example: appsettings.Development.json

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://rs-sm-az-openai.openai.azure.com/",
    "ApiKey": "your-api-key-here",
    "Deployment": "gpt-4.1",
    "Temperature": "0.7",
    "MaxOutputTokens": "800"
  }
}
```

---

### Option 2: Entra ID Authentication (Recommended for Production/Azure)

When deploying to Azure App Service with Managed Identity, you can use Entra ID authentication instead of API keys.

| Configuration Key | Environment Variable | Required | Example Value |
|-------------------|---------------------|----------|---------------|
| Endpoint | `AZURE_OPENAI_ENDPOINT` | **Yes** | `https://rs-sm-az-openai.openai.azure.com/` |
| Use Entra ID | `USE_ENTRA_ID` | No* | `true` |
| Deployment | `AZURE_OPENAI_DEPLOYMENT` | **Yes** | `gpt-4.1` |
| Temperature | `OPENAI_TEMPERATURE` | No | `0.7` |
| Max Tokens | `MAX_OUTPUT_TOKENS` | No | `800` |

**Note:** If `AZURE_OPENAI_API_KEY` is not set, the app automatically uses Entra ID authentication.

#### Example: Set Environment Variables for Entra ID

```bash
export AZURE_OPENAI_ENDPOINT="https://rs-sm-az-openai.openai.azure.com/"
export AZURE_OPENAI_DEPLOYMENT="gpt-4.1"
export USE_ENTRA_ID="true"
```

#### Prerequisites for Entra ID Authentication

1. **Enable Managed Identity** on your Azure App Service or Azure Function
2. **Assign Role**: Grant the managed identity the **"Cognitive Services OpenAI User"** role on your Azure OpenAI resource

```bash
# Get the managed identity principal ID
PRINCIPAL_ID=$(az webapp identity show --resource-group <rg> --name <app-name> --query principalId -o tsv)

# Assign role
az role assignment create \
  --role "Cognitive Services OpenAI User" \
  --assignee $PRINCIPAL_ID \
  --scope /subscriptions/<subscription-id>/resourceGroups/<rg>/providers/Microsoft.CognitiveServices/accounts/<openai-resource-name>
```

---

## Azure App Service Configuration

### Using Azure Portal

1. Go to your App Service ? **Configuration** ? **Application settings**
2. Add the following settings:
   - `AZURE_OPENAI_ENDPOINT` = `https://rs-sm-az-openai.openai.azure.com/`
   - `AZURE_OPENAI_API_KEY` = `your-api-key` (for API Key auth) OR
   - `USE_ENTRA_ID` = `true` (for Entra ID auth)
   - `AZURE_OPENAI_DEPLOYMENT` = `gpt-4.1`
3. Save and restart

### Using Azure CLI

```bash
az webapp config appsettings set \
  --resource-group <rg> \
  --name <app-name> \
  --settings \
  AZURE_OPENAI_ENDPOINT="https://rs-sm-az-openai.openai.azure.com/" \
  AZURE_OPENAI_API_KEY="your-api-key" \
  AZURE_OPENAI_DEPLOYMENT="gpt-4.1" \
  OPENAI_TEMPERATURE="0.7" \
  MAX_OUTPUT_TOKENS="800"
```

---

## Run Locally

```bash
dotnet run --project example.csproj
```

Open http://localhost:5173 to use the UI.

---

## Configuration Priority

The app resolves configuration in this order:
1. **Environment Variables** (highest priority)
2. **appsettings.Development.json** or **appsettings.json**
3. **Default values** (lowest priority)

---

## Troubleshooting

### Error: "Missing Azure OpenAI Endpoint configuration"
- Ensure `AZURE_OPENAI_ENDPOINT` is set

### Error: "Chat error: Unauthorized"
- **API Key Auth**: Verify your API key is correct
- **Entra ID Auth**: Ensure Managed Identity has the correct role assignment

### Error: "DefaultAzureCredential failed to retrieve a token"
- Make sure you're logged in via Azure CLI: `az login`
- Or enable Managed Identity in Azure App Service
