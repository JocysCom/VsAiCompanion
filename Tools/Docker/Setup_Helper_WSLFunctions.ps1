################################################################################
# File         : Setup_0_WSL.ps1
# Description  : Contains WSL helper functions for setup scripts:
#                - Check-WSLStatus: Verify WSL installation and required features.
################################################################################

#==============================================================================
# Function: Test-WSLStatus
#==============================================================================
<#
.SYNOPSIS
	Verifies WSL installation status, version, and required Windows features.
.DESCRIPTION
	For Windows Server 2022, uses the official Microsoft approach with 'wsl --install'.
	Avoids using Application version WSL commands like --version and --status which
	are not available in the inbox version of WSL on Windows Server 2022.
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

	# For Windows Server 2022, avoid using Application version WSL commands
	# Instead, check if features are enabled and use wsl --install approach

	# Check if the Windows Subsystem for Linux feature is enabled
	$wslFeature = Get-WindowsOptionalFeature -Online -FeatureName Microsoft-Windows-Subsystem-Linux -ErrorAction SilentlyContinue
	$vmFeature = Get-WindowsOptionalFeature -Online -FeatureName VirtualMachinePlatform -ErrorAction SilentlyContinue

	$featuresEnabled = ($wslFeature -and $wslFeature.State -eq "Enabled") -and ($vmFeature -and $vmFeature.State -eq "Enabled")

	if (-not $featuresEnabled) {
		Write-Warning "WSL features are not enabled."
		Write-Host "For Windows Server 2022, the recommended approach is to use 'wsl --install'."
		$runInstall = Read-Host "Would you like to run 'wsl --install' now? (Y/N, default is Y)"
		if ($runInstall -ne "N") {
			Write-Host "Running 'wsl --install'..." -ForegroundColor Cyan
			Write-Host "Installation output:" -ForegroundColor White
			Write-Host "==================" -ForegroundColor White

			# Run wsl --install and show output in real-time
			try {
				$installOutput = wsl --install
				Write-Host "Exit Code: $LASTEXITCODE" -ForegroundColor Gray
			}
			catch {
				Write-Error "Failed to execute wsl --install: $_"
				Write-Host "Falling back to manual feature enablement..."
			}

			# Also capture output for analysis
			$installOutputCapture = wsl --install 2>&1
			$installString = $installOutputCapture -join " "

			# Check if install command returned help text (indicates compatibility issues)
			if ($installString -like "*Copyright (c) Microsoft Corporation*" -or $installString -like "*Usage: wsl.exe*") {
				Write-Host "==================" -ForegroundColor White
				Write-Warning "wsl --install returned help text. Falling back to manual feature enablement."
				Write-Host "Enabling WSL and Virtual Machine Platform features manually..."

				if (-not $wslFeature -or $wslFeature.State -ne "Enabled") {
					Write-Host "Enabling WSL feature..."
					Enable-WindowsOptionalFeature -Online -FeatureName Microsoft-Windows-Subsystem-Linux -NoRestart | Out-Null
				}

				if (-not $vmFeature -or $vmFeature.State -ne "Enabled") {
					Write-Host "Enabling Virtual Machine Platform feature..."
					Enable-WindowsOptionalFeature -Online -FeatureName VirtualMachinePlatform -NoRestart | Out-Null
				}

				Write-Host "Features enabled. Please restart your computer and run this script again."
				Write-Host "After restart, you can try 'wsl --install' or the script should work properly."
				exit 0
			}

			Write-Host "==================" -ForegroundColor White
			if ($LASTEXITCODE -eq 0) {
				Write-Host "WSL installation initiated successfully." -ForegroundColor Green
				Write-Host "This has enabled features, downloaded the kernel, set WSL2 as default, and installed Ubuntu."
				Write-Host "Please restart your computer to complete the installation." -ForegroundColor Yellow
				exit 0
			}
			else {
				Write-Error "wsl --install failed with exit code $LASTEXITCODE"
				Write-Host "Captured output: $installOutputCapture" -ForegroundColor Gray
				Write-Host "Falling back to manual feature enablement..."
			}
		}

		# Manual feature enablement fallback
		if (-not $wslFeature -or $wslFeature.State -ne "Enabled") {
			Write-Warning "The Microsoft-Windows-Subsystem-Linux feature is not enabled."
			$choice = Read-Host "Do you want to enable it automatically? (Y/N)"
			if ($choice -and $choice.ToUpper() -eq "Y") {
				Write-Host "Enabling WSL feature..."
				Enable-WindowsOptionalFeature -Online -FeatureName Microsoft-Windows-Subsystem-Linux -NoRestart | Out-Null
				Write-Host "WSL feature enabled."
			}
			else {
				Write-Error "The Microsoft-Windows-Subsystem-Linux feature is required. Exiting."
				exit 1
			}
		}

		if (-not $vmFeature -or $vmFeature.State -ne "Enabled") {
			Write-Warning "The VirtualMachinePlatform feature is not enabled."
			$choice = Read-Host "Do you want to enable it automatically? (Y/N)"
			if ($choice -and $choice.ToUpper() -eq "Y") {
				Write-Host "Enabling VirtualMachinePlatform feature..."
				Enable-WindowsOptionalFeature -Online -FeatureName VirtualMachinePlatform -NoRestart | Out-Null
				Write-Host "VirtualMachinePlatform feature enabled."
			}
			else {
				Write-Error "The VirtualMachinePlatform feature is required. Exiting."
				exit 1
			}
		}

		Write-Host "Features have been enabled. A system restart is required to activate changes."
		Write-Host "Please restart your computer and run this script again."
		exit 0
	}

	# If we get here, features are enabled
	Write-Host "WSL and required Windows features are enabled."

	# Test if WSL is working by trying a simple command that should work on both versions
	Write-Host "Testing WSL functionality..."
	$testOutput = wsl --help 2>&1
	$testString = $testOutput -join " "

	if ($testString -like "*Copyright (c) Microsoft Corporation*" -and $testString -like "*Usage: wsl.exe*") {
		Write-Host "WSL help command is working properly."
	}
	else {
		Write-Warning "WSL may not be functioning correctly. Output: $testOutput"
	}
}
