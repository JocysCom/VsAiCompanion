#!/usr/bin/env pwsh
# Simple Terraform Apply Script

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

Write-Host "=== Applying Terraform: $Project ($Environment) ===" -ForegroundColor Cyan

# Change to project directory
Push-Location $Project

try {
	# Run terraform apply
	$varFile = "variables.$Environment.tfvars"
	Write-Host "Running: terraform apply -var-file=$varFile" -ForegroundColor Gray
	terraform apply -var-file="$varFile"

	if ($LASTEXITCODE -eq 0) {
		Write-Host "✅ Apply completed!" -ForegroundColor Green
	} else {
		Write-Host "❌ Apply failed" -ForegroundColor Red
	}
} finally {
	Pop-Location
}
