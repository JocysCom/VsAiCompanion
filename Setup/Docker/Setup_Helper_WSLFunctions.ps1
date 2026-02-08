################################################################################
# File         : Setup_Helper_WSLFunctions.ps1
# Description  : Contains WSL helper functions for setup scripts:
#                - Test-WSLStatus: Verify WSL installation and required features.
#                - Get-WSLNetworkingMode: Read the current networkingMode from .wslconfig.
#                - Set-WSLMirroredNetworking: Configure WSL2 mirrored networking in .wslconfig.
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
				wsl --install | Out-Null
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
	# Clean null characters from WSL output before string comparison
	$testString = ($testOutput -join " ") -replace '\x00', ''

	if ($testString -like "*Copyright (c) Microsoft Corporation*" -and $testString -like "*Usage: wsl.exe*") {
		Write-Host "WSL help command is working properly."
	}
	else {
		Write-Warning "WSL may not be functioning correctly. Output: $testString"
	}
}

#==============================================================================
# Function: Get-WSLNetworkingMode
#==============================================================================
<#
.SYNOPSIS
	Reads the current WSL2 networkingMode from the user's .wslconfig file.
.DESCRIPTION
	Parses %UserProfile%\.wslconfig for the [wsl2] section and returns the value
	of the networkingMode setting. Returns 'nat' (the WSL2 default) when the file
	or setting does not exist.
.OUTPUTS
	[string] The current networking mode ('nat', 'mirrored', or other configured value).
.EXAMPLE
	$mode = Get-WSLNetworkingMode
	Write-Host "Current WSL2 networking mode: $mode"
#>
function Get-WSLNetworkingMode {
	[CmdletBinding()]
	[OutputType([string])]
	param()

	$wslConfigPath = Join-Path $env:USERPROFILE ".wslconfig"
	if (-not (Test-Path $wslConfigPath)) {
		return "nat"
	}

	$lines = Get-Content -Path $wslConfigPath -ErrorAction SilentlyContinue
	if (-not $lines) {
		return "nat"
	}

	$inWsl2Section = $false
	foreach ($line in $lines) {
		$trimmed = $line.Trim()
		if ($trimmed -match '^\[(.+)\]$') {
			$inWsl2Section = ($Matches[1] -eq 'wsl2')
			continue
		}
		if ($inWsl2Section -and $trimmed -match '^networkingMode\s*=\s*(.+)$') {
			return $Matches[1].Trim()
		}
	}

	return "nat"
}

#==============================================================================
# Function: Set-WSLMirroredNetworking
#==============================================================================
<#
.SYNOPSIS
	Configures WSL2 mirrored networking mode in the user's .wslconfig file.
.DESCRIPTION
	Creates or updates %UserProfile%\.wslconfig to set networkingMode=mirrored,
	dnsTunneling=true, and autoProxy=true under the [wsl2] section. Preserves
	all existing settings in the file (e.g., kernelCommandLine for cgroups).

	Mirrored networking mode (available since WSL 2.0.0 on Windows 11 22H2+)
	makes WSL2 share the host's network interfaces directly. This ensures:
	- WSL2 traffic uses the same external IP address as the Windows host
	- VPN connections on the host are automatically available inside WSL2
	- DNS resolution uses the same DNS servers as the host
	- Corporate proxy settings are inherited automatically

	This is essential for corporate environments where firewalls whitelist
	traffic by source IP address.
.PARAMETER Force
	Skip the confirmation prompt and apply changes immediately.
.OUTPUTS
	[bool] Returns $true if changes were applied (or already correct), $false if
	the user declined or an error occurred.
.EXAMPLE
	Set-WSLMirroredNetworking
.EXAMPLE
	Set-WSLMirroredNetworking -Force
.NOTES
	Requires a WSL restart for changes to take effect. Use
	Setup_Util_RestartPodmanAndWSL.ps1 or 'wsl --shutdown' after applying.
#>
function Set-WSLMirroredNetworking {
	[CmdletBinding(SupportsShouldProcess = $true)]
	[OutputType([bool])]
	param(
		[Parameter(Mandatory = $false)]
		[switch]$Force
	)

	$wslConfigPath = Join-Path $env:USERPROFILE ".wslconfig"
	$currentMode = Get-WSLNetworkingMode

	Write-Host ""
	Write-Host "==================== WSL2 Network Configuration ===================="
	Write-Host "Config file: $wslConfigPath"
	Write-Host "Current networking mode: $currentMode"

	if ($currentMode -eq "mirrored") {
		Write-Host "WSL2 is already configured for mirrored networking." -ForegroundColor Green
		Write-Host "=================================================================="
		return $true
	}

	Write-Host ""
	Write-Host "MIRRORED NETWORKING MODE" -ForegroundColor Cyan
	Write-Host "========================" -ForegroundColor Cyan
	Write-Host "Mirrored mode makes WSL2 share the host's network interfaces."
	Write-Host "Benefits:" -ForegroundColor White
	Write-Host "  - WSL2/Podman traffic uses the SAME external IP as Windows" -ForegroundColor White
	Write-Host "  - VPN connections are automatically available inside WSL2" -ForegroundColor White
	Write-Host "  - DNS resolves the same corporate hostnames" -ForegroundColor White
	Write-Host "  - Corporate proxy settings are inherited" -ForegroundColor White
	Write-Host ""
	Write-Host "This is required for corporate environments where firewalls" -ForegroundColor Yellow
	Write-Host "whitelist traffic by source IP address." -ForegroundColor Yellow
	Write-Host ""

	if (-not $Force) {
		Write-Host "WARNING: This will modify $wslConfigPath" -ForegroundColor Yellow
		Write-Host "A WSL restart is required after this change." -ForegroundColor Yellow
		$confirm = Read-Host "Configure WSL2 mirrored networking? (Y/N, default is Y)"
		if ($confirm -eq "N") {
			Write-Host "Skipped. No changes made."
			Write-Host "=================================================================="
			return $false
		}
	}

	if (-not $PSCmdlet.ShouldProcess($wslConfigPath, "Set WSL2 networkingMode=mirrored")) {
		return $false
	}

	$desiredSettings = @{
		"networkingMode"   = "mirrored"
		"dnsTunneling"     = "true"
		"autoProxy"        = "true"
	}

	if (Test-Path $wslConfigPath) {
		$lines = Get-Content -Path $wslConfigPath
	}
	else {
		$lines = @()
	}

	$inWsl2Section = $false
	$wsl2SectionFound = $false
	$wsl2SectionEnd = -1
	$existingKeys = @{}

	for ($i = 0; $i -lt $lines.Count; $i++) {
		$trimmed = $lines[$i].Trim()
		if ($trimmed -match '^\[(.+)\]$') {
			if ($inWsl2Section) {
				$wsl2SectionEnd = $i - 1
			}
			$inWsl2Section = ($Matches[1] -eq 'wsl2')
				if ($inWsl2Section) {
					$wsl2SectionFound = $true
				}
			continue
		}
		if ($inWsl2Section -and $trimmed -match '^([^=]+?)\s*=\s*(.*)$') {
			$existingKeys[$Matches[1].Trim()] = $i
		}
	}
	if ($inWsl2Section -and $wsl2SectionEnd -eq -1) {
		$wsl2SectionEnd = $lines.Count - 1
	}

	$outputLines = [System.Collections.ArrayList]::new()
	foreach ($line in $lines) {
		$null = $outputLines.Add($line)
	}

	if (-not $wsl2SectionFound) {
		if ($outputLines.Count -gt 0) {
			$null = $outputLines.Add("")
		}
		$null = $outputLines.Add("[wsl2]")
		foreach ($key in $desiredSettings.Keys) {
			$null = $outputLines.Add("$key=$($desiredSettings[$key])")
		}
	}
	else {
		foreach ($key in $desiredSettings.Keys) {
			$value = $desiredSettings[$key]
			if ($existingKeys.ContainsKey($key)) {
				$lineIndex = $existingKeys[$key]
				$outputLines[$lineIndex] = "$key=$value"
			}
			else {
				$insertAt = $wsl2SectionEnd + 1
				$outputLines.Insert($insertAt, "$key=$value")
				$wsl2SectionEnd++
				foreach ($k in @($existingKeys.Keys)) {
					if ($existingKeys[$k] -ge $insertAt) {
						$existingKeys[$k]++
					}
				}
			}
		}
	}

	Set-Content -Path $wslConfigPath -Value $outputLines -Encoding UTF8
	Write-Host ""
	Write-Host "Updated $wslConfigPath with mirrored networking settings:" -ForegroundColor Green
	foreach ($key in $desiredSettings.Keys) {
		Write-Host "  $key = $($desiredSettings[$key])" -ForegroundColor White
	}
	Write-Host ""
	Write-Host "IMPORTANT: A WSL restart is required for changes to take effect." -ForegroundColor Yellow
	Write-Host "Run: wsl --shutdown" -ForegroundColor Yellow
	Write-Host "Then restart Podman: podman machine start" -ForegroundColor Yellow
	Write-Host "Or use: .\Setup_Util_RestartPodmanAndWSL.ps1 -FullShutdown" -ForegroundColor Yellow
	Write-Host "=================================================================="
	return $true
}
