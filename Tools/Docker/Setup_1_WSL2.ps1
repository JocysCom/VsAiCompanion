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
Test-AdminPrivileges
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
function Enable-WSL2 {
    [CmdletBinding()]
    param()

    Write-Output "Setting WSL default version to 2..."
    $setDefaultOutput = wsl.exe --set-default-version 2 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Output "WSL default version set to 2."
    }
    else {
        Write-Error "Failed to set WSL default version. Output: $setDefaultOutput"
    }

    try {
        $wslListOutput = wsl.exe -l -v 2>&1
    }
    catch {
        Write-Error "Failed to retrieve WSL distribution list: $_"
        return
    }

    if ($wslListOutput -match "has no installed distributions") {
        Write-Output "No WSL distributions found. Installing default distribution..."
        $installOutput = wsl.exe --install 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Output "Default WSL distribution installation initiated successfully."
        }
        else {
            Write-Error "Failed to install default WSL distribution. Output: $installOutput"
        }
    }
    else {
        Write-Output "WSL distribution(s) detected."
        Write-Output "Currently installed WSL distributions:"
        Write-Output $wslListOutput
        Write-Output "Verifying that they use WSL2..."

        $lines = $wslListOutput -split "`r?`n"
        $wsl1Found = $false
        foreach ($line in $lines) {
            if ($line -match "^\s*(\S+)\s+\S+\s+1\s*$") {
                $wsl1Found = $true
                $distro = $Matches[1]
                Write-Output "Distribution $distro is using WSL1. Converting to WSL2..."
                $convertOutput = wsl.exe --set-version $distro 2 2>&1
                if ($LASTEXITCODE -eq 0) {
                    Write-Output "Successfully converted $distro to WSL2."
                }
                else {
                    Write-Error "Failed to convert $distro to WSL2. Output: $convertOutput"
                }
            }
        }
        if (-not $wsl1Found) {
            Write-Output "All installed WSL distributions are using WSL2."
        }
    }
}

###############################################################################
# Section: Virtualization Feature Selection
###############################################################################

Write-Output "Select virtualization feature to enable:"
Write-Output "1) Windows Subsystem for Linux (WSL)"
Write-Output "2) Hyper-V"
$choice = Read-Host "Enter your choice (1 or 2, default is 1)"

if ([string]::IsNullOrWhiteSpace($choice)) {
    $choice = "1"
}

switch ($choice) {
    "1" {
        Write-Output "Enabling/Verifying WSL..."
        # Call existing function to check and enable WSL, then ensure WSL2 is set up.
        Test-WSLStatus
        Enable-WSL2
        Write-Output "WSL setup completed."
    }
    "2" {
        Write-Output "Enabling Hyper-V..."
        try {
            # Check if the Hyper-V feature is already enabled.
            $hyperVFeature = Get-WindowsOptionalFeature -Online -FeatureName Microsoft-Hyper-V-All -ErrorAction SilentlyContinue
            if ($hyperVFeature -and $hyperVFeature.State -eq "Enabled") {
                Write-Output "Hyper-V is already enabled."
            }
            else {
                Write-Output "Hyper-V is not enabled. Enabling the Microsoft-Hyper-V-All feature..."
                Enable-WindowsOptionalFeature -Online -FeatureName Microsoft-Hyper-V-All -All -NoRestart | Out-Null
                if ($?) {
                    Write-Output "Hyper-V has been enabled. A system restart may be required."
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

Write-Output "Virtualization setup completed successfully."
