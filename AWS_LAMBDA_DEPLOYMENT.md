# AWS Lambda Deployment Guide

## Prerequisites

### 1. Install AWS Tools
```bash
# Install AWS CLI
# Windows: Download from https://aws.amazon.com/cli/
# Mac: brew install awscli
# Linux: apt-get install awscli

# Install AWS Lambda Tools for .NET
dotnet tool install -g Amazon.Lambda.Tools

# Verify installation
aws --version
dotnet lambda --help
```

### 2. Configure AWS Credentials
```bash
# Configure AWS CLI with your credentials
aws configure

# Enter:
# - AWS Access Key ID
# - AWS Secret Access Key
# - Default region (e.g., us-east-1)
# - Default output format (json)
```

### 3. Update Configuration Files

**parameters.json** - Add your Azure OpenAI credentials:
```json
{
  "Parameters": {
    "AzureOpenAIEndpoint": "https://your-resource.openai.azure.com/",
    "AzureOpenAIApiKey": "your-actual-api-key",
    "AzureOpenAIDeployment": "gpt-4"
  }
}
```

**aws-lambda-tools-defaults.json** - Add S3 bucket:
```json
{
  "s3-bucket": "your-deployment-bucket-name"
}
```

## Deployment Steps

### Option 1: Using AWS Lambda Tools (Recommended)

```bash
# 1. Navigate to project directory
cd C:\Mac\Home\Documents\code\azureopenai

# 2. Restore packages
dotnet restore

# 3. Create S3 bucket for deployment (one-time)
aws s3 mb s3://your-deployment-bucket-name --region us-east-1

# 4. Deploy to AWS Lambda
dotnet lambda deploy-serverless \
  --stack-name ai-oncall-buddy-stack \
  --s3-bucket your-deployment-bucket-name \
  --s3-prefix ai-oncall-buddy \
  --region us-east-1 \
  --template template.json \
  --template-parameters parameters.json

# 5. Get the API endpoint
aws cloudformation describe-stacks \
  --stack-name ai-oncall-buddy-stack \
  --query "Stacks[0].Outputs[?OutputKey=='ApiUrl'].OutputValue" \
  --output text
```

### Option 2: Manual Deployment

```bash
# 1. Build and package
dotnet publish -c Release -o ./publish

# 2. Create deployment package
cd publish
zip -r ../deployment.zip .
cd ..

# 3. Upload to S3
aws s3 cp deployment.zip s3://your-deployment-bucket-name/ai-oncall-buddy/

# 4. Deploy CloudFormation stack
aws cloudformation deploy \
  --template-file template.json \
  --stack-name ai-oncall-buddy-stack \
  --parameter-overrides file://parameters.json \
  --capabilities CAPABILITY_IAM \
  --region us-east-1
```

## Cost Optimization for Troubleshooting

### Minimal Configuration (FREE TIER)
```json
{
  "MemorySize": 512,      // 512 MB (within free tier)
  "Timeout": 30,          // 30 seconds
  "EnableSwagger": false, // Disabled to reduce cold start
  "MaxOutputTokens": 2000 // Reduced from 6000
}
```

### AWS Lambda Free Tier
- **1 Million requests** per month (FREE)
- **400,000 GB-seconds** of compute time per month (FREE)
- **512 MB memory** = ~800,000 seconds of execution (FREE)

### Cost Estimates (Beyond Free Tier)
- **Lambda**: $0.20 per 1M requests
- **API Gateway**: $1.00 per 1M requests
- **CloudWatch Logs**: $0.50 per GB (retain only 7 days)

### Disable Expensive Features
```bash
# Set in Lambda environment variables
EnableSwagger=false              # Disable Swagger UI
MAX_OUTPUT_TOKENS=2000          # Reduce token usage
OPENAI_TEMPERATURE=0            # Consistent responses
```

## Health Check Testing

### 1. Test Health Endpoint
```bash
# Get your API URL
export API_URL=$(aws cloudformation describe-stacks \
  --stack-name ai-oncall-buddy-stack \
  --query "Stacks[0].Outputs[?OutputKey=='ApiUrl'].OutputValue" \
  --output text)

# Test health endpoint
curl $API_URL/api/health

# Expected response:
# {"status":"ok","version":"1.0.14"}
```

### 2. Test Chat Endpoint (Minimal)
```bash
# Minimal test to avoid costs
curl -X POST $API_URL/api/chat \
  -H "Content-Type: application/json" \
  -d '{
    "prompt": "test",
    "temperature": 0,
    "maxTokens": 100
  }'
```

## View Logs

### CloudWatch Logs
```bash
# View latest logs
aws logs tail /aws/lambda/ai-oncall-buddy-stack-AIOnCallBuddyFunction-XXXXX \
  --follow \
  --region us-east-1

# Filter for errors
aws logs filter-log-events \
  --log-group-name /aws/lambda/ai-oncall-buddy-stack-AIOnCallBuddyFunction-XXXXX \
  --filter-pattern "ERROR" \
  --region us-east-1
```

### Important Log Patterns to Look For

**Successful Startup:**
```
=== APPLICATION STARTING ===
Environment: Production
=== APPLICATION BUILT SUCCESSFULLY ===
Application configured and ready
```

**Configuration Issues:**
```
Missing Azure OpenAI Endpoint configuration
Invalid Azure OpenAI endpoint URL
```

**Lambda Issues:**
```
Task timed out after 30.00 seconds
Process exited before completing request
```

## Troubleshooting

### Issue: Function Times Out

**Solution 1: Increase timeout**
```bash
aws lambda update-function-configuration \
  --function-name ai-oncall-buddy-stack-AIOnCallBuddyFunction-XXXXX \
  --timeout 60 \
  --region us-east-1
```

**Solution 2: Reduce token usage**
```bash
aws lambda update-function-configuration \
  --function-name ai-oncall-buddy-stack-AIOnCallBuddyFunction-XXXXX \
  --environment "Variables={MAX_OUTPUT_TOKENS=1000}" \
  --region us-east-1
```

### Issue: Cold Start Latency

**Check cold start time:**
```bash
aws logs filter-log-events \
  --log-group-name /aws/lambda/ai-oncall-buddy-stack-AIOnCallBuddyFunction-XXXXX \
  --filter-pattern "Init Duration" \
  --region us-east-1
```

**Solution: Provisioned Concurrency (costs money)**
```bash
# Only enable if needed (NOT recommended for troubleshooting)
aws lambda put-provisioned-concurrency-config \
  --function-name ai-oncall-buddy-stack-AIOnCallBuddyFunction-XXXXX \
  --provisioned-concurrent-executions 1 \
  --region us-east-1
```

### Issue: Missing Environment Variables

**List current variables:**
```bash
aws lambda get-function-configuration \
  --function-name ai-oncall-buddy-stack-AIOnCallBuddyFunction-XXXXX \
  --query "Environment.Variables" \
  --region us-east-1
```

**Update variables:**
```bash
aws lambda update-function-configuration \
  --function-name ai-oncall-buddy-stack-AIOnCallBuddyFunction-XXXXX \
  --environment "Variables={AZURE_OPENAI_ENDPOINT=https://your-resource.openai.azure.com/,AZURE_OPENAI_API_KEY=your-key}" \
  --region us-east-1
```

## Cleanup (When Done Testing)

```bash
# Delete the CloudFormation stack
aws cloudformation delete-stack \
  --stack-name ai-oncall-buddy-stack \
  --region us-east-1

# Empty and delete S3 bucket
aws s3 rm s3://your-deployment-bucket-name --recursive
aws s3 rb s3://your-deployment-bucket-name

# Verify deletion
aws cloudformation describe-stacks \
  --stack-name ai-oncall-buddy-stack \
  --region us-east-1
# Should return: "Stack with id ai-oncall-buddy-stack does not exist"
```

## Monitoring Costs

### View Current Month Costs
```bash
# Install AWS Cost Explorer (if not installed)
pip install awscli-plugin-cost

# View costs for this month
aws ce get-cost-and-usage \
  --time-period Start=$(date -u +%Y-%m-01),End=$(date -u +%Y-%m-%d) \
  --granularity MONTHLY \
  --metrics BlendedCost \
  --group-by Type=SERVICE
```

### Set Budget Alert
```bash
# Create budget (one-time, $5 monthly limit)
aws budgets create-budget \
  --account-id $(aws sts get-caller-identity --query Account --output text) \
  --budget file://budget.json

# budget.json content:
cat > budget.json <<EOF
{
  "BudgetName": "AIOnCallBuddyBudget",
  "BudgetLimit": {
    "Amount": "5",
    "Unit": "USD"
  },
  "TimeUnit": "MONTHLY",
  "BudgetType": "COST"
}
EOF
```

## Next Steps

1. ? Deploy with minimal configuration
2. ? Test health endpoint
3. ? Check CloudWatch logs
4. ? Test one API call
5. ? Monitor costs
6. ? **DO NOT** enable provisioned concurrency
7. ? **DO NOT** increase memory beyond 512MB
8. ? **DO NOT** make frequent test calls

## Support

For issues, check:
1. CloudWatch Logs: `/aws/lambda/ai-oncall-buddy-stack-AIOnCallBuddyFunction-XXXXX`
2. CloudFormation Events: Console ? CloudFormation ? Stacks ? Events
3. Lambda Metrics: Console ? Lambda ? Functions ? Monitoring
