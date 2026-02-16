################################################################################
# File         : Setup_Core_WSL_OpenClaw.ps1
# Description  : Manages the dedicated WSL2 Ubuntu distro that hosts OpenClaw.
#                Handles distro creation, removal, backup, and restore operations.
# Usage        : Run as Administrator for WSL distro management.
################################################################################

using namespace System
using namespace System.IO

# Dot-source the necessary helper function files.
. "$PSScriptRoot\Setup_Helper_CoreFunctions.ps1"
. "$PSScriptRoot\Setup_Helper_WSLFunctions.ps1"

# Ensure the script working directory is set.
Set-ScriptLocation

#==============================================================================
# Global Configuration
#==============================================================================

$global:appName = "OpenClaw"
$global:wslDistroName = "OpenClaw-WSL"
$global:wslDistroBaseImage = "Ubuntu-24.04"
$global:programDataRoot = [Environment]::GetFolderPath([Environment+SpecialFolder]::CommonApplicationData)
$global:installRoot = Join-Path $global:programDataRoot $global:appName
$global:wslExportPath = Join-Path $global:installRoot "distro-backup"

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
# Function: Backup-WSLDistro
#==============================================================================
<#
.SYNOPSIS
    Exports the OpenClaw WSL distro to a tar file.
.DESCRIPTION
    Uses 'wsl --export' to create a backup of the entire distro.
.OUTPUTS
    [void]
#>
function Backup-WSLDistro {
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
# Function: Restore-WSLDistro
#==============================================================================
<#
.SYNOPSIS
    Restores the OpenClaw WSL distro from a tar backup file.
.DESCRIPTION
    Lists available backups and imports the selected one using 'wsl --import'.
.OUTPUTS
    [void]
#>
function Restore-WSLDistro {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param()

    if (-not $PSCmdlet.ShouldProcess($global:wslDistroName, "Restore Distro")) {
        return
    }

    if (-not (Test-Path -LiteralPath $global:wslExportPath)) {
        Write-Error "Backup directory not found: $($global:wslExportPath)"
        return
    }

    $backupFiles = Get-ChildItem -Path $global:wslExportPath -Filter "*.tar" | Sort-Object LastWriteTime -Descending

    if ($backupFiles.Count -eq 0) {
        Write-Error "No backup files found in: $($global:wslExportPath)"
        return
    }

    Write-Host ""
    Write-Host "Available backups:" -ForegroundColor Cyan
    for ($i = 0; $i -lt $backupFiles.Count; $i++) {
        $file = $backupFiles[$i]
        $sizeInMB = [math]::Round($file.Length / 1MB, 2)
        Write-Host "  $($i + 1). $($file.Name) ($sizeInMB MB) - $($file.LastWriteTime)"
    }
    Write-Host ""

    $selection = Read-Host "Select backup number to restore (1-$($backupFiles.Count)) or 0 to cancel"

    if ($selection -eq "0" -or [string]::IsNullOrWhiteSpace($selection)) {
        Write-Host "Operation cancelled." -ForegroundColor Yellow
        return
    }

    $index = [int]$selection - 1
    if ($index -lt 0 -or $index -ge $backupFiles.Count) {
        Write-Error "Invalid selection."
        return
    }

    $selectedBackup = $backupFiles[$index].FullName

    if (Test-WSLDistroExists -DistroName $global:wslDistroName) {
        Write-Warning "WSL distro '$($global:wslDistroName)' already exists."
        $removeFirst = Read-Host "Remove existing distro before restore? (Y/N)"
        if ($removeFirst -ne "Y") {
            Write-Host "Operation cancelled." -ForegroundColor Yellow
            return
        }

        wsl --unregister $global:wslDistroName 2>&1 | Write-Host
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Failed to remove existing distro."
            return
        }
    }

    $distroInstallPath = Join-Path $global:installRoot "wsl-distro"
    New-Directory -Path $distroInstallPath

    Write-Host "Restoring distro from: $selectedBackup" -ForegroundColor Yellow
    Write-Host "This may take several minutes..." -ForegroundColor Cyan

    wsl --import $global:wslDistroName $distroInstallPath $selectedBackup 2>&1 | Write-Host

    if ($LASTEXITCODE -eq 0) {
        Write-Host "Distro restored successfully." -ForegroundColor Green
    }
    else {
        Write-Error "Restore failed."
    }
}

#==============================================================================
# Function: Show-WSLDistroStatus
#==============================================================================
<#
.SYNOPSIS
    Shows comprehensive WSL distro status information.
.DESCRIPTION
    Displays distro existence, running state, disk usage, and backup information.
.OUTPUTS
    [void]
#>
function Show-WSLDistroStatus {
    [CmdletBinding()]
    param()

    Write-Host ""
    Write-Host "===========================================" -ForegroundColor Yellow
    Write-Host "WSL Distro Status: $($global:wslDistroName)" -ForegroundColor White
    Write-Host "===========================================" -ForegroundColor Yellow

    $distroExists = Test-WSLDistroExists -DistroName $global:wslDistroName
    $distroStatus = if ($distroExists) { Get-WSLDistroStatus -DistroName $global:wslDistroName } else { "Not Installed" }

    Write-Host ""
    Write-Host "Distro Information:" -ForegroundColor White
    Write-Host "  Name:       $($global:wslDistroName)" -ForegroundColor Cyan
    Write-Host "  Base Image: $($global:wslDistroBaseImage)" -ForegroundColor Cyan
    Write-Host "  Status:     $distroStatus" -ForegroundColor $(if ($distroStatus -eq "Running") { "Green" } elseif ($distroStatus -eq "Stopped") { "Yellow" } else { "Red" })

    Write-Host ""
    Write-Host "Paths:" -ForegroundColor White
    Write-Host "  Install Root: $($global:installRoot)" -ForegroundColor DarkGray
    Write-Host "  Backup Path:  $($global:wslExportPath)" -ForegroundColor DarkGray

    $distroPath = Join-Path $global:installRoot "wsl-distro"
    if (Test-Path -LiteralPath $distroPath) {
        $vhdxFiles = Get-ChildItem -Path $distroPath -Filter "*.vhdx" -ErrorAction SilentlyContinue
        if ($vhdxFiles) {
            $totalSize = ($vhdxFiles | Measure-Object -Property Length -Sum).Sum
            $sizeInGB = [math]::Round($totalSize / 1GB, 2)
            Write-Host "  Disk Usage:   $sizeInGB GB" -ForegroundColor DarkGray
        }
    }

    Write-Host ""
    Write-Host "Available Backups:" -ForegroundColor White
    if (Test-Path -LiteralPath $global:wslExportPath) {
        $backupFiles = Get-ChildItem -Path $global:wslExportPath -Filter "*.tar" | Sort-Object LastWriteTime -Descending
        if ($backupFiles.Count -gt 0) {
            foreach ($file in $backupFiles) {
                $sizeInMB = [math]::Round($file.Length / 1MB, 2)
                Write-Host "  - $($file.Name) ($sizeInMB MB)" -ForegroundColor DarkGray
            }
        }
        else {
            Write-Host "  No backups found." -ForegroundColor DarkGray
        }
    }
    else {
        Write-Host "  Backup directory does not exist yet." -ForegroundColor DarkGray
    }

    Write-Host ""
}

#==============================================================================
# Function: Open-WSLShell
#==============================================================================
<#
.SYNOPSIS
    Opens an interactive shell in the OpenClaw WSL distro.
.DESCRIPTION
    Launches a bash shell in the OpenClaw distro.
.OUTPUTS
    [void]
#>
function Open-WSLShell {
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
# Function: Start-WSLDistro
#==============================================================================
<#
.SYNOPSIS
    Starts the OpenClaw WSL distro.
.DESCRIPTION
    Boots the WSL distro by running a lightweight command inside it.
    If the distro is already running, reports the current state.
.OUTPUTS
    [void]
#>
function Start-WSLDistro {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param()

    if (-not (Test-WSLDistroExists -DistroName $global:wslDistroName)) {
        Write-Error "WSL distro '$($global:wslDistroName)' not found. Please install first."
        return
    }

    $status = Get-WSLDistroStatus -DistroName $global:wslDistroName
    if ($status -eq "Running") {
        Write-Host "WSL distro '$($global:wslDistroName)' is already running." -ForegroundColor Green
        return
    }

    if (-not $PSCmdlet.ShouldProcess($global:wslDistroName, "Start WSL Distro")) {
        return
    }

    Write-Host "Starting WSL distro '$($global:wslDistroName)'..." -ForegroundColor Yellow
    Start-Process -FilePath "wsl.exe" -ArgumentList "-d $($global:wslDistroName) -- sleep infinity" -WindowStyle Hidden

    Start-Sleep -Seconds 3

    $newStatus = Get-WSLDistroStatus -DistroName $global:wslDistroName
    if ($newStatus -eq "Running") {
        Write-Host "WSL distro '$($global:wslDistroName)' started successfully." -ForegroundColor Green
        Write-Host "A background keep-alive process is holding the distro running." -ForegroundColor DarkGray
    }
    else {
        Write-Warning "Distro status after start attempt: $newStatus"
    }
}

#==============================================================================
# Function: Stop-WSLDistro
#==============================================================================
<#
.SYNOPSIS
    Stops the OpenClaw WSL distro.
.DESCRIPTION
    Uses 'wsl --terminate' to shut down the running distro.
    If the distro is already stopped, reports the current state.
.OUTPUTS
    [void]
#>
function Stop-WSLDistro {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param()

    if (-not (Test-WSLDistroExists -DistroName $global:wslDistroName)) {
        Write-Error "WSL distro '$($global:wslDistroName)' not found. Please install first."
        return
    }

    $status = Get-WSLDistroStatus -DistroName $global:wslDistroName
    if ($status -eq "Stopped") {
        Write-Host "WSL distro '$($global:wslDistroName)' is already stopped." -ForegroundColor Yellow
        return
    }

    if (-not $PSCmdlet.ShouldProcess($global:wslDistroName, "Stop WSL Distro")) {
        return
    }

    Write-Host "Stopping WSL distro '$($global:wslDistroName)'..." -ForegroundColor Yellow

    Write-Host "Terminating keep-alive processes..." -ForegroundColor Cyan
    $keepAliveProcs = Get-Process -Name "wsl" -ErrorAction SilentlyContinue |
        Where-Object { $_.CommandLine -match "$($global:wslDistroName).*sleep infinity" }
    foreach ($proc in $keepAliveProcs) {
        Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
    }

    wsl --terminate $global:wslDistroName 2>&1 | Write-Host

    if ($LASTEXITCODE -eq 0) {
        Write-Host "WSL distro '$($global:wslDistroName)' stopped successfully." -ForegroundColor Green
    }
    else {
        Write-Warning "Failed to stop distro. Exit code: $LASTEXITCODE"
    }
}

################################################################################
# Main Menu Loop
################################################################################

New-Directory -Path $global:installRoot

Test-AdminPrivilege

Test-WSLStatus

$menuTitle = "OpenClaw WSL Distro Management Menu"
$menuItems = [ordered]@{
    "1" = "Show Distro Status"
    "2" = "Install Ubuntu Distro"
    "3" = "Uninstall Ubuntu Distro"
    "4" = "Start Distro"
    "5" = "Stop Distro"
    "6" = "Backup Distro (Export)"
    "7" = "Restore Distro (Import)"
    "S" = "Open Shell"
    "0" = "Exit menu"
}

$menuActions = @{
    "1" = { Show-WSLDistroStatus }
    "2" = { $null = Install-WSLDistro }
    "3" = { $null = Uninstall-WSLDistro }
    "4" = { Start-WSLDistro }
    "5" = { Stop-WSLDistro }
    "6" = { Backup-WSLDistro }
    "7" = { Restore-WSLDistro }
    "S" = { Open-WSLShell }
}

Invoke-MenuLoop -MenuTitle $menuTitle -MenuItems $menuItems -ActionMap $menuActions -ExitChoice "0"
