################################################################################
# Description  : Contains container engine helper functions for setup scripts:
#                - Get-DockerPath: Find the path to the Docker executable.
#                - Get-PodmanPath: Find the path to the Podman executable.
#                - Select-ContainerEngine: Prompt user to choose Docker or Podman.
################################################################################

#==============================================================================
# Function: Get-EnginePath
#==============================================================================
<#
.SYNOPSIS
	Finds the path to the specified container engine executable (docker or podman).
.DESCRIPTION
	Attempts to locate the engine's executable (e.g., 'docker.exe' or 'podman.exe').
	First, it uses Get-Command. If not found in PATH, it checks for the executable
	within a subdirectory named after the engine (e.g., '.\docker' or '.\podman')
	relative to the script's location. If still not found, it writes an error and exits.
.PARAMETER EngineName
	The name of the container engine ('docker' or 'podman'). Mandatory.
.OUTPUTS
	[string] The full path to the found engine executable. Exits script on failure.
.EXAMPLE
	$dockerExePath = Get-EnginePath -EngineName "docker"
	& $dockerExePath ps
.EXAMPLE
	$podmanExePath = Get-EnginePath -EngineName "podman"
	& $podmanExePath images
.NOTES
	Assumes a potential local subdirectory if the engine isn't in the system PATH.
#>
function Get-EnginePath {
	[CmdletBinding()]
	param(
		[Parameter(Mandatory = $true)]
		[ValidateSet("docker", "podman")]
		[string]$EngineName
	)

	$exeName = "$EngineName.exe"
	$engineCmd = Get-Command $EngineName -ErrorAction SilentlyContinue
	if ($engineCmd) {
		return $engineCmd.Source
	}
	else {
		# Check relative path (e.g., .\docker\docker.exe)
		$relativePath = Join-Path (Resolve-Path ".\$EngineName") $exeName
		if (Test-Path $relativePath) {
			return $relativePath
		}
		else {
			Write-Error "$($EngineName.ToUpper()) executable not found in PATH or relative directory '.\$EngineName'."
			exit 1
		}
	}
}

#==============================================================================
# Function: Select-ContainerEngine
#==============================================================================
<#
.SYNOPSIS
	Prompts the user to select either Docker or Podman as the container engine.
.DESCRIPTION
	Displays a simple menu asking the user to select '1' for Docker or '2' for Podman.
	Reads the user's input via Read-Host.
.OUTPUTS
	[string] Returns 'docker' or 'podman' based on valid user selection.
	Returns $null if the user enters empty input or an invalid selection.
.EXAMPLE
	$selectedEngine = Select-ContainerEngine
	if ($selectedEngine) { Write-Host "You selected $selectedEngine" }
.NOTES
	Returns $null for invalid or empty input, allowing the caller to handle exit/retry logic.
#>
function Select-ContainerEngine {
	[OutputType([string])]
	param() # Add empty param block for OutputType attribute
	# Define Menu Title and Items
	$menuTitle = "Select container engine"
	$menuItems = [ordered]@{
		"1" = "Docker"
		"2" = "Podman"
		"0" = "Exit menu"
	}
	$script:selectedEngine = $null # Variable to store the result
	$script:enginePath = $null # Variable to store the path

	$menuActions = @{
		"1" = {
			$script:selectedEngine = "docker"
			$script:enginePath = Get-EnginePath -EngineName "docker"
		}
		"2" = {
			$script:selectedEngine = "podman"
			$script:enginePath = Get-EnginePath -EngineName "podman"
		}
	}
	Invoke-MenuLoop -MenuTitle $menuTitle -MenuItems $menuItems -ActionMap $menuActions -ExitChoice "0" -DefaultChoice "1"

	# Validate $script:enginePath is not null after selection or exit
	if ($script:selectedEngine -and (-not $script:enginePath)) {
		Write-Error "Failed to get path for selected engine '$script:selectedEngine'. Exiting."
		exit 1
	}

	# Return the engine selected by the action block (or $null if '0' or invalid)
	# The path is now implicitly set in the script scope variable $script:enginePath
	# We return only the name for compatibility with existing scripts.
	# Scripts should now get the path separately if needed, or rely on global vars set by caller.
	# Consider returning a hashtable in the future: @{ Name = $script:selectedEngine; Path = $script:enginePath }
	return $script:selectedEngine

#==============================================================================
# Function: Repair-PodmanCDI
#==============================================================================
<#
.SYNOPSIS
	Repairs Podman CDI configuration by regenerating specs.
.DESCRIPTION
	Detects if running on Podman Machine (Windows) and regenerates CDI specs to fix conflicting device errors.
.PARAMETER EnginePath
	The path to the Podman executable. Mandatory.
.OUTPUTS
	[bool] Returns $true if repair was attempted and command succeeded, $false otherwise.
#>
function Repair-PodmanCDI {
	param(
		[Parameter(Mandatory = $true)]
		[string]$EnginePath
	)
	Write-Host "Attempting to repair Podman CDI configuration..." -ForegroundColor Yellow

	# Check if we are using Podman Machine (Windows)
	try {
		$machineListStr = & $EnginePath machine list --format json 2>$null
		if (-not [string]::IsNullOrWhiteSpace($machineListStr) -and $machineListStr.Trim().StartsWith("[")) {
			$machineList = $machineListStr | ConvertFrom-Json
		}
	} catch {
		Write-Warning "Failed to list podman machines: $_"
	}

	if ($machineList -and $machineList.Count -gt 0) {
		$machineName = $machineList[0].Name
		Write-Host "Detected Podman Machine: $machineName" -ForegroundColor Cyan

		# Commands to clean and regenerate CDI
		$fixCommand = "sudo rm -f /etc/cdi/*.yaml* /var/run/cdi/*.yaml* && sudo nvidia-ctk cdi generate --output=/etc/cdi/nvidia.yaml && sudo nvidia-ctk cdi generate --output=/var/run/cdi/nvidia.yaml"

		Write-Host "Regenerating CDI specifications..." -ForegroundColor Yellow
		& $EnginePath machine ssh $machineName $fixCommand

		if ($LASTEXITCODE -eq 0) {
			Write-Host "CDI repair command executed successfully." -ForegroundColor Green
			return $true
		} else {
			Write-Warning "CDI repair command failed."
			return $false
		}
	} else {
		Write-Warning "Not using Podman Machine or unable to detect. Cannot auto-repair CDI."
		return $false
	}
}
}
