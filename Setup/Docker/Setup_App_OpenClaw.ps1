################################################################################
# File         : Setup_App_OpenClaw.ps1
# Description  : Installs and manages OpenClaw AI agent platform within an
#                existing WSL2 distro. OpenClaw provides multi-channel AI
#                communication (WhatsApp, Telegram, Discord, etc.).
#                NOTE: Run Setup_Core_WSL_OpenClaw.ps1 first to create the distro.
# Usage        : Run as Administrator for service management.
################################################################################

using namespace System
using namespace System.IO

# Dot-source the necessary helper function files.
. "$PSScriptRoot\Setup_Helper_CoreFunctions.ps1"
. "$PSScriptRoot\Setup_Helper_NetworkTests.ps1"
. "$PSScriptRoot\Setup_Helper_WSLFunctions.ps1"

# Ensure the script working directory is set.
Set-ScriptLocation

#==============================================================================
# Global Configuration
#==============================================================================

$global:appName = "OpenClaw"
$global:wslDistroName = "OpenClaw-WSL"

# Network ports
$global:controlUiPort = 18789
$global:canvasPort = 18793

# Paths
$global:programDataRoot = [Environment]::GetFolderPath([Environment+SpecialFolder]::CommonApplicationData)
$global:installRoot = Join-Path $global:programDataRoot $global:appName

# OpenClaw installation paths inside WSL
$global:openclawConfigPath = "~/.openclaw"
$global:openclawServiceName = "openclaw"

# Node.js minimum version
$global:nodeMinVersion = 22

# Backup configuration
$global:backupFolder = ".\Backup"
$global:openclawBackupSubfolder = "openclaw_data"
$global:workspacePath = "~/workspace"


#==============================================================================
# Function: Test-WSLDistroExists
#==============================================================================
<#
.SYNOPSIS
    Checks if a WSL distro exists by name.
.DESCRIPTION
    Uses 'wsl --list' to check if the specified distro is registered.
.PARAMETER DistroName
    Name of the WSL distro to check.
.OUTPUTS
    [bool] True if distro exists, false otherwise.
#>
function Test-WSLDistroExists {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory = $true)]
        [string]$DistroName
    )

    try {
        $distros = wsl --list --quiet 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Failed to list WSL distros."
            return $false
        }

        foreach ($distro in $distros) {
            $cleanName = $distro -replace '\x00', '' -replace '^\s+|\s+$', ''
            if ($cleanName -eq $DistroName) {
                return $true
            }
        }
        return $false
    }
    catch {
        Write-Warning "Error checking WSL distros: $_"
        return $false
    }
}

#==============================================================================
# Function: Get-WSLDistroStatus
#==============================================================================
<#
.SYNOPSIS
    Gets the running status of a WSL distro.
.DESCRIPTION
    Uses 'wsl --list --verbose' to check if the distro is running or stopped.
.PARAMETER DistroName
    Name of the WSL distro to check.
.OUTPUTS
    [string] Status: "Running", "Stopped", or "NotFound".
#>
function Get-WSLDistroStatus {
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory = $true)]
        [string]$DistroName
    )

    try {
        $output = wsl --list --verbose 2>&1
        if ($LASTEXITCODE -ne 0) {
            return "NotFound"
        }

        foreach ($line in $output) {
            $cleanLine = $line -replace '\x00', ''
            if ($cleanLine -match "^\s*\*?\s*$DistroName\s+(Running|Stopped)\s+") {
                return $Matches[1]
            }
        }
        return "NotFound"
    }
    catch {
        return "NotFound"
    }
}

#==============================================================================
# Function: Invoke-WSLCommand
#==============================================================================
<#
.SYNOPSIS
    Executes a command inside a WSL distro.
.DESCRIPTION
    Runs a bash command inside the specified WSL distro and returns the output.
.PARAMETER DistroName
    Name of the WSL distro to run the command in.
.PARAMETER Command
    Bash command to execute.
.PARAMETER AsRoot
    Run command as root user.
.OUTPUTS
    [string] Command output.
#>
function Invoke-WSLCommand {
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory = $true)]
        [string]$DistroName,

        [Parameter(Mandatory = $true)]
        [string]$Command,

        [Parameter(Mandatory = $false)]
        [switch]$AsRoot
    )

    $wslArgs = @("--distribution", $DistroName)
    if ($AsRoot) {
        $wslArgs += @("--user", "root")
    }
    $wslArgs += @("--", "bash", "-c", $Command)

    Write-Host "Executing: $Command" -ForegroundColor DarkGray
    $output = & wsl @wslArgs 2>&1

    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Command failed with exit code $LASTEXITCODE"
    }

    return $output
}

#==============================================================================
# Function: Install-NodeJS
#==============================================================================
<#
.SYNOPSIS
    Installs Node.js 22+ inside the OpenClaw WSL distro.
.DESCRIPTION
    Uses NodeSource repository to install Node.js LTS version.
.OUTPUTS
    [bool] True if successful, false otherwise.
#>
function Install-NodeJS {
    [CmdletBinding(SupportsShouldProcess = $true)]
    [OutputType([bool])]
    param()

    if (-not $PSCmdlet.ShouldProcess($global:wslDistroName, "Install Node.js")) {
        return $false
    }

    Write-Host "Installing Node.js $($global:nodeMinVersion)+ in WSL distro..." -ForegroundColor Yellow

    $nodeVersion = Invoke-WSLCommand -DistroName $global:wslDistroName -Command "node --version 2>/dev/null || echo 'not-installed'"

    if ($nodeVersion -match "^v(\d+)\.") {
        $installedMajor = [int]$Matches[1]
        if ($installedMajor -ge $global:nodeMinVersion) {
            Write-Host "Node.js $nodeVersion already installed (meets minimum v$($global:nodeMinVersion))." -ForegroundColor Green
            return $true
        }
        Write-Host "Upgrading Node.js from $nodeVersion to v$($global:nodeMinVersion)+..." -ForegroundColor Cyan
    }

    Write-Host "Updating package lists..." -ForegroundColor Cyan
    Invoke-WSLCommand -DistroName $global:wslDistroName -AsRoot -Command "apt-get update -y"

    Write-Host "Installing prerequisites..." -ForegroundColor Cyan
    Invoke-WSLCommand -DistroName $global:wslDistroName -AsRoot -Command "apt-get install -y ca-certificates curl gnupg"

    Write-Host "Adding NodeSource repository..." -ForegroundColor Cyan
    Invoke-WSLCommand -DistroName $global:wslDistroName -AsRoot -Command "mkdir -p /etc/apt/keyrings"
    Invoke-WSLCommand -DistroName $global:wslDistroName -AsRoot -Command "curl -fsSL https://deb.nodesource.com/gpgkey/nodesource-repo.gpg.key | gpg --dearmor -o /etc/apt/keyrings/nodesource.gpg"
    Invoke-WSLCommand -DistroName $global:wslDistroName -AsRoot -Command "echo 'deb [signed-by=/etc/apt/keyrings/nodesource.gpg] https://deb.nodesource.com/node_$($global:nodeMinVersion).x nodistro main' > /etc/apt/sources.list.d/nodesource.list"

    Write-Host "Installing Node.js..." -ForegroundColor Cyan
    Invoke-WSLCommand -DistroName $global:wslDistroName -AsRoot -Command "apt-get update -y && apt-get install -y nodejs"

    $finalVersion = Invoke-WSLCommand -DistroName $global:wslDistroName -Command "node --version"
    Write-Host "Node.js installed: $finalVersion" -ForegroundColor Green

    return $true
}

#==============================================================================
# Function: Install-OpenClawCLI
#==============================================================================
<#
.SYNOPSIS
    Installs the OpenClaw CLI and gateway in the WSL distro.
.DESCRIPTION
    Uses the official OpenClaw installer script from https://openclaw.ai/install.sh
.OUTPUTS
    [bool] True if successful, false otherwise.
#>
function Install-OpenClawCLI {
    [CmdletBinding(SupportsShouldProcess = $true)]
    [OutputType([bool])]
    param()

    if (-not $PSCmdlet.ShouldProcess($global:wslDistroName, "Install OpenClaw CLI")) {
        return $false
    }

    Write-Host "Installing OpenClaw CLI..." -ForegroundColor Yellow

    $openclawVersion = Invoke-WSLCommand -DistroName $global:wslDistroName -Command "openclaw --version 2>/dev/null || echo 'not-installed'"

    if ($openclawVersion -notmatch "not-installed") {
        Write-Host "OpenClaw already installed: $openclawVersion" -ForegroundColor Green
        $upgrade = Read-Host "Upgrade to latest version? (Y/N)"
        if ($upgrade -ne "Y") {
            return $true
        }
    }

    Write-Host "Installing OpenClaw via npm (this may take several minutes)..." -ForegroundColor Cyan
    & wsl --distribution $global:wslDistroName -- npm install -g openclaw@latest

    $finalVersion = Invoke-WSLCommand -DistroName $global:wslDistroName -Command "openclaw --version 2>/dev/null || echo 'installation-failed'"

    if ($finalVersion -match "installation-failed") {
        Write-Error "OpenClaw installation failed."
        return $false
    }

    Write-Host "OpenClaw installed: $finalVersion" -ForegroundColor Green
    return $true
}

#==============================================================================
# Function: Initialize-OpenClawConfig
#==============================================================================
<#
.SYNOPSIS
    Initializes OpenClaw configuration with API key.
.DESCRIPTION
    Runs openclaw onboard in non-interactive mode to configure the gateway
    with the provided Anthropic API key.
.PARAMETER AnthropicApiKey
    The Anthropic API key for Claude access.
.OUTPUTS
    [bool] True if successful, false otherwise.
#>
function Initialize-OpenClawConfig {
    [CmdletBinding(SupportsShouldProcess = $true)]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory = $true)]
        [string]$AnthropicApiKey
    )

    if (-not $PSCmdlet.ShouldProcess($global:wslDistroName, "Initialize OpenClaw Configuration")) {
        return $false
    }

    Write-Host "Initializing OpenClaw configuration..." -ForegroundColor Yellow

    $onboardCommand = @(
        "openclaw onboard --non-interactive --accept-risk",
        "--mode local",
        "--auth-choice apiKey",
        "--anthropic-api-key `"$AnthropicApiKey`"",
        "--gateway-port $($global:controlUiPort)",
        "--gateway-bind loopback",
        "--install-daemon",
        "--daemon-runtime node",
        "--skip-skills"
    ) -join " "

    Write-Host "Running OpenClaw onboarding..." -ForegroundColor Cyan
    $result = Invoke-WSLCommand -DistroName $global:wslDistroName -Command $onboardCommand

    if ($LASTEXITCODE -eq 0) {
        Write-Host "OpenClaw configuration initialized successfully." -ForegroundColor Green
        return $true
    }
    else {
        Write-Warning "OpenClaw onboarding returned non-zero exit code. Output: $result"
        return $false
    }
}

#==============================================================================
# Function: Install-OpenClawService
#==============================================================================
<#
.SYNOPSIS
    Configures OpenClaw as a systemd user service.
.DESCRIPTION
    Creates a systemd user service file for the OpenClaw gateway daemon.
.OUTPUTS
    [bool] True if successful, false otherwise.
#>
function Install-OpenClawService {
    [CmdletBinding(SupportsShouldProcess = $true)]
    [OutputType([bool])]
    param()

    if (-not $PSCmdlet.ShouldProcess($global:wslDistroName, "Install OpenClaw Service")) {
        return $false
    }

    Write-Host "Configuring OpenClaw systemd service..." -ForegroundColor Yellow

    $serviceContent = @"
[Unit]
Description=OpenClaw Gateway Daemon
After=network.target

[Service]
Type=simple
ExecStart=/usr/bin/openclaw gateway --dev --allow-unconfigured
Restart=on-failure
RestartSec=5
Environment=NODE_ENV=production

[Install]
WantedBy=default.target
"@

    $escapedContent = $serviceContent -replace '"', '\"' -replace '\$', '\$'

    Write-Host "Creating systemd user service..." -ForegroundColor Cyan
    Invoke-WSLCommand -DistroName $global:wslDistroName -Command "mkdir -p ~/.config/systemd/user"
    Invoke-WSLCommand -DistroName $global:wslDistroName -Command "echo `"$escapedContent`" > ~/.config/systemd/user/$($global:openclawServiceName).service"

    Invoke-WSLCommand -DistroName $global:wslDistroName -Command "systemctl --user daemon-reload"

    Write-Host "OpenClaw service configured successfully." -ForegroundColor Green
    return $true
}

#==============================================================================
# Function: Start-OpenClawService
#==============================================================================
<#
.SYNOPSIS
    Starts the OpenClaw gateway service.
.DESCRIPTION
    Starts the systemd user service for OpenClaw.
.OUTPUTS
    [void]
#>
function Start-OpenClawService {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param()

    if (-not $PSCmdlet.ShouldProcess($global:openclawServiceName, "Start OpenClaw Service")) {
        return
    }

    Write-Host "Starting OpenClaw service..." -ForegroundColor Yellow

    if (-not (Test-WSLDistroExists -DistroName $global:wslDistroName)) {
        Write-Error "WSL distro '$($global:wslDistroName)' not found. Please run Setup_Core_WSL_OpenClaw.ps1 first."
        return
    }

    Invoke-WSLCommand -DistroName $global:wslDistroName -Command "systemctl --user start $($global:openclawServiceName)"

    Start-Sleep -Seconds 3

    $status = Invoke-WSLCommand -DistroName $global:wslDistroName -Command "systemctl --user is-active $($global:openclawServiceName)"

    if ($status -match "active") {
        Write-Host "OpenClaw service started successfully." -ForegroundColor Green
        Write-Host "Control UI: http://127.0.0.1:$($global:controlUiPort)/" -ForegroundColor Cyan
        Write-Host "Canvas:     http://127.0.0.1:$($global:canvasPort)/" -ForegroundColor Cyan
    }
    else {
        Write-Warning "Service may not have started correctly. Status: $status"
        Write-Host "Check logs with: wsl -d $($global:wslDistroName) -- journalctl --user -u $($global:openclawServiceName) -f"
    }
}

#==============================================================================
# Function: Stop-OpenClawService
#==============================================================================
<#
.SYNOPSIS
    Stops the OpenClaw gateway service.
.DESCRIPTION
    Stops the systemd user service for OpenClaw.
.OUTPUTS
    [void]
#>
function Stop-OpenClawService {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param()

    if (-not $PSCmdlet.ShouldProcess($global:openclawServiceName, "Stop OpenClaw Service")) {
        return
    }

    Write-Host "Stopping OpenClaw service..." -ForegroundColor Yellow

    if (-not (Test-WSLDistroExists -DistroName $global:wslDistroName)) {
        Write-Warning "WSL distro '$($global:wslDistroName)' not found."
        return
    }

    Invoke-WSLCommand -DistroName $global:wslDistroName -Command "systemctl --user stop $($global:openclawServiceName)"

    Write-Host "OpenClaw service stopped." -ForegroundColor Green
}

#==============================================================================
# Function: Get-OpenClawServiceStatus
#==============================================================================
<#
.SYNOPSIS
    Gets the status of the OpenClaw service.
.DESCRIPTION
    Returns the current status of the OpenClaw systemd service.
.OUTPUTS
    [string] Service status.
#>
function Get-OpenClawServiceStatus {
    [CmdletBinding()]
    [OutputType([string])]
    param()

    if (-not (Test-WSLDistroExists -DistroName $global:wslDistroName)) {
        return "Distro Not Found"
    }

    $status = Invoke-WSLCommand -DistroName $global:wslDistroName -Command "systemctl --user is-active $($global:openclawServiceName) 2>/dev/null || echo 'not-configured'"

    return $status.Trim()
}

#==============================================================================
# Function: Install-OpenClaw
#==============================================================================
<#
.SYNOPSIS
    Performs full OpenClaw installation.
.DESCRIPTION
    Orchestrates the complete installation: Node.js, OpenClaw CLI, and service.
    Requires the WSL distro to already exist (run Setup_Core_WSL_OpenClaw.ps1 first).
.OUTPUTS
    [void]
#>
function Install-OpenClaw {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param()

    if (-not $PSCmdlet.ShouldProcess("OpenClaw", "Full Installation")) {
        return
    }

    Write-Host ""
    Write-Host "===========================================" -ForegroundColor Yellow
    Write-Host "OpenClaw Installation" -ForegroundColor White
    Write-Host "===========================================" -ForegroundColor Yellow
    Write-Host ""

    if (-not (Test-WSLDistroExists -DistroName $global:wslDistroName)) {
        Write-Error "WSL distro '$($global:wslDistroName)' not found."
        Write-Host ""
        Write-Host "Please run Setup_Core_WSL_OpenClaw.ps1 first to create the distro." -ForegroundColor Yellow
        Write-Host ""
        return
    }

    Write-Host "This will install:" -ForegroundColor Cyan
    Write-Host "  - Node.js $($global:nodeMinVersion)+" -ForegroundColor Cyan
    Write-Host "  - OpenClaw CLI and Gateway" -ForegroundColor Cyan
    Write-Host "  - Systemd user service" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Ports:" -ForegroundColor Cyan
    Write-Host "  - Control UI:  127.0.0.1:$($global:controlUiPort)" -ForegroundColor Cyan
    Write-Host "  - Canvas Host: 127.0.0.1:$($global:canvasPort)" -ForegroundColor Cyan
    Write-Host ""

    Write-Host "===========================================" -ForegroundColor Yellow
    Write-Host "Configuration" -ForegroundColor White
    Write-Host "===========================================" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "OpenClaw requires an Anthropic API key to function." -ForegroundColor Cyan
    Write-Host "Get your API key from: https://console.anthropic.com/settings/keys" -ForegroundColor Cyan
    Write-Host ""

    $secureApiKey = Read-Host "Enter your Anthropic API key (or press Enter to skip)" -AsSecureString
    $anthropicApiKey = ""
    if ($secureApiKey.Length -gt 0) {
        $bstr = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureApiKey)
        $anthropicApiKey = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($bstr)
        [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
        $anthropicApiKey = $anthropicApiKey.Trim()
    }

    Write-Host ""

    Test-AdminPrivilege

    Test-WSLStatus

    Write-Host ""
    Write-Host "Step 1: Installing Node.js..." -ForegroundColor White
    if (-not (Install-NodeJS)) {
        Write-Error "Failed to install Node.js. Aborting."
        return
    }

    Write-Host ""
    Write-Host "Step 2: Installing OpenClaw CLI..." -ForegroundColor White
    if (-not (Install-OpenClawCLI)) {
        Write-Error "Failed to install OpenClaw CLI. Aborting."
        return
    }

    if (-not [string]::IsNullOrEmpty($anthropicApiKey)) {
        Write-Host ""
        Write-Host "Step 3: Configuring OpenClaw with API key..." -ForegroundColor White
        if (-not (Initialize-OpenClawConfig -AnthropicApiKey $anthropicApiKey)) {
            Write-Warning "OpenClaw configuration may not be complete. You can run 'openclaw onboard' manually later."
        }

        Write-Host ""
        Write-Host "Step 4: Configuring Service..." -ForegroundColor White
    }
    else {
        Write-Host ""
        Write-Host "Step 3: Configuring Service (skipped API configuration)..." -ForegroundColor White
        Write-Host "Note: You can run 'openclaw onboard' later to configure the API key." -ForegroundColor Yellow
    }

    if (-not (Install-OpenClawService)) {
        Write-Error "Failed to configure service. Aborting."
        return
    }

    Write-Host ""
    Write-Host "Step 5: Starting Service..." -ForegroundColor White
    Invoke-WSLCommand -DistroName $global:wslDistroName -Command "systemctl --user start $($global:openclawServiceName)"

    Start-Sleep -Seconds 3

    $status = Invoke-WSLCommand -DistroName $global:wslDistroName -Command "systemctl --user is-active $($global:openclawServiceName)"
    if ($status -notmatch "active") {
        Write-Warning "Service may not have started correctly. Status: $status"
    }

    Write-Host ""
    Write-Host "===========================================" -ForegroundColor Green
    Write-Host "OpenClaw Installation Complete!" -ForegroundColor Green
    Write-Host "===========================================" -ForegroundColor Green
    Write-Host ""

    Write-Host "Getting dashboard URL..." -ForegroundColor Cyan
    $dashboardOutput = Invoke-WSLCommand -DistroName $global:wslDistroName -Command "openclaw dashboard --no-open 2>&1"
    Write-Host ""
    Write-Host "Dashboard URL:" -ForegroundColor Cyan
    Write-Host "  $dashboardOutput" -ForegroundColor White
    Write-Host ""
}

#==============================================================================
# Function: Uninstall-OpenClaw
#==============================================================================
<#
.SYNOPSIS
    Uninstalls OpenClaw application.
.DESCRIPTION
    Stops services, removes the OpenClaw package and service files.
    Preserves the WSL distro and Node.js for potential reinstallation.
.OUTPUTS
    [void]
#>
function Uninstall-OpenClaw {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param()

    if (-not $PSCmdlet.ShouldProcess("OpenClaw", "Uninstall")) {
        return
    }

    Write-Host ""
    Write-Host "===========================================" -ForegroundColor Yellow
    Write-Host "OpenClaw Uninstallation" -ForegroundColor White
    Write-Host "===========================================" -ForegroundColor Yellow
    Write-Host ""

    if (-not (Test-WSLDistroExists -DistroName $global:wslDistroName)) {
        Write-Warning "WSL distro '$($global:wslDistroName)' not found. Nothing to uninstall."
        return
    }

    Stop-OpenClawService

    Write-Host "Disabling and removing service..." -ForegroundColor Cyan
    Invoke-WSLCommand -DistroName $global:wslDistroName -Command "systemctl --user disable $($global:openclawServiceName) 2>/dev/null || true"
    Invoke-WSLCommand -DistroName $global:wslDistroName -Command "rm -f ~/.config/systemd/user/$($global:openclawServiceName).service"
    Invoke-WSLCommand -DistroName $global:wslDistroName -Command "systemctl --user daemon-reload"

    Write-Host "Uninstalling OpenClaw..." -ForegroundColor Cyan
    Invoke-WSLCommand -DistroName $global:wslDistroName -AsRoot -Command "npm uninstall -g openclaw 2>/dev/null || true"
    Invoke-WSLCommand -DistroName $global:wslDistroName -Command "rm -f ~/.local/bin/openclaw 2>/dev/null || true"
    Invoke-WSLCommand -DistroName $global:wslDistroName -Command "rm -rf ~/.openclaw/bin 2>/dev/null || true"

    $removeConfig = Read-Host "Remove OpenClaw configuration (~/.openclaw)? (Y/N)"
    if ($removeConfig -eq "Y") {
        Write-Host "Removing configuration directory..." -ForegroundColor Cyan
        Invoke-WSLCommand -DistroName $global:wslDistroName -Command "rm -rf ~/.openclaw"
    }

    Write-Host ""
    Write-Host "OpenClaw uninstalled successfully." -ForegroundColor Green
    Write-Host "The WSL distro and Node.js have been preserved." -ForegroundColor DarkGray
    Write-Host "To reinstall OpenClaw, use option 2 from the menu." -ForegroundColor DarkGray
    Write-Host "To remove the distro entirely, run Setup_Core_WSL_OpenClaw.ps1." -ForegroundColor DarkGray
    Write-Host ""
}

#==============================================================================
# Function: Update-OpenClaw
#==============================================================================
<#
.SYNOPSIS
    Updates OpenClaw to the latest version.
.DESCRIPTION
    Stops the service, updates via npm, and restarts.
.OUTPUTS
    [void]
#>
function Update-OpenClaw {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param()

    if (-not $PSCmdlet.ShouldProcess("OpenClaw", "Update")) {
        return
    }

    Write-Host "Updating OpenClaw..." -ForegroundColor Yellow

    if (-not (Test-WSLDistroExists -DistroName $global:wslDistroName)) {
        Write-Error "WSL distro '$($global:wslDistroName)' not found. Please run Setup_Core_WSL_OpenClaw.ps1 first."
        return
    }

    $wasRunning = (Get-OpenClawServiceStatus) -eq "active"

    if ($wasRunning) {
        Stop-OpenClawService
    }

    Write-Host "Updating OpenClaw via npm (this may take a few minutes)..." -ForegroundColor Cyan
    & wsl --distribution $global:wslDistroName -- npm update -g openclaw

    $newVersion = Invoke-WSLCommand -DistroName $global:wslDistroName -Command "openclaw --version"
    Write-Host "OpenClaw updated to: $newVersion" -ForegroundColor Green

    if ($wasRunning) {
        Start-OpenClawService
    }
}

#==============================================================================
# Function: Show-OpenClawStatus
#==============================================================================
<#
.SYNOPSIS
    Shows comprehensive OpenClaw status information.
.DESCRIPTION
    Displays distro status, service status, version info, and tests connectivity.
.OUTPUTS
    [void]
#>
function Show-OpenClawStatus {
    [CmdletBinding()]
    param()

    Write-Host ""
    Write-Host "===========================================" -ForegroundColor Yellow
    Write-Host "OpenClaw Status" -ForegroundColor White
    Write-Host "===========================================" -ForegroundColor Yellow

    $distroExists = Test-WSLDistroExists -DistroName $global:wslDistroName
    $distroStatus = if ($distroExists) { Get-WSLDistroStatus -DistroName $global:wslDistroName } else { "Not Installed" }

    Write-Host ""
    Write-Host "WSL Distro:" -ForegroundColor White
    Write-Host "  Name:   $($global:wslDistroName)" -ForegroundColor Cyan
    Write-Host "  Status: $distroStatus" -ForegroundColor $(if ($distroStatus -eq "Running") { "Green" } elseif ($distroStatus -eq "Stopped") { "Yellow" } else { "Red" })

    if (-not $distroExists) {
        Write-Host ""
        Write-Host "WSL distro not found. Run Setup_Core_WSL_OpenClaw.ps1 to create it." -ForegroundColor Yellow
        Write-Host ""
        return
    }

    $serviceStatus = Get-OpenClawServiceStatus
    Write-Host ""
    Write-Host "Service:" -ForegroundColor White
    Write-Host "  Status: $serviceStatus" -ForegroundColor $(if ($serviceStatus -eq "active") { "Green" } else { "Yellow" })

    $nodeVersion = Invoke-WSLCommand -DistroName $global:wslDistroName -Command "node --version 2>/dev/null || echo 'not installed'"
    $openclawVersion = Invoke-WSLCommand -DistroName $global:wslDistroName -Command "openclaw --version 2>/dev/null || echo 'not installed'"

    Write-Host ""
    Write-Host "Versions:" -ForegroundColor White
    Write-Host "  Node.js:  $nodeVersion" -ForegroundColor Cyan
    Write-Host "  OpenClaw: $openclawVersion" -ForegroundColor Cyan

    Write-Host ""
    Write-Host "Network Connectivity:" -ForegroundColor White

    $tcpControlUi = Test-TCPPort -ComputerName "localhost" -Port $global:controlUiPort -serviceName "Control UI"
    $tcpCanvas = Test-TCPPort -ComputerName "localhost" -Port $global:canvasPort -serviceName "Canvas"

    $httpControlUi = $false
    if ($tcpControlUi) {
        $httpControlUi = Test-HTTPPort -Uri "http://localhost:$($global:controlUiPort)" -serviceName "Control UI"
    }

    Write-Host ""
    Write-Host "Connectivity Summary:" -ForegroundColor White
    Write-Host "  Control UI TCP ($($global:controlUiPort)): $(if ($tcpControlUi) { 'OK' } else { 'FAILED' })" -ForegroundColor $(if ($tcpControlUi) { "Green" } else { "Red" })
    Write-Host "  Control UI HTTP:       $(if ($httpControlUi) { 'OK' } else { 'FAILED' })" -ForegroundColor $(if ($httpControlUi) { "Green" } else { "Red" })
    Write-Host "  Canvas TCP ($($global:canvasPort)):     $(if ($tcpCanvas) { 'OK' } else { 'FAILED' })" -ForegroundColor $(if ($tcpCanvas) { "Green" } else { "Red" })

    Write-Host ""
    Write-Host "Access URLs:" -ForegroundColor White
    Write-Host "  Control UI:  http://127.0.0.1:$($global:controlUiPort)/" -ForegroundColor Cyan
    Write-Host "  Canvas Host: http://127.0.0.1:$($global:canvasPort)/" -ForegroundColor Cyan

    Write-Host ""
}

#==============================================================================
# Function: Show-OpenClawLogs
#==============================================================================
<#
.SYNOPSIS
    Shows OpenClaw service logs.
.DESCRIPTION
    Displays journalctl logs for the OpenClaw service.
.OUTPUTS
    [void]
#>
function Show-OpenClawLogs {
    [CmdletBinding()]
    param()

    if (-not (Test-WSLDistroExists -DistroName $global:wslDistroName)) {
        Write-Error "WSL distro '$($global:wslDistroName)' not found."
        return
    }

    Write-Host "Showing OpenClaw service logs (Ctrl+C to exit)..." -ForegroundColor Yellow
    Write-Host ""

    wsl --distribution $global:wslDistroName -- journalctl --user -u $global:openclawServiceName -f --no-pager
}

#==============================================================================
# Function: Backup-OpenClawData
#==============================================================================
<#
.SYNOPSIS
    Backs up OpenClaw bot personality, memory, and configuration files.
.DESCRIPTION
    Creates a timestamped tar.gz archive containing the OpenClaw configuration
    directory (~/.openclaw) and the workspace directory (~/workspace) which holds
    SOUL.md, USER.md, IDENTITY.md, MEMORY.md, HEARTBEAT.md, TOOLS.md, skills,
    tools, and daily memory files. The archive is saved to the local Backup folder.
.OUTPUTS
    [void]
#>
function Backup-OpenClawData {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param()

    if (-not $PSCmdlet.ShouldProcess("OpenClaw", "Backup Personality Data")) {
        return
    }

    Write-Host ""
    Write-Host "===========================================" -ForegroundColor Yellow
    Write-Host "OpenClaw Data Backup" -ForegroundColor White
    Write-Host "===========================================" -ForegroundColor Yellow
    Write-Host ""

    if (-not (Test-WSLDistroExists -DistroName $global:wslDistroName)) {
        Write-Error "WSL distro '$($global:wslDistroName)' not found."
        return
    }

    Write-Host "This will back up:" -ForegroundColor Cyan
    Write-Host "  - Configuration:  ~/.openclaw/ (openclaw.json, API keys, etc.)" -ForegroundColor DarkGray
    Write-Host "  - Personality:    ~/workspace/SOUL.md, IDENTITY.md" -ForegroundColor DarkGray
    Write-Host "  - User Context:   ~/workspace/USER.md" -ForegroundColor DarkGray
    Write-Host "  - Memory:         ~/workspace/MEMORY.md, memory/*.md" -ForegroundColor DarkGray
    Write-Host "  - Bot Skills:     ~/workspace/skills/" -ForegroundColor DarkGray
    Write-Host "  - Bot Tools:      ~/workspace/tools/" -ForegroundColor DarkGray
    Write-Host "  - Heartbeat:      ~/workspace/HEARTBEAT.md" -ForegroundColor DarkGray
    Write-Host "  - Optimization:   ~/workspace/OPTIMIZATION.md, TOOLS.md" -ForegroundColor DarkGray
    Write-Host ""

    $backupDir = Join-Path $global:backupFolder $global:openclawBackupSubfolder
    if (-not (Test-Path $backupDir)) {
        New-Item -ItemType Directory -Force -Path $backupDir | Out-Null
        Write-Host "Created backup folder: $backupDir" -ForegroundColor DarkGray
    }

    $timestamp = Get-Date -Format "yyyyMMdd-HHmm"
    $backupFileName = "openclaw-data-$timestamp.tar.gz"
    $backupFilePath = Join-Path $backupDir $backupFileName

    Write-Host "Checking available data directories..." -ForegroundColor Cyan

    $configDirExists = Invoke-WSLCommand -DistroName $global:wslDistroName -Command "test -d ~/.openclaw && echo 'yes' || echo 'no'"
    $workspaceDirExists = Invoke-WSLCommand -DistroName $global:wslDistroName -Command "test -d ~/workspace && echo 'yes' || echo 'no'"

    $dirsTrimmed = ""
    if ($configDirExists -match "yes") { $dirsTrimmed += " .openclaw" }
    if ($workspaceDirExists -match "yes") { $dirsTrimmed += " workspace" }
    $dirsTrimmed = $dirsTrimmed.Trim()

    if ([string]::IsNullOrWhiteSpace($dirsTrimmed)) {
        Write-Warning "No OpenClaw data directories found (~/.openclaw or ~/workspace)."
        Write-Host "Nothing to back up." -ForegroundColor Yellow
        return
    }

    Write-Host "Found directories to back up: $dirsTrimmed" -ForegroundColor DarkGray

    Write-Host "Creating backup archive..." -ForegroundColor Cyan
    $wslTarPath = "/tmp/$backupFileName"
    Invoke-WSLCommand -DistroName $global:wslDistroName -Command "cd ~ && tar -czf $wslTarPath $dirsTrimmed"

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to create backup archive in WSL."
        return
    }

    Write-Host "Copying archive to Windows..." -ForegroundColor Cyan
    $wslUncPath = "\\wsl`$\$($global:wslDistroName)\tmp\$backupFileName"
    try {
        Copy-Item -Path $wslUncPath -Destination $backupFilePath -Force -ErrorAction Stop
    }
    catch {
        Write-Error "Failed to copy backup to Windows: $_"
        Invoke-WSLCommand -DistroName $global:wslDistroName -Command "rm -f $wslTarPath"
        return
    }

    Invoke-WSLCommand -DistroName $global:wslDistroName -Command "rm -f $wslTarPath"

    $fileSize = (Get-Item $backupFilePath).Length
    $fileSizeKB = [math]::Round($fileSize / 1024, 1)

    Write-Host ""
    Write-Host "===========================================" -ForegroundColor Green
    Write-Host "Backup Complete!" -ForegroundColor Green
    Write-Host "===========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "File: $backupFilePath" -ForegroundColor Cyan
    Write-Host "Size: $fileSizeKB KB" -ForegroundColor Cyan
    Write-Host ""

    Write-Host "Archive contents:" -ForegroundColor White
    $wslBackupPath = "/tmp/openclaw-list-$timestamp.tar.gz"
    $wslUncListPath = "\\wsl`$\$($global:wslDistroName)\tmp\openclaw-list-$timestamp.tar.gz"
    try {
        Copy-Item -Path $backupFilePath -Destination $wslUncListPath -Force -ErrorAction SilentlyContinue
        Invoke-WSLCommand -DistroName $global:wslDistroName -Command "tar -tzf $wslBackupPath 2>/dev/null | head -30"
        Invoke-WSLCommand -DistroName $global:wslDistroName -Command "rm -f $wslBackupPath"
    }
    catch {
        Write-Host "  (unable to list contents)" -ForegroundColor DarkGray
    }
    Write-Host ""
}

#==============================================================================
# Function: Restore-OpenClawData
#==============================================================================
<#
.SYNOPSIS
    Restores OpenClaw bot personality, memory, and configuration files from backup.
.DESCRIPTION
    Lists available backup archives and prompts the user to select one.
    Extracts the selected archive back into the WSL distro home directory,
    restoring ~/.openclaw and ~/workspace with all personality files, memory,
    skills, tools, and configuration.
.OUTPUTS
    [void]
#>
function Restore-OpenClawData {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param()

    if (-not $PSCmdlet.ShouldProcess("OpenClaw", "Restore Personality Data")) {
        return
    }

    Write-Host ""
    Write-Host "===========================================" -ForegroundColor Yellow
    Write-Host "OpenClaw Data Restore" -ForegroundColor White
    Write-Host "===========================================" -ForegroundColor Yellow
    Write-Host ""

    if (-not (Test-WSLDistroExists -DistroName $global:wslDistroName)) {
        Write-Error "WSL distro '$($global:wslDistroName)' not found."
        return
    }

    $backupDir = Join-Path $global:backupFolder $global:openclawBackupSubfolder
    if (-not (Test-Path $backupDir)) {
        Write-Warning "Backup folder '$backupDir' not found. No backups available."
        return
    }

    $backupFiles = Get-ChildItem -Path $backupDir -Filter "openclaw-data-*.tar.gz" | Sort-Object LastWriteTime -Descending
    if (-not $backupFiles) {
        Write-Warning "No backup files found in '$backupDir'."
        return
    }

    Write-Host "Available backups:" -ForegroundColor Cyan
    Write-Host ""
    for ($i = 0; $i -lt $backupFiles.Count; $i++) {
        $file = $backupFiles[$i]
        $sizeKB = [math]::Round($file.Length / 1024, 1)
        $dateStr = $file.LastWriteTime.ToString("yyyy-MM-dd HH:mm")
        Write-Host "  [$($i + 1)] $($file.Name)  ($sizeKB KB, $dateStr)" -ForegroundColor White
    }
    Write-Host "  [0] Cancel" -ForegroundColor DarkGray
    Write-Host ""

    do {
        $userInput = Read-Host "Select backup to restore (0-$($backupFiles.Count))"
        if ([string]::IsNullOrWhiteSpace($userInput) -or $userInput -eq "0") {
            Write-Host "Restore cancelled." -ForegroundColor Yellow
            return
        }
        $choice = 0
        if ([int]::TryParse($userInput, [ref]$choice) -and $choice -ge 1 -and $choice -le $backupFiles.Count) {
            break
        }
        Write-Host "Invalid choice." -ForegroundColor Red
    } while ($true)

    $selectedBackup = $backupFiles[$choice - 1]

    Write-Host ""
    Write-Host "Selected: $($selectedBackup.Name)" -ForegroundColor Cyan

    # Convert Windows path to WSL /mnt/ path for direct access
    $winFullPath = (Resolve-Path $selectedBackup.FullName).Path
    $wslMntPath = $winFullPath
    if ($winFullPath -match '^([A-Z]):\\(.*)$') {
        $drive = $Matches[1].ToLower()
        $rest = $Matches[2] -replace '\\', '/'
        $wslMntPath = "/mnt/$drive/$rest"
    }

    Write-Host ""
    Write-Host "Archive contents preview:" -ForegroundColor White
    Invoke-WSLCommand -DistroName $global:wslDistroName -Command "tar -tzf '$wslMntPath' 2>/dev/null | head -30"
    Write-Host ""

    Write-Host "WARNING: This will overwrite existing files in ~/.openclaw and ~/workspace." -ForegroundColor Yellow
    $confirm = Read-Host "Proceed with restore? (Y/N)"
    if ($confirm -ne "Y") {
        Write-Host "Restore cancelled." -ForegroundColor Yellow
        return
    }

    Write-Host ""
    Write-Host "Restoring files..." -ForegroundColor Cyan
    Invoke-WSLCommand -DistroName $global:wslDistroName -Command "cd ~ && tar -xzf '$wslMntPath'"

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to extract backup archive."
        return
    }

    Write-Host ""
    Write-Host "===========================================" -ForegroundColor Green
    Write-Host "Restore Complete!" -ForegroundColor Green
    Write-Host "===========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Restored files:" -ForegroundColor Cyan

    $configExists = Invoke-WSLCommand -DistroName $global:wslDistroName -Command "test -d ~/.openclaw && echo 'yes' || echo 'no'"
    $workspaceExists = Invoke-WSLCommand -DistroName $global:wslDistroName -Command "test -d ~/workspace && echo 'yes' || echo 'no'"

    Write-Host "  Configuration (~/.openclaw): $(if ($configExists -match 'yes') { 'Restored' } else { 'Not found in backup' })" -ForegroundColor $(if ($configExists -match 'yes') { "Green" } else { "Yellow" })
    Write-Host "  Workspace (~/workspace):     $(if ($workspaceExists -match 'yes') { 'Restored' } else { 'Not found in backup' })" -ForegroundColor $(if ($workspaceExists -match 'yes') { "Green" } else { "Yellow" })

    if ($workspaceExists -match "yes") {
        $soulExists = Invoke-WSLCommand -DistroName $global:wslDistroName -Command "test -f ~/workspace/SOUL.md && echo 'yes' || echo 'no'"
        $identityExists = Invoke-WSLCommand -DistroName $global:wslDistroName -Command "test -f ~/workspace/IDENTITY.md && echo 'yes' || echo 'no'"
        $userExists = Invoke-WSLCommand -DistroName $global:wslDistroName -Command "test -f ~/workspace/USER.md && echo 'yes' || echo 'no'"
        $memoryExists = Invoke-WSLCommand -DistroName $global:wslDistroName -Command "test -d ~/workspace/memory && echo 'yes' || echo 'no'"
        $skillsExist = Invoke-WSLCommand -DistroName $global:wslDistroName -Command "test -d ~/workspace/skills && echo 'yes' || echo 'no'"
        $toolsExist = Invoke-WSLCommand -DistroName $global:wslDistroName -Command "test -d ~/workspace/tools && echo 'yes' || echo 'no'"

        Write-Host "    SOUL.md:     $(if ($soulExists -match 'yes') { 'OK' } else { 'Not found' })" -ForegroundColor $(if ($soulExists -match 'yes') { "Green" } else { "DarkGray" })
        Write-Host "    IDENTITY.md: $(if ($identityExists -match 'yes') { 'OK' } else { 'Not found' })" -ForegroundColor $(if ($identityExists -match 'yes') { "Green" } else { "DarkGray" })
        Write-Host "    USER.md:     $(if ($userExists -match 'yes') { 'OK' } else { 'Not found' })" -ForegroundColor $(if ($userExists -match 'yes') { "Green" } else { "DarkGray" })
        Write-Host "    memory/:     $(if ($memoryExists -match 'yes') { 'OK' } else { 'Not found' })" -ForegroundColor $(if ($memoryExists -match 'yes') { "Green" } else { "DarkGray" })
        Write-Host "    skills/:     $(if ($skillsExist -match 'yes') { 'OK' } else { 'Not found' })" -ForegroundColor $(if ($skillsExist -match 'yes') { "Green" } else { "DarkGray" })
        Write-Host "    tools/:      $(if ($toolsExist -match 'yes') { 'OK' } else { 'Not found' })" -ForegroundColor $(if ($toolsExist -match 'yes') { "Green" } else { "DarkGray" })
    }

    Write-Host ""
    Write-Host "Note: Restart the OpenClaw service for config changes to take effect." -ForegroundColor Yellow
    Write-Host ""
}

################################################################################
# Main Menu Loop
################################################################################

$menuTitle = "OpenClaw Application Menu"
$menuItems = [ordered]@{
    "1" = "Show Status and Test Connection"
    "2" = "Install OpenClaw"
    "3" = "Uninstall OpenClaw"
    "4" = "Update OpenClaw"
    "5" = "Start Service"
    "6" = "Stop Service"
    "7" = "Show Logs"
    "8" = "Backup Data (personality, memory, config)"
    "9" = "Restore Data (personality, memory, config)"
    "0" = "Exit menu"
}

$menuActions = @{
    "1" = { Show-OpenClawStatus }
    "2" = { Install-OpenClaw }
    "3" = { Uninstall-OpenClaw }
    "4" = { Update-OpenClaw }
    "5" = { Start-OpenClawService }
    "6" = { Stop-OpenClawService }
    "7" = { Show-OpenClawLogs }
    "8" = { Backup-OpenClawData }
    "9" = { Restore-OpenClawData }
}

Invoke-MenuLoop -MenuTitle $menuTitle -MenuItems $menuItems -ActionMap $menuActions -ExitChoice "0"
