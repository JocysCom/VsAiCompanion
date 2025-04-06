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

#==============================================================================
# Function: Test-AdminPrivilege
#==============================================================================
<#
.SYNOPSIS
	Verify administrator privileges and exit if not elevated.
.DESCRIPTION
	Checks if the current user has administrator privileges. If not, it writes an error
	and exits the script with status code 1.
.EXAMPLE
	Test-AdminPrivilege
	# Script continues if elevated, otherwise exits.
.NOTES
	Uses [Security.Principal.WindowsPrincipal] and IsInRole.
#>
function Test-AdminPrivilege {
	if (-NOT ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
		Write-Error "Administrator privileges required. Please run this script as an Administrator."
		exit 1
	}
}

#==============================================================================
# Function: Set-ScriptLocation
#==============================================================================
<#
.SYNOPSIS
	Sets the script's working directory to the directory containing the script.
.DESCRIPTION
	Determines the script's parent directory using $PSScriptRoot or $MyInvocation.MyCommand.Path
	and changes the current location to that directory using Set-Location.
	Supports -WhatIf via CmdletBinding.
.EXAMPLE
	Set-ScriptLocation
	# Current directory is now the script's directory.
.NOTES
	Handles cases where $PSScriptRoot might be empty.
#>
function Set-ScriptLocation {
	[CmdletBinding(SupportsShouldProcess = $true)]
	param()

	if ($PSScriptRoot -and $PSScriptRoot -ne "") {
		$scriptPath = $PSScriptRoot
	}
	else {
		$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
	}
	if ($scriptPath) {
		if ($PSCmdlet.ShouldProcess($scriptPath, "Set Location")) {
			Set-Location $scriptPath
			# Use Write-Host for status messages
			Write-Host "Script Path set to: $scriptPath"
		}
	}
	else {
		# Use Write-Host for status messages
		Write-Host "Script Path not found. Current directory remains unchanged."
	}
}

#==============================================================================
# Function: Invoke-DownloadFile
#==============================================================================
<#
.SYNOPSIS
	Downloads a file from a URL, preferring BITS transfer with a fallback to Invoke-WebRequest.
.DESCRIPTION
	Downloads a file specified by -SourceUrl to the -DestinationPath.
	Uses Start-BitsTransfer if available and not overridden by -UseFallback.
	Falls back to Invoke-WebRequest if BITS fails or is unavailable.
	Skips download if the destination file exists and -ForceDownload is not specified.
.PARAMETER SourceUrl
	The URL of the file to download. Alias: -url.
.PARAMETER DestinationPath
	The local path where the file should be saved.
.PARAMETER ForceDownload
	Switch parameter. If present, forces the download even if the destination file exists.
.PARAMETER UseFallback
	Switch parameter. If present, forces the use of Invoke-WebRequest instead of Start-BitsTransfer.
.EXAMPLE
	Invoke-DownloadFile -SourceUrl "http://example.com/file.zip" -DestinationPath "C:\temp\file.zip"
.EXAMPLE
	Invoke-DownloadFile -url "http://example.com/file.zip" -DestinationPath "C:\temp\file.zip" -ForceDownload -UseFallback
.NOTES
	Temporarily sets $ProgressPreference to 'SilentlyContinue' for Invoke-WebRequest to improve speed.
#>
function Invoke-DownloadFile {
	[CmdletBinding()]
	param(
		[Parameter(Mandatory = $true)]
		[Alias("url")]
		[string]$SourceUrl,
		[Parameter(Mandatory = $true)]
		[string]$DestinationPath,
		[switch]$ForceDownload, # Optional switch to force re-download
		[switch]$UseFallback
	)

	if ((Test-Path $DestinationPath) -and (-not $ForceDownload)) {
		# Use Write-Host for status messages
		Write-Host "File already exists at $DestinationPath. Skipping download."
		return
	}

	# Check if BITS is available or if fallback is requested
	if ((Get-Command Start-BitsTransfer -ErrorAction SilentlyContinue) -and (-not $UseFallback)) {
		# Use Write-Host for status messages
		Write-Host "Downloading file from $SourceUrl to $DestinationPath using Start-BitsTransfer..."
		try {
			Start-BitsTransfer -Source $SourceUrl -Destination $DestinationPath
			# Use Write-Host for status messages
			Write-Host "Download succeeded: $DestinationPath"
			return
		}
		catch {
			Write-Warning "BITS transfer failed: $_. Trying fallback method..."
		}
	}

	# Fallback to Invoke-WebRequest
	try {
		# Use Write-Host for status messages
		Write-Host "Downloading file from $SourceUrl to $DestinationPath using Invoke-WebRequest..."
		$ProgressPreference = 'SilentlyContinue'  # Speeds up Invoke-WebRequest significantly
		Invoke-WebRequest -Uri $SourceUrl -OutFile $DestinationPath -UseBasicParsing
		$ProgressPreference = 'Continue'  # Restore default
		# Use Write-Host for status messages
		Write-Host "Download succeeded: $DestinationPath"
	}
	catch {
		Write-Error "Failed to download file from $SourceUrl. Error details: $_"
		exit 1
	}
}

#==============================================================================
# Function: Test-GitInstallation
#==============================================================================
<#
.SYNOPSIS
	Checks if Git is available in the PATH and attempts to add it from common Visual Studio locations if not found.
.DESCRIPTION
	Verifies if the 'git' command can be resolved using Get-Command.
	If not found, it checks predefined paths within typical Visual Studio installations.
	If found in one of these paths, it appends that path to the current session's $env:Path.
	If Git still cannot be found, it writes an error and exits the script.
.EXAMPLE
	Test-GitInstallation
	# Script continues if Git is found or added, otherwise exits.
.NOTES
	The list of predefined paths might need updating for different VS versions or installations.
#>
function Test-GitInstallation {
	if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
		# Use Write-Host for status messages
		Write-Host "Git command not found in PATH. Attempting to locate Git via common installation paths..."
		$possibleGitPaths = @(
			"C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Git\cmd",
			"C:\Program Files\Microsoft Visual Studio\2022\Professional\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Git\cmd"
		)
		foreach ($path in $possibleGitPaths) {
			if (Test-Path $path) {
				$env:Path += ";" + $path
				# Use Write-Host for status messages
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

#==============================================================================
# Function: Test-ApplicationInstalled
#==============================================================================
<#
.SYNOPSIS
	Determines whether a specified application is installed by checking the registry and Get-Package.
.DESCRIPTION
	Checks standard Uninstall registry keys (HKLM, HKLM WOW6432Node, HKCU) for display names matching the AppName (with wildcards).
	If not found in the registry, it attempts to use Get-Package as a fallback.
.PARAMETER AppName
	The application name to search for (supports wildcards like '*AppName*'). Mandatory.
.EXAMPLE
	if (Test-ApplicationInstalled -AppName "Docker Desktop") { Write-Host "Docker is installed." }
.EXAMPLE
	$isVSCodeInstalled = Test-ApplicationInstalled -AppName "*Visual Studio Code*"
.NOTES
	Prioritizes registry check for performance.
	Get-Package check is used as a fallback and might fail depending on execution policy or module availability.
	Returns $true if found, $false otherwise.
#>
function Test-ApplicationInstalled {
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
		Write-Warning "Get-Package check failed for '$AppName': $_"
	}

	# Not found by any method
	return $false
}

#==============================================================================
# Function: Update-EnvironmentVariable
#==============================================================================
<#
.SYNOPSIS
	Refreshes the current session's PATH environment variable from registry values.
.DESCRIPTION
	Re-reads the machine and user PATH environment variables directly from the registry using
	[System.Environment]::GetEnvironmentVariable() and concatenates them to update the
	current PowerShell session's $env:PATH. This allows newly installed applications added
	to the system PATH to be recognized without restarting the PowerShell session.
	Supports -WhatIf via CmdletBinding.
.EXAMPLE
	Update-EnvironmentVariable
	# The $env:PATH in the current session is updated.
#>
function Update-EnvironmentVariable {
	[CmdletBinding(SupportsShouldProcess = $true)]
	param()


	# Check if the action should be performed
	if ($PSCmdlet.ShouldProcess("current session environment variables", "Update PATH")) {
		$machinePath = [System.Environment]::GetEnvironmentVariable("PATH", [System.EnvironmentVariableTarget]::Machine)
		$userPath = [System.Environment]::GetEnvironmentVariable("PATH", [System.EnvironmentVariableTarget]::User)
		$env:PATH = "$machinePath;$userPath"
		# Use Write-Host for status messages
		Write-Host "Environment variables refreshed. Current PATH:"
		Write-Host $env:PATH
	}
	else {
		Write-Host "Skipped refreshing environment variables due to ShouldProcess."
	}
}

#==============================================================================
# Function: Invoke-MenuLoop
#==============================================================================
<#
.SYNOPSIS
	Provides a generic, reusable menu loop structure.
.DESCRIPTION
	Displays a menu with a title and options derived from an ordered hashtable, prompts the user
	for input, and executes a corresponding action script block based on a provided mapping.
	The loop continues until the user enters the specified exit choice.
.PARAMETER MenuTitle
	The title to display above the menu options. Mandatory.
.PARAMETER MenuItems
	An ordered hashtable where keys are the menu choice strings (e.g., "1", "a") and values are
	the descriptive text for each menu option. Mandatory.
.PARAMETER ActionMap
	A hashtable where keys are the menu choice strings entered by the user (must match keys in MenuItems),
	and values are the script blocks to execute for that choice. Mandatory.
.PARAMETER ExitChoice
	The string the user must enter to exit the menu loop. Defaults to "0".
.EXAMPLE
	$title = "My Awesome Menu"
	$items = [ordered]@{
		"1" = "Do Thing One"
		"2" = "Do Thing Two"
		"0" = "Exit"
	}
	$actions = @{
		"1" = { Write-Host "Executing Thing One..." }
		"2" = { Write-Host "Executing Thing Two..." }
	}
	Invoke-MenuLoop -MenuTitle $title -MenuItems $items -ActionMap $actions -ExitChoice "0"
.NOTES
	Uses Write-Host for menu display and Read-Host for input.
	Uses dot sourcing (`. $ActionMap[$choice]`) to execute action script blocks in the current scope.
	Includes basic error handling for action execution.
	Clears the host and prompts user before showing the menu again (except on exit).
#>
function Invoke-MenuLoop {
	# Set type to null to avoid outputting extra information.
	[OutputType([System.Void])]
	[CmdletBinding()]
	param(
		[Parameter(Mandatory = $true)]
		[string]$MenuTitle,

		[Parameter(Mandatory = $true)]
		[System.Collections.Specialized.OrderedDictionary]$MenuItems, # Use [ordered] or this type

		[Parameter(Mandatory = $true)]
		[hashtable]$ActionMap,

		[string]$ExitChoice = $null,

		[string]$DefaultChoice = $null
	)

	do {
		# Display Menu Title
		Write-Host "===========================================" -ForegroundColor Yellow
		Write-Host $MenuTitle -ForegroundColor White
		Write-Host "===========================================" -ForegroundColor Yellow
		# Display Menu Items from Ordered Hashtable
		foreach ($key in $MenuItems.Keys) {
			$line = ("{0}. {1}" -f $key, $MenuItems[$key])
			if ($key -eq $DefaultChoice) {
				$line += " (Default)"
			}
			Write-Host $line -ForegroundColor Cyan
		}
		Write-Host "-------------------------------------------" -ForegroundColor Yellow
		$message = "Enter your choice"
		[string]$choice = Read-Host $message
		if ([string]::IsNullOrEmpty($choice)) {
			$choice = $DefaultChoice
		}
		# Execute Action or Exit
		if ($ActionMap.ContainsKey($choice)) {
			Write-Host "Choice: $($MenuItems[$choice])" -ForegroundColor Green
			try {
				. $ActionMap[$choice] # Use dot sourcing to execute in current scope
			}
			catch {
				Write-Error "An error occurred executing action for choice '$choice': $_"
			}
			return
		} elseif ($null -ne $ExitChoice -and $ExitChoice -eq $choice) {
			Write-Host "Exiting menu." -ForegroundColor Green
			return
		}
		Write-Warning "Invalid selection."
	} while ($choice -ne $ExitChoice)
}
