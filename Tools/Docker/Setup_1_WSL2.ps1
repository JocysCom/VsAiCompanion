################################################################################
# File         : Setup_1_VM.ps1
# Description  : Script to enable virtualization features on Windows.
#                Allows the user to choose between enabling Windows Subsystem for Linux (WSL)
#                or Hyper-V. For WSL, it calls the existing Check-WSLStatus function and
#                ensures that the default WSL version is set to 2, installing and/or upgrading
#                distributions as necessary.
# Usage        : Run as Administrator.
################################################################################

using namespace System
using namespace System.IO

# Dot-source the necessary helper function files.
. "$PSScriptRoot\Setup_0_Core.ps1"
. "$PSScriptRoot\Setup_0_WSL.ps1"

# Ensure the script is running as Administrator and set the working directory.
Test-AdminPrivileges # Renamed from Ensure-Elevated (assuming Setup_0_Core.ps1 is sourced)
Set-ScriptLocation

#---------------------------------------------
# Function: Enable-WSL2
#---------------------------------------------
<#
.SYNOPSIS
    Ensures that WSL default version is set to 2.
.DESCRIPTION
    Sets the default version for new WSL installations to 2, checks for installed distributions,
    installs the default distribution if none exist, and converts any WSL1 distribution to WSL2.
    Note: The installation command does not specify a distribution, so the system installs the default.
.EXAMPLE
    Enable-WSL2
#>
function Enable-WSL2 { # Renamed function
    [CmdletBinding()]
    param()

    Write-Output "Setting WSL default version to 2..." # Replaced Write-Host
    $setDefaultOutput = wsl.exe --set-default-version 2 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Output "WSL default version set to 2." # Replaced Write-Host
    }
    else {
        Write-Error "Failed to set WSL default version. Output: $setDefaultOutput"
    }
    # Removed trailing whitespace from original line 38

    try {
        $wslListOutput = wsl.exe -l -v 2>&1
    } # Removed trailing whitespace from original line 47
    catch {
        Write-Error "Failed to retrieve WSL distribution list: $_"
        return
    }
    # Removed trailing whitespace from original line 55

    if ($wslListOutput -match "has no installed distributions") {
        Write-Output "No WSL distributions found. Installing default distribution..." # Replaced Write-Host
        $installOutput = wsl.exe --install 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Output "Default WSL distribution installation initiated successfully." # Replaced Write-Host
        }
        else {
            Write-Error "Failed to install default WSL distribution. Output: $installOutput"
        }
    }
    else {
        Write-Output "WSL distribution(s) detected." # Replaced Write-Host
        Write-Output "Currently installed WSL distributions:" # Replaced Write-Host
        Write-Output $wslListOutput # Replaced Write-Host
        Write-Output "Verifying that they use WSL2..." # Replaced Write-Host

        $lines = $wslListOutput -split "`r?`n"
        $wsl1Found = $false # Removed trailing whitespace from original line 71
        foreach ($line in $lines) {
            if ($line -match "^\s*(\S+)\s+\S+\s+1\s*$") {
                $wsl1Found = $true
                $distro = $Matches[1]
                Write-Output "Distribution $distro is using WSL1. Converting to WSL2..." # Replaced Write-Host
                $convertOutput = wsl.exe --set-version $distro 2 2>&1
                if ($LASTEXITCODE -eq 0) {
                    Write-Output "Successfully converted $distro to WSL2." # Replaced Write-Host
                }
                else {
                    Write-Error "Failed to convert $distro to WSL2. Output: $convertOutput"
                }
            }
        }
        if (-not $wsl1Found) {
            Write-Output "All installed WSL distributions are using WSL2." # Replaced Write-Host
        }
    }
}

###############################################################################
# Section: Virtualization Feature Selection
###############################################################################

Write-Output "Select virtualization feature to enable:" # Replaced Write-Host
Write-Output "1) Windows Subsystem for Linux (WSL)" # Replaced Write-Host
Write-Output "2) Hyper-V" # Replaced Write-Host
$choice = Read-Host "Enter your choice (1 or 2, default is 1)"

if ([string]::IsNullOrWhiteSpace($choice)) {
    $choice = "1"
}

switch ($choice) {
    "1" {
        Write-Output "Enabling/Verifying WSL..." # Replaced Write-Host
        # Call existing function to check and enable WSL, then ensure WSL2 is set up.
        Test-WSLStatus # Use renamed function from Setup_0_WSL.ps1
        Enable-WSL2 # Use renamed function defined above
        Write-Output "WSL setup completed." # Replaced Write-Host
    }
    "2" {
        Write-Output "Enabling Hyper-V..." # Replaced Write-Host
        try {
            # Check if the Hyper-V feature is already enabled.
            $hyperVFeature = Get-WindowsOptionalFeature -Online -FeatureName Microsoft-Hyper-V-All -ErrorAction SilentlyContinue
            if ($hyperVFeature -and $hyperVFeature.State -eq "Enabled") {
                Write-Output "Hyper-V is already enabled." # Replaced Write-Host
            }
            else {
                Write-Output "Hyper-V is not enabled. Enabling the Microsoft-Hyper-V-All feature..." # Replaced Write-Host
                Enable-WindowsOptionalFeature -Online -FeatureName Microsoft-Hyper-V-All -All -NoRestart | Out-Null
                if ($?) {
                    Write-Output "Hyper-V has been enabled. A system restart may be required." # Replaced Write-Host
                }
                else {
                    Write-Error "Failed to enable Hyper-V."
                    exit 1
                }
            }
        }
        catch {
            Write-Error "An error occurred while enabling Hyper-V: $_"
            exit 1
        }
    }
    default {
        Write-Error "Invalid selection. Exiting."
        exit 1
    }
}

Write-Output "Virtualization setup completed successfully." # Replaced Write-Host
