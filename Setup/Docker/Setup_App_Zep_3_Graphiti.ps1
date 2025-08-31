################################################################################
# Description  : Script to set up and run the Graphiti container using Docker/Podman.
#                Verifies volume presence, pulls the Graphiti image if necessary,
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
$global:imageName = "zepai/graphiti:0.3"
$global:containerName = "graphiti"
$global:containerPort = 8003
$global:networkName = "podman" # Reverted to "podman" as per user instruction

# New global variables for Neo4j dependency
$global:neo4jContainerName = "neo4j"
$global:neo4jBoltPort = 7687
$global:neo4jHttpPort = 7474
$global:neo4jUser = "neo4j"
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
# Function: Test-Neo4jRequirement
#==============================================================================
<#
.SYNOPSIS
    Tests if Neo4j container is available and accessible.
.DESCRIPTION
    Checks if the Neo4j container is running and accessible on the expected ports.
    This is a prerequisite for Graphiti operation.
.PARAMETER ContainerName
    The name of the Neo4j container to test.
.PARAMETER EnginePath
    The path to the container engine executable.
.PARAMETER BoltPort
    The Neo4j Bolt port number.
.PARAMETER HttpPort
    The Neo4j HTTP port number.
.OUTPUTS
    [bool] Returns $true if Neo4j is available and ready, $false otherwise.
.EXAMPLE
    Test-Neo4jRequirement -ContainerName "neo4j" -EnginePath $global:enginePath -BoltPort 7687 -HttpPort 7474
.NOTES
    This function is essential to ensure Graphiti has a working Neo4j backend before starting.
#>
function Test-Neo4jRequirement {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory=$true)]
        [string]$ContainerName,

        [Parameter(Mandatory=$true)]
        [string]$EnginePath,

        [Parameter(Mandatory=$true)]
        [int]$BoltPort,

        [Parameter(Mandatory=$true)]
        [int]$HttpPort
    )

    Write-Host "Checking Neo4j requirement for Graphiti..."

    # Check if Neo4j container is running
    $containerStatus = & $EnginePath ps --filter "name=$ContainerName" --filter "status=running" --format "{{.Names}}"
    if (-not $containerStatus -or $containerStatus -ne $ContainerName) {
        Write-Warning "Neo4j container '$ContainerName' is not running."
        Write-Host "Please run Setup_App_Zep_2_Neo4j.ps1 first to install and start Neo4j."
        return $false
    }

    # Test TCP connectivity for Bolt port
    if (-not (Test-TCPPort -ComputerName "localhost" -Port $BoltPort -serviceName "Neo4j Bolt")) {
        Write-Warning "Neo4j Bolt is not accessible on port $BoltPort."
        return $false
    }

    # Test TCP connectivity for HTTP port
    if (-not (Test-TCPPort -ComputerName "localhost" -Port $HttpPort -serviceName "Neo4j HTTP")) {
        Write-Warning "Neo4j HTTP is not accessible on port $HttpPort."
        return $false
    }

    Write-Host "Neo4j requirement satisfied: Container running and accessible."
    return $true
}

#==============================================================================
# Function: Get-GraphitiContainerConfig
#==============================================================================
<#
.SYNOPSIS
	Gets the current Graphiti container configuration, including environment variables.
.DESCRIPTION
	Inspects the Graphiti container using the selected engine. Extracts the image name and relevant
	environment variables. Sets up basic environment variables needed for Graphiti operation.
	If the container doesn't exist, uses default settings.
.OUTPUTS
	[PSCustomObject] Returns a custom object containing the extracted/updated configuration details
					 (Image, EnvVars) or $null if inspection fails unexpectedly.
.EXAMPLE
	$currentConfig = Get-GraphitiContainerConfig
	if ($currentConfig) { Write-Host "Current Image: $($currentConfig.Image)" }
.NOTES
	Uses 'engine inspect'. Modifies the extracted environment variables list.
#>
function Get-GraphitiContainerConfig {
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
				if ($env -match "^(OPENAI_API_KEY|MODEL_NAME|NEO4J_URI|NEO4J_USER|NEO4J_PASSWORD|PORT)=") {
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

	# Set basic Graphiti environment variables from docker-compose.ce.yaml
	$envVars += "MODEL_NAME=gpt-4.1"
	$envVars += "NEO4J_URI=bolt://$($global:neo4jContainerName):$($global:neo4jBoltPort)"
	$envVars += "NEO4J_USER=$($global:neo4jUser)"
	$envVars += "NEO4J_PASSWORD=$($global:neo4jPassword)"
	$envVars += "PORT=$($global:containerPort)"

	# Return a custom object
	return [PSCustomObject]@{
		Image   = $imageName
		EnvVars = $envVars
	}
}

#==============================================================================
# Function: Start-GraphitiContainer
#==============================================================================
<#
.SYNOPSIS
	Starts a new Graphiti container with specified configuration.
.DESCRIPTION
	Runs a new container using the selected engine with the specified image.
	Configures standard Graphiti settings: detached mode, name 'graphiti',
	and maps host port 8003 to container port 8003.
	Applies any additional environment variables provided via the EnvVars parameter.
	After starting, waits 30 seconds and performs TCP and HTTP connectivity tests.
	Supports -WhatIf.
.PARAMETER Image
	The Graphiti container image to use (e.g., 'zepai/graphiti:0.3'). Mandatory.
.PARAMETER EnvVars
	Optional array of environment variables strings (e.g., @("OPENAI_API_KEY=xyz")).
.OUTPUTS
	[bool] Returns $true if the container starts successfully and connectivity tests pass.
		   Returns $false if start fails, tests fail, or action is skipped due to -WhatIf.
.EXAMPLE
	Start-GraphitiContainer -Image "zepai/graphiti:0.3" -EnvVars @("OPENAI_API_KEY=xyz")
.NOTES
	Relies on Test-TCPPort and Test-HTTPPort helper functions.
#>
function Start-GraphitiContainer {
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

	# No need to get Neo4j IP, as we are using default network and container name resolution
	# $Neo4jIp = (podman machine ssh "sudo podman inspect $($global:neo4jContainerName) --format '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}'").Trim()
	# if (-not [string]::IsNullOrWhiteSpace($Neo4jIp)) {
	# 	Write-Host "Neo4j container IP: $Neo4jIp"
	# } else {
	# 	Write-Warning "Could not determine Neo4j container IP. Ensure $($global:neo4jContainerName) is running."
	# }

	# Build the run command
	$runOptions = @(
		"--env", "TZ=Europe/London", # Removed --add-host "host.local:$HostIpForContainer"
		"--detach", # Run container in background.
		"--publish", "$($global:containerPort):$($global:containerPort)", # Map host port to container port.
		"--name", $global:containerName, # Assign a name to the container.
		"--network", $global:networkName # Connect to the same network as Neo4j
	)

	# Add Neo4j hostname mapping for direct IP resolution
	$Neo4jIp = (podman machine ssh "sudo podman inspect $($global:neo4jContainerName) --format '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}'").Trim()
	if (-not [string]::IsNullOrWhiteSpace($Neo4jIp)) {
		$runOptions += "--add-host"
		$runOptions += "$($global:neo4jContainerName):$Neo4jIp"
		Write-Host "Adding host mapping: $($global:neo4jContainerName) -> $Neo4jIp"
	} else {
		Write-Warning "Could not determine Neo4j container IP. Ensure $($global:neo4jContainerName) is running and accessible on the 'podman' network."
	}

	# Add all environment variables
	foreach ($env in $EnvVars) {
		$runOptions += "--env"
		$runOptions += $env
	}

	# Run the container
	if ($PSCmdlet.ShouldProcess($global:containerName, "Start Container with Image '$Image'")) {
		Write-Host "Starting Graphiti container with image: $Image"
		Write-Host "& $global:enginePath machine ssh sudo $global:containerEngine run $runOptions $Image"
		& $global:enginePath machine ssh sudo $global:containerEngine run $runOptions $Image

		if ($LASTEXITCODE -eq 0) {
			Write-Host "Waiting for container startup..."
			Start-Sleep -Seconds 30

			# Test connectivity
			$tcpTest = Test-TCPPort -ComputerName "localhost" -Port $global:containerPort -serviceName $global:containerName
			$httpTest = Test-HTTPPort -Uri "http://localhost:$($global:containerPort)/healthcheck" -serviceName $global:containerName # Healthcheck endpoint

			if ($tcpTest -and $httpTest) {
				Write-Host "Graphiti is now running and accessible at http://localhost:$($global:containerPort)"
				Write-Host "If accessing from another container, use 'http://host.docker.internal:$($global:containerPort)' as the URL."
				return $true
			}
			else {
				Write-Warning "Graphiti container started but connectivity tests failed. Please check the container logs."
				& $global:enginePath logs --tail 100 $global:containerName
				return $false
			}
		}
		else {
			Write-Error "Failed to start Graphiti container."
			return $false
		}
	}
	else {
		return $false # Action skipped due to -WhatIf
	}
}

#==============================================================================
# Function: Install-GraphitiContainer
#==============================================================================
<#
.SYNOPSIS
	Installs and starts the Graphiti container.
.DESCRIPTION
	Checks if the Graphiti image exists locally; if not, attempts to restore from backup using
	Test-AndRestoreBackup, falling back to pulling the image using Invoke-PullImage.
	Removes any existing 'graphiti' container using Remove-ContainerAndVolume.
	Gets the container configuration including environment variables.
	Starts the new container using Start-GraphitiContainer with the determined image and environment variables.
.EXAMPLE
	Install-GraphitiContainer
.NOTES
	Orchestrates image acquisition, cleanup, environment configuration, and container start.
	Relies on Test-AndRestoreBackup, Invoke-PullImage, Remove-ContainerAndVolume,
	Get-GraphitiContainerConfig, and Start-GraphitiContainer helper functions.
#>
function Install-GraphitiContainer {
	Write-Host "IMPORTANT: Graphiti does not use a persistent volume for its own data."

	# Test Neo4j requirement before proceeding
	if (-not (Test-Neo4jRequirement -ContainerName $global:neo4jContainerName -EnginePath $global:enginePath -BoltPort $global:neo4jBoltPort -HttpPort $global:neo4jHttpPort)) {
		Write-Error "Neo4j requirement not met. Graphiti installation cannot proceed."
		Write-Host "Please run Setup_App_Zep_2_Neo4j.ps1 first to install and start Neo4j."
		return
	}

	# Prompt for OpenAI API Key if not set
	$global:zepOpenAiApiKey = Get-EnvironmentVariableWithDefault -EnvVarName 'OPENAI_API_KEY' -DefaultValue "dummy_openai_api_key" -PromptText 'OpenAI API Key for Graphiti'

	# Check if the Graphiti image is already available, restore from backup, or pull new.
	$existingImage = & $global:enginePath images --filter "reference=$($global:imageName)" --format "{{.ID}}"
	if (-not $existingImage) {
		if (-not (Test-AndRestoreBackup -Engine $global:enginePath -ImageName $global:imageName)) {
			Write-Host "No backup restored. Pulling Graphiti image '$global:imageName'..."
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
		Write-Host "Graphiti image already exists. Skipping pull."
	}

	# Remove any existing container using the shared function
	# Graphiti does not use a named volume for its own data, so volumeName is $null
	Remove-ContainerAndVolume -Engine $global:enginePath -ContainerName $global:containerName -VolumeName $null

	# Get the configuration (which includes setting default environment variables)
	$config = Get-GraphitiContainerConfig

	# Add the obtained OpenAI API Key to the environment variables
	$config.EnvVars += "OPENAI_API_KEY=$($global:zepOpenAiApiKey)"

	# Start the container using the global image name and the retrieved config
	Start-GraphitiContainer -Image $global:imageName -EnvVars $config.EnvVars
}

#==============================================================================
# Function: Update-GraphitiContainer
#==============================================================================
<#
.SYNOPSIS
	Updates the Graphiti container to the latest image version.
.DESCRIPTION
	Orchestrates the update process:
	1. Gets the current container configuration.
	2. Prompts the user to optionally back up the current container image.
	3. Calls the simplified generic Update-Container function (handles update check, removal, pull).
	4. If core update steps succeed, calls Start-GraphitiContainer to start the new container with preserved config.
.EXAMPLE
	Update-GraphitiContainer -WhatIf
.NOTES
	Relies on Get-GraphitiContainerConfig, Backup-ContainerImage, Update-Container,
	Start-GraphitiContainer helper functions.
	User interaction handled via Read-Host for backup confirmation.
#>
function Update-GraphitiContainer {
	[CmdletBinding(SupportsShouldProcess = $true)]
	param()

	# Check ShouldProcess before proceeding
	if (-not $PSCmdlet.ShouldProcess($global:containerName, "Update Container")) {
		return
	}

	Write-Host "Initiating update for Graphiti..."
	$config = Get-GraphitiContainerConfig # Get config before potential removal
	if (-not $config) {
		Write-Error "Cannot update: Failed to get Graphiti configuration."
		return # Exit the function if config cannot be read
	}

	# Check if container actually exists before prompting for backup
	$existingContainer = & $global:enginePath ps -a --filter "name=$($global:containerName)" --format "{{.ID}}"
	if ($existingContainer) {
		$createBackup = Read-Host "Create backup before updating? (Y/N, default is Y)"
		if ($createBackup -ne "N") {
			Write-Host "Saving '$global:containerName' Container Image..."
			Backup-ContainerImage -Engine $global:enginePath -ImageName $global:imageName
			# Graphiti does not use a named volume for its own data, so no volume backup
		}
	}
	else {
		Write-Warning "Container '$($global:containerName)' not found. Skipping backup prompt."
	}

	# Call simplified Update-Container (handles check, remove, pull)
	# Graphiti does not use a named volume for its own data, so volumeName is $null
	if (Update-Container -Engine $global:enginePath -ContainerName $global:containerName -VolumeName $null -ImageName $global:imageName) {
		Write-Host "Core update steps successful. Starting new container..."
		# Start the new container using the config retrieved earlier
		if (-not (Start-GraphitiContainer -Image $global:imageName -EnvVars $config.EnvVars)) {
			Write-Error "Failed to start updated Graphiti container."
		}
		# Success message is handled within Start-GraphitiContainer if successful
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
$menuTitle = "Graphiti Container Management Menu"
$menuItems = [ordered]@{
	"1" = "Show Info & Test Connection"
	"2" = "Install container"
	"3" = "Uninstall container"
	"4" = "Save Image (App)"
	"5" = "Load Image (App)"
	"6" = "Update Image (App)"
	"9" = "Check for Updates"
	"N" = "Test Neo4j Requirement"
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
			-TcpPort $global:containerPort `
			-HttpPort $global:containerPort `
			-HttpHealthCheckPath "/healthcheck" # Specify healthcheck path
	}
	"2" = { Install-GraphitiContainer }
	"3" = { Remove-ContainerAndVolume -Engine $global:enginePath -ContainerName $global:containerName -VolumeName $null }
	"4" = { Backup-ContainerImage -Engine $global:enginePath -ImageName $global:imageName }
	"5" = { Test-AndRestoreBackup -Engine $global:enginePath -ImageName $global:imageName }
	"6" = { Update-GraphitiContainer }
	"9" = { Test-ImageUpdateAvailable -Engine $global:enginePath -ImageName $global:imageName }
	"N" = { Test-Neo4jRequirement -ContainerName $global:neo4jContainerName -EnginePath $global:enginePath -BoltPort $global:neo4jBoltPort -HttpPort $global:neo4jHttpPort }
	"L" = { & $global:enginePath logs --tail 100 $global:containerName }
	"R" = { & $global:enginePath restart $global:containerName }
	# Note: "0" action is handled internally by Invoke-MenuLoop
}

# Invoke the Menu Loop
Invoke-MenuLoop -MenuTitle $menuTitle -MenuItems $menuItems -ActionMap $menuActions -ExitChoice "0"