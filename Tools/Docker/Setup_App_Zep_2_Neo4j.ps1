################################################################################
# Description  : Script to set up and run the Neo4j container using Docker/Podman.
#                Verifies volume presence, pulls the Neo4j image if necessary,
#                and runs the container with port and volume mappings.
# Usage        : Run as Administrator if using Docker.
################################################################################

using namespace System
using namespace System.IO
using namespace System.Diagnostics.CodeAnalysis

# Dot-source the necessary helper function files.
. "$PSScriptRoot\Setup_Helper_CoreFunctions.ps1"
. "$PSScriptRoot\Setup_Helper_NetworkTests.ps1"
. "$PSScriptRoot\Setup_Helper_ContainerEngine.ps1"
. "$PSScriptRoot\Setup_Helper_BackupRestore.ps1"
. "$PSScriptRoot\Setup_Helper_ContainerManagement.ps1"

# Ensure the script working directory is set.
Set-ScriptLocation

#############################################
# Global Variables
#############################################
# Note: PSAvoidGlobalVars warnings are ignored here as these are used across menu actions.
$global:imageName = "neo4j:5.22.0"
$global:containerName = "neo4j"
$global:boltPort = 7687
$global:httpPort = 7474
$global:volumeName = "neo4j_data"
$global:volumeMountPath = "/data"
$global:networkName = "podman" # Reverted to "podman" as per user instruction
$global:neo4jPassword = "zepzepzep" # From docker-compose.ce.yaml

# --- Engine Selection ---
$global:containerEngine = Select-ContainerEngine
# Exit if no engine was selected
if (-not $global:containerEngine) {
	Write-Warning "No container engine selected. Exiting script."
	exit 1
}
# Set engine-specific options
if ($global:containerEngine -eq "docker") {
	Test-AdminPrivilege
	$global:pullOptions = @()
}
else { # Assumes podman
	$global:pullOptions = @("--tls-verify=false")
}
# Get the engine path after setting specific options
$global:enginePath = Get-EnginePath -EngineName $global:containerEngine

#==============================================================================
# Function: Get-Neo4jContainerConfig
#==============================================================================
<#
.SYNOPSIS
	Gets the current Neo4j container configuration, including environment variables.
.DESCRIPTION
	Inspects the Neo4j container using the selected engine. Extracts the image name and relevant
	environment variables. Sets up basic environment variables needed for Neo4j operation.
	If the container doesn't exist, uses default settings.
.OUTPUTS
	[PSCustomObject] Returns a custom object containing the extracted/updated configuration details
					 (Image, EnvVars) or $null if inspection fails unexpectedly.
.EXAMPLE
	$currentConfig = Get-Neo4jContainerConfig
	if ($currentConfig) { Write-Host "Current Image: $($currentConfig.Image)" }
.NOTES
	Uses 'engine inspect'. Modifies the extracted environment variables list.
#>
function Get-Neo4jContainerConfig {
	$envVars = @()
	$imageName = $global:imageName # Default image name

	$containerInfo = & $global:enginePath inspect $global:containerName 2>$null | ConvertFrom-Json
	if ($containerInfo) {
		# Container exists, try to preserve existing vars and image name
		$imageName = $containerInfo.Config.Image
		try {
			$envList = @($containerInfo.Config.Env)
			foreach ($env in $envList) {
				# Preserve existing environment variables
				if ($env -match "^(NEO4J_AUTH)=") {
					$envVars += $env
				}
			}
		}
		catch {
			Write-Warning "Could not parse existing environment variables: $_"
		}
	}
	else {
		Write-Host "Container '$global:containerName' not found. Using default settings for environment."
	}

	# Set Neo4j authentication variable
	$authExists = $false
	foreach ($env in $envVars) {
		if ($env -match "^NEO4J_AUTH=") {
			$authExists = $true
			break
		}
	}
	if (-not $authExists) {
		$envVars += "NEO4J_AUTH=neo4j/$($global:neo4jPassword)"
	}

	# Return a custom object
	return [PSCustomObject]@{
		Image   = $imageName
		EnvVars = $envVars
	}
}

#==============================================================================
# Function: Start-Neo4jContainer
#==============================================================================
<#
.SYNOPSIS
	Starts a new Neo4j container with specified configuration.
.DESCRIPTION
	Runs a new container using the selected engine with the specified image.
	Configures standard Neo4j settings: detached mode, name 'neo4j', mounts 'neo4j_data' volume
	to '/data', and maps host ports 7474 and 7687 to container ports 7474 and 7687.
	Applies any additional environment variables provided via the EnvVars parameter.
	After starting, waits 30 seconds and performs TCP connectivity tests.
	Supports -WhatIf.
.PARAMETER Image
	The Neo4j container image to use (e.g., 'neo4j:5.22.0'). Mandatory.
.PARAMETER EnvVars
	Optional array of environment variables strings (e.g., @("NEO4J_AUTH=neo4j/password")).
.OUTPUTS
	[bool] Returns $true if the container starts successfully and connectivity tests pass.
		   Returns $false if start fails, tests fail, or action is skipped due to -WhatIf.
.EXAMPLE
	Start-Neo4jContainer -Image "neo4j:5.22.0" -EnvVars @("NEO4J_AUTH=neo4j/zepzepzep")
.NOTES
	Relies on Test-TCPPort helper function.
#>
function Start-Neo4jContainer {
	[CmdletBinding(SupportsShouldProcess = $true)]
	[OutputType([bool])]
	param(
		[Parameter(Mandatory = $true)]
		[string]$Image,

		[Parameter(Mandatory = $false)]
		[array]$EnvVars = @()
	)

	# Get the host's IP as seen by Podman/WSL2
	$HostIpForContainer = (podman machine ssh "grep nameserver /etc/resolv.conf | cut -d' ' -f2").Trim()
	if (-not [string]::IsNullOrWhiteSpace($HostIpForContainer)) {
		Write-Host "Host IP for container: $HostIpForContainer"
	} else {
		Write-Error "Could not determine host IP for container."
	}
	
	# Build the run command
	$runOptions = @(
		"--env", "TZ=Europe/London", # Removed --add-host "host.local:$HostIpForContainer"
		"--detach", # Run container in background.
		"--publish", "$($global:httpPort):7474", # Map host HTTP port to container HTTP port.
		"--publish", "$($global:boltPort):7687", # Map host Bolt port to container Bolt port.
		"--volume", "$($global:volumeName):$($global:volumeMountPath)", # Mount the named volume for persistent data.
		"--name", $global:containerName, # Assign a name to the container.
		"--network", $global:networkName # Connect to the default network
	)

	# Add all environment variables
	foreach ($env in $EnvVars) {
		$runOptions += "--env"
		$runOptions += $env
	}

	# Run the container
	if ($PSCmdlet.ShouldProcess($global:containerName, "Start Container with Image '$Image'")) {
		Write-Host "Starting Neo4j container with image: $Image"
		Write-Host "& $global:enginePath machine ssh sudo $global:containerEngine run $runOptions $Image"
		& $global:enginePath machine ssh sudo $global:containerEngine run $runOptions $Image

		if ($LASTEXITCODE -eq 0) {
			Write-Host "Waiting for container startup..."
			Start-Sleep -Seconds 30

			# Test connectivity
			$boltTest = Test-TCPPort -ComputerName "localhost" -Port $global:boltPort -serviceName "$($global:containerName) Bolt"
			$httpTest = Test-TCPPort -ComputerName "localhost" -Port $global:httpPort -serviceName "$($global:containerName) HTTP" # Neo4j healthcheck is usually on Bolt or HTTP

			if ($boltTest -and $httpTest) {
				Write-Host "Neo4j is now running and accessible on Bolt port $($global:boltPort) and HTTP port $($global:httpPort)."
				return $true
			}
			else {
				Write-Warning "Neo4j container started but connectivity tests failed. Please check the container logs."
				& $global:enginePath logs --tail 100 $global:containerName
				return $false
			}
		}
		else {
			Write-Error "Failed to start Neo4j container."
			return $false
		}
	}
	else {
		return $false # Action skipped due to -WhatIf
	}
}

#==============================================================================
# Function: Install-Neo4jContainer
#==============================================================================
<#
.SYNOPSIS
	Installs and starts the Neo4j container.
.DESCRIPTION
	Checks if the Neo4j image exists locally; if not, attempts to restore from backup using
	Test-AndRestoreBackup, falling back to pulling the image using Invoke-PullImage.
	Removes any existing 'neo4j' container using Remove-ContainerAndVolume.
	Gets the container configuration including environment variables.
	Starts the new container using Start-Neo4jContainer with the determined image and environment variables.
.EXAMPLE
	Install-Neo4jContainer
.NOTES
	Orchestrates image acquisition, cleanup, environment configuration, and container start.
	Relies on Test-AndRestoreBackup, Invoke-PullImage, Remove-ContainerAndVolume, 
	Get-Neo4jContainerConfig, and Start-Neo4jContainer helper functions.
#>
function Install-Neo4jContainer {
	Write-Host "IMPORTANT: Using volume '$global:volumeName' - existing user data will be preserved."

	# Prompt for Neo4j Password if not set
	$global:neo4jPassword = Get-EnvironmentVariableWithDefault -EnvVarName 'NEO4J_PASSWORD' -DefaultValue $global:neo4jPassword -PromptText 'Neo4j Password'

	# Check if the Neo4j image is already available, restore from backup, or pull new.
	$existingImage = & $global:enginePath images --filter "reference=$($global:imageName)" --format "{{.ID}}"
	if (-not $existingImage) {
		if (-not (Test-AndRestoreBackup -Engine $global:enginePath -ImageName $global:imageName)) {
			Write-Host "No backup restored. Pulling Neo4j image '$global:imageName'..."
			# Use shared pull function
			if (-not (Invoke-PullImage -Engine $global:enginePath -ImageName $global:imageName -PullOptions $global:pullOptions)) {
				Write-Error "Image pull failed. Exiting..."
				return
			}
		}
		else {
			Write-Host "Using restored backup image '$global:imageName'."
		}
	}
	else {
		Write-Host "Neo4j image already exists. Skipping pull."
	}

	# Remove any existing container using the shared function
	# Pass container name and volume name. It will prompt about volume removal.
	Remove-ContainerAndVolume -Engine $global:enginePath -ContainerName $global:containerName -VolumeName $global:volumeName

	# Get the configuration (which includes setting default environment variables)
	$config = Get-Neo4jContainerConfig

	# Start the container using the global image name and the retrieved config
	Start-Neo4jContainer -Image $global:imageName -EnvVars $config.EnvVars
}

#==============================================================================
# Function: Update-Neo4jContainer
#==============================================================================
<#
.SYNOPSIS
	Updates the Neo4j container to the latest image version while preserving data.
.DESCRIPTION
	Orchestrates the update process:
	1. Gets the current container configuration.
	2. Prompts the user to optionally back up the current container image.
	3. Calls the simplified generic Update-Container function (handles update check, removal, pull).
	4. If core update steps succeed, calls Start-Neo4jContainer to start the new container with preserved config.
.EXAMPLE
	Update-Neo4jContainer -WhatIf
.NOTES
	Relies on Get-Neo4jContainerConfig, Backup-ContainerImage, Update-Container,
	Start-Neo4jContainer helper functions.
	User interaction handled via Read-Host for backup confirmation.
#>
function Update-Neo4jContainer {
	[CmdletBinding(SupportsShouldProcess = $true)]
	param()

	# Check ShouldProcess before proceeding
	if (-not $PSCmdlet.ShouldProcess($global:containerName, "Update Container")) {
		return
	}

	Write-Host "Initiating update for Neo4j..."
	$config = Get-Neo4jContainerConfig # Get config before potential removal
	if (-not $config) {
		Write-Error "Cannot update: Failed to get Neo4j configuration."
		return # Exit the function if config cannot be read
	}

	# Check if container actually exists before prompting for backup
	$existingContainer = & $global:enginePath ps -a --filter "name=$($global:containerName)" --format "{{.ID}}"
	if ($existingContainer) {
		$createBackup = Read-Host "Create backup before updating? (Y/N, default is Y)"
		if ($createBackup -ne "N") {
			Write-Host "Saving '$global:containerName' Container Image..."
			Backup-ContainerImage -Engine $global:enginePath -ContainerName $global:containerName
			Write-Host "Exporting '$($global:volumeName)' Volume..."
			$null = Backup-ContainerVolume -EngineType $global:containerEngine -VolumeName $global:volumeName
		}
	}
	else {
		Write-Warning "Container '$($global:containerName)' not found. Skipping backup prompt."
	}

	# Call simplified Update-Container (handles check, remove, pull)
	# Pass volume name for removal step
	if (Update-Container -Engine $global:enginePath -ContainerName $global:containerName -VolumeName $global:volumeName -ImageName $global:imageName) {
		Write-Host "Core update steps successful. Starting new container..."
		# Start the new container using the config retrieved earlier
		if (-not (Start-Neo4jContainer -Image $global:imageName -EnvVars $config.EnvVars)) {
			Write-Error "Failed to start updated Neo4j container."
		}
		# Success message is handled within Start-Neo4jContainer if successful
	}
	else {
		# Update-Container already wrote a message explaining why it returned false (e.g., no update available).
		# No need to write an error here.
	}
}

################################################################################
# Main Menu Loop using Generic Function
################################################################################

# Define Menu Title and Items
$menuTitle = "Neo4j Container & Data Management Menu"
$menuItems = [ordered]@{
	"1" = "Show Info & Test Connection"
	"2" = "Install container"
	"3" = "Uninstall container"
	"4" = "Save Image (App)"
	"5" = "Load Image (App)"
	"6" = "Update Image (App)"
	"7" = "Export Volume (Data)"
	"8" = "Import Volume (Data)"
	"9" = "Check for Updates"
	"L" = "Show Container Logs"
	"R" = "Restart Container"
	"0" = "Exit menu"
}

# Define Menu Actions
$menuActions = @{
	"1" = {
		Show-ContainerStatus -ContainerName $global:containerName `
			-ContainerEngine $global:containerEngine `
			-EnginePath $global:enginePath `
			-DisplayName $global:containerName `
			-TcpPort $global:boltPort `
			-HttpPort $global:httpPort
	}
	"2" = { Install-Neo4jContainer }
	"3" = { Remove-ContainerAndVolume -Engine $global:enginePath -ContainerName $global:containerName -VolumeName $global:volumeName }
	"4" = { Backup-ContainerImage -Engine $global:enginePath -ContainerName $global:containerName }
	"5" = { Restore-ContainerImage -Engine $global:enginePath -ContainerName $global:containerName }
	"6" = { Update-Neo4jContainer }
	"7" = { $null = Backup-ContainerVolume -EngineType $global:containerEngine -VolumeName $global:volumeName }
	"8" = {
		$null = Restore-ContainerVolume -EngineType $global:containerEngine -VolumeName $global:volumeName
		& $global:enginePath restart $global:containerName
	}
	"9" = { Test-ImageUpdateAvailable -Engine $global:enginePath -ImageName $global:imageName }
	"L" = { & $global:enginePath logs --tail 100 $global:containerName }
	"R" = { & $global:enginePath restart $global:containerName }
	# Note: "0" action is handled internally by Invoke-MenuLoop
}

# Invoke the Menu Loop
Invoke-MenuLoop -MenuTitle $menuTitle -MenuItems $menuItems -ActionMap $menuActions -ExitChoice "0"