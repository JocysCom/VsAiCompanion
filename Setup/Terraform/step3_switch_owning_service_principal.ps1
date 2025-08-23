#!/usr/bin/env pwsh
# Switch Owning Service Principal Script
# Sets environment variables for Terraform to use the appropriate service principal

param(
	[Parameter(Mandatory = $false)]
	[string]$Project,

	[Parameter(Mandatory = $false)]
	[ValidateSet("dev", "prod")]
	[string]$Environment,

	[Parameter(Mandatory = $false)]
	[switch]$Help
)

if ($Help) {
	Write-Host "Switch Owning Service Principal Script" -ForegroundColor Green
	Write-Host ""
	Write-Host "Sets environment variables for Terraform authentication." -ForegroundColor Gray
	Write-Host ""
	Write-Host "Usage:" -ForegroundColor Yellow
	Write-Host "  .\step3_switch_owning_service_principal.ps1 [-Project <project>] [-Environment <dev|prod>]"
	Write-Host ""
	Write-Host "Parameters:" -ForegroundColor Yellow
	Write-Host "  -Project       Terraform project name (Terraform-{ProjectName})"
	Write-Host "  -Environment   Target environment (dev or prod)"
	Write-Host ""
	Write-Host "Examples:" -ForegroundColor Yellow
	Write-Host "  .\step3_switch_owning_service_principal.ps1 -Project Terraform-{ProjectName} -Environment dev"
	Write-Host "  .\step3_switch_owning_service_principal.ps1    # Interactive mode"
	Write-Host ""
	exit 0
}

# Import shared functions
$sharedFunctionsPath = "./shared_terraform_functions.ps1"
if (-not (Test-Path $sharedFunctionsPath)) {
	Write-Host "❌ Shared functions not found at: $sharedFunctionsPath" -ForegroundColor Red
	exit 1
}
. $sharedFunctionsPath

# Get project selection if not provided
if (-not $Project) {
	$Project = Get-TerraformProjectSelection -PatternPrefix "Terraform-"
	if (-not $Project) {
		Write-Host "❌ No project selected or found." -ForegroundColor Red
		exit 1
	}
}

# Validate project exists
if (-not (Test-Path $Project)) {
	Write-Host "❌ Project directory '$Project' not found." -ForegroundColor Red
	exit 1
}

# Get environment selection if not provided
if (-not $Environment) {
	$envSelection = Get-EnvironmentSelection
	if (-not $envSelection) {
		Write-Host "❌ No environment selected." -ForegroundColor Red
		exit 1
	}
	$Environment = $envSelection.Environment
}

Write-Host "🔐 Setting environment variables for: $Project ($Environment)" -ForegroundColor Green

# Load configuration from backend.tfvars file
$backendConfigPath = Join-Path $Project "backend.$Environment.tfvars"
if (-not (Test-Path $backendConfigPath)) {
	Write-Host "❌ Backend configuration file not found: $backendConfigPath" -ForegroundColor Red
	exit 1
}

$config = GetConfig -configFile $backendConfigPath
if (-not $config -or -not $config.tenant_id -or -not $config.subscription_id) {
	Write-Host "❌ Failed to load tenant_id and subscription_id from $backendConfigPath" -ForegroundColor Red
	exit 1
}

Write-Host "✓ Loaded configuration from: $backendConfigPath" -ForegroundColor Green
$tenantId = $config.tenant_id
$subscriptionId = $config.subscription_id

# Get service principal credentials
Write-Host ""
Write-Host "Please enter the owning service principal credentials for $Environment environment:" -ForegroundColor Yellow
$clientId = Read-Host "Enter Owning Azure Service Principal Client ID (Application ID)"
$clientSecretSecure = Read-Host "Enter Owning Azure Service Principal Client Secret" -AsSecureString
$clientSecret = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($clientSecretSecure))

# Set environment variables
Set-TerraformEnvironmentVariables -TenantId $tenantId -SubscriptionId $subscriptionId -ClientId $clientId -ClientSecret $clientSecret

Write-Host "Environment variables set for $Environment" -ForegroundColor Green
