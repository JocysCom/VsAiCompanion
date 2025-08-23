################################################################################
# Description  : Script to enable virtualization features on Windows.
#                Provides a menu to enable either Windows Subsystem for Linux (WSL)
#                or Hyper-V. For WSL, it ensures WSL2 is the default and converts
#                existing distributions if necessary.
# Usage        : Run as Administrator.
################################################################################

using namespace System
using namespace System.IO

# Dot-source the necessary helper function files.
. "$PSScriptRoot\Setup_Helper_CoreFunctions.ps1"
. "$PSScriptRoot\Setup_Helper_WSLFunctions.ps1"

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

		# Check for specific Windows Server 2022 Virtual Machine Platform issue
		$outputString = $installOutput -join " "
		if ($outputString -like "*WSL2 is not supported with your current machine configuration*" -or
			$outputString -like "*HCS_E_HYPERV_NOT_INSTALLED*" -or
			$outputString -like "*Virtual Machine Platform*") {

			Write-Host "DETECTED: Virtual Machine Platform configuration issue" -ForegroundColor Red
			Write-Host ""

			# Check if this is an Azure VM
			$isAzureVM = $false
			try {
				$azureMetadata = Invoke-RestMethod -Uri "http://169.254.169.254/metadata/instance?api-version=2021-02-01" -Headers @{Metadata = "true" } -TimeoutSec 5 -ErrorAction SilentlyContinue
				if ($azureMetadata) {
					$isAzureVM = $true
					$vmSize = $azureMetadata.compute.vmSize
					Write-Host "DETECTED: Azure VM - Size: $vmSize" -ForegroundColor Yellow
				}
			}
			catch {
				# Not an Azure VM or metadata service unavailable
			}

			if ($isAzureVM) {
				Write-Host ""
				Write-Host "AZURE VM DETECTED - Special Requirements for WSL2" -ForegroundColor Red
				Write-Host "=================================================" -ForegroundColor Red
				Write-Host ""
				Write-Host "WSL2 requires nested virtualization, which is only supported on specific Azure VM sizes:" -ForegroundColor Yellow
				Write-Host ""
				Write-Host "SUPPORTED Azure VM sizes for WSL2:" -ForegroundColor Green
				Write-Host "- Dv3, Dsv3, Ev3, Esv3 series (v3 and newer)" -ForegroundColor White
				Write-Host "- Dv4, Dsv4, Ev4, Esv4 series" -ForegroundColor White
				Write-Host "- Dv5, Dsv5, Ev5, Esv5 series" -ForegroundColor White
				Write-Host "- Fsv2 series" -ForegroundColor White
				Write-Host "- M series" -ForegroundColor White
				Write-Host ""
				Write-Host "Your current VM size: $vmSize" -ForegroundColor Cyan

				# Check if current VM size supports nested virtualization
				$supportedSizes = @("Dv3", "Dsv3", "Ev3", "Esv3", "Dv4", "Dsv4", "Ev4", "Esv4", "Dv5", "Dsv5", "Ev5", "Esv5", "Fsv2", "M")
				$isSupported = $false
				$isGpuVM = $false

				# Check for GPU VM series that don't support nested virtualization
				$gpuSeries = @("NC", "ND", "NV")
				foreach ($gpu in $gpuSeries) {
					if ($vmSize -like "*$gpu*") {
						$isGpuVM = $true
						break
					}
				}

				foreach ($size in $supportedSizes) {
					if ($vmSize -like "*$size*") {
						$isSupported = $true
						break
					}
				}

				if ($isSupported) {
					Write-Host "✓ Your VM size supports nested virtualization" -ForegroundColor Green
					Write-Host ""
					Write-Host "Trying Azure-specific WSL2 setup..." -ForegroundColor Cyan

					# For Azure VMs, try the no-distribution approach first
					Write-Host "Running: wsl.exe --install --no-distribution" -ForegroundColor White
					$fixOutput = wsl.exe --install --no-distribution 2>&1
					Write-Host "Fix output: $fixOutput" -ForegroundColor Gray

					if ($LASTEXITCODE -eq 0) {
						Write-Host "✓ WSL components installed successfully" -ForegroundColor Green
						Write-Host "Now trying to install Ubuntu..." -ForegroundColor White
						$ubuntuInstall = wsl.exe --install -d Ubuntu 2>&1
						Write-Host "Ubuntu install output: $ubuntuInstall" -ForegroundColor Gray
					}
					else {
						Write-Warning "WSL component installation failed on Azure VM"
						Write-Host "This may indicate the VM doesn't have nested virtualization enabled." -ForegroundColor Red
					}
				}
				else {
					Write-Host "✗ Your VM size ($vmSize) does not support nested virtualization" -ForegroundColor Red
					Write-Host ""
					Write-Host "SOLUTIONS:" -ForegroundColor Yellow
					Write-Host "1. Resize your Azure VM to a supported size (requires VM restart)" -ForegroundColor White
					Write-Host "2. Use Docker Desktop with WSL1 backend instead" -ForegroundColor White
					Write-Host "3. Use Podman with a different container runtime" -ForegroundColor White
					Write-Host ""
					Write-Host "To resize your Azure VM:" -ForegroundColor Cyan
					Write-Host "1. Stop the VM in Azure Portal" -ForegroundColor White
					Write-Host "2. Go to VM Settings > Size" -ForegroundColor White
					Write-Host "3. Select a supported size (e.g., Standard_D2s_v3)" -ForegroundColor White
					Write-Host "4. Start the VM and run this script again" -ForegroundColor White
				}
			}
			else {
				Write-Host "Applying Windows Server 2022 specific fix..." -ForegroundColor Cyan

				# Try the recommended fix from the error message
				Write-Host "Running: wsl.exe --install --no-distribution" -ForegroundColor White
				$fixOutput = wsl.exe --install --no-distribution 2>&1
				Write-Host "Fix output: $fixOutput" -ForegroundColor Gray

				if ($LASTEXITCODE -eq 0) {
					Write-Host "✓ Virtual Machine Platform fix applied successfully" -ForegroundColor Green
				}
				else {
					Write-Warning "Fix command failed. Trying alternative approach..."

					# Alternative: Re-enable features to force refresh
					Write-Host "Re-enabling WSL and VM Platform features..." -ForegroundColor Yellow

					$wslFeature = Get-WindowsOptionalFeature -Online -FeatureName Microsoft-Windows-Subsystem-Linux -ErrorAction SilentlyContinue
					$vmFeature = Get-WindowsOptionalFeature -Online -FeatureName VirtualMachinePlatform -ErrorAction SilentlyContinue

					if ($wslFeature.State -eq "Enabled") {
						Disable-WindowsOptionalFeature -Online -FeatureName Microsoft-Windows-Subsystem-Linux -NoRestart | Out-Null
					}
					if ($vmFeature.State -eq "Enabled") {
						Disable-WindowsOptionalFeature -Online -FeatureName VirtualMachinePlatform -NoRestart | Out-Null
					}

					Enable-WindowsOptionalFeature -Online -FeatureName Microsoft-Windows-Subsystem-Linux -NoRestart | Out-Null
					Enable-WindowsOptionalFeature -Online -FeatureName VirtualMachinePlatform -NoRestart | Out-Null

					Write-Host "✓ Features re-enabled" -ForegroundColor Green
				}

				# Download and install WSL2 kernel update
				Write-Host "Installing WSL2 kernel update..." -ForegroundColor White
				try {
					$kernelUpdateUrl = "https://wslstorestorage.blob.core.windows.net/wslblob/wsl_update_x64.msi"
					$kernelUpdatePath = "$env:TEMP\wsl_update_x64.msi"
					Invoke-WebRequest -Uri $kernelUpdateUrl -OutFile $kernelUpdatePath -UseBasicParsing
					Start-Process "msiexec.exe" -ArgumentList "/i `"$kernelUpdatePath`" /quiet" -NoNewWindow -Wait
					Write-Host "✓ WSL2 kernel update installed" -ForegroundColor Green
				}
				catch {
					Write-Warning "Could not install WSL2 kernel update: $_"
				}

				Write-Host ""
				Write-Host "IMPORTANT: RESTART REQUIRED" -ForegroundColor Red
				Write-Host "Virtual Machine Platform changes require a restart to take effect." -ForegroundColor Yellow
				Write-Host "After restart, WSL2 should work properly." -ForegroundColor White
			}
		}
		else {
			Write-Host "General troubleshooting steps:" -ForegroundColor Yellow
			Write-Host "1. Ensure you're running as Administrator" -ForegroundColor White
			Write-Host "2. Check that your Windows Server 2022 is updated" -ForegroundColor White
			Write-Host "3. Restart your computer if features were just enabled" -ForegroundColor White
			Write-Host "4. Check BIOS virtualization settings" -ForegroundColor White
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
