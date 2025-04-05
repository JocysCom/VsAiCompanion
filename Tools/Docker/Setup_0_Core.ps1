################################################################################
# File         : Setup_0_Core.ps1
# Description  : Contains core helper functions for setup scripts:
#                - Ensure-Elevated: Verify administrator privileges.
#                - Set-ScriptLocation: Set the script's working directory.
#                - Download-File: Download files using BITS or WebRequest.
#                - Check-Git: Check for Git installation and add to PATH if needed.
#                - Test-ApplicationInstalled: Check if an application is installed.
#                - Refresh-EnvironmentVariables: Refresh PATH in the current session.
# Usage        : Dot-source this script in other setup scripts:
#                . "$PSScriptRoot\Setup_0_Core.ps1"
################################################################################

#------------------------------
# Function: Ensure-Elevated
#------------------------------
function Ensure-Elevated {
    if (-NOT ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
         Write-Error "Administrator privileges required. Please run this script as an Administrator."
         exit 1
    }
}

#------------------------------
# Function: Set-ScriptLocation
#------------------------------
function Set-ScriptLocation {
    if ($PSScriptRoot -and $PSScriptRoot -ne "") {
        $scriptPath = $PSScriptRoot
    }
    else {
        $scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
    }
    if ($scriptPath) {
        Set-Location $scriptPath
        Write-Host "Script Path: $scriptPath"
    }
    else {
        Write-Host "Script Path not found. Current directory remains unchanged."
    }
}

#------------------------------
# Function: Download-File
# Generic download function using Start-BitsTransfer (provides a fast download with a progress bar).
# Supports both -SourceUrl and -url as parameter aliases.
#------------------------------
function Download-File {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)]
        [Alias("url")]
        [string]$SourceUrl,
        [Parameter(Mandatory=$true)]
        [string]$DestinationPath,
        [switch]$ForceDownload,  # Optional switch to force re-download
        [switch]$UseFallback     # New parameter to force fallback method
    )

    if ((Test-Path $DestinationPath) -and (-not $ForceDownload)) {
        Write-Host "File already exists at $DestinationPath. Skipping download."
        return
    }

    # Check if BITS is available or if fallback is requested
    if ((Get-Command Start-BitsTransfer -ErrorAction SilentlyContinue) -and (-not $UseFallback)) {
        Write-Host "Downloading file from $SourceUrl to $DestinationPath using Start-BitsTransfer..."
        try {
            Start-BitsTransfer -Source $SourceUrl -Destination $DestinationPath
            Write-Host "Download succeeded: $DestinationPath" -ForegroundColor Green
            return
        }
        catch {
            Write-Warning "BITS transfer failed: $_. Trying fallback method..."
        }
    }

    # Fallback to Invoke-WebRequest
    try {
        Write-Host "Downloading file from $SourceUrl to $DestinationPath using Invoke-WebRequest..."
        $ProgressPreference = 'SilentlyContinue'  # Speeds up Invoke-WebRequest significantly
        Invoke-WebRequest -Uri $SourceUrl -OutFile $DestinationPath -UseBasicParsing
        $ProgressPreference = 'Continue'  # Restore default
        Write-Host "Download succeeded: $DestinationPath" -ForegroundColor Green
    }
    catch {
        Write-Error "Failed to download file from $SourceUrl. Error details: $_"
        exit 1
    }
}

#------------------------------
# Function: Check-Git
#------------------------------
function Check-Git {
    if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
        Write-Host "Git command not found in PATH. Attempting to locate Git via common installation paths..."
        $possibleGitPaths = @(
            "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Git\cmd",
            "C:\Program Files\Microsoft Visual Studio\2022\Professional\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Git\cmd"
        )
        foreach ($path in $possibleGitPaths) {
            if (Test-Path $path) {
                $env:Path += ";" + $path
                Write-Host "Added Git path: $path"
                break
            }
        }
        if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
            Write-Error "Git command not found. Please install Git and ensure it's in your PATH."
            exit 1
        }
    }
}

#------------------------------
# Function: Test-ApplicationInstalled
#------------------------------
function Test-ApplicationInstalled {
    <#
    .SYNOPSIS
        Determines whether a specified application is installed.
    .PARAMETER AppName
        The application name to search for (supports wildcards).
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$AppName
    )

    # First check registry for performance
    $uninstallPaths = @(
        "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*",
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*",
        "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*"
    )

    foreach ($path in $uninstallPaths) {
        try {
            $apps = Get-ItemProperty -Path $path -ErrorAction SilentlyContinue |
                    Where-Object { $_.DisplayName -like "*$AppName*" }
            if ($apps) { return $true }
        }
        catch { continue }
    }

    # Only if registry check fails, try Get-Package as fallback
    try {
        $package = Get-Package -Name "$AppName*" -ErrorAction SilentlyContinue
        if ($package) {
            return $true
        }
    }
    catch {
        # Ignore errors with Get-Package
    }

    # Not found by any method
    return $false
}

#############################################
# Function: Refresh-EnvironmentVariables
#############################################
function Refresh-EnvironmentVariables {
    <#
    .SYNOPSIS
      Refreshes the current session's environment variables.

    .DESCRIPTION
      Re-reads the machine and user PATH from the registry and updates the current session.
      This allows newly installed executables (such as podman) to be found without restarting PowerShell.
    #>
    $machinePath = [System.Environment]::GetEnvironmentVariable("PATH", [System.EnvironmentVariableTarget]::Machine)
    $userPath = [System.Environment]::GetEnvironmentVariable("PATH", [System.EnvironmentVariableTarget]::User)
    $env:PATH = "$machinePath;$userPath"
    Write-Host "Environment variables refreshed. Current PATH:"
    Write-Host $env:PATH
}

#############################################
# Function: Invoke-MenuLoop
# Description: Handles a generic menu loop.
# Parameters:
#   -ShowMenuScriptBlock: A script block that displays the menu options.
#   -ActionMap: A hashtable where keys are menu choices (strings) and values are script blocks to execute.
#   -ExitChoice: The menu choice string that exits the loop (default "0").
#############################################
function Invoke-MenuLoop {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)]
        [scriptblock]$ShowMenuScriptBlock,

        [Parameter(Mandatory=$true)]
        [hashtable]$ActionMap,

        [string]$ExitChoice = "0"
    )

    do {
        & $ShowMenuScriptBlock
        $choice = Read-Host "Enter your choice"

        if ($ActionMap.ContainsKey($choice)) {
            try {
                & $ActionMap[$choice]
            }
            catch {
                Write-Error "An error occurred executing action for choice '$choice': $_"
            }
        }
        elseif ($choice -eq $ExitChoice) {
            Write-Host "Exiting menu."
        }
        else {
            Write-Warning "Invalid selection."
        }

        if ($choice -ne $ExitChoice) {
             Write-Host "`nPress any key to continue..."
             $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
             Clear-Host
        }
    } while ($choice -ne $ExitChoice)
}
