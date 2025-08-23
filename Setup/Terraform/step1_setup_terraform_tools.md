# Setup Terraform Tools

General setup instructions that apply to all Terraform projects.

## Pre-requisites

1. **PowerShell 7 (x64)** - Automation command-line shell and an associated scripting language.
2. **az (Azure CLI)** - Command-line tool for managing Azure resources.
3. **`Az` PowerShell Module** - Manage and interact with Azure cloud services directly from PowerShell. This module replaces the older AzureRM module.
4. **`SqlServer` PowerShell Module** - Manage and interact with SQL Servers directly from PowerShell.

## Install PowerShell 7 (x64)

### Manual Installation

1. Visit the <https://github.com/PowerShell/PowerShell/releases>
2. Find the latest stable release and download the `PowerShell-<version>-win-x64.msi` file.
3. Run the downloaded `.msi` file.
4. Follow the installer prompts.

### Automated Installation

```PowerShell
# Update PowerShell using WinGet
winget install --id Microsoft.Powershell --source winget
```

## Open PowerShell 7 as Administrator

1. Press `WIN + S` to open Windows Search.
2. Type `pwsh`.
3. Right-click on the `PowerShell 7 (x64)` app item and select `Run as Administrator`.

Go to the Downloads folder:

```powershell
cd Downloads
```

## Install Azure CLI (az)

Allow modules to be installed without prompting for confirmation.

```powershell
Set-PSRepository -Name 'PSGallery' -InstallationPolicy Trusted
```

Install Azure CLI (az)

```PowerShell
Invoke-WebRequest -Uri https://aka.ms/installazurecliwindows -OutFile .\AzureCLI.msi
Start-Process msiexec.exe -ArgumentList '/I AzureCLI.msi /quiet' -Wait
Remove-Item .\AzureCLI.msi
```

## Install Terraform

### Manual Installation

1. **Download Terraform**:
    - Visit the <https://www.terraform.io/downloads.html>
    - Find the latest Windows AMD64 release and download the `terraform_<version>_darwin_amd64.zip` file.
2. **Extract and add to PATH**:
    - Extract the binary and move it to a directory included in your system's PATH (e.g., `C:\Program Files\Terraform`).
   - Optionally, add this path to your system's PATH environment variable:

    ```powershell
    # Current session:
    $env:Path += ";C:\Program Files\Terraform"
    # System-wide:
    [Environment]::SetEnvironmentVariable("Path", [Environment]::GetEnvironmentVariable("Path", [EnvironmentVariableTarget]::Machine) + ";C:\Program Files\Terraform", [EnvironmentVariableTarget]::Machine)
    ```

3. **Verify Terraform installation**:

    ```powershell
    terraform --version
    ```

## Reopen PowerShell

- Close the administrative session of PowerShell and reopen PowerShell 7 as a normal user.
- Go to the Downloads folder:

 ```powershell
 cd Downloads
 ```

## Install Required PowerShell Modules

**Install Az PowerShell module**:

```powershell
Install-Module -Name Az -AllowClobber -Scope AllUsers -Force
```

**Install SQL Server PowerShell module**:

```powershell
Install-Module -Name SqlServer -Force
```

## Clone the Repository

```powershell
git clone https://github.com/JocysCom/VsAiCompanion.git
cd VsAiCompanion\Resources\Setup\Terraform-Azure
```

## Authenticate with Azure

**Import Azure Module**:

```powershell
Import-Module -Name Az
```

**Login to Azure**:

```powershell
az login
```

## Alternative: Use the Interactive Setup Script

Instead of manual installation, you can use the interactive setup script:

```powershell
.\step1_setup_terraform_tools.ps1
```

This script provides an interactive menu to install/reinstall tools and check their status.
