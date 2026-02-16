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
$global:workspacePath = "~/.openclaw/workspace"

# Windows Scheduled Task "service" to auto-start WSL distro.
# WSL distros are per-user (HKCU), so SYSTEM/NetworkService cannot see them.
# When elevated: AtStartup + S4U logon starts before user login.
# When non-elevated: AtLogOn + Interactive logon starts at user login.
$global:openclawServiceTaskName = "OpenClaw-WSL-Boot"
$global:openclawServiceWrapperPath = Join-Path $global:installRoot "service-wrapper.ps1"
$global:openclawServiceLogPath = Join-Path $global:installRoot "service.log"

# UI password configuration (auto-generated alphanumeric password for remote access)
$global:uiPasswordLength = 16
$global:uiPasswordFile = Join-Path $global:installRoot "ui-password.txt"

# External URL for reverse proxy access (set during install, stored in config)
$global:externalUrlFile = Join-Path $global:installRoot "external-url.txt"



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
# Function: Get-OpenClawUIPassword
#==============================================================================
<#
.SYNOPSIS
    Reads the saved UI password from the local file.
.DESCRIPTION
    Returns the UI password previously generated and stored on the Windows side.
    Returns an empty string if no password file exists.
.OUTPUTS
    [string] The saved UI password, or empty string if not found.
#>
function Get-OpenClawUIPassword {
    [CmdletBinding()]
    [OutputType([string])]
    param()

    if (Test-Path -LiteralPath $global:uiPasswordFile) {
        return (Get-Content -LiteralPath $global:uiPasswordFile -Raw).Trim()
    }
    return ""
}

#==============================================================================
# Function: Set-OpenClawUIPassword
#==============================================================================
<#
.SYNOPSIS
    Generates and sets a UI password for OpenClaw remote access.
.DESCRIPTION
    Generates a random alphanumeric password (lowercase + uppercase + digits),
    saves it to a local file on the Windows side, and injects it into the
    OpenClaw gateway configuration (openclaw.json) via jq. The password
    provides an easier-to-type alternative to the long access token when
    connecting from remote machines.
.OUTPUTS
    [string] The generated password, or empty string on failure.
#>
function Set-OpenClawUIPassword {
    [CmdletBinding(SupportsShouldProcess = $true)]
    [OutputType([string])]
    param()

    if (-not $PSCmdlet.ShouldProcess("OpenClaw", "Generate and set UI password")) {
        return ""
    }

    $password = New-RandomPassword -Length $global:uiPasswordLength

    Write-Host "Setting UI password for remote access..." -ForegroundColor Cyan

    New-Item -ItemType Directory -Path (Split-Path $global:uiPasswordFile -Parent) -Force | Out-Null
    Set-Content -LiteralPath $global:uiPasswordFile -Value $password -Encoding UTF8
    Write-Host "UI password saved to: $($global:uiPasswordFile)" -ForegroundColor DarkGray

    Write-Host ""
    Write-Host "Gateway auth mode:" -ForegroundColor Cyan
    Write-Host "  1. token    - authenticate with the dashboard token URL (recommended for local)" -ForegroundColor White
    Write-Host "  2. password - authenticate with the UI password (required for remote/reverse proxy)" -ForegroundColor White
    Write-Host ""
    $authChoice = Read-Host "Select auth mode (1/2, default: 1)"
    $authMode = "token"
    if ($authChoice.Trim() -eq "2") {
        $authMode = "password"
    }

    $jqAvailable = Invoke-WSLCommand -DistroName $global:wslDistroName -Command "command -v jq >/dev/null 2>&1 && echo 'yes' || echo 'no'"
    if ($jqAvailable -match "yes") {
        $configFile = "$($global:openclawConfigPath)/openclaw.json"
        $jqCmd = "if [ -f $configFile ]; then cat $configFile | jq '.gateway.auth.password = `"$password`" | .gateway.auth.mode = `"$authMode`"' > $configFile.tmp && mv $configFile.tmp $configFile; fi"
        Invoke-WSLCommand -DistroName $global:wslDistroName -Command $jqCmd -Sensitive

        if ($LASTEXITCODE -eq 0) {
            Write-Host "UI password injected into OpenClaw gateway config (auth mode: $authMode)." -ForegroundColor Green
        }
        else {
            Write-Warning "Failed to inject password into config. Password saved locally only."
        }
    }
    else {
        Write-Warning "jq not available. Password saved locally but not injected into OpenClaw config."
        Write-Host "Manually set gateway.auth.password and gateway.auth.mode in ~/.openclaw/openclaw.json" -ForegroundColor Yellow
    }

    return $password
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

    Write-Host "Generating UI password for remote access..." -ForegroundColor Cyan
    $uiPassword = Set-OpenClawUIPassword
    if (-not [string]::IsNullOrEmpty($uiPassword)) {
        Write-Host ""
        Write-Host "UI Password (for remote access): $uiPassword" -ForegroundColor White
        Write-Host "This password is saved at: $($global:uiPasswordFile)" -ForegroundColor DarkGray
        Write-Host ""
    }

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

    Write-Host "Launching WSL keep-alive process..." -ForegroundColor Cyan
    Start-Process -FilePath "wsl.exe" -ArgumentList "-d $($global:wslDistroName) -- sleep infinity" -WindowStyle Hidden
    Start-Sleep -Seconds 2

    Invoke-WSLCommand -DistroName $global:wslDistroName -Command "systemctl --user start $($global:openclawServiceName)"

    Start-Sleep -Seconds 3

    $status = Invoke-WSLCommand -DistroName $global:wslDistroName -Command "systemctl --user is-active $($global:openclawServiceName)"
    $statusText = ($status -join " ").Trim()

    if ($statusText -eq "active") {
        Write-Host "OpenClaw service started successfully." -ForegroundColor Green
        Write-Host "Control UI: http://127.0.0.1:$($global:controlUiPort)/" -ForegroundColor Cyan
        Write-Host "Canvas:     http://127.0.0.1:$($global:canvasPort)/" -ForegroundColor Cyan
        Write-Host ""

        Write-Host "Dashboard Access:" -ForegroundColor Cyan
        $dashboardOutput = Invoke-WSLCommand -DistroName $global:wslDistroName -Command "openclaw dashboard --no-open 2>&1"
        $dashboardLines = ($dashboardOutput -join "`n").Trim()
        $tokenUrl = ""
        $gatewayToken = ""
        foreach ($line in $dashboardLines -split "`n") {
            if ($line -match "(https?://\S+)") {
                $tokenUrl = $Matches[1]
                if ($tokenUrl -match "[?&]token=([^&\s]+)") {
                    $gatewayToken = $Matches[1]
                }
                break
            }
        }

        if (-not [string]::IsNullOrEmpty($tokenUrl)) {
            Write-Host "  $tokenUrl" -ForegroundColor White
        }
        else {
            Write-Host "  $dashboardLines" -ForegroundColor White
        }

        Write-Host ""

        $savedPassword = Get-OpenClawUIPassword
        if ([string]::IsNullOrEmpty($savedPassword)) {
            Write-Host "Generating UI password for remote access..." -ForegroundColor Cyan
            $savedPassword = Set-OpenClawUIPassword
        }

        Write-Host "Authentication:" -ForegroundColor Cyan
        if (-not [string]::IsNullOrEmpty($gatewayToken)) {
            Write-Host "  Gateway Token: $gatewayToken" -ForegroundColor White
        }
        if (-not [string]::IsNullOrEmpty($savedPassword)) {
            Write-Host "  UI Password:   $savedPassword" -ForegroundColor White
            Write-Host "  Saved at:      $($global:uiPasswordFile)" -ForegroundColor DarkGray
        }

        Write-Host ""
        Write-Host "Gateway auth mode:" -ForegroundColor Cyan
        Write-Host "  1. token    - use the dashboard token URL (local access)" -ForegroundColor White
        Write-Host "  2. password - use the UI password (required for remote/reverse proxy)" -ForegroundColor White
        Write-Host ""
        $authChoice = Read-Host "Select auth mode (1/2, default: 1)"
        $newAuthMode = "token"
        if ($authChoice.Trim() -eq "2") {
            $newAuthMode = "password"
        }

        $currentMode = Invoke-WSLCommand -DistroName $global:wslDistroName -Command "cat $($global:openclawConfigPath)/openclaw.json 2>/dev/null | jq -r '.gateway.auth.mode // empty'"
        $currentModeText = ($currentMode -join "").Trim()

        $configChanged = $false
        if ($currentModeText -ne $newAuthMode) {
            $jqCmd = "cat $($global:openclawConfigPath)/openclaw.json | jq '.gateway.auth.mode = `"$newAuthMode`"' > $($global:openclawConfigPath)/openclaw.json.tmp && mv $($global:openclawConfigPath)/openclaw.json.tmp $($global:openclawConfigPath)/openclaw.json"
            Invoke-WSLCommand -DistroName $global:wslDistroName -Command $jqCmd -Sensitive
            $configChanged = $true
            Write-Host "Auth mode set to '$newAuthMode'." -ForegroundColor Green
        }
        else {
            Write-Host "Auth mode already set to '$newAuthMode'." -ForegroundColor DarkGray
        }

        if ($newAuthMode -eq "password") {
            $savedExternalUrl = ""
            if (Test-Path $global:externalUrlFile) {
                $savedExternalUrl = (Get-Content -LiteralPath $global:externalUrlFile -Raw).Trim()
            }

            Write-Host ""
            Write-Host "External URL for reverse proxy access:" -ForegroundColor Cyan
            if (-not [string]::IsNullOrEmpty($savedExternalUrl)) {
                Write-Host "  Current: $savedExternalUrl" -ForegroundColor DarkGray
            }
            $externalUrlInput = Read-Host "External URL (e.g. https://evo.jocys.com, Enter to keep current, 'none' to clear)"
            $externalUrlInput = $externalUrlInput.Trim().TrimEnd("/")

            if ($externalUrlInput -eq "none") {
                $savedExternalUrl = ""
                if (Test-Path $global:externalUrlFile) {
                    Remove-Item -LiteralPath $global:externalUrlFile -Force
                }
                $jqCmd = "cat $($global:openclawConfigPath)/openclaw.json | jq 'del(.gateway.controlUi.allowedOrigins)' > $($global:openclawConfigPath)/openclaw.json.tmp && mv $($global:openclawConfigPath)/openclaw.json.tmp $($global:openclawConfigPath)/openclaw.json"
                Invoke-WSLCommand -DistroName $global:wslDistroName -Command $jqCmd
                $configChanged = $true
                Write-Host "allowedOrigins cleared." -ForegroundColor Yellow
            }
            elseif (-not [string]::IsNullOrEmpty($externalUrlInput)) {
                $savedExternalUrl = $externalUrlInput
                New-Item -ItemType Directory -Path (Split-Path $global:externalUrlFile -Parent) -Force | Out-Null
                Set-Content -LiteralPath $global:externalUrlFile -Value $savedExternalUrl -Encoding UTF8
            }

            if (-not [string]::IsNullOrEmpty($savedExternalUrl)) {
                $jqCmd = "cat $($global:openclawConfigPath)/openclaw.json | jq '.gateway.controlUi.allowedOrigins = [`"$savedExternalUrl`"] | .gateway.controlUi.dangerouslyDisableDeviceAuth = true | .gateway.trustedProxies = [`"127.0.0.1`"]' > $($global:openclawConfigPath)/openclaw.json.tmp && mv $($global:openclawConfigPath)/openclaw.json.tmp $($global:openclawConfigPath)/openclaw.json"
                Invoke-WSLCommand -DistroName $global:wslDistroName -Command $jqCmd
                $configChanged = $true
                Write-Host "Reverse proxy config applied (allowedOrigins, trustedProxies, disableDeviceAuth)." -ForegroundColor Green
            }
        }

        if ($configChanged) {
            Write-Host "Restarting gateway..." -ForegroundColor Cyan
            Invoke-WSLCommand -DistroName $global:wslDistroName -Command "systemctl --user restart $($global:openclawServiceName)"
            Start-Sleep -Seconds 2
            Write-Host "Gateway restarted." -ForegroundColor Green
        }

        Write-Host ""
        if ($newAuthMode -eq "token") {
            Write-Host "Open the dashboard URL above in your browser to access the Control UI." -ForegroundColor Yellow
        }
        else {
            Write-Host "Open the Control UI, go to Settings, and enter the UI Password to connect." -ForegroundColor Yellow
        }

        Write-Host ""
        Write-Host "Docs:" -ForegroundColor White
        Write-Host "  https://docs.openclaw.ai/gateway/remote" -ForegroundColor DarkGray
        Write-Host "  https://docs.openclaw.ai/web/control-ui" -ForegroundColor DarkGray
        Write-Host "  https://docs.openclaw.ai/web/dashboard" -ForegroundColor DarkGray
    }
    else {
        Write-Warning "Service may not have started correctly. Status: $statusText"
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

    Write-Host "Stopping WSL keep-alive processes..." -ForegroundColor Cyan
    Invoke-WSLCommand -DistroName $global:wslDistroName -Command "pkill -f 'sleep infinity' 2>/dev/null || true"

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
`$p = Start-Process -FilePath "wsl.exe" -ArgumentList "-d $distroName -- sleep infinity" -WindowStyle Hidden -PassThru -Wait
Write-ServiceLog "WSL keep-alive process exited. PID=`$(`$p.Id) ExitCode=`$(`$p.ExitCode)"
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
    Write-Host "Step 4: External URL for reverse proxy..." -ForegroundColor White
    Write-Host "If you access this server via a reverse proxy (e.g. IIS, nginx)," -ForegroundColor Cyan
    Write-Host "enter the external URL so the Control UI accepts WebSocket connections from it." -ForegroundColor Cyan
    Write-Host ""
    $externalUrl = Read-Host "External URL (e.g. https://evo.jocys.com, or press Enter to skip)"
    $externalUrl = $externalUrl.Trim().TrimEnd("/")
    if (-not [string]::IsNullOrEmpty($externalUrl)) {
        New-Item -ItemType Directory -Path (Split-Path $global:externalUrlFile -Parent) -Force | Out-Null
        Set-Content -LiteralPath $global:externalUrlFile -Value $externalUrl -Encoding UTF8
        Write-Host "External URL saved to: $($global:externalUrlFile)" -ForegroundColor DarkGray

        $jqAvailable = Invoke-WSLCommand -DistroName $global:wslDistroName -Command "command -v jq >/dev/null 2>&1 && echo 'yes' || echo 'no'"
        if ($jqAvailable -match "yes") {
            $configFile = "$($global:openclawConfigPath)/openclaw.json"
            $jqCmd = "if [ -f $configFile ]; then cat $configFile | jq '.gateway.controlUi.allowedOrigins = [`"$externalUrl`"] | .gateway.controlUi.dangerouslyDisableDeviceAuth = true | .gateway.trustedProxies = [`"127.0.0.1`"]' > $configFile.tmp && mv $configFile.tmp $configFile; fi"
            Invoke-WSLCommand -DistroName $global:wslDistroName -Command $jqCmd
            if ($LASTEXITCODE -eq 0) {
                Write-Host "Reverse proxy config applied (allowedOrigins, trustedProxies, disableDeviceAuth)." -ForegroundColor Green
            }
        }
    }
    else {
        Write-Host "Skipped. You can set gateway.controlUi.allowedOrigins later if needed." -ForegroundColor DarkGray
    }

    Write-Host ""
    # WSL2 shuts down idle VMs after vmIdleTimeout ms (default 60000 = 60s).
    # Setting vmIdleTimeout=-1 disables the idle shutdown so the OpenClaw
    # gateway service stays running 24/7 even when no terminal is attached.
    # Ref: https://learn.microsoft.com/en-us/windows/wsl/wsl-config#main-wsl-settings
    Write-Host ""
    Write-Host "Step 5: Ensuring WSL stays running (vmIdleTimeout=-1)..." -ForegroundColor White
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

    $gatewayToken = ""
    if ($serviceStatus -eq "active") {
        $dashboardOutput = Invoke-WSLCommand -DistroName $global:wslDistroName -Command "openclaw dashboard --no-open 2>&1"
        $dashboardLines = ($dashboardOutput -join "`n").Trim()
        $tokenUrl = ""
        foreach ($line in $dashboardLines -split "`n") {
            if ($line -match "(https?://\S+)") {
                $tokenUrl = $Matches[1]
                if ($tokenUrl -match "[?&]token=([^&\s]+)") {
                    $gatewayToken = $Matches[1]
                }
                break
            }
        }
        if (-not [string]::IsNullOrEmpty($tokenUrl)) {
            Write-Host "  Dashboard (token link): $tokenUrl" -ForegroundColor Cyan
        }
    }

    $savedPassword = Get-OpenClawUIPassword

    Write-Host ""
    Write-Host "Authentication:" -ForegroundColor White
    if (-not [string]::IsNullOrEmpty($gatewayToken)) {
        Write-Host "  Gateway Token: $gatewayToken" -ForegroundColor Cyan
    }
    if (-not [string]::IsNullOrEmpty($savedPassword)) {
        Write-Host "  UI Password:   $savedPassword" -ForegroundColor Cyan
        Write-Host "  Saved at:      $($global:uiPasswordFile)" -ForegroundColor DarkGray
    }
    if ([string]::IsNullOrEmpty($gatewayToken) -and [string]::IsNullOrEmpty($savedPassword)) {
        Write-Host "  (No credentials available. Start the service to generate.)" -ForegroundColor DarkGray
    }

    Write-Host ""
    Write-Host "Docs:" -ForegroundColor White
    Write-Host "  https://docs.openclaw.ai/gateway/remote" -ForegroundColor DarkGray
    Write-Host "  https://docs.openclaw.ai/web/control-ui" -ForegroundColor DarkGray
    Write-Host "  https://docs.openclaw.ai/web/dashboard" -ForegroundColor DarkGray

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
# Function: Backup-OpenClawArchive
#==============================================================================
<#
.SYNOPSIS
    Creates a tar.gz backup archive from specified WSL directories.
.DESCRIPTION
    Checks which of the specified directories exist in the WSL distro home,
    creates a tar.gz archive, and copies it to the Windows backup folder.
    Shared helper used by Backup-OpenClawPersonality and Backup-OpenClawSystem.
.PARAMETER FilePrefix
    Prefix for the backup filename (e.g., 'openclaw-personality', 'openclaw-system').
.PARAMETER WslDirectories
    Array of directory names relative to ~ to include in the backup.
.PARAMETER ExcludePatterns
    Optional array of tar --exclude patterns (e.g., '.openclaw/workspace').
.PARAMETER DisplayName
    Human-readable name for display messages (e.g., 'Personality', 'System').
.PARAMETER DisplayItems
    Array of strings describing what will be backed up, shown to the user.
.OUTPUTS
    [void]
#>
function Backup-OpenClawArchive {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePrefix,

        [Parameter(Mandatory = $true)]
        [string[]]$WslDirectories,

        [Parameter(Mandatory = $false)]
        [string[]]$ExcludePatterns = @(),

        [Parameter(Mandatory = $true)]
        [string]$DisplayName,

        [Parameter(Mandatory = $true)]
        [string[]]$DisplayItems
    )

    if (-not $PSCmdlet.ShouldProcess("OpenClaw", "Backup $DisplayName")) {
        return
    }

    Write-Host ""
    Write-Host "===========================================" -ForegroundColor Yellow
    Write-Host "OpenClaw $DisplayName Backup" -ForegroundColor White
    Write-Host "===========================================" -ForegroundColor Yellow
    Write-Host ""

    if (-not (Test-WSLDistroExists -DistroName $global:wslDistroName)) {
        Write-Error "WSL distro '$($global:wslDistroName)' not found."
        return
    }

    Write-Host "This will back up:" -ForegroundColor Cyan
    foreach ($item in $DisplayItems) {
        Write-Host "  - $item" -ForegroundColor DarkGray
    }
    Write-Host ""

    $backupDir = Join-Path $global:backupFolder $global:openclawBackupSubfolder
    if (-not (Test-Path $backupDir)) {
        New-Item -ItemType Directory -Force -Path $backupDir | Out-Null
        Write-Host "Created backup folder: $backupDir" -ForegroundColor DarkGray
    }

    $timestamp = Get-Date -Format "yyyyMMdd-HHmm"
    $backupFileName = "$FilePrefix-$timestamp.tar.gz"
    $backupFilePath = Join-Path $backupDir $backupFileName

    Write-Host "Checking available data directories..." -ForegroundColor Cyan

    $dirsTrimmed = ""
    foreach ($dir in $WslDirectories) {
        $exists = Invoke-WSLCommand -DistroName $global:wslDistroName -Command "test -d ~/$dir && echo 'yes' || echo 'no'"
        if ($exists -match "yes") {
            $dirsTrimmed += " $dir"
        }
    }
    $dirsTrimmed = $dirsTrimmed.Trim()

    if ([string]::IsNullOrWhiteSpace($dirsTrimmed)) {
        Write-Warning "No data directories found for $DisplayName backup."
        Write-Host "Nothing to back up." -ForegroundColor Yellow
        return
    }

    Write-Host "Found directories to back up: $dirsTrimmed" -ForegroundColor DarkGray

    Write-Host "Creating backup archive..." -ForegroundColor Cyan
    $wslTarPath = "/tmp/$backupFileName"
    $excludeArgs = ""
    foreach ($pattern in $ExcludePatterns) {
        $excludeArgs += " --exclude='$pattern'"
    }
    Invoke-WSLCommand -DistroName $global:wslDistroName -Command "cd ~ && tar -czf $wslTarPath$excludeArgs $dirsTrimmed"

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
    Write-Host "$DisplayName Backup Complete!" -ForegroundColor Green
    Write-Host "===========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "File: $backupFilePath" -ForegroundColor Cyan
    Write-Host "Size: $fileSizeKB KB" -ForegroundColor Cyan
    Write-Host ""

    Write-Host "Archive contents:" -ForegroundColor White
    $wslListPath = "/tmp/$FilePrefix-list-$timestamp.tar.gz"
    $wslUncListPath = "\\wsl`$\$($global:wslDistroName)\tmp\$FilePrefix-list-$timestamp.tar.gz"
    try {
        Copy-Item -Path $backupFilePath -Destination $wslUncListPath -Force -ErrorAction SilentlyContinue
        Invoke-WSLCommand -DistroName $global:wslDistroName -Command "tar -tzf $wslListPath 2>/dev/null | head -30"
        Invoke-WSLCommand -DistroName $global:wslDistroName -Command "rm -f $wslListPath"
    }
    catch {
        Write-Host "  (unable to list contents)" -ForegroundColor DarkGray
    }
    Write-Host ""
}

#==============================================================================
# Function: Restore-OpenClawArchive
#==============================================================================
<#
.SYNOPSIS
    Restores an OpenClaw backup archive by listing available files and prompting the user.
.DESCRIPTION
    Lists available backup archives matching the specified file prefix,
    prompts the user to select one, and extracts it into the WSL distro home directory.
    Automatically stops the OpenClaw service before restoring to prevent the running
    process from overwriting restored files during its own save cycle. Restarts the
    service afterwards if it was previously running.
    Shared helper used by Restore-OpenClawPersonality and Restore-OpenClawSystem.
.PARAMETER FilePrefix
    Prefix for matching backup filenames (e.g., 'openclaw-personality', 'openclaw-system').
.PARAMETER DisplayName
    Human-readable name for display messages (e.g., 'Personality', 'System').
.OUTPUTS
    [void]
#>
function Restore-OpenClawArchive {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePrefix,

        [Parameter(Mandatory = $true)]
        [string]$DisplayName
    )

    if (-not $PSCmdlet.ShouldProcess("OpenClaw", "Restore $DisplayName")) {
        return
    }

    Write-Host ""
    Write-Host "===========================================" -ForegroundColor Yellow
    Write-Host "OpenClaw $DisplayName Restore" -ForegroundColor White
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

    $backupFiles = Get-ChildItem -Path $backupDir -Filter "$FilePrefix-*.tar.gz" | Sort-Object LastWriteTime -Descending
    if (-not $backupFiles) {
        Write-Warning "No $DisplayName backup files found in '$backupDir'."
        return
    }

    Write-Host "Available $DisplayName backups:" -ForegroundColor Cyan
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

    Write-Host "WARNING: This will overwrite existing $DisplayName files." -ForegroundColor Yellow
    $confirm = Read-Host "Proceed with restore? (Y/N)"
    if ($confirm -ne "Y") {
        Write-Host "Restore cancelled." -ForegroundColor Yellow
        return
    }

    $wasRunning = (Get-OpenClawServiceStatus) -eq "active"
    if ($wasRunning) {
        Write-Host ""
        Write-Host "Stopping OpenClaw service to prevent file overwrites during restore..." -ForegroundColor Yellow
        Stop-OpenClawService
        Start-Sleep -Seconds 2
    }

    Write-Host ""
    Write-Host "Restoring $DisplayName files..." -ForegroundColor Cyan
    Invoke-WSLCommand -DistroName $global:wslDistroName -Command "cd ~ && tar -xzf '$wslMntPath'"

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to extract backup archive."
        if ($wasRunning) {
            Write-Host "Restarting OpenClaw service..." -ForegroundColor Yellow
            Start-OpenClawService
        }
        return
    }

    Write-Host ""
    Write-Host "===========================================" -ForegroundColor Green
    Write-Host "$DisplayName Restore Complete!" -ForegroundColor Green
    Write-Host "===========================================" -ForegroundColor Green
    Write-Host ""

    Write-Host "Restored files:" -ForegroundColor Cyan
    $archiveEntries = Invoke-WSLCommand -DistroName $global:wslDistroName -Command "tar -tzf '$wslMntPath' 2>/dev/null"
    foreach ($entry in $archiveEntries) {
        $entryTrimmed = ($entry -join "").Trim()
        if ([string]::IsNullOrWhiteSpace($entryTrimmed)) { continue }
        $testFlag = if ($entryTrimmed.EndsWith("/")) { "-d" } else { "-f" }
        $exists = Invoke-WSLCommand -DistroName $global:wslDistroName -Command "test $testFlag ~/$entryTrimmed && echo 'yes' || echo 'no'"
        $ok = $exists -match 'yes'
        Write-Host "  ${entryTrimmed}: $(if ($ok) { 'OK' } else { 'MISSING' })" -ForegroundColor $(if ($ok) { "Green" } else { "Red" })
    }

    if ($wasRunning) {
        Write-Host ""
        Write-Host "Restarting OpenClaw service..." -ForegroundColor Yellow
        Start-OpenClawService
    }
    else {
        Write-Host ""
        Write-Host "Note: Start the OpenClaw service for restored files to take effect." -ForegroundColor Yellow
    }
    Write-Host ""
}

#==============================================================================
# Function: Backup-OpenClawPersonality
#==============================================================================
<#
.SYNOPSIS
    Backs up OpenClaw personality files (~/workspace/).
.DESCRIPTION
    Creates a timestamped tar.gz archive of the workspace directory containing
    SOUL.md, USER.md, IDENTITY.md, MEMORY.md, HEARTBEAT.md, TOOLS.md,
    OPTIMIZATION.md, skills/, tools/, and memory/ subdirectories.
    Analogous to backing up a container volume (data) separately from its image.
.OUTPUTS
    [void]
#>
function Backup-OpenClawPersonality {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param()

    $displayItems = @(
        "Personality:    ~/.openclaw/workspace/SOUL.md, IDENTITY.md"
        "User Context:   ~/.openclaw/workspace/USER.md"
        "Memory:         ~/.openclaw/workspace/MEMORY.md, memory/*.md"
        "Bot Skills:     ~/.openclaw/workspace/skills/"
        "Bot Tools:      ~/.openclaw/workspace/tools/"
        "Heartbeat:      ~/.openclaw/workspace/HEARTBEAT.md"
        "Optimization:   ~/.openclaw/workspace/OPTIMIZATION.md, TOOLS.md"
    )

    Backup-OpenClawArchive `
        -FilePrefix "openclaw-personality" `
        -WslDirectories @(".openclaw/workspace") `
        -DisplayName "Personality" `
        -DisplayItems $displayItems
}

#==============================================================================
# Function: Restore-OpenClawPersonality
#==============================================================================
<#
.SYNOPSIS
    Restores OpenClaw personality files from a backup archive.
.DESCRIPTION
    Lists available personality backup archives and prompts the user to select one.
    Extracts the selected archive back into ~/workspace/ in the WSL distro,
    restoring all personality, memory, skills, and tools files.
    The system configuration (~/.openclaw/) is NOT affected.
.OUTPUTS
    [void]
#>
function Restore-OpenClawPersonality {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param()

    Restore-OpenClawArchive `
        -FilePrefix "openclaw-personality" `
        -DisplayName "Personality"
}

#==============================================================================
# Function: Backup-OpenClawSystem
#==============================================================================
<#
.SYNOPSIS
    Backs up OpenClaw system configuration (~/.openclaw/).
.DESCRIPTION
    Creates a timestamped tar.gz archive of the ~/.openclaw directory containing
    openclaw.json, API keys, gateway configuration, and service definitions.
    Analogous to backing up a container image separately from its volume data.
.OUTPUTS
    [void]
#>
function Backup-OpenClawSystem {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param()

    $displayItems = @(
        "Configuration:  ~/.openclaw/openclaw.json"
        "API Keys:       ~/.openclaw/ (stored credentials)"
        "Gateway Config: ~/.openclaw/ (port bindings, daemon settings)"
    )

    Backup-OpenClawArchive `
        -FilePrefix "openclaw-system" `
        -WslDirectories @(".openclaw") `
        -ExcludePatterns @(".openclaw/workspace") `
        -DisplayName "System" `
        -DisplayItems $displayItems
}

#==============================================================================
# Function: Restore-OpenClawSystem
#==============================================================================
<#
.SYNOPSIS
    Restores OpenClaw system configuration from a backup archive.
.DESCRIPTION
    Lists available system backup archives and prompts the user to select one.
    Extracts the selected archive back into ~/.openclaw/ in the WSL distro,
    restoring gateway configuration, API keys, and service definitions.
    The personality files (~/workspace/) are NOT affected.
.OUTPUTS
    [void]
#>
function Restore-OpenClawSystem {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param()

    Restore-OpenClawArchive `
        -FilePrefix "openclaw-system" `
        -DisplayName "System"
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
    Write-Host "A. Backup Personality (.openclaw/workspace: SOUL.md, memory, skills)" -ForegroundColor Cyan
    Write-Host "B. Restore Personality" -ForegroundColor Cyan
    Write-Host "C. Backup System (.openclaw config, excluding workspace)" -ForegroundColor Cyan
    Write-Host "D. Restore System" -ForegroundColor Cyan
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
            Backup-OpenClawPersonality
        }
        "B" {
            Restore-OpenClawPersonality
        }
        "C" {
            Backup-OpenClawSystem
        }
        "D" {
            Restore-OpenClawSystem
        }
        "0" { return }
        default {
            Write-Warning "Invalid selection."
        }
    }
} while ($choice -ne "0")
