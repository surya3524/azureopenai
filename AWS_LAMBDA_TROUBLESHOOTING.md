# AWS Lambda Troubleshooting Guide

## Quick Diagnostics

### 1. Check Lambda Function Status
```bash
aws lambda get-function \
  --function-name ai-oncall-buddy-stack-AIOnCallBuddyFunction-XXXXX \
  --region us-east-1 \
  --query "Configuration.[State,LastUpdateStatus]" \
  --output text
```

Expected: `Active UpdateSuccessful`

### 2. View Recent Logs (Last 10 Minutes)
```bash
aws logs tail /aws/lambda/ai-oncall-buddy-stack-AIOnCallBuddyFunction-XXXXX \
  --since 10m \
  --region us-east-1
```

### 3. Check for Errors
```bash
aws logs filter-log-events \
  --log-group-name /aws/lambda/ai-oncall-buddy-stack-AIOnCallBuddyFunction-XXXXX \
  --filter-pattern "ERROR" \
  --start-time $(date -u -d '1 hour ago' +%s)000 \
  --region us-east-1
```

## Common Startup Failures

### Error: "Missing Azure OpenAI Endpoint configuration"

**Cause:** Environment variables not set

**Check:**
```bash
aws lambda get-function-configuration \
  --function-name ai-oncall-buddy-stack-AIOnCallBuddyFunction-XXXXX \
  --query "Environment.Variables" \
  --region us-east-1
```

**Fix:**
```bash
aws lambda update-function-configuration \
  --function-name ai-oncall-buddy-stack-AIOnCallBuddyFunction-XXXXX \
  --environment "Variables={AZURE_OPENAI_ENDPOINT=https://your-resource.openai.azure.com/,AZURE_OPENAI_API_KEY=your-key,AZURE_OPENAI_DEPLOYMENT=gpt-4,OPENAI_TEMPERATURE=0,MAX_OUTPUT_TOKENS=6000,EnableSwagger=false}" \
  --region us-east-1
```

### Error: "Task timed out after 30.00 seconds"

**Cause:** Function takes too long (cold start or slow Azure OpenAI response)

**Check execution time:**
```bash
aws logs filter-log-events \
  --log-group-name /aws/lambda/ai-oncall-buddy-stack-AIOnCallBuddyFunction-XXXXX \
  --filter-pattern "Duration:" \
  --start-time $(date -u -d '1 hour ago' +%s)000 \
  --region us-east-1 | grep "Duration"
```

**Fix: Increase timeout to 60 seconds**
```bash
aws lambda update-function-configuration \
  --function-name ai-oncall-buddy-stack-AIOnCallBuddyFunction-XXXXX \
  --timeout 60 \
  --region us-east-1
```

**Fix: Increase memory (faster CPU)**
```bash
aws lambda update-function-configuration \
  --function-name ai-oncall-buddy-stack-AIOnCallBuddyFunction-XXXXX \
  --memory-size 1024 \
  --region us-east-1
```

### Error: "Process exited before completing request"

**Cause:** Application crashed during startup

**Check logs for exceptions:**
```bash
aws logs filter-log-events \
  --log-group-name /aws/lambda/ai-oncall-buddy-stack-AIOnCallBuddyFunction-XXXXX \
  --filter-pattern "Exception" \
  --start-time $(date -u -d '1 hour ago' +%s)000 \
  --region us-east-1
```

**Common causes:**
1. Missing `Amazon.Lambda.AspNetCoreServer.Hosting` package
2. Incorrect handler name in template.json
3. Missing appsettings.json in deployment

**Fix: Verify deployment package**
```bash
# Download current package
aws lambda get-function \
  --function-name ai-oncall-buddy-stack-AIOnCallBuddyFunction-XXXXX \
  --query "Code.Location" \
  --output text | xargs curl -o current-package.zip

# Extract and check contents
unzip -l current-package.zip | grep -E "(appsettings|example.dll)"
```

### Error: "Could not load file or assembly"

**Cause:** Missing dependencies in deployment package

**Check for all required DLLs:**
```bash
unzip -l current-package.zip | grep -E "\.dll$"
```

**Fix: Redeploy with all dependencies**
```bash
dotnet clean
dotnet restore
dotnet publish -c Release -o ./publish
cd publish
zip -r ../deployment.zip .
cd ..

aws lambda update-function-code \
  --function-name ai-oncall-buddy-stack-AIOnCallBuddyFunction-XXXXX \
  --zip-file fileb://deployment.zip \
  --region us-east-1
```

## Health Check Diagnostics

### Test Health Endpoint
```bash
# Get API URL
export API_URL=$(aws cloudformation describe-stacks \
  --stack-name ai-oncall-buddy-stack \
  --query "Stacks[0].Outputs[?OutputKey=='ApiUrl'].OutputValue" \
  --output text)

# Test with verbose output
curl -v $API_URL/api/health

# Test with timing
time curl $API_URL/api/health
```

**Expected Response:**
```json
{
  "status": "ok",
  "version": "1.0.14"
}
```

**If 502/504 Error:**
- Function is crashing or timing out
- Check CloudWatch logs immediately
- Function may not be starting properly

**If 403 Error:**
- API Gateway authorization issue
- Check IAM permissions
- Verify API Gateway configuration

## Detailed Log Analysis

### Find Startup Logs
```bash
aws logs filter-log-events \
  --log-group-name /aws/lambda/ai-oncall-buddy-stack-AIOnCallBuddyFunction-XXXXX \
  --filter-pattern "APPLICATION STARTING" \
  --start-time $(date -u -d '1 hour ago' +%s)000 \
  --region us-east-1
```

**Expected output:**
```
=== APPLICATION STARTING ===
Environment: Production
Time: 2024-01-15 10:30:00 UTC
Configuration loaded from:
  - JsonConfigurationProvider
  - EnvironmentVariablesConfigurationProvider
=== BUILDING APPLICATION ===
=== APPLICATION BUILT SUCCESSFULLY ===
Application configured and ready
```

### Find Cold Start Duration
```bash
aws logs filter-log-events \
  --log-group-name /aws/lambda/ai-oncall-buddy-stack-AIOnCallBuddyFunction-XXXXX \
  --filter-pattern "Init Duration" \
  --start-time $(date -u -d '1 hour ago' +%s)000 \
  --region us-east-1
```

**Example:**
```
REPORT RequestId: xxx Duration: 5000.00 ms Billed Duration: 5000 ms
Memory Size: 512 MB Max Memory Used: 250 MB Init Duration: 3500.00 ms
```

- **Init Duration > 5000ms**: Slow cold start (consider increasing memory)
- **Memory Used > 80%**: Increase memory size
- **Duration near timeout**: Increase timeout or optimize code

### Find Configuration Issues
```bash
aws logs filter-log-events \
  --log-group-name /aws/lambda/ai-oncall-buddy-stack-AIOnCallBuddyFunction-XXXXX \
  --filter-pattern "Missing" \
  --start-time $(date -u -d '1 hour ago' +%s)000 \
  --region us-east-1
```

## API Gateway Issues

### Check API Gateway Logs
```bash
# Enable API Gateway logging (if not enabled)
aws apigatewayv2 get-stage \
  --api-id YOUR_API_ID \
  --stage-name $default \
  --region us-east-1
```

### Test Lambda Directly (Bypass API Gateway)
```bash
aws lambda invoke \
  --function-name ai-oncall-buddy-stack-AIOnCallBuddyFunction-XXXXX \
  --payload '{"httpMethod":"GET","path":"/api/health"}' \
  --region us-east-1 \
  response.json

cat response.json
```

## Performance Monitoring

### View Metrics
```bash
# Get function metrics
aws cloudwatch get-metric-statistics \
  --namespace AWS/Lambda \
  --metric-name Duration \
  --dimensions Name=FunctionName,Value=ai-oncall-buddy-stack-AIOnCallBuddyFunction-XXXXX \
  --start-time $(date -u -d '1 hour ago' --iso-8601) \
  --end-time $(date -u --iso-8601) \
  --period 300 \
  --statistics Average,Maximum \
  --region us-east-1
```

### Check Concurrency
```bash
aws cloudwatch get-metric-statistics \
  --namespace AWS/Lambda \
  --metric-name ConcurrentExecutions \
  --dimensions Name=FunctionName,Value=ai-oncall-buddy-stack-AIOnCallBuddyFunction-XXXXX \
  --start-time $(date -u -d '1 hour ago' --iso-8601) \
  --end-time $(date -u --iso-8601) \
  --period 300 \
  --statistics Maximum \
  --region us-east-1
```

## Cost Analysis

### Check Current Costs
```bash
# Lambda invocations today
aws cloudwatch get-metric-statistics \
  --namespace AWS/Lambda \
  --metric-name Invocations \
  --dimensions Name=FunctionName,Value=ai-oncall-buddy-stack-AIOnCallBuddyFunction-XXXXX \
  --start-time $(date -u -d '1 day ago' --iso-8601) \
  --end-time $(date -u --iso-8601) \
  --period 86400 \
  --statistics Sum \
  --region us-east-1
```

### Estimate Costs
```bash
# Get duration metrics
DURATION=$(aws cloudwatch get-metric-statistics \
  --namespace AWS/Lambda \
  --metric-name Duration \
  --dimensions Name=FunctionName,Value=ai-oncall-buddy-stack-AIOnCallBuddyFunction-XXXXX \
  --start-time $(date -u -d '1 day ago' --iso-8601) \
  --end-time $(date -u --iso-8601) \
  --period 86400 \
  --statistics Average \
  --query "Datapoints[0].Average" \
  --output text \
  --region us-east-1)

echo "Average Duration: ${DURATION}ms"
echo "Cost per invocation: ~$0.0000002"
```

## Emergency Actions

### Disable Function (Stop ALL requests)
```bash
# Update to return 503
aws lambda update-function-configuration \
  --function-name ai-oncall-buddy-stack-AIOnCallBuddyFunction-XXXXX \
  --environment "Variables={DISABLED=true}" \
  --region us-east-1
```

### Reduce Token Usage (Emergency cost control)
```bash
aws lambda update-function-configuration \
  --function-name ai-oncall-buddy-stack-AIOnCallBuddyFunction-XXXXX \
  --environment "Variables={MAX_OUTPUT_TOKENS=500}" \
  --region us-east-1
```

### Roll Back to Previous Version
```bash
# List versions
aws lambda list-versions-by-function \
  --function-name ai-oncall-buddy-stack-AIOnCallBuddyFunction-XXXXX \
  --region us-east-1

# Update alias to previous version
aws lambda update-alias \
  --function-name ai-oncall-buddy-stack-AIOnCallBuddyFunction-XXXXX \
  --name live \
  --function-version 2 \
  --region us-east-1
```

## Debugging Checklist

- [ ] Function status is "Active"
- [ ] Environment variables are set correctly
- [ ] CloudWatch logs show startup messages
- [ ] Health endpoint returns 200 OK
- [ ] No errors in last 10 minutes of logs
- [ ] Memory usage < 80% of allocated
- [ ] Duration < 80% of timeout
- [ ] Cold start < 10 seconds
- [ ] API Gateway returns valid responses
- [ ] Costs are within budget

## Getting Help

### Collect Diagnostic Information
```bash
# Create diagnostic report
cat > diagnostic-report.txt <<EOF
Function Name: ai-oncall-buddy-stack-AIOnCallBuddyFunction-XXXXX
Region: us-east-1
Date: $(date)

=== Function Configuration ===
$(aws lambda get-function-configuration \
  --function-name ai-oncall-buddy-stack-AIOnCallBuddyFunction-XXXXX \
  --region us-east-1)

=== Recent Logs ===
$(aws logs tail /aws/lambda/ai-oncall-buddy-stack-AIOnCallBuddyFunction-XXXXX \
  --since 30m \
  --region us-east-1)

=== Recent Errors ===
$(aws logs filter-log-events \
  --log-group-name /aws/lambda/ai-oncall-buddy-stack-AIOnCallBuddyFunction-XXXXX \
  --filter-pattern "ERROR" \
  --start-time $(date -u -d '30 minutes ago' +%s)000 \
  --region us-east-1)
EOF

cat diagnostic-report.txt
```

Share this report when asking for help!
