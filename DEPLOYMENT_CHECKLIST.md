# AWS Lambda Deployment Checklist

## ? Pre-Deployment Checklist

### Prerequisites Installation
- [ ] AWS CLI installed (`aws --version`)
- [ ] .NET SDK installed (`dotnet --version`)
- [ ] AWS Lambda Tools installed (`dotnet lambda --version`)
- [ ] AWS credentials configured (`aws configure`)

### Configuration Files
- [ ] `parameters.json` - Azure OpenAI credentials updated
- [ ] `aws-lambda-tools-defaults.json` - S3 bucket name set
- [ ] `appsettings.json` - EnableSwagger set to false
- [ ] Build successful (`dotnet build`)

### AWS Account Setup
- [ ] AWS account created
- [ ] IAM user with Lambda permissions
- [ ] S3 bucket created (or will be created during deployment)
- [ ] Budget alert configured (recommended)

## ?? Deployment Steps

### 1. Quick Deploy (Recommended)
```powershell
./deploy-lambda.ps1
```

### 2. Manual Deploy (Alternative)
```bash
# Step 1: Create S3 bucket
aws s3 mb s3://YOUR-BUCKET-NAME

# Step 2: Deploy
dotnet lambda deploy-serverless

# Step 3: Get API URL
aws cloudformation describe-stacks \
  --stack-name ai-oncall-buddy-stack \
  --query "Stacks[0].Outputs[?OutputKey=='ApiUrl'].OutputValue" \
  --output text
```

## ? Post-Deployment Verification

### Immediate Checks (< 1 minute)
- [ ] Deployment completed without errors
- [ ] CloudFormation stack created successfully
- [ ] API Gateway endpoint URL received

### Health Checks (< 2 minutes)
```bash
# Save your API URL
export API_URL="https://YOUR-API-ID.execute-api.us-east-1.amazonaws.com/"

# Test health endpoint
curl $API_URL/api/health
# Expected: {"status":"ok","version":"1.0.14"}
```

### Log Verification (< 3 minutes)
```bash
# View recent logs
aws logs tail /aws/lambda/ai-oncall-buddy-stack-AIOnCallBuddyFunction-XXXXX --since 5m

# Look for these messages:
# ? === APPLICATION STARTING ===
# ? === APPLICATION BUILT SUCCESSFULLY ===
# ? Application configured and ready
```

### Function Configuration (< 1 minute)
```bash
# Check function exists
aws lambda get-function \
  --function-name ai-oncall-buddy-stack-AIOnCallBuddyFunction-XXXXX \
  --query "Configuration.[State,LastUpdateStatus,MemorySize,Timeout]"

# Expected: ["Active","Successful",512,30]
```

## ?? Detailed Verification

### 1. CloudFormation Stack Status
```bash
aws cloudformation describe-stacks \
  --stack-name ai-oncall-buddy-stack \
  --query "Stacks[0].StackStatus"
# Expected: "CREATE_COMPLETE" or "UPDATE_COMPLETE"
```

### 2. Lambda Function Logs
```bash
# Check for startup messages
aws logs filter-log-events \
  --log-group-name /aws/lambda/ai-oncall-buddy-stack-AIOnCallBuddyFunction-XXXXX \
  --filter-pattern "APPLICATION STARTING"

# Check for errors
aws logs filter-log-events \
  --log-group-name /aws/lambda/ai-oncall-buddy-stack-AIOnCallBuddyFunction-XXXXX \
  --filter-pattern "ERROR"
# Expected: No results (0 errors)
```

### 3. Environment Variables
```bash
aws lambda get-function-configuration \
  --function-name ai-oncall-buddy-stack-AIOnCallBuddyFunction-XXXXX \
  --query "Environment.Variables"

# Verify these are set:
# - AZURE_OPENAI_ENDPOINT
# - AZURE_OPENAI_API_KEY
# - AZURE_OPENAI_DEPLOYMENT
# - EnableSwagger: false
```

### 4. API Gateway Configuration
```bash
# Get API ID
aws cloudformation describe-stacks \
  --stack-name ai-oncall-buddy-stack \
  --query "Stacks[0].Outputs[?OutputKey=='ApiUrl'].OutputValue" \
  --output text

# Test different endpoints
curl $API_URL/api/health              # Should return 200
curl $API_URL/swagger                 # Should return 404 (disabled)
```

## ?? Functional Testing

### Test 1: Health Endpoint
```bash
curl -v $API_URL/api/health
# Expected:
# HTTP/2 200
# {"status":"ok","version":"1.0.14"}
```

### Test 2: Chat Endpoint (Minimal Test)
```bash
curl -X POST $API_URL/api/chat \
  -H "Content-Type: application/json" \
  -d '{
    "prompt": "test connection",
    "temperature": 0,
    "maxTokens": 100
  }'

# Expected: JSON response with AI analysis
# Note: This uses Azure OpenAI tokens
```

### Test 3: Cold Start Performance
```bash
# Wait 15 minutes for function to go cold
sleep 900

# Test cold start
time curl $API_URL/api/health

# Expected:
# - First request: 5-15 seconds (cold start)
# - Subsequent requests: < 1 second (warm)
```

## ?? Monitoring Setup

### Enable CloudWatch Alarms (Optional)
```bash
# Create alarm for errors
aws cloudwatch put-metric-alarm \
  --alarm-name ai-oncall-buddy-errors \
  --alarm-description "Alert on Lambda errors" \
  --metric-name Errors \
  --namespace AWS/Lambda \
  --statistic Sum \
  --period 300 \
  --evaluation-periods 1 \
  --threshold 1 \
  --comparison-operator GreaterThanThreshold \
  --dimensions Name=FunctionName,Value=ai-oncall-buddy-stack-AIOnCallBuddyFunction-XXXXX
```

### Set Budget Alert
```bash
# Create budget.json
cat > budget.json <<EOF
{
  "BudgetName": "AIOnCallBudget",
  "BudgetLimit": {
    "Amount": "5",
    "Unit": "USD"
  },
  "TimeUnit": "MONTHLY",
  "BudgetType": "COST"
}
EOF

# Create budget
aws budgets create-budget \
  --account-id $(aws sts get-caller-identity --query Account --output text) \
  --budget file://budget.json
```

## ?? Cost Verification

### Check Current Usage
```bash
# Today's invocations
aws cloudwatch get-metric-statistics \
  --namespace AWS/Lambda \
  --metric-name Invocations \
  --dimensions Name=FunctionName,Value=ai-oncall-buddy-stack-AIOnCallBuddyFunction-XXXXX \
  --start-time $(date -u -d '1 day ago' --iso-8601) \
  --end-time $(date -u --iso-8601) \
  --period 86400 \
  --statistics Sum

# Expected: Low number during testing (< 100)
```

### Verify Free Tier Settings
- [ ] Memory: 512 MB (within free tier)
- [ ] Timeout: 30 seconds (reasonable)
- [ ] Provisioned concurrency: DISABLED (should be 0)
- [ ] Invocations today: < 1000 (safe for testing)

## ?? Troubleshooting Checklist

### If Health Check Fails

- [ ] Wait 30 seconds and try again (cold start)
- [ ] Check CloudWatch logs for errors
- [ ] Verify Lambda function exists
- [ ] Check API Gateway configuration
- [ ] Test Lambda directly (bypass API Gateway)

### If Logs Show Errors

- [ ] Check environment variables are set
- [ ] Verify Azure OpenAI credentials are correct
- [ ] Increase timeout if timing out
- [ ] Check memory usage (< 80% of 512 MB)
- [ ] Review stack trace in logs

### If Costs Are High

- [ ] Check invocation count (should be low)
- [ ] Verify provisioned concurrency is disabled
- [ ] Reduce MaxOutputTokens to 1000
- [ ] Enable budget alerts
- [ ] Review CloudWatch Insights for usage patterns

## ? Success Criteria

### Deployment Successful
- [x] CloudFormation stack status: CREATE_COMPLETE
- [x] Lambda function status: Active
- [x] API Gateway endpoint created
- [x] No errors in CloudWatch logs

### Application Healthy
- [x] Health endpoint returns 200 OK
- [x] Logs show successful startup messages
- [x] No errors in last 30 minutes
- [x] Test API call completes successfully

### Cost Optimized
- [x] Memory: 512 MB (not higher)
- [x] Timeout: 30-60 seconds
- [x] Swagger: Disabled
- [x] Provisioned concurrency: Disabled
- [x] Invocations: < 1M/month (free tier)

## ?? Ongoing Monitoring

### Daily Checks
```bash
# Check for errors
aws logs filter-log-events \
  --log-group-name /aws/lambda/FUNCTION-NAME \
  --filter-pattern "ERROR" \
  --start-time $(date -u -d '1 day ago' +%s)000

# Check invocations
aws cloudwatch get-metric-statistics \
  --namespace AWS/Lambda \
  --metric-name Invocations \
  --dimensions Name=FunctionName,Value=FUNCTION-NAME \
  --start-time $(date -u -d '1 day ago' --iso-8601) \
  --end-time $(date -u --iso-8601) \
  --period 86400 \
  --statistics Sum
```

### Weekly Checks
- [ ] Review CloudWatch metrics
- [ ] Check AWS billing dashboard
- [ ] Verify free tier usage
- [ ] Review any error patterns
- [ ] Optimize if needed

### Monthly Checks
- [ ] Review total costs
- [ ] Analyze usage patterns
- [ ] Optimize MaxOutputTokens if needed
- [ ] Update documentation
- [ ] Plan capacity if scaling

## ?? Next Steps

### After Successful Deployment
1. Save API URL in a safe place
2. Document any custom configurations
3. Share API URL with team
4. Set up monitoring alerts
5. Review documentation

### For Production Use
1. Enable custom domain
2. Set up WAF (Web Application Firewall)
3. Configure VPC if needed
4. Implement rate limiting
5. Enable X-Ray tracing
6. Set up CI/CD pipeline

## ?? Support Resources

### Documentation
- `AWS_QUICKSTART.md` - Fast deployment
- `AWS_LAMBDA_DEPLOYMENT.md` - Full guide
- `AWS_LAMBDA_TROUBLESHOOTING.md` - Common issues
- `README_AWS_LAMBDA.md` - Overview

### AWS Resources
- CloudWatch Logs: Console ? CloudWatch ? Log Groups
- Lambda Console: Console ? Lambda ? Functions
- CloudFormation: Console ? CloudFormation ? Stacks
- Billing: Console ? Billing ? Bills

### Commands Reference
```bash
# View logs
aws logs tail /aws/lambda/FUNCTION-NAME --follow

# Update environment
aws lambda update-function-configuration \
  --function-name FUNCTION-NAME \
  --environment "Variables={KEY=VALUE}"

# Delete stack
aws cloudformation delete-stack --stack-name ai-oncall-buddy-stack
```

---

**Deployment Complete!** ?

If all checkboxes are marked, your Lambda function is successfully deployed and ready to use!
