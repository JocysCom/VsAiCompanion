################################################################################
# File         : Setup_0_ContainerEngine.ps1
# Description  : Contains container engine helper functions for setup scripts:
#                - Get-DockerPath: Find the path to the Docker executable.
#                - Get-PodmanPath: Find the path to the Podman executable.
#                - Select-ContainerEngine: Prompt user to choose Docker or Podman.
# Usage        : Dot-source this script in other setup scripts:
#                . "$PSScriptRoot\Setup_0_ContainerEngine.ps1"
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
}
