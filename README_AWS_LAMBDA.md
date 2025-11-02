# ? AWS Lambda Conversion Complete

## What Was Modified

### 1. **Project Configuration** (`example.csproj`)
- Added AWS Lambda hosting packages
- Configured for Lambda runtime
- All packages compatible with .NET 9

### 2. **Application Code** (`run_model.cs`)
- ? Added Lambda hosting support
- ? Enhanced logging for troubleshooting
- ? Startup diagnostics
- ? Conditional Swagger (disabled in production)
- ? Health check endpoint remains functional

### 3. **AWS Configuration Files** (NEW)
- `template.json` - CloudFormation template
- `aws-lambda-tools-defaults.json` - Deployment settings
- `parameters.json` - Azure OpenAI credentials
- `deploy-lambda.ps1` - Automated deployment script

### 4. **Documentation** (NEW)
- `AWS_QUICKSTART.md` - 5-minute deployment
- `AWS_LAMBDA_DEPLOYMENT.md` - Full deployment guide
- `AWS_LAMBDA_TROUBLESHOOTING.md` - Common issues
- `AWS_CONVERSION_SUMMARY.md` - Technical details

## ?? Deployment Instructions

### Quick Start (5 Minutes)

1. **Install Tools**
   ```powershell
   # Install AWS CLI from https://aws.amazon.com/cli/
   dotnet tool install -g Amazon.Lambda.Tools
   aws configure
   ```

2. **Update Configuration**
   - Edit `parameters.json` with Azure OpenAI credentials
   - Edit `aws-lambda-tools-defaults.json` with S3 bucket name

3. **Deploy**
   ```powershell
   ./deploy-lambda.ps1
   ```

4. **Test**
   ```bash
   curl https://YOUR-API-URL/api/health
   ```

## ?? Cost Optimization

### FREE TIER Limits
- **1 Million requests/month** (FREE)
- **400,000 GB-seconds** compute (FREE)
- **~800,000 seconds** execution @ 512MB (FREE)

### Configuration for FREE Usage
```json
{
  "MemorySize": 512,
  "Timeout": 30,
  "EnableSwagger": false,
  "MaxOutputTokens": 2000
}
```

### What's Disabled to Save Costs
- ? Swagger UI in production
- ? Provisioned concurrency
- ? High memory allocations
- ? Excessive logging

## ?? Health Check & Logging

### Application Startup Indicators
Look for these in CloudWatch logs:
```
=== APPLICATION STARTING ===
Environment: Production
=== APPLICATION BUILT SUCCESSFULLY ===
Application configured and ready
```

### Health Endpoint
```bash
curl https://YOUR-API-URL/api/health
# Expected: {"status":"ok","version":"1.0.14"}
```

### View Logs
```bash
aws logs tail /aws/lambda/FUNCTION-NAME --follow
```

### Find Errors
```bash
aws logs filter-log-events \
  --log-group-name /aws/lambda/FUNCTION-NAME \
  --filter-pattern "ERROR"
```

## ?? Troubleshooting

### Application Won't Start
**Symptoms:**
- No `=== APPLICATION BUILT SUCCESSFULLY ===` in logs
- 502 Bad Gateway errors
- Process exited errors

**Solutions:**
1. Check environment variables are set
2. View CloudWatch logs
3. Increase timeout if needed
4. See `AWS_LAMBDA_TROUBLESHOOTING.md`

### Common Errors

**"Task timed out after 30.00 seconds"**
```bash
aws lambda update-function-configuration \
  --function-name YOUR-FUNCTION \
  --timeout 60
```

**"Missing Azure OpenAI Endpoint"**
```bash
aws lambda update-function-configuration \
  --function-name YOUR-FUNCTION \
  --environment "Variables={AZURE_OPENAI_ENDPOINT=https://your-resource.openai.azure.com/,AZURE_OPENAI_API_KEY=your-key}"
```

## ?? Monitoring

### Quick Status
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

## ?? Cleanup

When done testing:
```bash
# Delete everything
aws cloudformation delete-stack --stack-name ai-oncall-buddy-stack
aws s3 rm s3://YOUR-BUCKET --recursive
aws s3 rb s3://YOUR-BUCKET
```

## ?? Documentation

| File | Purpose |
|------|---------|
| `AWS_QUICKSTART.md` | Fast deployment (5 min) |
| `AWS_LAMBDA_DEPLOYMENT.md` | Complete deployment guide |
| `AWS_LAMBDA_TROUBLESHOOTING.md` | Common issues & solutions |
| `AWS_CONVERSION_SUMMARY.md` | Technical details |
| `deploy-lambda.ps1` | Automated deployment script |

## ? Deployment Checklist

**Before Deploying:**
- [ ] AWS CLI installed
- [ ] Lambda Tools installed
- [ ] AWS credentials configured
- [ ] `parameters.json` updated
- [ ] S3 bucket name chosen
- [ ] Budget alert configured (optional)

**After Deploying:**
- [ ] Health endpoint returns 200
- [ ] Logs show successful startup
- [ ] No errors in CloudWatch
- [ ] Test one API call works
- [ ] Costs within free tier

## ?? What You Can Do Now

### Test Locally (Still Works!)
```bash
dotnet run
# Test at http://localhost:5000
```

### Deploy to AWS Lambda
```powershell
./deploy-lambda.ps1
```

### Monitor Production
```bash
# Logs
aws logs tail /aws/lambda/FUNCTION-NAME --follow

# Errors
aws logs filter-log-events --filter-pattern "ERROR"

# Costs
aws ce get-cost-and-usage --time-period Start=2024-01-01,End=2024-01-31
```

## ?? Need Help?

1. **Deployment fails**: Check `AWS_LAMBDA_DEPLOYMENT.md`
2. **Application fails**: Check `AWS_LAMBDA_TROUBLESHOOTING.md`
3. **High costs**: Reduce `MaxOutputTokens`, disable Swagger
4. **Logs not showing**: Wait 1-2 minutes, check CloudWatch

## ?? Next Steps

1. ? **Deploy with script**: `./deploy-lambda.ps1`
2. ? **Test health**: `curl https://API-URL/api/health`
3. ? **Check logs**: `aws logs tail ...`
4. ? **Monitor costs**: AWS Console
5. ? **Don't enable provisioned concurrency** (expensive)
6. ? **Don't increase memory beyond 1024MB** (unless needed)

---

## Summary

Your application is now **fully configured for AWS Lambda** with:
- ? Complete health check support
- ? Comprehensive logging
- ? Cost optimization
- ? Troubleshooting guides
- ? Automated deployment

**Ready to deploy!** Run `./deploy-lambda.ps1` to get started.
