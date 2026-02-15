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
$global:openclawServiceName = "openclaw-gateway"

# Node.js minimum version (LTS)
$global:nodeMinVersion = 24

# Backup configuration
$global:backupFolder = ".\Backup"
$global:openclawBackupSubfolder = "openclaw_data"
$global:workspacePath = "~/workspace"

# Windows Scheduled Task "service" to auto-start WSL distro.
# WSL distros are per-user (HKCU), so SYSTEM/NetworkService cannot see them.
# When elevated: AtStartup + S4U logon starts before user login.
# When non-elevated: AtLogOn + Interactive logon starts at user login.
$global:openclawServiceTaskName = "OpenClaw-WSL-Boot"
$global:openclawServiceWrapperPath = Join-Path $global:installRoot "service-wrapper.ps1"
$global:openclawServiceLogPath = Join-Path $global:installRoot "service.log"



#==============================================================================
# Function: Test-WSLMirroredNetworking
#==============================================================================
<#
.SYNOPSIS
    Checks if WSL2 mirrored networking is configured.
.DESCRIPTION
    Reads %USERPROFILE%\.wslconfig to check if networkingMode=mirrored is set.
    WSL2 NAT mode (default) does not reliably forward ports from Windows to WSL.
    Mirrored mode shares the host network stack, making WSL services accessible
    on localhost from Windows.
.OUTPUTS
    [bool] True if mirrored networking is configured, false otherwise.
#>
function Test-WSLMirroredNetworking {
    [CmdletBinding()]
    [OutputType([bool])]
    param()

    $wslConfigPath = Join-Path ([Environment]::GetFolderPath("UserProfile")) ".wslconfig"

    if (Test-Path $wslConfigPath) {
        $content = Get-Content $wslConfigPath -Raw
        if ($content -match "networkingMode\s*=\s*mirrored") {
            return $true
        }
    }

    return $false
}

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
.PARAMETER Sensitive
    Suppresses command logging to avoid exposing secrets (API keys, tokens).
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
        [switch]$AsRoot,

        [Parameter(Mandatory = $false)]
        [switch]$Sensitive
    )

    $wslArgs = @("--distribution", $DistroName)
    if ($AsRoot) {
        $wslArgs += @("--user", "root")
    }
    $wslArgs += @("--", "bash", "-c", $Command)

    if ($Sensitive) {
        Write-Host "Executing: [command hidden - contains sensitive data]" -ForegroundColor DarkGray
    }
    else {
        Write-Host "Executing: $Command" -ForegroundColor DarkGray
    }
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

    Write-Host "Installing prerequisites..." -ForegroundColor Cyan
    Invoke-WSLCommand -DistroName $global:wslDistroName -AsRoot -Command "apt-get update -y && apt-get install -y ca-certificates curl gnupg"

    Write-Host "Adding NodeSource repository for Node.js $($global:nodeMinVersion).x..." -ForegroundColor Cyan
    Invoke-WSLCommand -DistroName $global:wslDistroName -AsRoot -Command "curl -fsSL https://deb.nodesource.com/setup_$($global:nodeMinVersion).x | bash -"

    Write-Host "Installing Node.js..." -ForegroundColor Cyan
    Invoke-WSLCommand -DistroName $global:wslDistroName -AsRoot -Command "apt-get install -y nodejs"

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
        "--gateway-bind lan",
        "--install-daemon",
        "--daemon-runtime node",
        "--skip-skills"
    ) -join " "

    Write-Host "Running OpenClaw onboarding..." -ForegroundColor Cyan
    $result = Invoke-WSLCommand -DistroName $global:wslDistroName -Command $onboardCommand -Sensitive

    if ($LASTEXITCODE -ne 0) {
        Write-Warning "OpenClaw onboarding returned non-zero exit code. Output: $result"
        return $false
    }

    Write-Host "OpenClaw configuration initialized successfully." -ForegroundColor Green

    Write-Host "Enabling loginctl linger for systemd user service persistence..." -ForegroundColor Cyan
    $wslUser = (Invoke-WSLCommand -DistroName $global:wslDistroName -Command "whoami") -join ""
    $wslUser = $wslUser.Trim()
    Invoke-WSLCommand -DistroName $global:wslDistroName -AsRoot -Command "loginctl enable-linger $wslUser"
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Linger enabled for user '$wslUser': systemd user services will persist across sessions." -ForegroundColor Green
    }
    else {
        Write-Warning "Failed to enable linger. The gateway service may stop when the WSL session ends."
    }

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

    if ($status.Trim() -eq "active") {
        Write-Host "OpenClaw service started successfully." -ForegroundColor Green
        Write-Host "Control UI: http://127.0.0.1:$($global:controlUiPort)/" -ForegroundColor Cyan
        Write-Host "Canvas:     http://127.0.0.1:$($global:canvasPort)/" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "Dashboard Access:" -ForegroundColor Cyan
        $dashboardOutput = Invoke-WSLCommand -DistroName $global:wslDistroName -Command "openclaw dashboard --no-open 2>&1"
        $dashboardText = ($dashboardOutput -join "`n").Trim()
        Write-Host "  $dashboardText" -ForegroundColor White
        Write-Host ""
        Write-Host "This URL contains your access token." -ForegroundColor Yellow
        Write-Host "Open it in your browser to access the OpenClaw Control UI." -ForegroundColor Yellow
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
    $trimmed = ($status -join " ").Trim()
    if ($trimmed -eq "active") { return "active" }
    if ($trimmed -match "inactive") { return "inactive" }
    if ($trimmed -match "failed") { return "failed" }
    if ($trimmed -match "not-configured") { return "not-configured" }
    return $trimmed
}

#==============================================================================
# Function: Test-WSLVmIdleTimeout
#==============================================================================
<#
.SYNOPSIS
    Checks if vmIdleTimeout=-1 is set in .wslconfig.
.DESCRIPTION
    Reads %USERPROFILE%\.wslconfig and checks for vmIdleTimeout=-1 under [wsl2].
    This setting prevents WSL from automatically shutting down idle distros,
    which is required for OpenClaw to run 24/7 as a background service.
.OUTPUTS
    [bool] True if vmIdleTimeout=-1 is configured, false otherwise.
#>
function Test-WSLVmIdleTimeout {
    [CmdletBinding()]
    [OutputType([bool])]
    param()

    $wslConfigPath = Join-Path ([Environment]::GetFolderPath("UserProfile")) ".wslconfig"

    if (Test-Path $wslConfigPath) {
        $content = Get-Content $wslConfigPath -Raw
        if ($content -match "vmIdleTimeout\s*=\s*-1") {
            return $true
        }
    }

    return $false
}

#==============================================================================
# Function: Set-WSLVmIdleTimeout
#==============================================================================
<#
.SYNOPSIS
    Ensures vmIdleTimeout=-1 is set in .wslconfig to keep WSL running 24/7.
.DESCRIPTION
    Adds or updates the vmIdleTimeout=-1 setting under the [wsl2] section
    of %USERPROFILE%\.wslconfig. This prevents WSL from automatically
    stopping idle distros, which would kill the OpenClaw background service.
    A WSL restart (wsl --shutdown) is required for the change to take effect.
.OUTPUTS
    [bool] True if the setting was added/updated, false if already set.
#>
function Set-WSLVmIdleTimeout {
    [CmdletBinding(SupportsShouldProcess = $true)]
    [OutputType([bool])]
    param()

    $wslConfigPath = Join-Path ([Environment]::GetFolderPath("UserProfile")) ".wslconfig"

    if (-not $PSCmdlet.ShouldProcess($wslConfigPath, "Set vmIdleTimeout=-1")) {
        return $false
    }

    if (Test-WSLVmIdleTimeout) {
        Write-Host "vmIdleTimeout=-1 is already set in .wslconfig." -ForegroundColor Green
        return $false
    }

    if (Test-Path $wslConfigPath) {
        $content = Get-Content $wslConfigPath -Raw

        if ($content -match "vmIdleTimeout\s*=") {
            $content = $content -replace "vmIdleTimeout\s*=\s*\S+", "vmIdleTimeout=-1"
            Write-Host "Updated vmIdleTimeout=-1 in .wslconfig." -ForegroundColor Green
        }
        elseif ($content -match "\[wsl2\]") {
            $content = $content -replace "(\[wsl2\])", "`$1`nvmIdleTimeout=-1"
            Write-Host "Added vmIdleTimeout=-1 to [wsl2] section in .wslconfig." -ForegroundColor Green
        }
        else {
            $content = $content.TrimEnd() + "`n`n[wsl2]`nvmIdleTimeout=-1`n"
            Write-Host "Added [wsl2] section with vmIdleTimeout=-1 to .wslconfig." -ForegroundColor Green
        }

        Set-Content -Path $wslConfigPath -Value $content -NoNewline
    }
    else {
        $content = "[wsl2]`nvmIdleTimeout=-1`n"
        Set-Content -Path $wslConfigPath -Value $content -NoNewline
        Write-Host "Created .wslconfig with vmIdleTimeout=-1." -ForegroundColor Green
    }

    return $true
}

#==============================================================================
# Function: Write-OpenClawServiceWrapper
#==============================================================================
<#
.SYNOPSIS
    Creates the service wrapper PowerShell script for the WSL keep-alive task.
.DESCRIPTION
    Generates a wrapper script at $global:openclawServiceWrapperPath that waits
    for WSL availability and then runs 'wsl.exe -d <distro> -- sleep infinity'
    to keep the distro running. Logs activity to $global:openclawServiceLogPath.
    This pattern matches the n8n/Qdrant service wrapper approach.
.OUTPUTS
    [void]
#>
function Write-OpenClawServiceWrapper {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param()

    $distroName = $global:wslDistroName
    $logPath = $global:openclawServiceLogPath

    $wrapper = @"
`$ErrorActionPreference = 'Stop'
`$logPath = '$logPath'

New-Item -ItemType Directory -Path '$(Split-Path $logPath -Parent)' -Force | Out-Null

function Write-ServiceLog {
    param([string]`$Message)
    "[`$(Get-Date -Format o)] `$Message" | Out-File -FilePath `$logPath -Append -Encoding UTF8
}

Write-ServiceLog "OpenClaw WSL keep-alive starting..."

`$maxRetries = 30
for (`$i = 1; `$i -le `$maxRetries; `$i++) {
    `$wslOutput = wsl --list --quiet 2>&1
    if (`$LASTEXITCODE -eq 0) {
        Write-ServiceLog "WSL is available."
        break
    }
    Write-ServiceLog "Waiting for WSL availability (attempt `$i/`$maxRetries)..."
    Start-Sleep -Seconds 10
}

Write-ServiceLog "Starting WSL distro '$distroName' with sleep infinity..."
`$p = Start-Process -FilePath "wsl.exe" -ArgumentList "-d $distroName -- sleep infinity" -WindowStyle Hidden -PassThru
Write-ServiceLog "WSL keep-alive process started. PID=`$(`$p.Id)"
"@

    if ($PSCmdlet.ShouldProcess($global:openclawServiceWrapperPath, "Write OpenClaw service wrapper script")) {
        New-Item -ItemType Directory -Path (Split-Path $global:openclawServiceWrapperPath -Parent) -Force | Out-Null
        Set-Content -LiteralPath $global:openclawServiceWrapperPath -Value $wrapper -Encoding UTF8
    }
}

#==============================================================================
# Function: Install-OpenClawService
#==============================================================================
<#
.SYNOPSIS
    Installs a Scheduled Task as a per-user "service" to keep the WSL distro running.
.DESCRIPTION
    Writes a WSL keep-alive wrapper script and registers it as a Scheduled Task
    using the shared Install-ScheduledTaskService helper. Interactively asks
    whether to enable auto-start and which mode (pre-login or post-login).
.OUTPUTS
    [void]
#>
function Install-OpenClawService {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param()

    Write-OpenClawServiceWrapper

    if (-not (Test-Path -LiteralPath $global:openclawServiceWrapperPath)) {
        throw "Wrapper script was not created: $($global:openclawServiceWrapperPath)"
    }

    $installParams = @{
        TaskName          = $global:openclawServiceTaskName
        WrapperScriptPath = $global:openclawServiceWrapperPath
    }

    Write-Host ""
    $autoChoice = Read-Host "Enable automatic start? (Y/N, default: Y)"
    if ([string]::IsNullOrWhiteSpace($autoChoice) -or $autoChoice.Trim().ToUpper() -eq "Y") {
        $installParams.AutoStart = $true
        Write-Host ""
        Write-Host "  1. Pre-login  - starts at Windows boot, before login (may request elevation)" -ForegroundColor Cyan
        Write-Host "  2. Post-login - starts when current user logs in" -ForegroundColor Cyan
        Write-Host ""
        $modeChoice = Read-Host "Select start mode (1/2, default: 2)"
        if ($modeChoice.Trim() -eq "1") {
            $installParams.PreLogin = $true
        }
    }

    Install-ScheduledTaskService @installParams
}

#==============================================================================
# Function: Uninstall-OpenClawService
#==============================================================================
<#
.SYNOPSIS
    Uninstalls the Scheduled Task "service" for OpenClaw WSL keep-alive.
.DESCRIPTION
    Delegates to the shared Uninstall-ScheduledTaskService helper.
    Stops the task, unregisters it, and removes the wrapper script.
.OUTPUTS
    [void]
#>
function Uninstall-OpenClawService {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param()

    if ($PSCmdlet.ShouldProcess($global:openclawServiceTaskName, "Uninstall OpenClaw WSL keep-alive service")) {
        Uninstall-ScheduledTaskService -TaskName $global:openclawServiceTaskName -WrapperScriptPath $global:openclawServiceWrapperPath
    }
}

#==============================================================================
# Function: Get-OpenClawServiceTaskStatus
#==============================================================================
<#
.SYNOPSIS
    Gets the status of the WSL keep-alive Scheduled Task.
.DESCRIPTION
    Delegates to the shared Get-ScheduledTaskServiceStatus helper.
.OUTPUTS
    [string] Task state (Running, Ready, Disabled, Not Registered).
#>
function Get-OpenClawServiceTaskStatus {
    [CmdletBinding()]
    [OutputType([string])]
    param()

    return Get-ScheduledTaskServiceStatus -TaskName $global:openclawServiceTaskName
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

    if (-not (Test-WSLMirroredNetworking)) {
        Write-Host ""
        Write-Host "WARNING: WSL2 mirrored networking is NOT configured." -ForegroundColor Yellow
        Write-Host "Without it, the OpenClaw service will run inside WSL but may NOT" -ForegroundColor Yellow
        Write-Host "be accessible from Windows browsers (localhost forwarding is unreliable)." -ForegroundColor Yellow
        Write-Host ""
        Write-Host "To fix this, run: .\Setup_Core_1_WSL2.ps1" -ForegroundColor Cyan
        Write-Host "and select the option to configure mirrored networking." -ForegroundColor Cyan
        Write-Host ""
        $continueChoice = Read-Host "Continue installation anyway? (Y/N)"
        if ($continueChoice -ne "Y") {
            Write-Host "Installation cancelled. Run Setup_Core_1_WSL2.ps1 first." -ForegroundColor Yellow
            return
        }
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
        Write-Host "Step 3: Configuring OpenClaw with API key and daemon service..." -ForegroundColor White
        if (-not (Initialize-OpenClawConfig -AnthropicApiKey $anthropicApiKey)) {
            Write-Warning "OpenClaw configuration may not be complete. You can run 'openclaw onboard' manually later."
        }
    }
    else {
        Write-Host ""
        Write-Host "Step 3: Skipped API configuration." -ForegroundColor White
        Write-Host "Note: Run 'openclaw onboard' inside WSL later to configure the API key and daemon." -ForegroundColor Yellow
    }

    Write-Host ""
    # WSL2 shuts down idle VMs after vmIdleTimeout ms (default 60000 = 60s).
    # Setting vmIdleTimeout=-1 disables the idle shutdown so the OpenClaw
    # gateway service stays running 24/7 even when no terminal is attached.
    # Ref: https://learn.microsoft.com/en-us/windows/wsl/wsl-config#main-wsl-settings
    Write-Host ""
    Write-Host "Step 4: Ensuring WSL stays running (vmIdleTimeout=-1)..." -ForegroundColor White
    $vmIdleChanged = Set-WSLVmIdleTimeout
    if ($vmIdleChanged) {
        Write-Host "NOTE: A WSL restart (wsl --shutdown) is needed for vmIdleTimeout to take effect." -ForegroundColor Yellow
        Write-Host "This will be done automatically after installation completes." -ForegroundColor Yellow
    }

    Write-Host ""
    Write-Host "===========================================" -ForegroundColor Green
    Write-Host "OpenClaw App Installation Complete!" -ForegroundColor Green
    Write-Host "===========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Cyan
    Write-Host "  1. Install Service - registers a Scheduled Task to keep WSL running" -ForegroundColor Cyan
    Write-Host "  2. Start Service   - starts the OpenClaw gateway and shows dashboard URL" -ForegroundColor Cyan
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

    Write-Host "Disabling and removing services..." -ForegroundColor Cyan
    Invoke-WSLCommand -DistroName $global:wslDistroName -Command "systemctl --user stop $($global:openclawServiceName) 2>/dev/null || true"
    Invoke-WSLCommand -DistroName $global:wslDistroName -Command "systemctl --user disable $($global:openclawServiceName) 2>/dev/null || true"
    Invoke-WSLCommand -DistroName $global:wslDistroName -Command "rm -f ~/.config/systemd/user/$($global:openclawServiceName).service"
    Invoke-WSLCommand -DistroName $global:wslDistroName -Command "systemctl --user stop openclaw 2>/dev/null || true"
    Invoke-WSLCommand -DistroName $global:wslDistroName -Command "systemctl --user disable openclaw 2>/dev/null || true"
    Invoke-WSLCommand -DistroName $global:wslDistroName -Command "rm -f ~/.config/systemd/user/openclaw.service"
    Invoke-WSLCommand -DistroName $global:wslDistroName -Command "systemctl --user daemon-reload"

    Write-Host "Killing any remaining OpenClaw processes..." -ForegroundColor Cyan
    Invoke-WSLCommand -DistroName $global:wslDistroName -Command "pkill -f 'openclaw' 2>/dev/null || true"
    Invoke-WSLCommand -DistroName $global:wslDistroName -Command "pkill -f 'openclaw-gateway' 2>/dev/null || true"

    Write-Host "Uninstalling OpenClaw..." -ForegroundColor Cyan
    Invoke-WSLCommand -DistroName $global:wslDistroName -AsRoot -Command "npm uninstall -g openclaw 2>/dev/null || true"
    Invoke-WSLCommand -DistroName $global:wslDistroName -Command "rm -f ~/.local/bin/openclaw 2>/dev/null || true"
    Invoke-WSLCommand -DistroName $global:wslDistroName -Command "rm -rf ~/.openclaw/bin 2>/dev/null || true"

    $removeConfig = Read-Host "Remove OpenClaw configuration (~/.openclaw)? (Y/N)"
    if ($removeConfig -eq "Y") {
        Write-Host "Removing configuration directory..." -ForegroundColor Cyan
        Invoke-WSLCommand -DistroName $global:wslDistroName -Command "rm -rf ~/.openclaw"
    }

    Write-Host "Removing WSL keep-alive service (Scheduled Task)..." -ForegroundColor Cyan
    Uninstall-OpenClawService

    Write-Host "Disabling loginctl linger..." -ForegroundColor Cyan
    $wslUser = (Invoke-WSLCommand -DistroName $global:wslDistroName -Command "whoami") -join ""
    $wslUser = $wslUser.Trim()
    Invoke-WSLCommand -DistroName $global:wslDistroName -AsRoot -Command "loginctl disable-linger $wslUser 2>/dev/null || true"

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
    $vmIdleSet = Test-WSLVmIdleTimeout

    Write-Host ""
    Write-Host "Service:" -ForegroundColor White
    Write-Host "  Status: $serviceStatus" -ForegroundColor $(if ($serviceStatus -eq "active") { "Green" } else { "Yellow" })

    Write-Host ""
    Write-Host "WSL Keep-Alive (vmIdleTimeout=-1):" -ForegroundColor White
    Write-Host "  Configured: $vmIdleSet" -ForegroundColor $(if ($vmIdleSet) { "Green" } else { "Red" })

    $serviceTaskStatus = Get-OpenClawServiceTaskStatus
    Write-Host ""
    Write-Host "WSL Service Task ($($global:openclawServiceTaskName)):" -ForegroundColor White
    Write-Host "  Status: $serviceTaskStatus" -ForegroundColor $(if ($serviceTaskStatus -eq "Running") { "Green" } elseif ($serviceTaskStatus -eq "Ready") { "Yellow" } else { "Red" })

    $nodeVersion = Invoke-WSLCommand -DistroName $global:wslDistroName -Command "node --version 2>/dev/null || echo 'not installed'"
    $openclawVersion = Invoke-WSLCommand -DistroName $global:wslDistroName -Command "openclaw --version 2>/dev/null || echo 'not installed'"

    Write-Host ""
    Write-Host "Versions:" -ForegroundColor White
    Write-Host "  Node.js:  $nodeVersion" -ForegroundColor Cyan
    Write-Host "  OpenClaw: $openclawVersion" -ForegroundColor Cyan

    Write-Host ""
    Write-Host "Network Connectivity:" -ForegroundColor White

    Write-Host ""
    Write-Host "  WSL-internal (curl from inside WSL):" -ForegroundColor White
    $wslHttpResult = Invoke-WSLCommand -DistroName $global:wslDistroName -Command "curl -s --connect-timeout 5 --max-time 10 -o /dev/null -w '%{http_code}' http://127.0.0.1:$($global:controlUiPort)/ 2>/dev/null || echo '000'"
    $wslHttpCode = ($wslHttpResult -join "").Trim()
    $wslOk = ($wslHttpCode -eq "200")
    Write-Host "    Control UI (port $($global:controlUiPort)): HTTP $wslHttpCode" -ForegroundColor $(if ($wslOk) { "Green" } else { "Red" })

    Write-Host ""
    Write-Host "  Windows-side (from host to WSL):" -ForegroundColor White
    $tcpControlUi = Test-TCPPort -ComputerName "localhost" -Port $global:controlUiPort -serviceName "Control UI" -Timeout 15
    $httpControlUi = $false
    if ($tcpControlUi) {
        $httpControlUi = Test-HTTPPort -Uri "http://localhost:$($global:controlUiPort)" -serviceName "Control UI" -Timeout 15
    }

    Write-Host ""
    Write-Host "Connectivity Summary:" -ForegroundColor White
    Write-Host "  WSL-internal HTTP:           $(if ($wslOk) { 'OK' } else { 'FAILED' })" -ForegroundColor $(if ($wslOk) { "Green" } else { "Red" })
    Write-Host "  Windows TCP ($($global:controlUiPort)):       $(if ($tcpControlUi) { 'OK' } else { 'FAILED' })" -ForegroundColor $(if ($tcpControlUi) { "Green" } else { "Red" })
    Write-Host "  Windows HTTP:                $(if ($httpControlUi) { 'OK' } else { 'FAILED' })" -ForegroundColor $(if ($httpControlUi) { "Green" } else { "Red" })

    if ($wslOk -and -not $httpControlUi) {
        Write-Host ""
        Write-Host "  NOTE: Service works inside WSL but WSL2 port forwarding is not active." -ForegroundColor Yellow
        Write-Host "  Fix: Enable mirrored networking in %USERPROFILE%\.wslconfig:" -ForegroundColor Yellow
        Write-Host "    [wsl2]" -ForegroundColor DarkGray
        Write-Host "    networkingMode=mirrored" -ForegroundColor DarkGray
        Write-Host "  Then: wsl --shutdown && wsl -d $($global:wslDistroName)" -ForegroundColor Yellow
    }

    Write-Host ""
    Write-Host "Access URLs:" -ForegroundColor White
    Write-Host "  Control UI:  http://127.0.0.1:$($global:controlUiPort)/" -ForegroundColor Cyan

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

#==============================================================================
# Function: Show-OpenClawMenu
#==============================================================================
<#
.SYNOPSIS
    Shows the OpenClaw application menu.
.DESCRIPTION
    Provides options for install, service management, update, uninstall, logs, backup, and restore.
    Displays current service task state and admin elevation status.
.OUTPUTS
    [void]
#>
function Show-OpenClawMenu {
    [CmdletBinding()]
    param()

    $taskName = $global:openclawServiceTaskName
    $state = "Not Installed"
    $lastResult = ""
    $triggerMode = ""
    try {
        $t = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
        if ($t) {
            $info = Get-ScheduledTaskInfo -TaskName $taskName -ErrorAction SilentlyContinue
            if ($info) {
                if ($info.State) { $state = [string]$info.State } else { $state = "Installed" }
                if ($null -ne $info.LastTaskResult) { $lastResult = [string]$info.LastTaskResult }
            }
            else {
                $state = "Installed"
            }
            $trigger = $t.Triggers | Select-Object -First 1
            if ($trigger -is [Microsoft.Management.Infrastructure.CimInstance]) {
                $cimClass = $trigger.CimClass.CimClassName
                if ($cimClass -eq "MSFT_TaskBootTrigger") {
                    $triggerMode = "At Boot (pre-login)"
                }
                elseif ($cimClass -eq "MSFT_TaskLogonTrigger") {
                    $triggerMode = "At Logon (post-login)"
                }
                else {
                    $triggerMode = $cimClass
                }
            }
        }
    }
    catch {
        $state = "Unknown"
    }

    $isAdmin = Test-IsAdministrator
    $adminTag = if ($isAdmin) { " [Admin]" } else { "" }

    Write-Host "===========================================" -ForegroundColor Yellow
    Write-Host "OpenClaw (WSL)$adminTag" -ForegroundColor White
    Write-Host "===========================================" -ForegroundColor Yellow
    Write-Host "Service Task: $taskName" -ForegroundColor DarkGray
    Write-Host "Service State: $state" -ForegroundColor DarkGray
    if (-not [string]::IsNullOrWhiteSpace($triggerMode)) {
        Write-Host "Service Start: $triggerMode" -ForegroundColor DarkGray
    }
    if (-not [string]::IsNullOrWhiteSpace($lastResult)) {
        Write-Host "Last Task Result: $lastResult" -ForegroundColor DarkGray
    }
    Write-Host "-------------------------------------------" -ForegroundColor Yellow
    Write-Host "1. Show Status and Test Connection" -ForegroundColor Cyan
    Write-Host "2. Install App" -ForegroundColor Cyan
    Write-Host "3. Install Service" -ForegroundColor Cyan
    Write-Host "4. Start Service" -ForegroundColor Cyan
    Write-Host "5. Stop Service" -ForegroundColor Cyan
    Write-Host "6. Uninstall Service" -ForegroundColor Cyan
    Write-Host "7. Update App" -ForegroundColor Cyan
    Write-Host "8. Uninstall App" -ForegroundColor Cyan
    Write-Host "9. Show Logs" -ForegroundColor Cyan
    Write-Host "A. Backup Data (personality, memory, config)" -ForegroundColor Cyan
    Write-Host "B. Restore Data (personality, memory, config)" -ForegroundColor Cyan
    Write-Host "0. Exit" -ForegroundColor Cyan
    Write-Host "-------------------------------------------" -ForegroundColor Yellow
}

#==============================================================================
# Main
#==============================================================================

$choice = ""
do {
    Show-OpenClawMenu
    $choice = Read-Host "Enter your choice"
    if ([string]::IsNullOrWhiteSpace($choice)) { continue }

    switch ($choice.ToUpper()) {
        "1" {
            Show-OpenClawStatus
        }
        "2" {
            Install-OpenClaw
        }
        "3" {
            Install-OpenClawService
        }
        "4" {
            Start-OpenClawService
        }
        "5" {
            Stop-OpenClawService
        }
        "6" {
            Uninstall-OpenClawService
        }
        "7" {
            Update-OpenClaw
        }
        "8" {
            Uninstall-OpenClaw
        }
        "9" {
            Show-OpenClawLogs
        }
        "A" {
            Backup-OpenClawData
        }
        "B" {
            Restore-OpenClawData
        }
        "0" { return }
        default {
            Write-Warning "Invalid selection."
        }
    }
} while ($choice -ne "0")
