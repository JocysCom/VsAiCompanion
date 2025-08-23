#!/usr/bin/env pwsh
# Simple Terraform Plan Validation Script

# Import shared functions
. .\shared_terraform_functions.ps1

# Get project selection
$Project = Get-TerraformProjectSelection -PatternPrefix "Terraform-"
if (-not $Project) {
	Write-Host "❌ No project selected." -ForegroundColor Red
	exit 1
}

# Get environment selection
$envResult = Get-EnvironmentSelection
$Environment = $envResult.Environment

Write-Host "=== Validating Terraform Plan: $Project ($Environment) ===" -ForegroundColor Cyan

# Change to project directory
Push-Location $Project

try {
	# Check if terraform is initialized
	$backendFile = "backend.$Environment.tfvars"
	$varFile = "variables.$Environment.tfvars"

	Write-Host "Checking Terraform initialization..." -ForegroundColor Yellow

	if (-not (Test-Path ".terraform") -or -not (Test-Path ".terraform/providers")) {
		Write-Host "Initializing Terraform with backend config: $backendFile..." -ForegroundColor Yellow
		terraform init -backend-config="$backendFile"

		if ($LASTEXITCODE -ne 0) {
			Write-Host "❌ Terraform initialization failed" -ForegroundColor Red
			Write-Host "Please check your authentication and backend configuration" -ForegroundColor Yellow
			return
		}
		Write-Host "✅ Terraform initialized successfully" -ForegroundColor Green
	} else {
		Write-Host "✅ Terraform already initialized" -ForegroundColor Green
	}

	# Run terraform plan
	Write-Host "Running: terraform plan -var-file=$varFile" -ForegroundColor Gray
	terraform plan -var-file="$varFile"

	if ($LASTEXITCODE -eq 0) {
		Write-Host "✅ Plan validation completed!" -ForegroundColor Green
	} else {
		Write-Host "❌ Plan validation failed" -ForegroundColor Red
	}
} finally {
	Pop-Location
}
