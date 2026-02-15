################################################################################
# File         : Setup_App_3_n8n_Core_Windows.ps1
# Description  : Installs n8n (via npm, if needed) and launches n8n on Windows
#                with environment variables equivalent to the container manifest.
# Usage        : Run in PowerShell. Choose ports during setup. Then open the shown URL.
################################################################################

using namespace System
using namespace System.IO

# Ensure script runs from its own directory
Set-Location -Path $PSScriptRoot

#==============================================================================
# Global Configuration
#==============================================================================

$global:appName = "n8n"
$global:defaultTimeZone = "Europe/London"
$global:defaultPort = 5678
$global:settingsVersion = 1

$global:programDataRoot = [Environment]::GetFolderPath([Environment+SpecialFolder]::CommonApplicationData)
$global:installRoot = Join-Path $global:programDataRoot $global:appName
$global:userDataRoot = Join-Path $global:installRoot "data"
$global:settingsPath = Join-Path $global:installRoot "settings.json"

$global:npmCommandName = "npm"
$global:n8nCommandName = "n8n"

# Base environment values (match Files/Aspire/manifest.json -> resources.n8n.properties.environment)
$global:baseEnvVars = @{
	GENERIC_TIMEZONE                        = $global:defaultTimeZone
	TZ                                      = $global:defaultTimeZone
	N8N_COMMUNITY_PACKAGES_ENABLED          = "true"
	N8N_COMMUNITY_PACKAGES_ALLOW_TOOL_USAGE = "true"
	N8N_ENFORCE_SETTINGS_FILE_PERMISSIONS   = "true"
	N8N_TRUST_HOST_HEADERS                  = "true"
	N8N_LOG_LEVEL                           = "info"
	NODE_OPTIONS                            = "--max-old-space-size=12288"
	NODES_EXCLUDE                           = "[]"
}

$global:n8nServiceTaskName = "VsAiCompanion-n8n"
$global:n8nServiceWrapperPath = Join-Path $global:installRoot "service-wrapper.ps1"
$global:n8nServiceLogPath = Join-Path $global:installRoot "service.log"
$global:n8nServicePidPath = Join-Path $global:installRoot "service.pid"
$global:n8nServicePidPath = Join-Path $global:installRoot "service.pid"

#==============================================================================
# Function: Assert-CommandAvailable
#==============================================================================
<#
.SYNOPSIS
	Ensures a command exists in PATH.
.DESCRIPTION
	Checks if a command can be resolved by Get-Command. Throws a friendly error if missing.
.PARAMETER CommandName
	Name of the command to verify.
.OUTPUTS
	[void]
#>
function Assert-CommandAvailable {
	[CmdletBinding()]
	param(
		[Parameter(Mandatory = $true)]
		[string]$CommandName
	)

	$cmd = Get-Command $CommandName -ErrorAction SilentlyContinue
	if (-not $cmd) {
		throw "Required command '$CommandName' was not found in PATH. Install Node.js (includes npm) and restart your shell."
	}
}

#==============================================================================
# Function: New-Directory
#==============================================================================
<#
.SYNOPSIS
	Creates a directory if it does not exist.
.DESCRIPTION
	Ensures the specified directory exists, creating it (and parents) if needed.
.PARAMETER Path
	Directory path to ensure exists.
.OUTPUTS
	[void]
#>
function New-Directory {
	[CmdletBinding(SupportsShouldProcess = $true)]
	param(
		[Parameter(Mandatory = $true)]
		[string]$Path
	)

	if (-not (Test-Path -LiteralPath $Path)) {
		if ($PSCmdlet.ShouldProcess($Path, "Create directory")) {
			New-Item -ItemType Directory -Path $Path -Force | Out-Null
			Write-Host "Created directory: $Path" -ForegroundColor DarkGray
		}
	}
}

#==============================================================================
# Function: Test-TcpPortAvailable
#==============================================================================
<#
.SYNOPSIS
	Tests if a local TCP port is available.
.DESCRIPTION
	Attempts to bind a TcpListener to localhost on the specified port.
	Returns $true if the bind succeeds; otherwise $false.
.PARAMETER Port
	TCP port to test.
.OUTPUTS
	[bool]
#>
function Test-TcpPortAvailable {
	[CmdletBinding()]
	[OutputType([bool])]
	param(
		[Parameter(Mandatory = $true)]
		[int]$Port
	)

	try {
		$listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, $Port)
		$listener.Start()
		$listener.Stop()
		return $true
	}
	catch {
		return $false
	}
}

#==============================================================================
# Function: Read-ValidatedPort
#==============================================================================
<#
.SYNOPSIS
	Prompts the user for a TCP port number.
.DESCRIPTION
	Prompts for a port with a default value. Validates range (1..65535) and availability.
.PARAMETER Prompt
	Prompt text.
.PARAMETER DefaultPort
	Default port to use when the user presses Enter.
.OUTPUTS
	[int]
#>
function Read-ValidatedPort {
	[CmdletBinding()]
	[OutputType([int])]
	param(
		[Parameter(Mandatory = $true)]
		[string]$Prompt,

		[Parameter(Mandatory = $true)]
		[int]$DefaultPort
	)

	while ($true) {
		$raw = Read-Host "$Prompt [default: $DefaultPort]"
		if ([string]::IsNullOrWhiteSpace($raw)) {
			$port = $DefaultPort
		}
		else {
			$parsed = 0
			if (-not [int]::TryParse($raw, [ref]$parsed)) {
			Write-Warning "Invalid port '$raw'. Please enter a number between 1 and 65535."
			continue
		}
			$port = $parsed
		}

		if ($port -lt 1 -or $port -gt 65535) {
			Write-Warning "Port must be between 1 and 65535."
			continue
		}

		if (-not (Test-TcpPortAvailable -Port $port)) {
			Write-Warning "Port $port is already in use on this machine. Choose another port."
			continue
		}

		return $port
	}
}

#==============================================================================
# Function: Get-n8nSetting
#==============================================================================
<#
.SYNOPSIS
	Loads persisted n8n Windows settings.
.DESCRIPTION
	Reads settings from $global:settingsPath if present. Returns defaults when missing/invalid.
.OUTPUTS
	[pscustomobject]
#>
function Get-n8nSetting {
	[CmdletBinding()]
	[OutputType([pscustomobject])]
	param()

	if (Test-Path -LiteralPath $global:settingsPath) {
		try {
			$content = Get-Content -LiteralPath $global:settingsPath -Raw -Encoding UTF8
			$settings = $content | ConvertFrom-Json
			if ($null -ne $settings -and $null -ne $settings.Port) {
				return [PSCustomObject]@{
					Version = $settings.Version
					Port    = [int]$settings.Port
				}
			}
		}
		catch {
			Write-Warning "Failed to load settings from '$($global:settingsPath)'. Using defaults. Details: $_"
		}
	}

	return [PSCustomObject]@{
		Version = $global:settingsVersion
		Port    = $global:defaultPort
	}
}

#==============================================================================
# Function: Set-n8nSetting
#==============================================================================
<#
.SYNOPSIS
	Saves persisted n8n Windows settings.
.DESCRIPTION
	Writes a small JSON file to $global:settingsPath.
.PARAMETER Port
	HTTP port used by n8n.
.OUTPUTS
	[void]
#>
function Set-n8nSetting {
	[CmdletBinding(SupportsShouldProcess = $true)]
	param(
		[Parameter(Mandatory = $true)]
		[int]$Port
	)

	New-Directory -Path $global:installRoot

	$settings = [PSCustomObject]@{
		Version     = $global:settingsVersion
		UpdatedUtc  = (Get-Date).ToUniversalTime().ToString("o")
		InstallRoot = $global:installRoot
		UserData    = $global:userDataRoot
		Port        = $Port
	}

	if ($PSCmdlet.ShouldProcess($global:settingsPath, "Save settings")) {
		$settings | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $global:settingsPath -Encoding UTF8
		Write-Host "Saved settings to: $($global:settingsPath)" -ForegroundColor DarkGray
	}
}

#==============================================================================
# Function: Install-n8nIfMissing
#==============================================================================
<#
.SYNOPSIS
	Installs n8n via npm if not present.
.DESCRIPTION
	Checks for npm and for the n8n command. If missing, installs n8n globally using npm.
.OUTPUTS
	[void]
#>
function Install-n8nIfMissing {
	[CmdletBinding()]
	param()

	Assert-CommandAvailable -CommandName $global:npmCommandName

	$n8nCmd = Get-Command $global:n8nCommandName -ErrorAction SilentlyContinue
	if ($n8nCmd) {
		Write-Host "n8n is already installed: $($n8nCmd.Source)" -ForegroundColor Green
		return
	}

	Write-Host "Installing n8n globally using npm..." -ForegroundColor Yellow
	& $global:npmCommandName install -g $global:n8nCommandName
	if ($LASTEXITCODE -ne 0) {
		throw "npm install -g n8n failed with exit code $LASTEXITCODE"
	}
}

#==============================================================================
# Function: Start-n8nWithEnv
#==============================================================================
<#
.SYNOPSIS
	Starts n8n with the configured environment.
.DESCRIPTION
	Sets environment variables for the current process (including port and data folder), then
	starts n8n in the foreground.
.PARAMETER Port
	HTTP port to run n8n on.
.OUTPUTS
	[void]
#>
function Start-n8nWithEnv {
	[CmdletBinding(SupportsShouldProcess = $true)]
	param(
		[Parameter(Mandatory = $true)]
		[int]$Port
	)

	if (-not $PSCmdlet.ShouldProcess("n8n", "Start (Port: $Port)")) {
		return
	}

	Assert-CommandAvailable -CommandName $global:n8nCommandName

	New-Directory -Path $global:installRoot
	New-Directory -Path $global:userDataRoot

	$envVars = @{}
	foreach ($k in $global:baseEnvVars.Keys) {
		$envVars[$k] = $global:baseEnvVars[$k]
	}
	$envVars["N8N_PORT"] = $Port.ToString()
	$envVars["N8N_USER_FOLDER"] = $global:userDataRoot

	Write-Host ""
	Write-Host "n8n installation / data locations:" -ForegroundColor White
	Write-Host "  App data root : $global:installRoot" -ForegroundColor Cyan
	Write-Host "  User data     : $global:userDataRoot" -ForegroundColor Cyan
	Write-Host "  Settings file : $global:settingsPath" -ForegroundColor Cyan

	Write-Host ""
	Write-Host "Launching n8n with the following environment variables:" -ForegroundColor White
	foreach ($k in ($envVars.Keys | Sort-Object)) {
		Write-Host "  $k=$($envVars[$k])" -ForegroundColor DarkGray
	}

	foreach ($k in $envVars.Keys) {
		Set-Item -Path ("Env:{0}" -f $k) -Value $envVars[$k]
	}

	Write-Host ""
	Write-Host "Starting n8n in the foreground (Ctrl+C to stop)..." -ForegroundColor Yellow
	Write-Host "Open: http://localhost:$Port" -ForegroundColor Green

	& $global:n8nCommandName
}

#==============================================================================
# Function: Uninstall-n8n
#==============================================================================
<#
.SYNOPSIS
	Uninstalls n8n and optionally removes user data.
.DESCRIPTION
	Prompts the user to uninstall the global npm package and/or delete the ProgramData folder
	used for n8n user data and settings.
.OUTPUTS
	[void]
#>
function Uninstall-n8n {
	[CmdletBinding()]
	param()

	Write-Host ""
	Write-Host "n8n uninstall" -ForegroundColor White
	Write-Host "-------------------------------------------" -ForegroundColor Yellow
	Write-Host "App data root : $global:installRoot" -ForegroundColor Cyan
	Write-Host "User data     : $global:userDataRoot" -ForegroundColor Cyan
	Write-Host "Settings file : $global:settingsPath" -ForegroundColor Cyan

	$removePackage = Read-Host "Uninstall n8n npm package (global)? (Y/N, default Y)"
	if ($removePackage -ne "N") {
		try {
			Assert-CommandAvailable -CommandName $global:npmCommandName
			Write-Host "Uninstalling n8n globally..." -ForegroundColor Yellow
			& $global:npmCommandName uninstall -g $global:n8nCommandName
			if ($LASTEXITCODE -ne 0) {
				Write-Warning "npm uninstall -g n8n failed with exit code $LASTEXITCODE"
			}
		}
		catch {
			Write-Warning "Failed to uninstall n8n npm package: $_"
		}
	}

	$deleteData = Read-Host "Delete ALL n8n app data under '$($global:installRoot)'? (Y/N, default N)"
	if ($deleteData -eq "Y") {
		if (Test-Path -LiteralPath $global:installRoot) {
			try {
				Remove-Item -LiteralPath $global:installRoot -Recurse -Force -ErrorAction Stop
				Write-Host "Removed: $($global:installRoot)" -ForegroundColor Green
			}
			catch {
				Write-Warning "Failed to remove '$($global:installRoot)': $_"
			}
		}
		else {
			Write-Host "Nothing to delete (folder not found)." -ForegroundColor DarkGray
		}
	}
	else {
		Write-Host "Keeping app data folder." -ForegroundColor DarkGray
	}
}

#==============================================================================
# Function: Install-n8n
#==============================================================================
<#
.SYNOPSIS
	Installs n8n prerequisites and persists configuration.
.DESCRIPTION
	Prompts for the n8n HTTP port (saved under ProgramData), ensures required folders exist,
	and installs n8n via npm if it is missing.
.OUTPUTS
	[void]
#>
function Install-n8n {
	[CmdletBinding()]
	param()

	$settings = Get-n8nSetting

	Write-Host ""
	Write-Host "Default n8n ports:" -ForegroundColor White
	Write-Host "  HTTP : $($global:defaultPort)" -ForegroundColor Cyan
	if ($settings.Port -ne $global:defaultPort) {
		Write-Host "Saved n8n HTTP port: $($settings.Port)" -ForegroundColor DarkGray
	}
	Write-Host ""
	Write-Host "If you are already running a container instance on the default port, choose a different port for Windows." -ForegroundColor DarkGray
	Write-Host "Example (Windows): 5679" -ForegroundColor DarkGray

	Write-Host ""
	Write-Host "n8n installation / data locations:" -ForegroundColor White
	Write-Host "  App data root : $global:installRoot" -ForegroundColor Cyan
	Write-Host "  User data     : $global:userDataRoot" -ForegroundColor Cyan
	Write-Host "  Settings file : $global:settingsPath" -ForegroundColor Cyan
	Write-Host ""

	$port = Read-ValidatedPort -Prompt "Enter n8n HTTP port" -DefaultPort $settings.Port
	Set-n8nSetting -Port $port
	Install-n8nIfMissing
}

#==============================================================================
# Function: Start-n8nConsole
#==============================================================================
<#
.SYNOPSIS
	Starts n8n in the current console (foreground).
.DESCRIPTION
	Loads saved settings and starts n8n in the foreground with the configured environment.
.OUTPUTS
	[void]
#>
function Start-n8nConsole {
	[CmdletBinding(SupportsShouldProcess = $true)]
	param()

	$settings = Get-n8nSetting
	Start-n8nWithEnv -Port $settings.Port
}

#==============================================================================
# Function: Write-n8nServiceWrapper
#==============================================================================
<#
.SYNOPSIS
	Writes the Scheduled Task wrapper script for n8n.
.DESCRIPTION
	Creates a PowerShell script under ProgramData which sets environment variables from saved settings
	and starts n8n with stdout/stderr appended to a log file.
.OUTPUTS
	[void]
#>
function Write-n8nServiceWrapper {
	[CmdletBinding(SupportsShouldProcess = $true)]
	param()

	New-Directory -Path $global:installRoot
	New-Directory -Path $global:userDataRoot

	$settings = Get-n8nSetting
	$port = [int]$settings.Port

	$envLines = @()
	foreach ($k in ($global:baseEnvVars.Keys | Sort-Object)) {
		$val = [string]$global:baseEnvVars[$k]
		$valEsc = $val.Replace("'", "''")
		$envLines += "`$env:$k = '$valEsc'"
	}
	$envLines += "`$env:N8N_PORT = '$port'"
	$envLines += "`$env:N8N_USER_FOLDER = `"$($global:userDataRoot)`""

	$wrapper = @"
`$ErrorActionPreference = 'Stop'

`$installRoot = '$($global:installRoot)'
`$userDataRoot = '$($global:userDataRoot)'
`$logPath = '$($global:n8nServiceLogPath)'
`$pidPath = '$($global:n8nServicePidPath)'

New-Item -ItemType Directory -Path `"`$installRoot`" -Force | Out-Null
New-Item -ItemType Directory -Path `"`$userDataRoot`" -Force | Out-Null

$($envLines -join "`r`n")

`"[`$(Get-Date -Format o)] Starting n8n...`" | Out-File -FilePath `"`$logPath`" -Append -Encoding UTF8

if (Test-Path -LiteralPath `"`$pidPath`" ) {
	try {
		`$oldPid = [int](Get-Content -LiteralPath `"`$pidPath`" -ErrorAction SilentlyContinue | Select-Object -First 1)
		if (`$oldPid -gt 0) {
			`$pOld = Get-Process -Id `$oldPid -ErrorAction SilentlyContinue
			if (`$pOld) {
				`"[`$(Get-Date -Format o)] Existing PID found (`$oldPid). Stopping it...`" | Out-File -FilePath `"`$logPath`" -Append -Encoding UTF8
				Stop-Process -Id `$oldPid -Force -ErrorAction SilentlyContinue
			}
		}
	}
	catch { }
}

`$stdoutPath = `"`$logPath`"
`$stderrPath = `"`$logPath`" + ".err"
`$p = Start-Process -FilePath "n8n" -WindowStyle Hidden -RedirectStandardOutput `"`$stdoutPath`" -RedirectStandardError `"`$stderrPath`" -PassThru
Set-Content -LiteralPath `"`$pidPath`" -Value `$p.Id -Encoding UTF8
`"[`$(Get-Date -Format o)] n8n started. PID=`$(`$p.Id)`" | Out-File -FilePath `"`$logPath`" -Append -Encoding UTF8
"@

	if ($PSCmdlet.ShouldProcess($global:n8nServiceWrapperPath, "Write n8n service wrapper script")) {
		Set-Content -LiteralPath $global:n8nServiceWrapperPath -Value $wrapper -Encoding UTF8
	}
}

#==============================================================================
# Function: Install-n8nService
#==============================================================================
<#
.SYNOPSIS
	Installs a Scheduled Task as a per-user "service" for n8n.
.DESCRIPTION
	Installs n8n, writes a service wrapper script, and registers a Scheduled Task
	using the shared Install-ScheduledTaskService helper. Interactively asks
	whether to enable auto-start and which mode (pre-login or post-login).
.OUTPUTS
	[void]
#>
function Install-n8nService {
	[CmdletBinding(SupportsShouldProcess = $true)]
	param()

	Install-n8n
	Write-n8nServiceWrapper

	if (-not (Test-Path -LiteralPath $global:n8nServiceWrapperPath)) {
		throw "Wrapper script was not created: $($global:n8nServiceWrapperPath)"
	}

	$installParams = @{
		TaskName          = $global:n8nServiceTaskName
		WrapperScriptPath = $global:n8nServiceWrapperPath
	}

	Write-Host ""
	$autoChoice = Read-Host "Enable automatic start? (Y/N, default: Y)"
	if ([string]::IsNullOrWhiteSpace($autoChoice) -or $autoChoice.Trim().ToUpper() -eq "Y") {
		$installParams.AutoStart = $true
		Write-Host ""
		Write-Host "  1. Pre-login  - starts at Windows boot, before login (may request elevation)" -ForegroundColor Cyan
		Write-Host "  2. Post-login - starts when current user logs in" -ForegroundColor Cyan
		Write-Host ""
		$modeChoice = Read-Host "Select start mode (1/2, default: 2)"
		if ($modeChoice.Trim() -eq "1") {
			$installParams.PreLogin = $true
		}
	}

	if ($PSCmdlet.ShouldProcess($global:n8nServiceTaskName, "Install n8n Scheduled Task Service")) {
		Install-ScheduledTaskService @installParams
	}
}

#==============================================================================
# Function: Uninstall-n8nService
#==============================================================================
<#
.SYNOPSIS
	Uninstalls the Scheduled Task "service" for n8n.
.DESCRIPTION
	Delegates to the shared Uninstall-ScheduledTaskService helper.
	Stops the task, unregisters it, and removes the wrapper script.
.OUTPUTS
	[void]
#>
function Uninstall-n8nService {
	[CmdletBinding(SupportsShouldProcess = $true)]
	param()

	if ($PSCmdlet.ShouldProcess($global:n8nServiceTaskName, "Uninstall n8n Scheduled Task Service")) {
		Uninstall-ScheduledTaskService -TaskName $global:n8nServiceTaskName -WrapperScriptPath $global:n8nServiceWrapperPath
	}
}

#==============================================================================
# Function: Start-n8nService
#==============================================================================
<#
.SYNOPSIS
	Starts the n8n Scheduled Task "service".
.DESCRIPTION
	Starts the scheduled task by name.
.OUTPUTS
	[void]
#>
function Start-n8nService {
	[CmdletBinding(SupportsShouldProcess = $true)]
	param()

	$taskName = $global:n8nServiceTaskName
	if ($PSCmdlet.ShouldProcess($taskName, "Start Scheduled Task")) {
		Start-ScheduledTask -TaskName $taskName
	}
}

#==============================================================================
# Function: Stop-n8nService
#==============================================================================
<#
.SYNOPSIS
	Stops the n8n Scheduled Task "service".
.DESCRIPTION
	Stops the scheduled task by name.
.OUTPUTS
	[void]
#>
function Stop-n8nService {
	[CmdletBinding(SupportsShouldProcess = $true)]
	param()

	$taskName = $global:n8nServiceTaskName
	if ($PSCmdlet.ShouldProcess($taskName, "Stop Scheduled Task")) {
		Stop-ScheduledTask -TaskName $taskName
	}

	$killed = $false

	if (Test-Path -LiteralPath $global:n8nServicePidPath) {
		try {
			$pidRaw = Get-Content -LiteralPath $global:n8nServicePidPath -ErrorAction SilentlyContinue | Select-Object -First 1
			[int]$procId = 0
			if ([int]::TryParse([string]$pidRaw, [ref]$procId) -and $procId -gt 0) {
				$proc = Get-Process -Id $procId -ErrorAction SilentlyContinue
				if ($proc -and $proc.ProcessName -in @('n8n','node','nodejs')) {
					if ($PSCmdlet.ShouldProcess("PID $procId", "Stop n8n process")) {
						Stop-Process -Id $procId -Force -ErrorAction SilentlyContinue
						$killed = $true
					}
				}
				elseif ($proc) {
					Write-Warning "PID file points to '$($proc.ProcessName)' (PID $procId). Ignoring PID file."
				}
			}
		}
		catch {
			Write-Verbose "Failed to stop n8n process by PID."
		}
	}

	if (-not $killed) {
		foreach ($p in @(Get-Process n8n,node,nodejs -ErrorAction SilentlyContinue)) {
			if ($PSCmdlet.ShouldProcess("PID $($p.Id)", "Stop candidate n8n process")) {
				Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
			}
		}
	}
}

#==============================================================================
# Function: Show-n8nMenu
#==============================================================================
<#
.SYNOPSIS
	Shows the n8n Windows menu.
.DESCRIPTION
	Provides options for install, start (console), start service, stop service, and uninstall.
.OUTPUTS
	[void]
#>
function Show-n8nMenu {
	[CmdletBinding()]
	param()

	$taskName = $global:n8nServiceTaskName
	$state = "Not Installed"
	$lastResult = ""
	$triggerMode = ""
	try {
		$t = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
		if ($t) {
			$info = Get-ScheduledTaskInfo -TaskName $taskName -ErrorAction SilentlyContinue
			if ($info) {
				if ($info.State) { $state = [string]$info.State } else { $state = "Installed" }
				if ($null -ne $info.LastTaskResult) { $lastResult = [string]$info.LastTaskResult }
			}
			else {
				$state = "Installed"
			}
			$trigger = $t.Triggers | Select-Object -First 1
			if ($trigger -is [Microsoft.Management.Infrastructure.CimInstance]) {
				$cimClass = $trigger.CimClass.CimClassName
				if ($cimClass -eq "MSFT_TaskBootTrigger") {
					$triggerMode = "At Boot (pre-login)"
				}
				elseif ($cimClass -eq "MSFT_TaskLogonTrigger") {
					$triggerMode = "At Logon (post-login)"
				}
				else {
					$triggerMode = $cimClass
				}
			}
		}
	}
	catch {
		$state = "Unknown"
	}

	$isAdmin = Test-IsAdministrator
	$adminTag = if ($isAdmin) { " [Admin]" } else { "" }

	Write-Host "===========================================" -ForegroundColor Yellow
	Write-Host "n8n (Windows)$adminTag" -ForegroundColor White
	Write-Host "===========================================" -ForegroundColor Yellow
	Write-Host "Service Task: $taskName" -ForegroundColor DarkGray
	Write-Host "Service State: $state" -ForegroundColor DarkGray
	if (-not [string]::IsNullOrWhiteSpace($triggerMode)) {
		Write-Host "Service Start: $triggerMode" -ForegroundColor DarkGray
	}
	if (-not [string]::IsNullOrWhiteSpace($lastResult)) {
		Write-Host "Last Task Result: $lastResult" -ForegroundColor DarkGray
	}
	Write-Host "-------------------------------------------" -ForegroundColor Yellow
	Write-Host "1. Install App" -ForegroundColor Cyan
	Write-Host "2. Start Console" -ForegroundColor Cyan
	Write-Host "3. Install Service" -ForegroundColor Cyan
	Write-Host "4. Start Service" -ForegroundColor Cyan
	Write-Host "5. Stop Service" -ForegroundColor Cyan
	Write-Host "6. Uninstall Service" -ForegroundColor Cyan
	Write-Host "7. Uninstall App" -ForegroundColor Cyan
	Write-Host "0. Exit" -ForegroundColor Cyan
	Write-Host "-------------------------------------------" -ForegroundColor Yellow
}

#==============================================================================
# Main
#==============================================================================

New-Directory -Path $global:installRoot
New-Directory -Path $global:userDataRoot

$choice = ""
do {
	Show-n8nMenu
	$choice = Read-Host "Enter your choice"
	if ([string]::IsNullOrWhiteSpace($choice)) { continue }

	switch ($choice) {
		"1" {
			Install-n8n
		}
		"2" {
			Start-n8nConsole
		}
		"3" {
			Install-n8nService
		}
		"4" {
			if (-not (Test-Path -LiteralPath $global:settingsPath)) {
				Write-Warning "n8n service requires saved port. Run 'Install App' first."
				break
			}
			Start-n8nService
		}
		"5" {
			Stop-n8nService
		}
		"6" {
			Uninstall-n8nService
		}
		"7" {
			Uninstall-n8n
		}
		"0" { return }
		default {
			Write-Warning "Invalid selection."
		}
	}
} while ($choice -ne "0")
