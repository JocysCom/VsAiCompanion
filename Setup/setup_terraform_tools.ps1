# Simple script to install required tools for Terraform Azure projects
# Installs Azure CLI, Terraform, and PowerShell Az module

Write-Host "Installing required tools for Terraform Azure projects..." -ForegroundColor Cyan

# Check if running as administrator for installations
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")

if (-not $isAdmin) {
	Write-Warning "Running without administrator privileges. Some installations may fail."
}

# Install/Update PowerShellGet and Az module
if (-not (Get-Module -ListAvailable -Name "Az")) {
	Write-Host "Installing Az PowerShell module..." -ForegroundColor Yellow
	Install-Module -Name Az -Force -AllowClobber -Scope CurrentUser
}

# Install Azure CLI via winget
try {
	$azVersion = az version 2>$null
	if (-not $azVersion) {
		Write-Host "Installing Azure CLI..." -ForegroundColor Yellow
		winget install Microsoft.AzureCLI
	}
}
catch {
	Write-Host "Azure CLI installation may have failed. Install manually if needed." -ForegroundColor Red
}

# Install Terraform via winget
try {
	$tfVersion = terraform version 2>$null
	if (-not $tfVersion) {
		Write-Host "Installing Terraform..." -ForegroundColor Yellow
		winget install Hashicorp.Terraform
	}
}
catch {
	Write-Host "Terraform installation may have failed. Install manually if needed." -ForegroundColor Red
}

Write-Host "Tool installation completed." -ForegroundColor Green
