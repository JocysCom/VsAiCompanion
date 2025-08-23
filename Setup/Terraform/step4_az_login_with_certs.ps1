#!/usr/bin/env pwsh
# Azure CLI Login with Certificates and Authentication Choice

# Import shared functions
. .\shared_terraform_functions.ps1

# Set environment variables for certificate bundle
$env:REQUESTS_CA_BUNDLE = "$($pwd.Path)\Files\trusted_root_certificates.pem"
$env:CURL_CA_BUNDLE = "$($pwd.Path)\Files\trusted_root_certificates.pem"

Write-Host "Setting certificate bundle environment variables..." -ForegroundColor Green
Write-Host "REQUESTS_CA_BUNDLE: $env:REQUESTS_CA_BUNDLE" -ForegroundColor Gray
Write-Host "CURL_CA_BUNDLE: $env:CURL_CA_BUNDLE" -ForegroundColor Gray
Write-Host ""

# Show current authentication context
Show-AuthContext "BEFORE Login"

# Authentication choice
Write-Host "Choose authentication method:" -ForegroundColor Yellow
Write-Host "1. User login (interactive)" -ForegroundColor White
Write-Host "2. Service Principal (from environment variables)" -ForegroundColor White

do {
    $choice = Read-Host "Enter your choice (1-2)"
    switch ($choice) {
        "1" {
            Write-Host "Attempting Azure CLI user login..." -ForegroundColor Green
            az login
            $validChoice = $true
        }
        "2" {
            # Check if service principal environment variables exist
            if ($env:TF_VAR_ARM_CLIENT_ID -and $env:TF_VAR_ARM_CLIENT_SECRET -and $env:TF_VAR_ARM_TENANT_ID) {
                Write-Host "Attempting service principal login..." -ForegroundColor Green
                az login --service-principal -u $env:TF_VAR_ARM_CLIENT_ID -p $env:TF_VAR_ARM_CLIENT_SECRET --tenant $env:TF_VAR_ARM_TENANT_ID
                $validChoice = $true
            } else {
                Write-Host "❌ Service principal environment variables not set!" -ForegroundColor Red
                Write-Host "Please run step3_switch_owning_service_principal.ps1 first" -ForegroundColor Yellow
                $validChoice = $false
            }
        }
        default {
            Write-Host "Invalid choice. Please enter 1 or 2." -ForegroundColor Red
            $validChoice = $false
        }
    }
} while (-not $validChoice)

Write-Host ""
# Show authentication context after login
Show-AuthContext "AFTER Login"
