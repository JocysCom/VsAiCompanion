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
# Function: Get-DockerPath
#==============================================================================
<#
.SYNOPSIS
	Finds the path to the Docker executable.
.DESCRIPTION
	Attempts to locate the 'docker.exe' executable. First, it uses Get-Command.
	If not found in PATH, it checks for a 'docker.exe' within a 'docker' subdirectory
	relative to the script's location. If still not found, it writes an error and exits.
.OUTPUTS
	[string] The full path to the found docker.exe. Exits script on failure.
.EXAMPLE
	$dockerExePath = Get-DockerPath
	& $dockerExePath ps
.NOTES
	Assumes a potential local 'docker' subdirectory if Docker isn't in the system PATH.
#>
function Get-DockerPath {
	$dockerCmd = Get-Command docker -ErrorAction SilentlyContinue
	if ($dockerCmd) {
		return $dockerCmd.Source
	}
	else {
		$dockerPath = Join-Path (Resolve-Path ".\docker") "docker.exe"
		if (Test-Path $dockerPath) {
			return $dockerPath
		}
		else {
			Write-Error "Docker executable not found."
			exit 1
		}
	}
}

#==============================================================================
# Function: Get-PodmanPath
#==============================================================================
<#
.SYNOPSIS
	Finds the path to the Podman executable.
.DESCRIPTION
	Attempts to locate the 'podman.exe' executable. First, it uses Get-Command.
	If not found in PATH, it checks for a 'podman.exe' within a 'podman' subdirectory
	relative to the script's location. If still not found, it writes an error and exits.
.OUTPUTS
	[string] The full path to the found podman.exe. Exits script on failure.
.EXAMPLE
	$podmanExePath = Get-PodmanPath
	& $podmanExePath images
.NOTES
	Assumes a potential local 'podman' subdirectory if Podman isn't in the system PATH.
#>
function Get-PodmanPath {
	$podmanCmd = Get-Command podman -ErrorAction SilentlyContinue
	if ($podmanCmd) {
		return $podmanCmd.Source
	}
	else {
		$podmanPath = Join-Path (Resolve-Path ".\podman") "podman.exe"
		if (Test-Path $podmanPath) {
			return $podmanPath
		}
		else {
			Write-Error "Podman executable not found."
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
	[OutputType([string])] # Explicitly declare return type as string
	param() # Add empty param block for OutputType attribute

	# Define Menu Title and Items
	$menuTitle = "Select container engine"
	$menuItems = [ordered]@{
		"1" = "Docker"
		"2" = "Podman"
		# "0" = "Exit/Cancel" # Implicit exit choice
	}

	# Define Menu Actions
	$selectedEngine = $null # Variable to store the result
	$actionCompleted = $false # Flag to exit loop

	$menuActions = @{
		"1" = {
			$script:selectedEngine = "docker"
			$script:actionCompleted = $true
		}
		"2" = {
			$script:selectedEngine = "podman"
			$script:actionCompleted = $true
		}
		# "0" action will exit the loop, returning the initial $selectedEngine ($null)
	}

	# Invoke the Menu Loop - modify the loop slightly to exit after one action
	do {
		# Need to capture the choice made within Invoke-MenuLoop to check for '0'
		# Invoke-MenuLoop doesn't return the choice, so we rely on the flag
		# The actual choice value isn't needed here, only whether the loop exited via '0' or action.
		Invoke-MenuLoop -MenuTitle $menuTitle -MenuItems $menuItems -ActionMap $menuActions -ExitChoice "0"

		# Check if an action was completed or if the user chose to exit (choice 0)
		# We infer '0' was chosen if the loop finished but actionCompleted is still false
		if ($actionCompleted) {
			break # Exit the custom do-while loop if an action was performed
		}
		else {
			# If actionCompleted is false after Invoke-MenuLoop finishes, it means '0' was chosen.
			$selectedEngine = $null # Ensure null is returned if '0' was chosen
			break # Exit the custom do-while loop
		}
	} while ($true) # Loop until explicitly broken

	# Return the engine selected by the action block (or $null if '0' or invalid)
	return $selectedEngine
}
