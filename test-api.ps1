# Test script for BMC Exception Analyzer API
# Usage: .\test-api.ps1

Write-Host "BMC Exception Analyzer - API Test" -ForegroundColor Cyan
Write-Host "===================================" -ForegroundColor Cyan
Write-Host ""

# API endpoint - update this if running on a different port or host
$apiUrl = "http://localhost:5000/api/analyze-exception"

# Read the test request from the JSON file
$requestBody = Get-Content -Path "test-request-example.json" -Raw

Write-Host "Sending test request to: $apiUrl" -ForegroundColor Yellow
Write-Host ""

try {
    $response = Invoke-RestMethod -Uri $apiUrl `
        -Method Post `
        -Body $requestBody `
        -ContentType "application/json" `
        -ErrorAction Stop
    
    Write-Host "? SUCCESS!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Response:" -ForegroundColor Green
    $response | ConvertTo-Json -Depth 10 | Write-Host
    
    if ($response.emailSent -eq $true) {
        Write-Host ""
        Write-Host "? Email sent successfully to developer" -ForegroundColor Green
    } elseif ($response.status -eq "partial_success") {
        Write-Host ""
        Write-Host "? Analysis completed but email failed:" -ForegroundColor Yellow
        Write-Host "  Error: $($response.emailError)" -ForegroundColor Yellow
    }
    
    Write-Host ""
    Write-Host "AI Analysis Preview:" -ForegroundColor Cyan
    Write-Host "-------------------" -ForegroundColor Cyan
    $analysisPreview = $response.analysis.analysisText.Substring(0, [Math]::Min(500, $response.analysis.analysisText.Length))
    Write-Host $analysisPreview -ForegroundColor White
    if ($response.analysis.analysisText.Length -gt 500) {
        Write-Host "..." -ForegroundColor Gray
        Write-Host "(truncated - full analysis sent via email)" -ForegroundColor Gray
    }
    
} catch {
    Write-Host "? ERROR!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Status Code: $($_.Exception.Response.StatusCode.value__)" -ForegroundColor Red
    Write-Host "Error Message: $($_.Exception.Message)" -ForegroundColor Red
    
    if ($_.ErrorDetails.Message) {
        Write-Host ""
        Write-Host "Details:" -ForegroundColor Red
        Write-Host $_.ErrorDetails.Message -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "Test complete." -ForegroundColor Cyan
