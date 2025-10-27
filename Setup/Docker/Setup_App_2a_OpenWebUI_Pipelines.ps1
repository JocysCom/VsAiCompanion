################################################################################
# Description  : Script to set up, update, backup, restore, uninstall the Pipelines
#                container using Docker or Podman. This version installs Pipelines
#                from scratch by cloning the repository (without converting LF to CRLF)
#                and running the container with the default configuration.
# Usage        : Run as Administrator if using Docker.
################################################################################

using namespace System
using namespace System.IO

# Dot-source the necessary helper function files.
. "$PSScriptRoot\Setup_Helper_CoreFunctions.ps1"
. "$PSScriptRoot\Setup_Helper_NetworkTests.ps1"
. "$PSScriptRoot\Setup_Helper_ContainerEngine.ps1"
. "$PSScriptRoot\Setup_Helper_BackupRestore.ps1"
. "$PSScriptRoot\Setup_Helper_ContainerManagement.ps1"

# Ensure the working directory is set.
Set-ScriptLocation

#############################################
# Global Configuration
#############################################
$global:containerName = "pipelines"
$global:pipelinesFolder = ".\pipelines"
$global:downloadFolder = ".\downloads"

# Load configuration from Aspire manifest
$aspireManifestPath = Join-Path $PSScriptRoot "Files\Aspire\manifest.json"
$manifest = Get-Content -Raw $aspireManifestPath | ConvertFrom-Json
$manifestConfig = $manifest.resources.'pipelines'

# Create consolidated configuration object
$config = [PSCustomObject]@{
	imageName = $manifestConfig.properties.image
	volumeName = $manifestConfig.properties.volumes[0].name
	containerPort = $manifestConfig.properties.bindings[0].containerPort
	hostPort = $manifestConfig.properties.bindings[0].hostPort
	dataPath = $manifestConfig.properties.volumes[0].containerPath
	restartPolicy = $manifestConfig.properties.restart
	environment = $manifestConfig.properties.environment
	additionalHosts = $manifestConfig.properties.additionalHosts
}

Write-Host "Configuration loaded from Aspire manifest:"
foreach ($property in $config.PSObject.Properties) {
	$name = $property.Name
	$value = $property.Value
	if ($null -eq $value -or ($value -is [string] -and [string]::IsNullOrEmpty($value))) {
		Write-Error "Configuration property '$name' is missing or empty in manifest."
		exit 1
	}
	Write-Host "  $($name): $value"
}

# --- Engine Selection ---
$global:containerEngine = Select-ContainerEngine
# Exit if no engine was selected
if (-not $global:containerEngine) {
	Write-Warning "No container engine selected. Exiting script."
	exit 1
}
# No engine-specific options needed here, just get the path
$global:enginePath = Get-EnginePath -EngineName $global:containerEngine

# Validate that we have a valid engine path
if (-not $global:enginePath) {
	Write-Error "Failed to get path to container engine executable. Exiting."
	exit 1
}

#==============================================================================
# Function: Install-PipelinesContainer
#==============================================================================
<#
.SYNOPSIS
	Installs (or reinstalls) the Pipelines container using the official pre-built image.
.DESCRIPTION
	Ensures the required volume exists using Confirm-ContainerResource.
	Removes any existing container named 'pipelines'.
	Pulls and runs the 'ghcr.io/open-webui/pipelines:main' image.
	Configures port mapping (9099:9099), volume mount, restart policy, and container name.
	Adds '--add-host host.docker.internal:host-gateway' specifically for Docker.
	Waits 20 seconds after starting and performs TCP/HTTP connectivity tests.
.EXAMPLE
	Install-PipelinesContainer
.NOTES
	Relies on Confirm-ContainerResource, Test-TCPPort, Test-HTTPPort helper functions.
	Uses Write-Host for status messages.
#>
function Install-PipelinesContainer {
	Write-Host "Installing Pipelines using pre-built image from $($config.imageName)"

	# Ensure the volume exists
	if (-not (Confirm-ContainerResource -Engine $global:enginePath -ResourceType "volume" -ResourceName $config.volumeName)) {
		Write-Error "Failed to ensure volume '$($config.volumeName)' exists. Exiting..."
		return
	}
	Write-Host "IMPORTANT: Using volume '$($config.volumeName)' - existing user data will be preserved."

	# Remove any existing container with the same name
	$existingContainer = & $global:enginePath ps -a --filter "name=$($global:containerName)" --format "{{.ID}}"
	if ($existingContainer) {
		Write-Host "Pipelines container already exists. Removing it..."
		& $global:enginePath rm --force $global:containerName
	}

	Write-Host "Running Pipelines container..."

	# Build the run arguments array
	$runArgs = @(
		'--detach',
		'--publish', "$($config.hostPort):$($config.containerPort)",
		'--volume', "$($config.volumeName):$($config.dataPath)",
		'--restart', $config.restartPolicy,
		'--name', $global:containerName
	)

	# Add additional hosts from manifest (conditionally for Docker)
	if ($global:containerEngine -eq "docker" -and $config.additionalHosts) {
		foreach ($hostEntry in $config.additionalHosts.PSObject.Properties) {
			$runArgs += '--add-host'
			$runArgs += "$($hostEntry.Name):$($hostEntry.Value)"
		}
	}

	# Add environment variables from manifest
	foreach ($key in $config.environment.PSObject.Properties.Name) {
		$runArgs += "--env"
		$runArgs += "$key=$($config.environment.$key)"
	}

	# Add the image name
	$runArgs += $config.imageName

	& $global:enginePath run @runArgs
	if ($LASTEXITCODE -ne 0) {
		Write-Error "Failed to run the Pipelines container."
		return
	}
	Write-Host "Pipelines container is now running."

	# Wait for the container to initialize, then test connectivity
	Start-Sleep -Seconds 20
	Test-TCPPort -ComputerName "localhost" -Port $config.hostPort -serviceName $global:containerName
	Test-HTTPPort -Uri "http://localhost:$($config.hostPort)" -serviceName $global:containerName
}

# Note: Backup-PipelinesContainer, Restore-PipelinesContainer, Uninstall-PipelinesContainer functions removed. Shared functions called directly from menu.

#==============================================================================
# Function: Add-PipelineToContainer
#==============================================================================
<#
.SYNOPSIS
	Downloads a specified pipeline file and copies it into the running Pipelines container.
.DESCRIPTION
	Downloads the pipeline file from the specified URL to a temporary location.
	If using Podman, converts the temporary file's Windows path to a WSL path using ConvertTo-WSLPath.
	Copies the file into the specified destination directory within the container using 'podman machine ssh "podman cp ..."'.
	Restarts the container to load the new pipeline.
	Removes the temporary downloaded file.
.PARAMETER PipelineUrl
	The raw URL of the pipeline Python file to download. Defaults to the Azure OpenAI example.
.PARAMETER DestinationDir
	The directory path inside the container where the pipeline file should be copied. Defaults to '/app/pipelines'.
.PARAMETER ContainerName
	The name of the target Pipelines container. Defaults to the global $containerName ('pipelines').
.EXAMPLE
	Add-PipelineToContainer
.EXAMPLE
	Add-PipelineToContainer -PipelineUrl "https://example.com/my_custom_pipeline.py"
.NOTES
	Relies on Invoke-DownloadFile and ConvertTo-WSLPath helper functions.
	Uses 'podman machine ssh "podman cp ..."' for copying, which requires the Podman machine to be running.
	Uses Write-Host for status messages.
#>
function Add-PipelineToContainer {
	param(
		# URL of the pipeline file (use the raw URL)
		[string]$PipelineUrl = "https://raw.githubusercontent.com/open-webui/pipelines/main/examples/pipelines/providers/azure_openai_pipeline.py",
		# Destination directory inside the container
		[string]$DestinationDir = "/app/pipelines",
		# Container name (defaults to the global container name)
		[string]$ContainerName = $global:containerName
	)

	$fileName = "azure_openai_pipeline.py"
	# Create a temporary file path for the download (assume $global:downloadFolder is a Windows path)
	$tempFile = Join-Path $global:downloadFolder $fileName
	Write-Host "Downloading pipeline from $PipelineUrl to $tempFile..."
	# Use shared download function
	Invoke-DownloadFile -SourceUrl $PipelineUrl -DestinationPath $tempFile -ForceDownload:$true # Force download as it's temporary

	# If using Podman, convert the Windows path to WSL path
	if ($global:containerEngine -eq "podman") {
		$hostPath = ConvertTo-WSLPath -winPath $tempFile
	}
	else {
		$hostPath = $tempFile
	}

	Write-Host "Host Path: $hostPath"

	#Write-Host "Removing any existing copy of $fileName in container '$ContainerName'..."
	#& $global:enginePath exec $ContainerName rm -f "$DestinationDir/$fileName"

	Write-Host "Copying downloaded pipeline into container '$ContainerName' at '$DestinationDir'..."
	& $global:enginePath machine ssh "podman cp '$hostPath' '$($ContainerName):$DestinationDir'"

	Write-Host "Restarting container '$ContainerName' to load the new pipeline..."
	& $global:enginePath restart $ContainerName

	# Clean up the temporary file
	Remove-Item $tempFile -Force
	Write-Host "Pipeline added successfully."

	Write-Host "Reminder: In Open WebUI settings, set the OpenAI API URL to 'http://host.docker.internal:9099' and API key to '0p3n-w3bu!' if integrating pipelines."
}

#==============================================================================
# Function: Invoke-StartPipelinesForUpdate
#==============================================================================
<#
.SYNOPSIS
	Helper function called by Update-Container to start the Pipelines container after an update.
.DESCRIPTION
	This function encapsulates the specific logic required to start the Pipelines container,
	including ensuring the volume exists, setting engine-specific parameters (like --add-host for Docker),
	and running the container with the updated image name. It performs connectivity tests after starting.
	It adheres to the parameter signature expected by the -RunFunction parameter of Update-Container.
.PARAMETER EnginePath
	Path to the container engine executable (passed by Update-Container).
.PARAMETER ContainerEngineType
	Type of the container engine ('docker' or 'podman') (passed by Update-Container).
.PARAMETER ContainerName
	Name of the container being updated (passed by Update-Container).
.PARAMETER VolumeName
	Name of the volume associated with the container (passed by Update-Container).
.PARAMETER ImageName
	The new image name/tag to use for the updated container (passed by Update-Container).
.OUTPUTS
	Throws an error if the container fails to start, which signals failure back to Update-Container.
.EXAMPLE
	# This function is intended to be called internally by Update-Container via -RunFunction
	# Update-Container -RunFunction ${function:Invoke-StartPipelinesForUpdate}
.NOTES
	Relies on Confirm-ContainerVolume, Test-TCPPort, Test-HTTPPort helper functions.
	Uses Write-Host for status messages.
#>
function Invoke-StartPipelinesForUpdate {
	param(
		[string]$EnginePath,
		[string]$ContainerEngineType,
		[string]$ContainerName,
		[string]$VolumeName,
		[string]$ImageName
	)

	# Ensure the volume exists (important if it was removed manually)
	if (-not (Confirm-ContainerResource -Engine $EnginePath -ResourceType "volume" -ResourceName $VolumeName)) {
		throw "Failed to ensure volume '$VolumeName' exists during update."
	}

	# Build the run arguments array
	$runArgs = @(
		'--detach',
		'--publish', "$($config.hostPort):$($config.containerPort)",
		'--volume', "$($VolumeName):$($config.dataPath)",
		'--restart', $config.restartPolicy,
		'--name', $ContainerName
	)

	# Add additional hosts from manifest (conditionally for Docker)
	if ($ContainerEngineType -eq "docker" -and $config.additionalHosts) {
		foreach ($hostEntry in $config.additionalHosts.PSObject.Properties) {
			$runArgs += '--add-host'
			$runArgs += "$($hostEntry.Name):$($hostEntry.Value)"
		}
	}

	# Add environment variables from manifest
	foreach ($key in $config.environment.PSObject.Properties.Name) {
		$runArgs += "--env"
		$runArgs += "$key=$($config.environment.$key)"
	}

	# Add the image name
	$runArgs += $ImageName

	Write-Host "Running updated Pipelines container with image '$ImageName'..."
	& $EnginePath run @runArgs
	if ($LASTEXITCODE -ne 0) {
		Write-Error "Failed to run the updated Pipelines container."
		throw "Failed to run the updated Pipelines container."
	}
	Write-Host "Pipelines container started."

	# Wait for the container to initialize, then test connectivity
	Write-Host "Waiting for container startup..."
	Start-Sleep -Seconds 20
	Test-TCPPort -ComputerName "localhost" -Port $config.hostPort -serviceName $ContainerName
	Test-HTTPPort -Uri "http://localhost:$($config.hostPort)" -serviceName $ContainerName
}

#==============================================================================
# Function: Update-PipelinesContainer
#==============================================================================
<#
.SYNOPSIS
	Updates the Pipelines container to the latest official image using the generic update workflow.
.DESCRIPTION
	Orchestrates the update process:
	1. Prompts the user to optionally back up the current container state.
	2. Calls the simplified generic Update-Container function (handles update check, removal, pull).
	3. If core update steps succeed, calls Invoke-StartPipelinesForUpdate to start the new container.
	4. Offers to restore from backup if the start fails (and a backup was made).
.EXAMPLE
	Update-PipelinesContainer -WhatIf
.NOTES
	Relies on Backup-PipelinesContainer, Update-Container, Invoke-StartPipelinesForUpdate,
	Restore-PipelinesContainer helper functions.
	User interaction handled via Read-Host for backup confirmation.
#>
function Update-PipelinesContainer {
	[CmdletBinding(SupportsShouldProcess = $true)] # Keep ShouldProcess for overall control
	param()

	# Check ShouldProcess before proceeding
	if (-not $PSCmdlet.ShouldProcess($global:containerName, "Update Container")) {
		return
	}

	Write-Host "Initiating update for Pipelines..."
	$backupMade = $false
	# Check if container actually exists before prompting for backup
	$existingContainer = & $global:enginePath ps -a --filter "name=$($global:containerName)" --format "{{.ID}}"
	if ($existingContainer) {
		$createBackup = Read-Host "Create backup before updating? (Y/N, default is Y)"
		if ($createBackup -ne "N") {
			Write-Host "Saving '$($global:containerName)' Container Image..."
			Backup-ContainerImage -Engine $global:enginePath -ImageName $config.imageName
			Write-Host "Exporting '$($config.volumeName)' Volume..."
			$null = Backup-ContainerVolume -EngineType $global:containerEngine -VolumeName $config.volumeName
			$backupMade = $true
		}
	}
	else {
		Write-Warning "Container '$($global:containerName)' not found. Skipping backup prompt."
	}

	# Call simplified Update-Container (handles check, remove, pull)
	if (Update-Container -Engine $global:enginePath -ContainerName $global:containerName -VolumeName $config.volumeName -ImageName $config.imageName) {
		Write-Host "Core update steps successful. Starting new container..."
		try {
			Invoke-StartPipelinesForUpdate -EnginePath $global:enginePath `
				-ContainerEngineType $global:containerEngine `
				-ContainerName $global:containerName `
				-VolumeName $config.volumeName `
				-ImageName $config.imageName
			Write-Host "Pipelines container updated successfully!"
		}
		catch {
			Write-Error "Failed to start updated Pipelines container: $_"
			if ($backupMade) {
				$restore = Read-Host "Would you like to restore from backup? (Y/N, default is Y)"
				if ($restore -ne "N") {
					Write-Host "Loading '$($global:containerName)' Container Image..."
					Test-AndRestoreBackup -Engine $global:enginePath -ImageName $config.imageName
					Write-Host "Importing '$($config.volumeName)' Volume..."
					$null = Restore-ContainerVolume -EngineType $global:containerEngine -VolumeName $config.volumeName
				}
			}
		}
	}
	else {
		Write-Error "Update process failed during check, removal, or pull."
		if ($backupMade) {
			$restore = Read-Host "Would you like to restore from backup? (Y/N, default is Y)"
			if ($restore -ne "N") {
				Write-Host "Loading '$($global:containerName)' Container Image..."
				Test-AndRestoreBackup -Engine $global:enginePath -ImageName $config.imageName
				Write-Host "Importing '$($config.volumeName)' Volume..."
				$null = Restore-ContainerVolume -EngineType $global:containerEngine -VolumeName $config.volumeName
			}
		}
	}
}

################################################################################
# Main Menu Loop using Generic Function
################################################################################

# Define Menu Title and Items
$menuTitle = "Pipelines Container Menu"
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
	"A" = "Add Pipeline"
	"0" = "Exit menu"
}

# Define Menu Actions
$menuActions = @{
	"1" = {
		$hostPort = $config.hostPort
		Show-ContainerStatus -ContainerName $global:containerName `
			-ContainerEngine $global:containerEngine `
			-EnginePath $global:enginePath `
			-DisplayName "Pipelines" `
			-TcpPort $hostPort `
			-HttpPort $hostPort
	}
	"2" = { Install-PipelinesContainer }
	"3" = {
		$volumeName = $config.volumeName
		Remove-ContainerAndVolume -Engine $global:enginePath -ContainerName $global:containerName -VolumeName $volumeName
	}
	"4" = {
		$imageName = $config.imageName
		Backup-ContainerImage -Engine $global:enginePath -ImageName $imageName
	}
	"5" = {
		$imageName = $config.imageName
		Test-AndRestoreBackup -Engine $global:enginePath -ImageName $imageName
	}
	"6" = { Update-PipelinesContainer }
	"7" = {
		$volumeName = $config.volumeName
		$null = Backup-ContainerVolume -EngineType $global:containerEngine -VolumeName $volumeName
	}
	"8" = {
		$volumeName = $config.volumeName
		$null = Restore-ContainerVolume -EngineType $global:containerEngine -VolumeName $volumeName
		& $global:enginePath restart $global:containerName
	}
	"9" = {
		$imageName = $config.imageName
		Test-ImageUpdateAvailable -Engine $global:enginePath -ImageName $imageName
	}
	"A" = { Add-PipelineToContainer }
}

# Invoke the Menu Loop
Invoke-MenuLoop -MenuTitle $menuTitle -MenuItems $menuItems -ActionMap $menuActions -ExitChoice "0"
