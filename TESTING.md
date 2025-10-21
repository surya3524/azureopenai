# Testing Deterministic AI Analysis

## Quick Test

Test the deterministic exception analyzer with the sample payload:

```bash
# Run the app
dotnet run --project example.csproj

# In another terminal, send test request
curl -X POST http://localhost:5173/api/analyze-exception \
  -H "Content-Type: application/json" \
  -d @test_payload.json
```

## Determinism Validation Test

Run the same input multiple times and verify consistent outputs:

```bash
# Create output directory
mkdir -p test_results

# Run same test 5 times
for i in {1..5}; do
  echo "Run $i..."
  curl -X POST http://localhost:5173/api/analyze-exception \
    -H "Content-Type: application/json" \
    -d @test_payload.json > test_results/output_$i.json
  sleep 2
done

# Extract and compare severity classifications
echo "Checking SEVERITY consistency..."
jq -r '.analysis.AnalysisText' test_results/output_*.json | grep "SEVERITY:" | sort | uniq -c

# Extract and compare retry decisions
echo "Checking SAFE_TO_RETRY consistency..."
jq -r '.analysis.AnalysisText' test_results/output_*.json | grep "SAFE_TO_RETRY:" | sort | uniq -c

# Full text comparison (should show high similarity)
echo "Full text comparison..."
diff test_results/output_1.json test_results/output_2.json
```

## PowerShell Version (Windows)

```powershell
# Run determinism test
1..5 | ForEach-Object {
    Write-Host "Run $_..."
    Invoke-RestMethod -Uri "http://localhost:5173/api/analyze-exception" `
        -Method Post `
        -ContentType "application/json" `
        -InFile "test_payload.json" `
        -OutFile "test_results/output_$_.json"
    Start-Sleep -Seconds 2
}

# Compare results
Get-Content test_results/*.json | Select-String "SEVERITY:" | Group-Object | Select-Object Count, Name
Get-Content test_results/*.json | Select-String "SAFE_TO_RETRY:" | Group-Object | Select-Object Count, Name
```

## Expected Results

### ? Deterministic Fields (Should Match 100%)
- **SEVERITY**: All 5 runs should have identical classification
- **SAFE_TO_RETRY**: All 5 runs should have identical decision (true/false)
- **QUICK_CHECKS**: Order should be consistent
- **NEXT_STEPS**: Action order should be consistent (MITIGATE?PROBE?FIX?TEST)

### ?? Natural Language Fields (May Have Minor Variations)
- **SUMMARY**: Wording may vary slightly but meaning should be identical
- **ROOT_CAUSE**: Phrasing may differ but diagnosis should match
- **RELATED_PATTERNS**: Examples might vary

## Test Scenarios

### Scenario 1: SQL Timeout (Current test_payload.json)
**Expected**:
- SEVERITY: MEDIUM or HIGH
- SAFE_TO_RETRY: true
- ROOT_CAUSE: Database connection timeout
- QUICK_CHECKS: Connection string, network, query performance, database health

### Scenario 2: Null Reference Exception
```json
{
  "ExceptionLogs": "System.NullReferenceException: Object reference not set...",
  "DeveloperEmail": "dev@example.com",
  "JobName": "DataProcessor"
}
```
**Expected**:
- SEVERITY: HIGH
- SAFE_TO_RETRY: false
- ROOT_CAUSE: Null object access

### Scenario 3: Authentication Failure
```json
{
  "ExceptionLogs": "System.UnauthorizedAccessException: Access denied...",
  "DeveloperEmail": "dev@example.com",
  "JobName": "SecurityJob",
  "Environment": "Production"
}
```
**Expected**:
- SEVERITY: CRITICAL
- SAFE_TO_RETRY: false
- ROOT_CAUSE: Authentication/authorization failure

## Troubleshooting

### Results vary between runs
1. Check temperature setting (should be 0.0)
   ```bash
   curl http://localhost:5173/api/health -v | grep Temperature
   ```
2. Verify model deployment supports seed (use GPT-4)
3. Check logs for "Set deterministic seed=42" message

### Email not sending
1. Configure SMTP settings in environment:
   ```bash
   export SMTP_HOST=smtp.gmail.com
   export SMTP_PORT=587
   export SMTP_USER=your-email@gmail.com
   export SMTP_PASS=your-app-password
   export SMTP_FROM=your-email@gmail.com
   export SMTP_SSL=true
   ```

### AI analysis too generic
1. Increase token limit: `export MAX_OUTPUT_TOKENS=3000`
2. Provide more context in JobDescription field
3. Check input normalization didn't strip critical info

## Performance Benchmarks

Typical response times:
- **Normalization**: <50ms
- **AI Analysis**: 2-8 seconds (depends on model/region)
- **Email Delivery**: 1-3 seconds
- **Total**: 3-12 seconds

Token usage (typical):
- **Input**: 800-1500 tokens
- **Output**: 600-1200 tokens
- **Total**: 1400-2700 tokens per analysis

## CI/CD Integration

Add to your pipeline:

```yaml
# .github/workflows/test-determinism.yml
name: Test Determinism
on: [push, pull_request]
jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '9.0.x'
      - name: Build
        run: dotnet build example.csproj
      - name: Run app
        run: dotnet run --project example.csproj &
        env:
          AZURE_OPENAI_ENDPOINT: ${{ secrets.AZURE_OPENAI_ENDPOINT }}
          AZURE_OPENAI_API_KEY: ${{ secrets.AZURE_OPENAI_API_KEY }}
      - name: Wait for startup
        run: sleep 10
      - name: Test determinism
        run: ./test-determinism.sh
      - name: Upload results
        uses: actions/upload-artifact@v3
        with:
          name: test-results
          path: test_results/
```

## Documentation
- Full determinism details: `DETERMINISM.md`
- Configuration guide: `INSTRUCTIONS.md`
- API reference: Swagger UI at `http://localhost:5173/swagger`
