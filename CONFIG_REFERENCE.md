# Configuration Settings Reference

## Temperature & Token Configuration

### Quick Reference

| Setting | UI Input | Config Key | Environment Variable | Default | Description |
|---------|----------|-----------|---------------------|---------|-------------|
| Temperature | ? Yes | `AzureOpenAI:Temperature` | `OPENAI_TEMPERATURE` | `0` | Controls response randomness (0-2) |
| Max Tokens | ? Yes | `AzureOpenAI:MaxOutputTokens` | `MAX_OUTPUT_TOKENS` | `6000` | Maximum response length |

### UI Input Boxes

The web interface includes input boxes to set these values per request:

**Temperature**:
- Range: 0 to 2
- Step: 0.1
- Default: 0
- Description: Lower values = more deterministic, higher = more creative

**Max Output Tokens**:
- Range: 100 to 128000
- Step: 100
- Default: 6000
- Description: Maximum length of AI response

### Configuration Examples

**Via UI** (Highest Priority):
- Enter values in the input boxes before clicking "Send"
- Values are sent with each request
- Settings display in the user message

**appsettings.json**:
```json
{
  "AzureOpenAI": {
    "Temperature": "0",
    "MaxOutputTokens": "6000"
  }
}
```

**Environment Variables**:
```bash
export OPENAI_TEMPERATURE="0"
export MAX_OUTPUT_TOKENS="6000"
```

### Priority Order
1. **UI Input Values** (highest priority) - per request
2. **Environment Variables** - runtime configuration
3. **appsettings.Development.json** - development defaults
4. **appsettings.json** - production defaults
5. **Hardcoded defaults** (lowest) - 0f and 6000

### Use Cases

#### Interactive Testing (UI)
- Adjust temperature/tokens for each query
- Test different settings without restart
- Great for experimentation

#### Production Consistency (Config)
- Set in appsettings.json
- All requests use same settings
- Predictable behavior

#### Environment-Specific (Env Vars)
- Different settings per environment
- No code changes needed
- Docker/K8s friendly

### Recommendations
- **Production**: Temperature=0, MaxTokens=6000 (via config)
- **Development**: Use UI inputs to experiment
- **Cost Saving**: MaxTokens=2000 (via config)
- **High Detail**: MaxTokens=8000 (via UI or config)

### Examples

**Deterministic responses**:
- UI: Temperature=0, MaxTokens=6000
- Best for: Production, consistent analysis

**Creative responses**:
- UI: Temperature=0.7, MaxTokens=8000
- Best for: Brainstorming, varied suggestions

**Quick/Cheap responses**:
- UI: Temperature=0, MaxTokens=2000
- Best for: Simple errors, cost optimization
