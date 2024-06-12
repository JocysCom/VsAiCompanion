# Install Terraform on Windows with Azure PowerShell

Follow these steps to install Terraform on Windows using Azure PowerShell. This guide includes all the necessary steps and PowerShell commands to execute the installation and configuration.

## Open PowerShell 7 as Administrator

1. Press `WIN + S` to open Windows Search.
2. Type `pwsh`.
3. Wait for the `PowerShell 7 (x64)` app item to appear.
4. Right-click on the `PowerShell 7 (x64)` item.
5. Select `Run as Administrator` from the context menu.

## Install the Latest Version of PowerShell

1. Open PowerShell as an Administrator.
2. Change to a different working directory, such as `%USERPROFILE%\Downloads`, as PowerShell opens in the `C:\windows\system32` folder by default:

```powershell
# Change to the Downloads folder
Set-Location -Path "$env:USERPROFILE\Downloads"
```

3. Check if you already have the latest version of PowerShell installed by running `pwsh`. If you decide to upgrade or install PowerShell, follow these steps:

```powershell
# Open PowerShell 7 if installed
pwsh

# Check the installed version
$PSVersionTable.PSVersion

# If not installed or upgrade needed, download the PowerShell installer
Invoke-WebRequest -Uri "https://github.com/PowerShell/PowerShell/releases/download/v7.4.2/PowerShell-7.4.2-win-x64.msi" -OutFile "PowerShell-7.4.2-win-x64.msi"

# Install PowerShell
Start-Process msiexec.exe -Wait -ArgumentList "/I PowerShell-7.4.2-win-x64.msi /quiet /norestart"

# Verify installation by opening PowerShell 7
pwsh

# Check the installed version again
$PSVersionTable.PSVersion
```

You can always check for the latest version [here](https://github.com/PowerShell/PowerShell/releases).

## Install the new PowerShell Az Module

1. Check if the Az module is already installed:

```powershell
# Check if the Az module is installed
Get-InstalledModule -Name Az

# If the Az module is not installed or you want to upgrade, uninstall AzureRM module (if installed)
Uninstall-AzureRm -Force  # Skip if not applicable

# Set the PSGallery as a trusted repository
Set-PSRepository -Name 'PSGallery' -InstallationPolicy Trusted

# Install the new PowerShell Az module
Install-Module -Name Az -AllowClobber -Scope AllUsers -Force

# Import the module
Import-Module -Name Az

# Verify the installation
Get-InstalledModule -Name Az
```

## Install Terraform

1. Download the latest Terraform binary from the official website: [Terraform Downloads](https://www.terraform.io/downloads.html)
2. Extract the binary and move it to a directory included in your system's PATH (e.g., `C:\terraform`).

3. Optionally, you can add the Terraform path to your system's PATH environment variable:

```powershell
# Add Terraform to the system PATH
$env:Path += ";C:\terraform"

# Verify Terraform installation
terraform --version
```

## Understand Common Terraform and Azure Authentication Scenarios

Terraform can authenticate to Azure in several ways. Here are the most common methods:

### Authenticate via a Microsoft account from Windows using PowerShell

1. Open PowerShell and run the following command to log in:

```powershell
# Login to Azure
Connect-AzAccount

# Verify the login
Get-AzSubscription
```

### Create a Service Principal Using Azure PowerShell

1. Open PowerShell and execute the following commands to create a Service Principal:

```powershell
# Login to Azure
Connect-AzAccount

# Create a new Service Principal
$sp = New-AzADServicePrincipal -DisplayName "terraform-sp"

# Display the Service Principal details
$sp
```

2. Capture the `ApplicationId`, `TenantId`, and generated password.

### Specify Service Principal Credentials in Environment Variables

1. Set the Service Principal credentials in environment variables:

```powershell
# Set environment variables for Service Principal credentials
$env:ARM_CLIENT_ID = "your-application-id"
$env:ARM_CLIENT_SECRET = "your-password"
$env:ARM_SUBSCRIPTION_ID = "your-subscription-id"
$env:ARM_TENANT_ID = "your-tenant-id"
```

### Specify Service Principal Credentials in a Terraform Provider Block

1. Create a Terraform configuration file (e.g., `main.tf`) and specify the Service Principal credentials in the provider block:

```hcl
# Configure the Azure provider
provider "azurerm" {
  features {}

  client_id       = var.client_id
  client_secret   = var.client_secret
  subscription_id = var.subscription_id
  tenant_id       = var.tenant_id
}
```

2. Create a `variables.tf` file to define the variables:

```hcl
# Define the variables
variable "client_id" {}
variable "client_secret" {}
variable "subscription_id" {}
variable "tenant_id" {}
```

3. Initialize Terraform and apply the configuration:

```powershell
# Navigate to the directory containing the Terraform configuration files
cd path\to\your\terraform\config

# Initialize Terraform
terraform init

# Apply the configuration
terraform apply
```

### Install the Azure Terraform Visual Studio Code extension

https://learn.microsoft.com/en-us/azure/developer/terraform/configure-vs-code-extension-for-terraform?source=recommendations&tabs=azure-powershell
https://learn.microsoft.com/en-us/cli/azure/install-azure-cli-windows?tabs=powershell

https://www.youtube.com/watch?v=V53AHWun17s