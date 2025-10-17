## Azure OpenAI Web API + UI

This repo now runs a minimal ASP.NET Core Web API that calls Azure OpenAI and serves a lightweight chat UI at `/`.

**Authentication Options:**
- **API Key Authentication** (for development)
- **Entra ID / Managed Identity** (for production)

See [CONFIGURATION.md](./CONFIGURATION.md) for detailed configuration guide.

Endpoints
- `POST /api/chat` — send `{ system?, prompt, deployment? }` and get `{ text, raw }`
- `GET /api/health` — health check
- Static UI at `/` — Markdown rendering similar to ChatGPT

## Quick Start - Run Locally with API Key

1) Set environment variables (macOS/zsh):

```bash
export AZURE_OPENAI_ENDPOINT="https://rs-sm-az-openai.openai.azure.com/"
export AZURE_OPENAI_API_KEY="<your-api-key>"
export AZURE_OPENAI_DEPLOYMENT="gpt-4.1"
export ASPNETCORE_URLS="http://localhost:5173"
```

Windows PowerShell:
```powershell
$env:AZURE_OPENAI_ENDPOINT="https://rs-sm-az-openai.openai.azure.com/"
$env:AZURE_OPENAI_API_KEY="<your-api-key>"
$env:AZURE_OPENAI_DEPLOYMENT="gpt-4.1"
$env:ASPNETCORE_URLS="http://localhost:5173"
```

2) Run the app:

```bash
dotnet run --project example.csproj
```

Open http://localhost:5173 to use the UI.

## Configuration Keys Required

### For API Key Authentication:
- `AZURE_OPENAI_ENDPOINT` - Your Azure OpenAI endpoint (e.g., `https://rs-sm-az-openai.openai.azure.com/`)
- `AZURE_OPENAI_API_KEY` - Your Azure OpenAI API key
- `AZURE_OPENAI_DEPLOYMENT` - Your model deployment name (e.g., `gpt-4.1`)

### For Entra ID Authentication (Production):
- `AZURE_OPENAI_ENDPOINT` - Your Azure OpenAI endpoint
- `AZURE_OPENAI_DEPLOYMENT` - Your model deployment name
- `USE_ENTRA_ID` - Set to `true` (optional, auto-detected if no API key provided)

See [CONFIGURATION.md](./CONFIGURATION.md) for complete details.

## Deploy to Azure App Service

### Option A: API Key Authentication

```bash
az webapp config appsettings set \
  --resource-group <rg> \
  --name <app-name> \
  --settings \
  AZURE_OPENAI_ENDPOINT="https://rs-sm-az-openai.openai.azure.com/" \
  AZURE_OPENAI_API_KEY="<your-key>" \
  AZURE_OPENAI_DEPLOYMENT="gpt-4.1"
```

### Option B: Entra ID with Managed Identity (Recommended)

1. Enable Managed Identity on your App Service
2. Assign the **"Cognitive Services OpenAI User"** role to the managed identity
3. Configure app settings:

```bash
az webapp config appsettings set \
  --resource-group <rg> \
  --name <app-name> \
  --settings \
  AZURE_OPENAI_ENDPOINT="https://rs-sm-az-openai.openai.azure.com/" \
  AZURE_OPENAI_DEPLOYMENT="gpt-4.1" \
  USE_ENTRA_ID="true"
```

### Azure Key Vault Reference (Alternative)

Store secrets in Key Vault and reference them:

1. Create Key Vault and store secret `openai-api-key`
2. Enable Managed Identity on App Service
3. Grant Key Vault access to the identity
4. Reference in App Settings:
   ```
   AZURE_OPENAI_API_KEY=@Microsoft.KeyVault(SecretUri=https://<vault>.vault.azure.net/secrets/openai-api-key/)
   ```

For complete configuration options, see [CONFIGURATION.md](./CONFIGURATION.md).
