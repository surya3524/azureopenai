# AWS Lambda Quick Start

## TL;DR - Deploy in 5 Minutes

### 1. Install Prerequisites (2 minutes)
```powershell
# Install AWS CLI
# Download from: https://aws.amazon.com/cli/

# Install Lambda Tools
dotnet tool install -g Amazon.Lambda.Tools

# Verify
aws --version
dotnet lambda --version
```

### 2. Configure AWS (1 minute)
```bash
aws configure
# Enter your AWS Access Key ID, Secret Key, and region (us-east-1)
```

### 3. Update Configuration (1 minute)

Edit `parameters.json`:
```json
{
  "Parameters": {
    "AzureOpenAIEndpoint": "https://YOUR-RESOURCE.openai.azure.com/",
    "AzureOpenAIApiKey": "YOUR-API-KEY-HERE",
    "AzureOpenAIDeployment": "gpt-4"
  }
}
```

Edit `aws-lambda-tools-defaults.json`:
```json
{
  "s3-bucket": "YOUR-UNIQUE-BUCKET-NAME"
}
```

### 4. Deploy (1 minute)
```bash
# Create S3 bucket
aws s3 mb s3://YOUR-UNIQUE-BUCKET-NAME

# Deploy
dotnet lambda deploy-serverless

# Get API URL
aws cloudformation describe-stacks \
  --stack-name ai-oncall-buddy-stack \
  --query "Stacks[0].Outputs[?OutputKey=='ApiUrl'].OutputValue" \
  --output text
```

### 5. Test
```bash
# Test health endpoint
curl https://YOUR-API-URL/api/health

# Should return:
# {"status":"ok","version":"1.0.14"}
```

## Cost Control

### FREE TIER LIMITS
- ? 1 Million requests/month
- ? 400,000 GB-seconds compute
- ? ~800,000 seconds execution @ 512MB

### Settings for FREE Usage
```json
{
  "MemorySize": 512,
  "Timeout": 30,
  "EnableSwagger": false,
  "MaxOutputTokens": 2000
}
```

### Monitor Costs
```bash
# View today's invocations
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

## Troubleshooting

### Check Logs
```bash
# View recent logs
aws logs tail /aws/lambda/ai-oncall-buddy-stack-AIOnCallBuddyFunction-XXXXX --follow

# Search for errors
aws logs filter-log-events \
  --log-group-name /aws/lambda/ai-oncall-buddy-stack-AIOnCallBuddyFunction-XXXXX \
  --filter-pattern "ERROR"
```

### Common Issues

**"Task timed out"**
```bash
# Increase timeout to 60s
aws lambda update-function-configuration \
  --function-name ai-oncall-buddy-stack-AIOnCallBuddyFunction-XXXXX \
  --timeout 60
```

**"Missing Azure OpenAI Endpoint"**
```bash
# Update environment variables
aws lambda update-function-configuration \
  --function-name ai-oncall-buddy-stack-AIOnCallBuddyFunction-XXXXX \
  --environment "Variables={AZURE_OPENAI_ENDPOINT=https://your-resource.openai.azure.com/,AZURE_OPENAI_API_KEY=your-key}"
```

## Cleanup

```bash
# Delete everything
aws cloudformation delete-stack --stack-name ai-oncall-buddy-stack
aws s3 rm s3://YOUR-BUCKET-NAME --recursive
aws s3 rb s3://YOUR-BUCKET-NAME
```

## Full Documentation

- **Deployment**: See `AWS_LAMBDA_DEPLOYMENT.md`
- **Troubleshooting**: See `AWS_LAMBDA_TROUBLESHOOTING.md`

## Support

Check these in order:
1. Health endpoint: `curl https://YOUR-API-URL/api/health`
2. CloudWatch logs: `aws logs tail /aws/lambda/...`
3. Function status: `aws lambda get-function --function-name ...`
4. Stack status: `aws cloudformation describe-stacks --stack-name ai-oncall-buddy-stack`
