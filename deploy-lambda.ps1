# AWS Lambda Deployment Helper Script
# Run this from PowerShell in your project directory

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "  AI On Call Buddy - Lambda Deploy  " -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

# Check prerequisites
Write-Host "Checking prerequisites..." -ForegroundColor Yellow

# Check AWS CLI
if (Get-Command aws -ErrorAction SilentlyContinue) {
    $awsVersion = aws --version
    Write-Host "? AWS CLI installed: $awsVersion" -ForegroundColor Green
} else {
    Write-Host "? AWS CLI not found. Please install from https://aws.amazon.com/cli/" -ForegroundColor Red
    exit 1
}

# Check dotnet
if (Get-Command dotnet -ErrorAction SilentlyContinue) {
    $dotnetVersion = dotnet --version
    Write-Host "? .NET SDK installed: $dotnetVersion" -ForegroundColor Green
} else {
    Write-Host "? .NET SDK not found" -ForegroundColor Red
    exit 1
}

# Check Lambda Tools
try {
    dotnet lambda --version | Out-Null
    Write-Host "? AWS Lambda Tools installed" -ForegroundColor Green
} catch {
    Write-Host "? AWS Lambda Tools not found. Installing..." -ForegroundColor Yellow
    dotnet tool install -g Amazon.Lambda.Tools
}

Write-Host ""

# Check configuration files
Write-Host "Checking configuration..." -ForegroundColor Yellow

if (Test-Path "parameters.json") {
    $params = Get-Content "parameters.json" | ConvertFrom-Json
    $endpoint = $params.Parameters.AzureOpenAIEndpoint
    
    if ($endpoint -like "*your-resource*" -or $endpoint -eq "") {
        Write-Host "? WARNING: parameters.json needs to be updated with your Azure OpenAI credentials" -ForegroundColor Yellow
        Write-Host "  Please edit parameters.json before deploying" -ForegroundColor Yellow
        $continue = Read-Host "Continue anyway? (y/n)"
        if ($continue -ne "y") {
            exit 0
        }
    } else {
        Write-Host "? parameters.json configured" -ForegroundColor Green
    }
} else {
    Write-Host "? parameters.json not found" -ForegroundColor Red
    exit 1
}

if (Test-Path "aws-lambda-tools-defaults.json") {
    $defaults = Get-Content "aws-lambda-tools-defaults.json" | ConvertFrom-Json
    $bucket = $defaults."s3-bucket"
    
    if ($bucket -eq "") {
        Write-Host "? WARNING: S3 bucket not configured in aws-lambda-tools-defaults.json" -ForegroundColor Yellow
        $bucketName = Read-Host "Enter S3 bucket name (must be globally unique)"
        
        # Update the file
        $defaults."s3-bucket" = $bucketName
        $defaults | ConvertTo-Json | Set-Content "aws-lambda-tools-defaults.json"
        Write-Host "? S3 bucket configured: $bucketName" -ForegroundColor Green
    } else {
        Write-Host "? S3 bucket configured: $bucket" -ForegroundColor Green
    }
} else {
    Write-Host "? aws-lambda-tools-defaults.json not found" -ForegroundColor Red
    exit 1
}

Write-Host ""

# Verify AWS credentials
Write-Host "Verifying AWS credentials..." -ForegroundColor Yellow
try {
    $identity = aws sts get-caller-identity 2>&1
    if ($LASTEXITCODE -eq 0) {
        $identityJson = $identity | ConvertFrom-Json
        Write-Host "? AWS credentials valid" -ForegroundColor Green
        Write-Host "  Account: $($identityJson.Account)" -ForegroundColor Gray
        Write-Host "  User: $($identityJson.Arn)" -ForegroundColor Gray
    } else {
        Write-Host "? AWS credentials not configured" -ForegroundColor Red
        Write-Host "  Run: aws configure" -ForegroundColor Yellow
        exit 1
    }
} catch {
    Write-Host "? Error verifying AWS credentials" -ForegroundColor Red
    exit 1
}

Write-Host ""

# Get deployment details
$defaults = Get-Content "aws-lambda-tools-defaults.json" | ConvertFrom-Json
$bucket = $defaults."s3-bucket"
$stackName = $defaults."stack-name"
$region = $defaults."region"

Write-Host "Deployment Configuration:" -ForegroundColor Cyan
Write-Host "  Stack Name: $stackName" -ForegroundColor Gray
Write-Host "  S3 Bucket: $bucket" -ForegroundColor Gray
Write-Host "  Region: $region" -ForegroundColor Gray
Write-Host ""

# Create S3 bucket if it doesn't exist
Write-Host "Checking S3 bucket..." -ForegroundColor Yellow
$bucketExists = aws s3 ls "s3://$bucket" 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "Creating S3 bucket: $bucket" -ForegroundColor Yellow
    aws s3 mb "s3://$bucket" --region $region
    if ($LASTEXITCODE -eq 0) {
        Write-Host "? S3 bucket created" -ForegroundColor Green
    } else {
        Write-Host "? Failed to create S3 bucket" -ForegroundColor Red
        Write-Host "  Bucket may already exist in another region or account" -ForegroundColor Yellow
        exit 1
    }
} else {
    Write-Host "? S3 bucket exists" -ForegroundColor Green
}

Write-Host ""

# Deploy
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "         DEPLOYING TO AWS            " -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

$deploy = Read-Host "Ready to deploy? (y/n)"
if ($deploy -ne "y") {
    Write-Host "Deployment cancelled" -ForegroundColor Yellow
    exit 0
}

Write-Host ""
Write-Host "Deploying... This may take 2-5 minutes" -ForegroundColor Yellow
Write-Host ""

try {
    dotnet lambda deploy-serverless `
        --stack-name $stackName `
        --s3-bucket $bucket `
        --region $region `
        --template template.json `
        --template-parameters parameters.json

    if ($LASTEXITCODE -eq 0) {
        Write-Host ""
        Write-Host "=====================================" -ForegroundColor Green
        Write-Host "     DEPLOYMENT SUCCESSFUL! ?        " -ForegroundColor Green
        Write-Host "=====================================" -ForegroundColor Green
        Write-Host ""
        
        # Get API URL
        Write-Host "Retrieving API URL..." -ForegroundColor Yellow
        $apiUrl = aws cloudformation describe-stacks `
            --stack-name $stackName `
            --query "Stacks[0].Outputs[?OutputKey=='ApiUrl'].OutputValue" `
            --output text `
            --region $region
        
        Write-Host ""
        Write-Host "Your API is available at:" -ForegroundColor Cyan
        Write-Host "  $apiUrl" -ForegroundColor White
        Write-Host ""
        
        # Test health endpoint
        Write-Host "Testing health endpoint..." -ForegroundColor Yellow
        try {
            $response = Invoke-RestMethod -Uri "${apiUrl}api/health" -Method Get -TimeoutSec 30
            Write-Host "? Health check passed!" -ForegroundColor Green
            Write-Host "  Status: $($response.status)" -ForegroundColor Gray
            Write-Host "  Version: $($response.version)" -ForegroundColor Gray
        } catch {
            Write-Host "? Health check failed (may need a few seconds to warm up)" -ForegroundColor Yellow
            Write-Host "  Try: curl ${apiUrl}api/health" -ForegroundColor Gray
        }
        
        Write-Host ""
        Write-Host "Next Steps:" -ForegroundColor Cyan
        Write-Host "1. Test the API: curl ${apiUrl}api/health" -ForegroundColor Gray
        Write-Host "2. View logs: aws logs tail /aws/lambda/${stackName}-AIOnCallBuddyFunction-*" -ForegroundColor Gray
        Write-Host "3. Monitor costs: AWS Console > Billing Dashboard" -ForegroundColor Gray
        Write-Host ""
        Write-Host "Documentation:" -ForegroundColor Cyan
        Write-Host "- Troubleshooting: AWS_LAMBDA_TROUBLESHOOTING.md" -ForegroundColor Gray
        Write-Host "- Full Guide: AWS_LAMBDA_DEPLOYMENT.md" -ForegroundColor Gray
        Write-Host ""
        
    } else {
        Write-Host ""
        Write-Host "? Deployment failed" -ForegroundColor Red
        Write-Host "Check the error messages above for details" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "Common issues:" -ForegroundColor Yellow
        Write-Host "1. S3 bucket permissions" -ForegroundColor Gray
        Write-Host "2. CloudFormation stack already exists" -ForegroundColor Gray
        Write-Host "3. IAM permissions insufficient" -ForegroundColor Gray
        Write-Host ""
        exit 1
    }
} catch {
    Write-Host ""
    Write-Host "? Deployment error: $_" -ForegroundColor Red
    exit 1
}
