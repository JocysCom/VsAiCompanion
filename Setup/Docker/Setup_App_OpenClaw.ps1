################################################################################
# File         : Setup_App_OpenClaw.ps1
# Description  : Installs and manages OpenClaw AI agent platform in a dedicated
#                WSL2 distro. OpenClaw provides multi-channel AI communication
#                (WhatsApp, Telegram, Discord, etc.) with optional sandbox support.
# Usage        : Run as Administrator for WSL distro management.
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
$global:wslDistroName = "OpenClaw"
$global:wslDistroBaseImage = "Ubuntu-24.04"
$global:settingsVersion = 1

# Network ports
$global:controlUiPort = 18789
$global:canvasPort = 18793

# Paths
$global:programDataRoot = [Environment]::GetFolderPath([Environment+SpecialFolder]::CommonApplicationData)
$global:installRoot = Join-Path $global:programDataRoot $global:appName
$global:settingsPath = Join-Path $global:installRoot "settings.json"
$global:wslExportPath = Join-Path $global:installRoot "distro-backup"

# OpenClaw installation paths inside WSL
$global:openclawConfigPath = "~/.openclaw"
$global:openclawServiceName = "openclaw"

# Node.js minimum version
$global:nodeMinVersion = 22

# Default settings
$global:defaultSettings = @{
    SandboxMode = "none"
    AutoStart   = $false
}

#==============================================================================
# Function: New-Directory
#==============================================================================
<#
.SYNOPSIS
    Creates a directory if it does not exist.
.DESCRIPTION
    Ensures the specified directory exists, creating it (and parents) if needed.
.PARAMETER Path
    Directory path to ensure exists.
.OUTPUTS
    [void]
#>
function New-Directory {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        if ($PSCmdlet.ShouldProcess($Path, "Create directory")) {
            New-Item -ItemType Directory -Path $Path -Force | Out-Null
            Write-Host "Created directory: $Path" -ForegroundColor DarkGray
        }
    }
}

#==============================================================================
# Function: Get-OpenClawSetting
#==============================================================================
<#
.SYNOPSIS
    Loads persisted OpenClaw settings.
.DESCRIPTION
    Reads settings from $global:settingsPath if present. Returns defaults when missing/invalid.
.OUTPUTS
    [pscustomobject]
#>
function Get-OpenClawSetting {
    [CmdletBinding()]
    [OutputType([pscustomobject])]
    param()

    if (Test-Path -LiteralPath $global:settingsPath) {
        try {
            $content = Get-Content -LiteralPath $global:settingsPath -Raw -Encoding UTF8
            $settings = $content | ConvertFrom-Json
            if ($null -ne $settings) {
                return [PSCustomObject]@{
                    Version     = if ($null -ne $settings.Version) { $settings.Version } else { $global:settingsVersion }
                    SandboxMode = if ($settings.SandboxMode) { $settings.SandboxMode } else { $global:defaultSettings.SandboxMode }
                    AutoStart   = if ($null -ne $settings.AutoStart) { $settings.AutoStart } else { $global:defaultSettings.AutoStart }
                }
            }
        }
        catch {
            Write-Warning "Failed to load settings from '$($global:settingsPath)'. Using defaults. Details: $_"
        }
    }

    return [PSCustomObject]@{
        Version     = $global:settingsVersion
        SandboxMode = $global:defaultSettings.SandboxMode
        AutoStart   = $global:defaultSettings.AutoStart
    }
}

#==============================================================================
# Function: Set-OpenClawSetting
#==============================================================================
<#
.SYNOPSIS
    Saves persisted OpenClaw settings.
.DESCRIPTION
    Writes a small JSON file to $global:settingsPath.
.PARAMETER SandboxMode
    Sandbox mode for agent tools (none, docker, podman).
.PARAMETER AutoStart
    Whether to auto-start OpenClaw service on WSL startup.
.OUTPUTS
    [void]
#>
function Set-OpenClawSetting {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory = $false)]
        [ValidateSet("none", "docker", "podman")]
        [string]$SandboxMode = "none",

        [Parameter(Mandatory = $false)]
        [bool]$AutoStart = $false
    )

    New-Directory -Path $global:installRoot

    $settings = [PSCustomObject]@{
        Version     = $global:settingsVersion
        UpdatedUtc  = (Get-Date).ToUniversalTime().ToString("o")
        InstallRoot = $global:installRoot
        SandboxMode = $SandboxMode
        AutoStart   = $AutoStart
    }

    if ($PSCmdlet.ShouldProcess($global:settingsPath, "Save settings")) {
        $settings | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $global:settingsPath -Encoding UTF8
        Write-Host "Saved settings to: $($global:settingsPath)" -ForegroundColor DarkGray
    }
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
# Function: Install-WSLDistro
#==============================================================================
<#
.SYNOPSIS
    Installs a new WSL distro for OpenClaw.
.DESCRIPTION
    Creates a new WSL distro by installing from the Microsoft Store base image,
    then configures it for OpenClaw use.
.OUTPUTS
    [bool] True if successful, false otherwise.
#>
function Install-WSLDistro {
    [CmdletBinding(SupportsShouldProcess = $true)]
    [OutputType([bool])]
    param()

    if (-not $PSCmdlet.ShouldProcess($global:wslDistroName, "Install WSL Distro")) {
        return $false
    }

    Write-Host "Installing WSL distro '$($global:wslDistroName)'..." -ForegroundColor Yellow

    if (Test-WSLDistroExists -DistroName $global:wslDistroName) {
        Write-Host "WSL distro '$($global:wslDistroName)' already exists." -ForegroundColor Green
        return $true
    }

    Write-Host "Installing base Ubuntu distro..." -ForegroundColor Cyan
    $installOutput = wsl --install --distribution $global:wslDistroBaseImage --no-launch 2>&1
    Write-Host $installOutput

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to install base Ubuntu distro."
        return $false
    }

    New-Directory -Path $global:installRoot

    $exportPath = Join-Path $global:installRoot "ubuntu-base.tar"
    Write-Host "Exporting base distro for import as '$($global:wslDistroName)'..." -ForegroundColor Cyan

    wsl --export $global:wslDistroBaseImage $exportPath 2>&1 | Write-Host
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to export base distro."
        return $false
    }

    $distroInstallPath = Join-Path $global:installRoot "wsl-distro"
    New-Directory -Path $distroInstallPath

    Write-Host "Importing distro as '$($global:wslDistroName)'..." -ForegroundColor Cyan
    wsl --import $global:wslDistroName $distroInstallPath $exportPath 2>&1 | Write-Host

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to import distro."
        return $false
    }

    Remove-Item -LiteralPath $exportPath -Force -ErrorAction SilentlyContinue

    Write-Host "WSL distro '$($global:wslDistroName)' installed successfully." -ForegroundColor Green
    return $true
}

#==============================================================================
# Function: Uninstall-WSLDistro
#==============================================================================
<#
.SYNOPSIS
    Unregisters and removes the OpenClaw WSL distro.
.DESCRIPTION
    Uses 'wsl --unregister' to completely remove the distro and its filesystem.
.OUTPUTS
    [bool] True if successful, false otherwise.
#>
function Uninstall-WSLDistro {
    [CmdletBinding(SupportsShouldProcess = $true)]
    [OutputType([bool])]
    param()

    if (-not (Test-WSLDistroExists -DistroName $global:wslDistroName)) {
        Write-Host "WSL distro '$($global:wslDistroName)' does not exist." -ForegroundColor Yellow
        return $true
    }

    if (-not $PSCmdlet.ShouldProcess($global:wslDistroName, "Unregister WSL Distro")) {
        return $false
    }

    Write-Host "Unregistering WSL distro '$($global:wslDistroName)'..." -ForegroundColor Yellow
    Write-Warning "This will delete all data in the distro. This cannot be undone!"

    $confirm = Read-Host "Are you sure you want to unregister the distro? (Y/N)"
    if ($confirm -ne "Y") {
        Write-Host "Operation cancelled." -ForegroundColor Yellow
        return $false
    }

    wsl --unregister $global:wslDistroName 2>&1 | Write-Host

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to unregister distro."
        return $false
    }

    Write-Host "WSL distro '$($global:wslDistroName)' unregistered successfully." -ForegroundColor Green
    return $true
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
    Uses npm to install the openclaw package globally.
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

    Write-Host "Installing OpenClaw via npm..." -ForegroundColor Cyan
    Invoke-WSLCommand -DistroName $global:wslDistroName -AsRoot -Command "npm install -g @anthropic/openclaw"

    $finalVersion = Invoke-WSLCommand -DistroName $global:wslDistroName -Command "openclaw --version 2>/dev/null || echo 'installation-failed'"

    if ($finalVersion -match "installation-failed") {
        Write-Error "OpenClaw installation failed."
        return $false
    }

    Write-Host "OpenClaw installed: $finalVersion" -ForegroundColor Green
    return $true
}

#==============================================================================
# Function: Install-OpenClawService
#==============================================================================
<#
.SYNOPSIS
    Configures OpenClaw as a systemd user service.
.DESCRIPTION
    Creates a systemd user service file for the OpenClaw gateway daemon
    and optionally enables it for auto-start.
.PARAMETER AutoStart
    Enable service auto-start on WSL startup.
.OUTPUTS
    [bool] True if successful, false otherwise.
#>
function Install-OpenClawService {
    [CmdletBinding(SupportsShouldProcess = $true)]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory = $false)]
        [bool]$AutoStart = $false
    )

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
ExecStart=/usr/bin/openclaw gateway
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

    if ($AutoStart) {
        Write-Host "Enabling auto-start..." -ForegroundColor Cyan
        Invoke-WSLCommand -DistroName $global:wslDistroName -Command "systemctl --user enable $($global:openclawServiceName)"

        Invoke-WSLCommand -DistroName $global:wslDistroName -Command "loginctl enable-linger \$USER 2>/dev/null || true"
    }

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
        Write-Error "WSL distro '$($global:wslDistroName)' not found. Please install first."
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
    Orchestrates the complete installation: WSL distro, Node.js, OpenClaw CLI, and service.
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
    Write-Host "OpenClaw Full Installation" -ForegroundColor White
    Write-Host "===========================================" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "This will install:" -ForegroundColor Cyan
    Write-Host "  - Dedicated WSL2 distro: $($global:wslDistroName)" -ForegroundColor Cyan
    Write-Host "  - Node.js $($global:nodeMinVersion)+" -ForegroundColor Cyan
    Write-Host "  - OpenClaw CLI and Gateway" -ForegroundColor Cyan
    Write-Host "  - Systemd user service" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Ports:" -ForegroundColor Cyan
    Write-Host "  - Control UI:  127.0.0.1:$($global:controlUiPort)" -ForegroundColor Cyan
    Write-Host "  - Canvas Host: 127.0.0.1:$($global:canvasPort)" -ForegroundColor Cyan
    Write-Host ""

    Test-AdminPrivilege

    Test-WSLStatus

    $settings = Get-OpenClawSetting

    Write-Host ""
    $sandboxChoice = Read-Host "Configure sandbox mode for agent tools? (none/docker/podman) [default: $($settings.SandboxMode)]"
    if ([string]::IsNullOrWhiteSpace($sandboxChoice)) {
        $sandboxChoice = $settings.SandboxMode
    }

    $autoStartChoice = Read-Host "Enable auto-start on WSL startup? (Y/N) [default: $(if ($settings.AutoStart) { 'Y' } else { 'N' })]"
    $autoStart = if ([string]::IsNullOrWhiteSpace($autoStartChoice)) { $settings.AutoStart } else { $autoStartChoice -eq 'Y' }

    Set-OpenClawSetting -SandboxMode $sandboxChoice -AutoStart $autoStart

    Write-Host ""
    Write-Host "Step 1: Installing WSL Distro..." -ForegroundColor White
    if (-not (Install-WSLDistro)) {
        Write-Error "Failed to install WSL distro. Aborting."
        return
    }

    Write-Host ""
    Write-Host "Step 2: Installing Node.js..." -ForegroundColor White
    if (-not (Install-NodeJS)) {
        Write-Error "Failed to install Node.js. Aborting."
        return
    }

    Write-Host ""
    Write-Host "Step 3: Installing OpenClaw CLI..." -ForegroundColor White
    if (-not (Install-OpenClawCLI)) {
        Write-Error "Failed to install OpenClaw CLI. Aborting."
        return
    }

    Write-Host ""
    Write-Host "Step 4: Configuring Service..." -ForegroundColor White
    if (-not (Install-OpenClawService -AutoStart $autoStart)) {
        Write-Error "Failed to configure service. Aborting."
        return
    }

    if ($sandboxChoice -ne "none") {
        Write-Host ""
        Write-Host "Step 5: Configuring Sandbox ($sandboxChoice)..." -ForegroundColor White
        Install-OpenClawSandbox -Mode $sandboxChoice
    }

    Write-Host ""
    Write-Host "===========================================" -ForegroundColor Green
    Write-Host "OpenClaw Installation Complete!" -ForegroundColor Green
    Write-Host "===========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "To start the service, use option 5 from the menu or run:" -ForegroundColor Cyan
    Write-Host "  wsl -d $($global:wslDistroName) -- systemctl --user start $($global:openclawServiceName)" -ForegroundColor White
    Write-Host ""
    Write-Host "Then access the Control UI at: http://127.0.0.1:$($global:controlUiPort)/" -ForegroundColor Cyan
    Write-Host ""
}

#==============================================================================
# Function: Install-OpenClawSandbox
#==============================================================================
<#
.SYNOPSIS
    Configures sandbox mode for OpenClaw agent tools.
.DESCRIPTION
    Installs Docker or Podman inside the WSL distro for sandboxed tool execution.
.PARAMETER Mode
    Sandbox mode: docker or podman.
.OUTPUTS
    [void]
#>
function Install-OpenClawSandbox {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet("docker", "podman")]
        [string]$Mode
    )

    if (-not $PSCmdlet.ShouldProcess($global:wslDistroName, "Install Sandbox ($Mode)")) {
        return
    }

    Write-Host "Installing $Mode for sandbox mode..." -ForegroundColor Yellow

    if ($Mode -eq "docker") {
        Write-Host "Installing Docker..." -ForegroundColor Cyan
        Invoke-WSLCommand -DistroName $global:wslDistroName -AsRoot -Command "apt-get update && apt-get install -y docker.io"
        Invoke-WSLCommand -DistroName $global:wslDistroName -AsRoot -Command "usermod -aG docker \$USER"
        Invoke-WSLCommand -DistroName $global:wslDistroName -AsRoot -Command "systemctl enable docker && systemctl start docker"
    }
    elseif ($Mode -eq "podman") {
        Write-Host "Installing Podman..." -ForegroundColor Cyan
        Invoke-WSLCommand -DistroName $global:wslDistroName -AsRoot -Command "apt-get update && apt-get install -y podman"
    }

    Write-Host "Sandbox ($Mode) configured. Update OpenClaw config to use sandbox mode." -ForegroundColor Green
    Write-Host "Edit ~/.openclaw/config.yaml and set:" -ForegroundColor Cyan
    Write-Host "  agents:" -ForegroundColor White
    Write-Host "    defaults:" -ForegroundColor White
    Write-Host "      sandbox:" -ForegroundColor White
    Write-Host "        mode: $Mode" -ForegroundColor White
}

#==============================================================================
# Function: Uninstall-OpenClaw
#==============================================================================
<#
.SYNOPSIS
    Uninstalls OpenClaw and optionally removes the WSL distro.
.DESCRIPTION
    Stops services, optionally backs up data, and removes the installation.
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

    Stop-OpenClawService

    $removeDistro = Read-Host "Remove WSL distro '$($global:wslDistroName)'? This deletes ALL data. (Y/N)"
    if ($removeDistro -eq "Y") {
        Uninstall-WSLDistro
    }

    $removeSettings = Read-Host "Remove Windows settings and backup folder? (Y/N)"
    if ($removeSettings -eq "Y") {
        if (Test-Path -LiteralPath $global:installRoot) {
            Remove-Item -LiteralPath $global:installRoot -Recurse -Force -ErrorAction SilentlyContinue
            Write-Host "Removed: $($global:installRoot)" -ForegroundColor Green
        }
    }

    Write-Host ""
    Write-Host "OpenClaw uninstallation complete." -ForegroundColor Green
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
        Write-Error "WSL distro '$($global:wslDistroName)' not found. Please install first."
        return
    }

    $wasRunning = (Get-OpenClawServiceStatus) -eq "active"

    if ($wasRunning) {
        Stop-OpenClawService
    }

    Write-Host "Updating OpenClaw via npm..." -ForegroundColor Cyan
    Invoke-WSLCommand -DistroName $global:wslDistroName -AsRoot -Command "npm update -g @anthropic/openclaw"

    $newVersion = Invoke-WSLCommand -DistroName $global:wslDistroName -Command "openclaw --version"
    Write-Host "OpenClaw updated to: $newVersion" -ForegroundColor Green

    if ($wasRunning) {
        Start-OpenClawService
    }
}

#==============================================================================
# Function: Backup-OpenClawDistro
#==============================================================================
<#
.SYNOPSIS
    Exports the OpenClaw WSL distro to a tar file.
.DESCRIPTION
    Uses 'wsl --export' to create a backup of the entire distro.
.OUTPUTS
    [void]
#>
function Backup-OpenClawDistro {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param()

    if (-not $PSCmdlet.ShouldProcess($global:wslDistroName, "Backup Distro")) {
        return
    }

    if (-not (Test-WSLDistroExists -DistroName $global:wslDistroName)) {
        Write-Error "WSL distro '$($global:wslDistroName)' not found."
        return
    }

    New-Directory -Path $global:wslExportPath

    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $exportFile = Join-Path $global:wslExportPath "$($global:wslDistroName)-$timestamp.tar"

    Write-Host "Exporting distro to: $exportFile" -ForegroundColor Yellow
    Write-Host "This may take several minutes..." -ForegroundColor Cyan

    wsl --export $global:wslDistroName $exportFile 2>&1 | Write-Host

    if ($LASTEXITCODE -eq 0) {
        $fileSize = [math]::Round((Get-Item $exportFile).Length / 1MB, 2)
        Write-Host "Backup complete: $exportFile ($fileSize MB)" -ForegroundColor Green
    }
    else {
        Write-Error "Backup failed."
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

    if ($distroExists) {
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
    }

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
    Write-Host "Settings:" -ForegroundColor White
    Write-Host "  Config Path: $($global:settingsPath)" -ForegroundColor DarkGray
    Write-Host "  Distro Path: $($global:installRoot)" -ForegroundColor DarkGray
}

#==============================================================================
# Function: Open-OpenClawShell
#==============================================================================
<#
.SYNOPSIS
    Opens an interactive shell in the OpenClaw WSL distro.
.DESCRIPTION
    Launches a bash shell in the OpenClaw distro.
.OUTPUTS
    [void]
#>
function Open-OpenClawShell {
    [CmdletBinding()]
    param()

    if (-not (Test-WSLDistroExists -DistroName $global:wslDistroName)) {
        Write-Error "WSL distro '$($global:wslDistroName)' not found. Please install first."
        return
    }

    Write-Host "Opening shell in '$($global:wslDistroName)'..." -ForegroundColor Cyan
    Write-Host "Type 'exit' to return to PowerShell." -ForegroundColor DarkGray
    Write-Host ""

    wsl --distribution $global:wslDistroName
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

################################################################################
# Main Menu Loop
################################################################################

New-Directory -Path $global:installRoot

$menuTitle = "OpenClaw Management Menu"
$menuItems = [ordered]@{
    "1" = "Show Status & Test Connection"
    "2" = "Install OpenClaw (Full)"
    "3" = "Uninstall OpenClaw"
    "4" = "Update OpenClaw"
    "5" = "Start Service"
    "6" = "Stop Service"
    "7" = "Backup Distro"
    "8" = "Show Logs"
    "S" = "Open Shell"
    "0" = "Exit menu"
}

$menuActions = @{
    "1" = { Show-OpenClawStatus }
    "2" = { Install-OpenClaw }
    "3" = { Uninstall-OpenClaw }
    "4" = { Update-OpenClaw }
    "5" = { Start-OpenClawService }
    "6" = { Stop-OpenClawService }
    "7" = { Backup-OpenClawDistro }
    "8" = { Show-OpenClawLogs }
    "S" = { Open-OpenClawShell }
}

Invoke-MenuLoop -MenuTitle $menuTitle -MenuItems $menuItems -ActionMap $menuActions -ExitChoice "0"
