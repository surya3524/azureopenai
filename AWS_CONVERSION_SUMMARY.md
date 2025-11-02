# AWS Lambda Conversion Summary

## Changes Made

### 1. Project File (`example.csproj`)
**Added AWS Lambda Support:**
- `Amazon.Lambda.AspNetCoreServer.Hosting` (v1.9.0)
- `Amazon.Lambda.Core` (v2.7.0)
- `Amazon.Lambda.Serialization.SystemTextJson` (v2.4.4)
- Added `<AWSProjectType>Lambda</AWSProjectType>`
- Added `<GenerateRuntimeConfigurationFiles>true</GenerateRuntimeConfigurationFiles>`

### 2. Application Code (`run_model.cs`)
**Added:**
- Lambda hosting: `builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);`
- Enhanced logging for troubleshooting
- Startup diagnostics (`=== APPLICATION STARTING ===`)
- Conditional Swagger (disabled by default for cost savings)
- Configuration source logging

### 3. AWS Configuration Files (New)

**`template.json`** - CloudFormation/SAM template
- Lambda function definition
- API Gateway HTTP API
- Environment variables configuration
- 512 MB memory (free tier)
- 30 second timeout
- Parameters for Azure OpenAI credentials

**`aws-lambda-tools-defaults.json`** - Deployment settings
- S3 bucket configuration
- Stack name
- Region settings

**`parameters.json`** - Environment-specific parameters
- Azure OpenAI Endpoint
- Azure OpenAI API Key
- Azure OpenAI Deployment name

### 4. Documentation (New)

**`AWS_QUICKSTART.md`**
- 5-minute deployment guide
- Essential commands
- Quick troubleshooting

**`AWS_LAMBDA_DEPLOYMENT.md`**
- Complete deployment instructions
- Cost optimization strategies
- Health check procedures
- Monitoring setup

**`AWS_LAMBDA_TROUBLESHOOTING.md`**
- Common errors and solutions
- Log analysis commands
- Performance diagnostics
- Emergency actions

### 5. Configuration Updates

**`appsettings.json`**
- Added `EnableSwagger: false` for production
- Prepared for Lambda environment

**`.gitignore`**
- Added AWS Lambda artifacts
- Excluded deployment packages
- Protected credentials files

## Cost Optimization Features

### FREE TIER Configuration
```json
{
  "MemorySize": 512,
  "Timeout": 30,
  "EnableSwagger": false,
  "MaxOutputTokens": 2000
}
```

### What's Disabled for Cost Savings
1. ? **Swagger UI** - Disabled in production (`EnableSwagger: false`)
2. ? **Provisioned Concurrency** - Not configured (cold starts acceptable)
3. ? **Excessive Logging** - Only essential logs
4. ? **High Memory** - 512 MB (minimum needed)
5. ? **Long Timeout** - 30 seconds (adjustable if needed)

### What's Enabled for Troubleshooting
1. ? **CloudWatch Logs** - All console output captured
2. ? **Startup Diagnostics** - Detailed logging at startup
3. ? **Health Endpoint** - `/api/health` for status checks
4. ? **Error Logging** - All exceptions logged
5. ? **Configuration Logging** - Shows what config was loaded

## Health Check Indicators

### Application Startup (Logs)
```
=== APPLICATION STARTING ===
Environment: Production
Time: 2024-01-15 10:30:00 UTC
Configuration loaded from:
  - JsonConfigurationProvider
  - EnvironmentVariablesConfigurationProvider
Swagger disabled (cost optimization)
=== BUILDING APPLICATION ===
=== APPLICATION BUILT SUCCESSFULLY ===
Application configured and ready
Environment: Production
ContentRootPath: /var/task
Middleware pipeline configured
```

### Health Endpoint Response
```bash
curl https://YOUR-API-URL/api/health
# Expected:
# {"status":"ok","version":"1.0.14"}
```

### CloudWatch Metrics to Monitor
- **Invocations**: Number of requests
- **Duration**: Execution time (should be < 5000ms after warm)
- **Errors**: Should be 0
- **Throttles**: Should be 0
- **ConcurrentExecutions**: Number of parallel executions

## Failure Indicators

### Application Won't Start
**Logs will show:**
- Missing `=== APPLICATION BUILT SUCCESSFULLY ===`
- Exception stack traces
- "Missing Azure OpenAI Endpoint configuration"
- "Process exited before completing request"

**Solutions:**
1. Check environment variables are set
2. Verify all DLLs are in deployment package
3. Increase timeout if slow
4. Check memory usage

### Health Check Fails
**Possible responses:**
- `502 Bad Gateway` - Lambda crashed
- `504 Gateway Timeout` - Lambda too slow
- `403 Forbidden` - IAM/API Gateway issue
- No response - Function not deployed

**Solutions:**
1. Check CloudWatch logs immediately
2. Verify Lambda function exists
3. Test Lambda directly (bypass API Gateway)
4. Increase timeout

### High Costs
**Check these metrics:**
1. Invocations > 1M/month (beyond free tier)
2. Duration > 5 seconds average (inefficient)
3. Memory usage > 80% (increase memory)
4. Provisioned concurrency enabled (EXPENSIVE - should be disabled)

**Solutions:**
1. Reduce `MaxOutputTokens` to 500-1000
2. Cache responses if possible
3. Implement rate limiting
4. Monitor with budget alerts

## Deployment Checklist

Before deploying:
- [ ] AWS CLI installed and configured
- [ ] Lambda Tools installed (`dotnet tool install -g Amazon.Lambda.Tools`)
- [ ] S3 bucket created for deployment
- [ ] `parameters.json` updated with Azure OpenAI credentials
- [ ] `aws-lambda-tools-defaults.json` updated with S3 bucket name
- [ ] Budget alert configured (optional but recommended)

After deploying:
- [ ] Health endpoint returns 200 OK
- [ ] CloudWatch logs show successful startup
- [ ] No errors in logs
- [ ] Test one API call successfully
- [ ] Verify costs are within free tier
- [ ] Save API URL for future use

## Monitoring Commands

### Quick Status Check
```bash
# Health check
curl https://YOUR-API-URL/api/health

# Recent logs
aws logs tail /aws/lambda/FUNCTION-NAME --since 10m

# Today's invocations
aws cloudwatch get-metric-statistics \
  --namespace AWS/Lambda \
  --metric-name Invocations \
  --dimensions Name=FunctionName,Value=FUNCTION-NAME \
  --start-time $(date -u -d '1 day ago' --iso-8601) \
  --end-time $(date -u --iso-8601) \
  --period 86400 \
  --statistics Sum
```

### Error Check
```bash
# Find errors in last hour
aws logs filter-log-events \
  --log-group-name /aws/lambda/FUNCTION-NAME \
  --filter-pattern "ERROR" \
  --start-time $(date -u -d '1 hour ago' +%s)000
```

## What to Expect

### First Deployment (Cold Start)
- **Duration**: 5-15 seconds
- **Memory**: 200-300 MB
- **Init Duration**: 3-8 seconds
- **Status**: May timeout on first request (normal)

### Subsequent Requests (Warm)
- **Duration**: 1-3 seconds
- **Memory**: 150-250 MB
- **Init Duration**: 0 seconds
- **Status**: Fast and reliable

### After 15 Minutes Idle
- Function goes cold
- Next request will be slow again
- This is NORMAL for free tier
- Don't enable provisioned concurrency (expensive)

## Support

### If deployment fails:
1. Check `AWS_LAMBDA_DEPLOYMENT.md`
2. Run troubleshooting commands
3. Create diagnostic report

### If application fails:
1. Check `AWS_LAMBDA_TROUBLESHOOTING.md`
2. View CloudWatch logs
3. Test health endpoint

### If costs are high:
1. Check invocation count
2. Reduce `MaxOutputTokens`
3. Disable Swagger if enabled
4. Set budget alerts

## Next Steps

1. **Deploy**: Follow `AWS_QUICKSTART.md`
2. **Test**: Verify health endpoint works
3. **Monitor**: Check logs and metrics
4. **Optimize**: Adjust settings based on usage
5. **Scale**: Only when needed (stay in free tier for testing)
