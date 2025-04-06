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
Test-AdminPrivilege
Set-ScriptLocation

#==============================================================================
# Function: Enable-WSL2
#==============================================================================
<#
.SYNOPSIS
	Ensures that WSL default version is set to 2 and converts existing distributions.
.DESCRIPTION
	Sets the default version for new WSL installations to 2 using 'wsl.exe --set-default-version 2'.
	Checks for installed distributions using 'wsl.exe -l -v'.
	If no distributions are found, it attempts to install the default one using 'wsl.exe --install'.
	If distributions exist, it iterates through them and converts any found running WSL1 to WSL2
	using 'wsl.exe --set-version <distro> 2'.
.EXAMPLE
	Enable-WSL2
	# Ensures WSL 2 is the default and attempts to convert existing distros.
.NOTES
	Requires administrative privileges to set default version or convert distributions.
	Uses Write-Information for status messages and Write-Error for failures.
	Relies on parsing the output of 'wsl.exe -l -v'.
#>
function Enable-WSL2 {
	[CmdletBinding()]
	param()

	Write-Information "Setting WSL default version to 2..."
	$setDefaultOutput = wsl.exe --set-default-version 2 2>&1
	if ($LASTEXITCODE -eq 0) {
		Write-Information "WSL default version set to 2."
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
		Write-Information "No WSL distributions found. Installing default distribution..."
		$installOutput = wsl.exe --install 2>&1
		if ($LASTEXITCODE -eq 0) {
			Write-Information "Default WSL distribution installation initiated successfully."
		}
		else {
			Write-Error "Failed to install default WSL distribution. Output: $installOutput"
		}
	}
	else {
		Write-Information "WSL distribution(s) detected."
		Write-Information "Currently installed WSL distributions:"
		Write-Information $wslListOutput
		Write-Information "Verifying that they use WSL2..."

		$lines = $wslListOutput -split "`r?`n"
		$wsl1Found = $false
		foreach ($line in $lines) {
			if ($line -match "^\s*(\S+)\s+\S+\s+1\s*$") {
				$wsl1Found = $true
				$distro = $Matches[1]
				Write-Information "Distribution $distro is using WSL1. Converting to WSL2..."
				$convertOutput = wsl.exe --set-version $distro 2 2>&1
				if ($LASTEXITCODE -eq 0) {
					Write-Information "Successfully converted $distro to WSL2."
				}
				else {
					Write-Error "Failed to convert $distro to WSL2. Output: $convertOutput"
				}
			}
		}
		if (-not $wsl1Found) {
			Write-Information "All installed WSL distributions are using WSL2."
		}
	}
}

###############################################################################
# Section: Virtualization Feature Selection using Invoke-MenuLoop
###############################################################################

# Define Menu Title and Items
$menuTitle = "Select virtualization feature to enable"
$menuItems = [ordered]@{
	"1" = "Windows Subsystem for Linux (WSL)"
	"2" = "Hyper-V"
	# "0" = "Exit" # Implicit exit choice
}

# Define Menu Actions
$menuActions = @{
	"1" = {
		Write-Information "Enabling/Verifying WSL..."
		# Call existing function to check and enable WSL, then ensure WSL2 is set up.
		Test-WSLStatus
		Enable-WSL2
		Write-Information "WSL setup completed successfully."
		exit 0 # Exit script after successful action
	}
	"2" = {
		Write-Information "Enabling Hyper-V..."
		try {
			# Check if the Hyper-V feature is already enabled.
			$hyperVFeature = Get-WindowsOptionalFeature -Online -FeatureName Microsoft-Hyper-V-All -ErrorAction SilentlyContinue
			if ($hyperVFeature -and $hyperVFeature.State -eq "Enabled") {
				Write-Information "Hyper-V is already enabled."
			}
			else {
				Write-Information "Hyper-V is not enabled. Enabling the Microsoft-Hyper-V-All feature..."
				Enable-WindowsOptionalFeature -Online -FeatureName Microsoft-Hyper-V-All -All -NoRestart | Out-Null
				if ($?) {
					Write-Information "Hyper-V has been enabled. A system restart may be required."
				}
				else {
					Write-Error "Failed to enable Hyper-V."
					exit 1 # Exit script on error
				}
			}
			Write-Information "Hyper-V setup completed successfully."
			exit 0 # Exit script after successful action
		}
		catch {
			Write-Error "An error occurred while enabling Hyper-V: $_"
			exit 1 # Exit script on error
		}
	}
	# Note: "0" action is handled internally by Invoke-MenuLoop to exit the loop.
	# The script will then terminate naturally.
}

# Invoke the Menu Loop (will exit after one valid choice due to 'exit' in actions)
Invoke-MenuLoop -MenuTitle $menuTitle -MenuItems $menuItems -ActionMap $menuActions -ExitChoice "0"

# This line will only be reached if the user enters '0' or an invalid choice repeatedly until they enter '0'.
Write-Information "Exited without making a selection."
