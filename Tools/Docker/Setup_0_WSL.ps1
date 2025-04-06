################################################################################
# File         : Setup_0_WSL.ps1
# Description  : Contains WSL helper functions for setup scripts:
#                - Check-WSLStatus: Verify WSL installation and required features.
# Usage        : Dot-source this script in other setup scripts:
#                . "$PSScriptRoot\Setup_0_WSL.ps1"
################################################################################

#==============================================================================
# Function: Test-WSLStatus
#==============================================================================
<#
.SYNOPSIS
	Verifies WSL installation status, version, and required Windows features.
.DESCRIPTION
	Checks if the 'wsl.exe' command is available.
	Checks if the default WSL version is 2, prompting to set it if not.
	Checks if the 'Microsoft-Windows-Subsystem-Linux' Windows feature is enabled, prompting to enable it if not.
	Checks if the 'VirtualMachinePlatform' Windows feature is enabled, prompting to enable it if not.
	Exits the script if requirements are not met or if the user declines to enable features.
.EXAMPLE
	Test-WSLStatus
	# Script continues if WSL is correctly configured, otherwise exits or prompts.
.NOTES
	Uses wsl.exe, Get-WindowsOptionalFeature, and dism.exe.
	Requires administrative privileges to enable features or set the default WSL version.
	Uses Write-Host for status messages and Write-Warning/Error for issues.
	User interaction handled via Read-Host.
#>
function Test-WSLStatus {
	# Use Write-Host for status messages
	Write-Host "Verifying WSL installation and required service status..."

	# Check if the wsl command is available
	if (!(Get-Command wsl -ErrorAction SilentlyContinue)) {
		Write-Error "WSL (wsl.exe) is not available. Please install Windows Subsystem for Linux."
		exit 1
	}

	# Check WSL version - we need WSL2
	$wslVersionInfo = wsl --version 2>&1
	# Use Write-Host for status messages
	Write-Host "WSL Version Info:`n$wslVersionInfo"

	# Check if running WSL 2
	$wslVersion = wsl --status | Select-String -Pattern "Default Version: (\d+)" | ForEach-Object { $_.Matches.Groups[1].Value }
	if ($wslVersion -ne "2") {
		Write-Warning "WSL seems to be running version $wslVersion but WSL 2 is required."
		Write-Warning "Please run 'wsl --set-default-version 2' as Administrator to set WSL 2 as default."
		$setWsl2 = Read-Host "Would you like to set WSL 2 as default now? (Y/N, default is Y)"
		if ($setWsl2 -ne "N") {
			wsl --set-default-version 2
			if ($LASTEXITCODE -ne 0) {
				Write-Error "Failed to set WSL 2 as default. Please do this manually."
				exit 1
			}
			# Use Write-Host for status messages
			Write-Host "WSL 2 has been set as the default."
		}
		else {
			Write-Error "WSL 2 is required but not set as default. Exiting."
			exit 1
		}
	}

	# Check if the Windows Subsystem for Linux feature is enabled
	$wslFeature = Get-WindowsOptionalFeature -Online -FeatureName Microsoft-Windows-Subsystem-Linux
	if ($wslFeature.State -ne "Enabled") {
		Write-Warning "The Microsoft-Windows-Subsystem-Linux feature is not enabled."
		$choice = Read-Host "Do you want to enable it automatically? (Y/N)"
		if ($choice -and $choice.ToUpper() -eq "Y") {
			# Use Write-Host for status messages
			Write-Host "Enabling WSL feature..."
			dism.exe /Online /Enable-Feature /FeatureName:Microsoft-Windows-Subsystem-Linux /All /NoRestart | Out-Null
			# Use Write-Host for status messages
			Write-Host "WSL feature enabled. A system restart may be required to activate changes."
		}
		else {
			Write-Error "The Microsoft-Windows-Subsystem-Linux feature is required. Exiting."
			exit 1
		}
	}

	# Check if the Virtual Machine Platform feature is enabled
	$vmFeature = Get-WindowsOptionalFeature -Online -FeatureName VirtualMachinePlatform
	if ($vmFeature.State -ne "Enabled") {
		Write-Warning "The VirtualMachinePlatform feature is not enabled."
		$choice = Read-Host "Do you want to enable it automatically? (Y/N)"
		if ($choice -and $choice.ToUpper() -eq "Y") {
			# Use Write-Host for status messages
			Write-Host "Enabling VirtualMachinePlatform feature..."
			dism.exe /Online /Enable-Feature /FeatureName:VirtualMachinePlatform /All /NoRestart | Out-Null
			# Use Write-Host for status messages
			Write-Host "VirtualMachinePlatform feature enabled. A system restart may be required to activate changes."
		}
		else {
			Write-Error "The VirtualMachinePlatform feature is required. Exiting."
			exit 1
		}
	}

	# Use Write-Host for status messages
	Write-Host "WSL and required Windows features are enabled."
}
