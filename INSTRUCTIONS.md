## Azure OpenAI Web API + UI

This repo now runs a minimal ASP.NET Core Web API that calls Azure OpenAI and serves a lightweight chat UI at `/`.

Endpoints
- `POST /api/chat` — send `{ system?, prompt, deployment? }` and get `{ text, raw }`
- `GET /api/health` — health check
- Static UI at `/` — Markdown rendering similar to ChatGPT

## Run locally

1) Set environment variables (macOS/zsh):

```bash
export AZURE_OPENAI_ENDPOINT="https://<your-endpoint>.openai.azure.com/"
export AZURE_OPENAI_API_KEY="<your-key>"
# optional if you want a default
export AZURE_OPENAI_DEPLOYMENT="gpt-4.1"
export ASPNETCORE_URLS="http://localhost:5173"
```

2) Run the app:

```bash
dotnet run --project example.csproj
```

Open http://localhost:5173 to use the UI.

## Store the API key in Azure (App Service)

Recommended: use App Settings or reference a Key Vault secret via Managed Identity.

### A) Azure Portal (App Settings)
1. Go to your Web App → Configuration → Application settings → New application setting.
2. Add:
	- `AZURE_OPENAI_ENDPOINT = https://<your-endpoint>.openai.azure.com/`
	- `AZURE_OPENAI_API_KEY = <your-key>`
	- `AZURE_OPENAI_DEPLOYMENT = gpt-4.1` (optional)
3. Save and restart. Settings are encrypted at rest and exposed to the app as environment variables.

### B) Azure CLI (App Settings)

Avoid typing secrets directly into shell history. Prefer exporting first:

```bash
export AZURE_OPENAI_ENDPOINT="https://<your-endpoint>.openai.azure.com/"
export AZURE_OPENAI_API_KEY="<your-key>"

az webapp config appsettings set \
  --resource-group <rg> \
  --name <app-name> \
  --settings \
  AZURE_OPENAI_ENDPOINT="$AZURE_OPENAI_ENDPOINT" \
  AZURE_OPENAI_API_KEY="$AZURE_OPENAI_API_KEY" \
  AZURE_OPENAI_DEPLOYMENT="gpt-4.1" \
  ASPNETCORE_URLS="http://0.0.0.0:8080"
```

App Service listens on an internal port and maps it to your site; exposing on 8080 is conventional.

### C) Azure Key Vault reference (Recommended)

1. Create a Key Vault and store a secret:
	- Name: `openai-api-key`
	- Value: `<your-key>`
2. Enable a system-assigned managed identity on your Web App (Identity → System assigned → On → Save).
3. Grant the Web App access to the Key Vault secret (Key Vault → Access control (IAM) → Add role assignment → `Key Vault Secrets User` → assign to the Web App’s managed identity).
4. In Web App → Configuration → Application settings, add:
	- `AZURE_OPENAI_API_KEY = @Microsoft.KeyVault(SecretUri=https://<kv-name>.vault.azure.net/secrets/openai-api-key/<optional-version>)`
	- `AZURE_OPENAI_ENDPOINT = https://<your-endpoint>.openai.azure.com/`
	- `AZURE_OPENAI_DEPLOYMENT = gpt-4.1` (optional)
5. Save and restart. The app will resolve the secret via managed identity at runtime.

## Low-cost publish to Azure App Service (Linux)

1) Create resources (sizes aim for low cost):
- App Service Plan: Basic (B1) or Free (F1 if available)
- Web App: Linux, .NET 9

```bash
az group create -n <rg> -l <region>
az appservice plan create -g <rg> -n <plan> --sku B1 --is-linux
az webapp create -g <rg> -p <plan> -n <app-name> -r "DOTNET:9.0"
```

2) Configure app settings (as above, section B or C).

3) Deploy code:

```bash
dotnet publish -c Release -o ./publish
az webapp deploy -g <rg> -n <app-name> --src-path ./publish
```

4) Verify:
- Browse the site URL
- GET `/api/health`
- Load `/` to use the UI

Cost tips:
- Use Basic B1 or Free if available, 1 instance
- Turn off Always On (saves cost, but may add cold-starts)
- Keep token limits reasonable in the API (MaxOutputTokenCount)

## Alternative: Azure Container Apps (scale-to-zero)

If usage is sporadic, ACA with minReplicas=0 can be cheaper:
- Store the API key as a secret: `az containerapp secret set ...`
- Map env var from secret: `--env-vars AZURE_OPENAI_API_KEY=secretref:openai-api-key`
- Set `AZURE_OPENAI_ENDPOINT` and (optionally) `AZURE_OPENAI_DEPLOYMENT` as env vars

## Notes
- Never commit secrets to git
- This app reads keys from environment variables; no code change needed for Azure
