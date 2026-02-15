################################################################################
# Description  : Contains core helper functions for setup scripts:
#                - Ensure-Elevated: Verify administrator privileges.
#                - Set-ScriptLocation: Set the script's working directory.
#                - Download-File: Download files using BITS or WebRequest.
#                - Check-Git: Check for Git installation and add to PATH if needed.
#                - Test-ApplicationInstalled: Check if an application is installed.
#                - Refresh-EnvironmentVariables: Refresh PATH in the current session.
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
# Function: Unprotect-SecureString
#==============================================================================

function Unprotect-SecureString {
    param([SecureString]$SecureString)
    $plainText = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto(
        [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($SecureString)
    )
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR(
        [Runtime.InteropServices.Marshal]::SecureStringToBSTR($SecureString)
    )
    return $plainText
}

#==============================================================================
# Function: Get-EnvironmentVariableWithDefault
#==============================================================================

function Get-EnvironmentVariableWithDefault {
    param(
        [Parameter(Mandatory = $true)]
        [string]$EnvVarName,

        [Parameter(Mandatory = $true)]
        [string]$DefaultValue,

        [Parameter(Mandatory = $false)]
        [string]$PromptText = ""
    )

    # Auto-generate prompt text if not provided
    if ([string]::IsNullOrWhiteSpace($PromptText)) {
        $parts = $EnvVarName.Split('_') | ForEach-Object {
            $part = $_.ToLower()
            if (@('api', 'url', 'id', 'ai', 'db', 'sql') -contains $part) {
                $part.ToUpper()
            }
            else {
                $part.Substring(0, 1).ToUpper() + $part.Substring(1)
            }
        }
        $PromptText = ($parts -join ' ')
    }

    # Get existing environment variable value
    $existingValue = [Environment]::GetEnvironmentVariable($EnvVarName)

    if (-not [string]::IsNullOrWhiteSpace($existingValue)) {
        # Mask existing value for display
        $len = $existingValue.Length
        $pre = $existingValue.Substring(0, [Math]::Min(4, $len))
        $suf = $existingValue.Substring([Math]::Max(0, $len - 4))
        $masked = "$pre****$suf"
        $prompt = "`nEnter $PromptText [default: $masked]"
        $secureInput = Read-Host $prompt -AsSecureString
        $inputValue = Unprotect-SecureString -SecureString $secureInput

        if ([string]::IsNullOrWhiteSpace($inputValue)) {
            return $existingValue
        }
        else {
            return $inputValue
        }
    }
    else {
        # Environment variable is empty or doesn't exist
        Write-Host "`n> Environment variable '$EnvVarName' is empty or not set." -ForegroundColor Yellow
        $prompt = "`nEnter $PromptText [default: $DefaultValue]"
        $secureInput = Read-Host $prompt -AsSecureString
        $inputValue = Unprotect-SecureString -SecureString $secureInput

        if ([string]::IsNullOrWhiteSpace($inputValue)) {
            return $DefaultValue
        }
        else {
            return $inputValue
        }
    }
}

#==============================================================================
# Function: Invoke-OptionsMenu
#==============================================================================
<#
.SYNOPSIS
    Displays a menu of options and returns the key corresponding to the user's choice.
.DESCRIPTION
    Generates a dynamic menu based on the provided items array. It assigns numeric/alphabetic keys
    to each item. The function returns the value selected by the user.
#>
function Invoke-OptionsMenu {
	# Set type to null to avoid outputting extra information.
	[OutputType([System.Void])]
	[CmdletBinding()]
	param(
		[Parameter(Mandatory = $true)]
		[string]$Title,
		[Parameter(Mandatory = $true)]
		[array]$Options,
		[string]$ExitChoice = $null,
		[string]$DefaultChoice = $null
	)
	do {
		$keys = "1234567890ABCDEFGHIJKLMNOPQRSTUVWXYZ"
		$MenuItems = [ordered]@{}
		[int]$i = 0
		foreach ($value in $Options) {
			if ([string]::IsNullOrWhiteSpace($value)) {
				continue
			}
			if ($i -ge $keys.Length) {
				Write-Warning "Too many menu items. Maximum supported is $($keys.Length)."
				break
			}
			$key = $keys[$i]
			$MenuItems["$key"] = $value
			$i++
		}

		# Display Menu Title
		Write-Host "===========================================" -ForegroundColor Yellow
		Write-Host $Title -ForegroundColor White
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
		[string]$choice = Read-Host "Enter your choice"
		if ([string]::IsNullOrEmpty($choice)) {
			$choice = $DefaultChoice
		}
		if ($null -ne $ExitChoice -and $ExitChoice -eq $choice) {
			Write-Host "Exiting menu." -ForegroundColor Green
			return $null
		}
		if ($MenuItems.Keys.Contains($choice)) {
			Write-Host "Choice: $($MenuItems[$choice])" -ForegroundColor Green
			return $MenuItems[$choice]
		}
		Write-Warning "Invalid selection."
	} while ($choice -ne $ExitChoice)
	return $null
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
		[string]$choice = Read-Host "Enter your choice"
		if ([string]::IsNullOrEmpty($choice)) {
			$choice = $DefaultChoice
		}
		Write-Host "Choice: $($MenuItems[$choice])" -ForegroundColor Green
		if ($null -ne $ExitChoice -and $ExitChoice -eq $choice) {
			Write-Host "Exiting menu." -ForegroundColor Green
			return
		}
		if ($null -ne $ActionMap -and $ActionMap.ContainsKey($choice)) {
			try {
				. $ActionMap[$choice] # Use dot sourcing to execute in current scope
			}
			catch {
				Write-Error "An error occurred executing action for choice '$choice': $_"
			}
			return
		}
		Write-Warning "Invalid selection."
	} while ($choice -ne $ExitChoice)
}

#==============================================================================
# Function: Save-ScriptSettings
#==============================================================================
<#
.SYNOPSIS
	Saves script settings to a JSON file in the Backup directory.
.DESCRIPTION
	Creates a Backup directory if it doesn't exist and saves the provided settings
	object to a JSON file named after the calling script. Uses simple PowerShell
	serialization (ConvertTo-Json) for compatibility.
.PARAMETER Settings
	The settings object to save. Can be any object that can be serialized to JSON.
.PARAMETER ScriptName
	Optional script name to use for the filename. If not provided, uses the calling script's name.
.EXAMPLE
	$settings = @{ AcceptSelfSigned = $true; UseDNS = $true; ExternalDomain = "example.com" }
	Save-ScriptSettings -Settings $settings
.NOTES
	Creates Backup/<script_name>.json file. Overwrites existing settings.
#>
function Save-ScriptSettings {
	[CmdletBinding(SupportsShouldProcess = $true)]
	param(
		[Parameter(Mandatory = $true)]
		[object]$Settings,

		[Parameter(Mandatory = $false)]
		[string]$ScriptName = $null
	)

	# Get script name if not provided
	if ([string]::IsNullOrWhiteSpace($ScriptName)) {
		$ScriptName = [System.IO.Path]::GetFileNameWithoutExtension($MyInvocation.PSCommandPath)
	}

	# Ensure Backup directory exists
	$backupDir = Join-Path $PSScriptRoot "Backup"
	if (-not (Test-Path $backupDir)) {
		if ($PSCmdlet.ShouldProcess($backupDir, "Create Backup Directory")) {
			New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
			Write-Host "Created Backup directory: $backupDir"
		}
	}

	# Create settings file path
	$settingsPath = Join-Path $backupDir "$ScriptName.json"

	if ($PSCmdlet.ShouldProcess($settingsPath, "Save Settings")) {
		try {
			$Settings | ConvertTo-Json -Depth 10 | Set-Content -Path $settingsPath -Encoding UTF8
			Write-Host "Settings saved to: $settingsPath"
		}
		catch {
			Write-Error "Failed to save settings: $_"
		}
	}
}

#==============================================================================
# Function: Import-ScriptSettings
#==============================================================================
<#
.SYNOPSIS
	Loads script settings from a JSON file in the Backup directory.
.DESCRIPTION
	Attempts to load settings from Backup/<script_name>.json using ConvertFrom-Json.
	Returns the loaded object or $null if the file doesn't exist or cannot be loaded.
.PARAMETER ScriptName
	Optional script name to use for the filename. If not provided, uses the calling script's name.
.OUTPUTS
	[object] Returns the loaded settings object or $null if not found or invalid.
.EXAMPLE
	$settings = Import-ScriptSettings
	if ($settings) {
		$acceptSelfSigned = $settings.AcceptSelfSigned
	}
.NOTES
	Reads from Backup/<script_name>.json file. Returns $null if file doesn't exist.
#>
function Import-ScriptSettings {
	[CmdletBinding()]
	[OutputType([object])]
	param(
		[Parameter(Mandatory = $false)]
		[string]$ScriptName = $null
	)

	# Get script name if not provided
	if ([string]::IsNullOrWhiteSpace($ScriptName)) {
		$ScriptName = [System.IO.Path]::GetFileNameWithoutExtension($MyInvocation.PSCommandPath)
	}

	# Create settings file path
	$backupDir = Join-Path $PSScriptRoot "Backup"
	$settingsPath = Join-Path $backupDir "$ScriptName.json"

	if (Test-Path $settingsPath) {
		try {
			$content = Get-Content -Path $settingsPath -Raw -Encoding UTF8
			$settings = $content | ConvertFrom-Json
			Write-Host "Settings loaded from: $settingsPath"
			return $settings
		}
		catch {
			Write-Warning "Failed to load settings from $settingsPath`: $_"
			return $null
		}
	}
	else {
		Write-Host "No existing settings found at: $settingsPath"
		return $null
	}
}

#==============================================================================
# Function: Test-IsAdministrator
#==============================================================================
<#
.SYNOPSIS
	Checks if the current session is running with Administrator privileges.
.DESCRIPTION
	Uses [Security.Principal.WindowsPrincipal] to check for the Administrator role.
	Unlike Test-AdminPrivilege, this returns a boolean instead of exiting.
.OUTPUTS
	[bool] True if running as Administrator, false otherwise.
.EXAMPLE
	if (Test-IsAdministrator) { Write-Host "Elevated." }
#>
function Test-IsAdministrator {
	[CmdletBinding()]
	[OutputType([bool])]
	param()

	return ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

#==============================================================================
# Function: Install-ScheduledTaskService
#==============================================================================
<#
.SYNOPSIS
	Registers a Scheduled Task as a per-user "service" using a wrapper script.
.DESCRIPTION
	Creates or replaces a Scheduled Task that runs a PowerShell wrapper script
	as the current user. Supports three modes:

	- Manual (no -AutoStart): No trigger, task must be started manually.
	- Pre-login (-AutoStart -PreLogin): AtStartup trigger + S4U logon.
	  Starts at Windows boot before user login. Requires Administrator.
	- Post-login (-AutoStart without -PreLogin): AtLogOn trigger + Interactive
	  logon. Starts when the user logs in. No admin required.

	Both auto-start modes run as the current user ($env:UserName). This is
	required for WSL-based services because WSL distros are per-user (HKCU)
	and cannot be accessed by SYSTEM or NetworkService.

	Used by: n8n, Qdrant, OpenClaw.
.PARAMETER TaskName
	Name of the Scheduled Task to register.
.PARAMETER WrapperScriptPath
	Full path to the PowerShell wrapper script to execute.
.PARAMETER AutoStart
	If set, adds a trigger. Without this switch the task is registered with no
	trigger (manual start only). Combine with -PreLogin for AtStartup mode.
.PARAMETER PreLogin
	When combined with -AutoStart, uses AtStartup trigger + S4U logon (starts
	before user login). Requires Administrator; if not elevated, a UAC prompt
	is shown automatically. Without -PreLogin, uses AtLogOn + Interactive.
.OUTPUTS
	[void]
.EXAMPLE
	Install-ScheduledTaskService -TaskName "MyApp-Service" -WrapperScriptPath "C:\MyApp\wrapper.ps1" -AutoStart -PreLogin
.NOTES
	AtStartup + S4U requires Administrator elevation. When not already elevated,
	the function automatically triggers a UAC prompt to register the task in an
	elevated child process. If the user declines UAC, the operation is cancelled.
	View registered tasks in: Task Scheduler (taskschd.msc) or Sysinternals Autoruns.
#>
function Install-ScheduledTaskService {
	[CmdletBinding(SupportsShouldProcess = $true)]
	param(
		[Parameter(Mandatory = $true, HelpMessage = "Name of the Scheduled Task.")]
		[string]$TaskName,

		[Parameter(Mandatory = $true, HelpMessage = "Full path to the PowerShell wrapper script.")]
		[string]$WrapperScriptPath,

		[Parameter(Mandatory = $false)]
		[switch]$AutoStart,

		[Parameter(Mandatory = $false)]
		[switch]$PreLogin
	)

	if (-not (Test-Path -LiteralPath $WrapperScriptPath)) {
		throw "Wrapper script not found: $WrapperScriptPath"
	}

	$action = New-ScheduledTaskAction -Execute "PowerShell.exe" -Argument "-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File `"$WrapperScriptPath`""
	$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable -MultipleInstances IgnoreNew

	$isAdmin = Test-IsAdministrator
	$trigger = $null

	if ($AutoStart -and $PreLogin) {
		if (-not $isAdmin) {
			Write-Host "Pre-login (AtStartup) mode requires Administrator elevation." -ForegroundColor Yellow
			Write-Host "A UAC prompt will appear to register the task..." -ForegroundColor Cyan
			$elevatedCmd = @'
$ErrorActionPreference = "Stop"
try {
	   $existing = Get-ScheduledTask -TaskName "__TASK__" -ErrorAction SilentlyContinue
	   if ($existing) { Unregister-ScheduledTask -TaskName "__TASK__" -Confirm:$false }
	   $action = New-ScheduledTaskAction -Execute "PowerShell.exe" -Argument "-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File `"__PATH__`""
	   $trigger = New-ScheduledTaskTrigger -AtStartup
	   $principal = New-ScheduledTaskPrincipal -UserId "__USER__" -LogonType S4U -RunLevel Limited
	   $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable -MultipleInstances IgnoreNew
	   $task = New-ScheduledTask -Action $action -Trigger $trigger -Principal $principal -Settings $settings
	   Register-ScheduledTask -TaskName "__TASK__" -InputObject $task -Force | Out-Null
	   exit 0
} catch { exit 1 }
'@
			$elevatedCmd = $elevatedCmd.Replace('__TASK__', $TaskName).Replace('__PATH__', $WrapperScriptPath).Replace('__USER__', $env:UserName)
			$cmdBytes = [System.Text.Encoding]::Unicode.GetBytes($elevatedCmd)
			$cmdEncoded = [Convert]::ToBase64String($cmdBytes)
			try {
				$proc = Start-Process -FilePath "PowerShell.exe" -ArgumentList "-NoProfile", "-ExecutionPolicy", "Bypass", "-EncodedCommand", $cmdEncoded -Verb RunAs -Wait -PassThru
			}
			catch {
				Write-Warning "UAC elevation was declined or failed."
				return
			}
			if ($proc.ExitCode -eq 0) {
				$verifyTask = Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
				if ($verifyTask) {
					Write-Host "Scheduled task '$TaskName' registered (runs at Windows startup, before login)." -ForegroundColor Green
				}
				else {
					Write-Warning "Elevated process completed but task '$TaskName' was not found."
				}
			}
			else {
				Write-Warning "Elevated registration failed (exit code: $($proc.ExitCode))."
			}
			return
		}
		Write-Host "Registering AtStartup service '$TaskName' (pre-login, S4U)..." -ForegroundColor Cyan
		$trigger = New-ScheduledTaskTrigger -AtStartup
		$principal = New-ScheduledTaskPrincipal -UserId "$env:UserName" -LogonType S4U -RunLevel Limited
	}
	elseif ($AutoStart) {
		Write-Host "Registering AtLogOn service '$TaskName' (post-login, Interactive)..." -ForegroundColor Cyan
		$trigger = New-ScheduledTaskTrigger -AtLogOn -User $env:UserName
		$principal = New-ScheduledTaskPrincipal -UserId "$env:UserName" -LogonType Interactive -RunLevel Limited
	}
	else {
		$principal = New-ScheduledTaskPrincipal -UserId "$env:UserName" -LogonType Interactive -RunLevel Limited
	}

	Uninstall-ScheduledTaskService -TaskName $TaskName -WrapperScriptPath $null

	$task = if ($trigger) { New-ScheduledTask -Action $action -Trigger $trigger -Principal $principal -Settings $settings } else { New-ScheduledTask -Action $action -Principal $principal -Settings $settings }

	if ($PSCmdlet.ShouldProcess($TaskName, "Register Scheduled Task")) {
		Register-ScheduledTask -TaskName $TaskName -InputObject $task -Force | Out-Null
	}

	if ($AutoStart -and $PreLogin) {
		Write-Host "Scheduled task '$TaskName' registered (runs at Windows startup, before login)." -ForegroundColor Green
	}
	elseif ($AutoStart) {
		Write-Host "Scheduled task '$TaskName' registered (runs at user logon, after login)." -ForegroundColor Green
	}
	else {
		Write-Host "Scheduled task '$TaskName' registered (manual start only)." -ForegroundColor Green
	}
}

#==============================================================================
# Function: Uninstall-ScheduledTaskService
#==============================================================================
<#
.SYNOPSIS
	Stops and removes a Scheduled Task "service".
.DESCRIPTION
	Stops the running task if present, unregisters it from Task Scheduler,
	and optionally removes the wrapper script file.

	Used by: n8n, Qdrant, OpenClaw.
.PARAMETER TaskName
	Name of the Scheduled Task to remove.
.PARAMETER WrapperScriptPath
	Optional path to the wrapper script file to delete. Pass $null to skip deletion.
.OUTPUTS
	[void]
.EXAMPLE
	Uninstall-ScheduledTaskService -TaskName "MyApp-Service" -WrapperScriptPath "C:\MyApp\wrapper.ps1"
.NOTES
	Safe to call even if the task does not exist.
	View registered tasks in: Task Scheduler (taskschd.msc) or Sysinternals Autoruns.
#>
function Uninstall-ScheduledTaskService {
	[CmdletBinding(SupportsShouldProcess = $true)]
	param(
		[Parameter(Mandatory = $true, HelpMessage = "Name of the Scheduled Task.")]
		[string]$TaskName,

		[Parameter(Mandatory = $false, HelpMessage = "Path to wrapper script to remove (or `$null to skip).")]
		[AllowNull()]
		[string]$WrapperScriptPath
	)

	try { Stop-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue | Out-Null } catch { Write-Verbose "Task '$TaskName' was not running or does not exist." }

	if ($PSCmdlet.ShouldProcess($TaskName, "Unregister Scheduled Task")) {
		Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false -ErrorAction SilentlyContinue | Out-Null
	}

	if ($WrapperScriptPath -and (Test-Path -LiteralPath $WrapperScriptPath)) {
		if ($PSCmdlet.ShouldProcess($WrapperScriptPath, "Remove wrapper script")) {
			Remove-Item -LiteralPath $WrapperScriptPath -Force -ErrorAction SilentlyContinue
		}
	}
}

#==============================================================================
# Function: Get-ScheduledTaskServiceStatus
#==============================================================================
<#
.SYNOPSIS
	Gets the status of a Scheduled Task "service".
.DESCRIPTION
	Returns the state of the specified Scheduled Task, or 'Not Registered'
	if the task does not exist.

	Used by: n8n, Qdrant, OpenClaw.
.PARAMETER TaskName
	Name of the Scheduled Task to check.
.OUTPUTS
	[string] Task state: Running, Ready, Disabled, or "Not Registered".
.EXAMPLE
	$status = Get-ScheduledTaskServiceStatus -TaskName "MyApp-Service"
.NOTES
	View registered tasks in: Task Scheduler (taskschd.msc) or Sysinternals Autoruns.
#>
function Get-ScheduledTaskServiceStatus {
	[CmdletBinding()]
	[OutputType([string])]
	param(
		[Parameter(Mandatory = $true, HelpMessage = "Name of the Scheduled Task.")]
		[string]$TaskName
	)

	$existing = Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
	if (-not $existing) {
		return "Not Registered"
	}
	return $existing.State.ToString()
}
