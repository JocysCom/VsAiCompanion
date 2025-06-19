################################################################################
# File         : Setup_1_WSL2.ps1
# Description  : Script to enable virtualization features on Windows.
#                Provides a menu to enable either Windows Subsystem for Linux (WSL)
#                or Hyper-V. For WSL, it ensures WSL2 is the default and converts
#                existing distributions if necessary.
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
	Uses Write-Host for status messages and Write-Error for failures.
	Relies on parsing the output of 'wsl.exe -l -v'.
#>
function Enable-WSL2 {
	[CmdletBinding()]
	param()

	# For Windows Server 2022, the recommended approach is wsl --install
	# This handles everything: features, kernel, default version, and distribution
	Write-Host "For Windows Server 2022, using the official 'wsl --install' method..." -ForegroundColor Cyan
	Write-Host "This will enable features, download kernel, set WSL2 as default, and install Ubuntu."
	Write-Host ""
	Write-Host "Installation output:" -ForegroundColor White
	Write-Host "==================" -ForegroundColor White

	# Run wsl --install and show output in real-time
	try {
		$installResult = wsl --install
		Write-Host "Exit Code: $LASTEXITCODE" -ForegroundColor Gray
	}
	catch {
		Write-Error "Failed to execute wsl --install: $_"
		return
	}

	# Also capture output for analysis
	$installOutput = wsl --install 2>&1
	$installString = $installOutput -join " "

	Write-Host "==================" -ForegroundColor White

	# Check if command returned help text (indicates features not enabled)
	if ($installString -like "*Copyright (c) Microsoft Corporation*" -or $installString -like "*Usage: wsl.exe*") {
		Write-Warning "wsl --install returned help text instead of executing."
		Write-Host "This indicates WSL features may not be enabled yet."
		Write-Host "The Test-WSLStatus function should have handled feature enablement."
		Write-Host "Please restart your computer and run this script again."
		return
	}

	if ($LASTEXITCODE -eq 0) {
		Write-Host "WSL installation completed successfully!" -ForegroundColor Green
		Write-Host ""
		Write-Host "This command has:" -ForegroundColor White
		Write-Host "- Enabled the required optional components" -ForegroundColor White
		Write-Host "- Downloaded the latest Linux kernel" -ForegroundColor White
		Write-Host "- Set WSL 2 as the default" -ForegroundColor White
		Write-Host "- Installed Ubuntu Linux distribution" -ForegroundColor White
		Write-Host ""
		Write-Host "Please restart your computer to complete the installation." -ForegroundColor Yellow
		Write-Host "After restart, you can run 'wsl' to start using Linux." -ForegroundColor Green
	}
	else {
		Write-Error "wsl --install failed with exit code $LASTEXITCODE"
		Write-Host "Captured output:" -ForegroundColor Gray
		Write-Host $installOutput -ForegroundColor Gray
		Write-Host ""
		Write-Host "You may need to:" -ForegroundColor Yellow
		Write-Host "1. Ensure you're running as Administrator" -ForegroundColor White
		Write-Host "2. Check that your Windows Server 2022 is updated" -ForegroundColor White
		Write-Host "3. Restart your computer if features were just enabled" -ForegroundColor White
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
		Write-Host "Enabling/Verifying WSL..."
		# Call existing function to check and enable WSL, then ensure WSL2 is set up.
		Test-WSLStatus
		Enable-WSL2
		Write-Host "WSL setup completed successfully."
		exit 0 # Exit script after successful action
	}
	"2" = {
		Write-Host "Enabling Hyper-V..."
		try {
			# Check if the Hyper-V feature is already enabled.
			$hyperVFeature = Get-WindowsOptionalFeature -Online -FeatureName Microsoft-Hyper-V-All -ErrorAction SilentlyContinue
			if ($hyperVFeature -and $hyperVFeature.State -eq "Enabled") {
				Write-Host "Hyper-V is already enabled."
			}
			else {
				Write-Host "Hyper-V is not enabled. Enabling the Microsoft-Hyper-V-All feature..."
				Enable-WindowsOptionalFeature -Online -FeatureName Microsoft-Hyper-V-All -All -NoRestart | Out-Null
				if ($?) {
					Write-Host "Hyper-V has been enabled. A system restart may be required."
				}
				else {
					Write-Error "Failed to enable Hyper-V."
					exit 1 # Exit script on error
				}
			}
			Write-Host "Hyper-V setup completed successfully."
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
Write-Host "Exited without making a selection."
