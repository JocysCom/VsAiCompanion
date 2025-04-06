################################################################################
# File         : Setup_3_n8n.ps1
# Description  : Script to set up and run the n8n container using Docker/Podman.
#                Verifies volume presence, pulls the n8n image if necessary,
#                and runs the container with port and volume mappings.
#                Additionally, prompts for an external domain to set N8N_HOST
#                and WEBHOOK_URL if needed.
# Usage        : Run as Administrator if using Docker.
################################################################################

using namespace System
using namespace System.IO
using namespace System.Diagnostics.CodeAnalysis

# Set Information Preference to show Write-Information messages by default
$InformationPreference = 'Continue'

# Dot-source the necessary helper function files.
. "$PSScriptRoot\Setup_0_Core.ps1"
. "$PSScriptRoot\Setup_0_Network.ps1"
. "$PSScriptRoot\Setup_0_ContainerEngine.ps1"
. "$PSScriptRoot\Setup_0_BackupRestore.ps1"
. "$PSScriptRoot\Setup_0_ContainerMgmt.ps1"

# Ensure the script working directory is set.
Set-ScriptLocation

#############################################
# Global Variables
#############################################
# Note: PSAvoidGlobalVars warnings are ignored here as these are used across menu actions.
$global:containerName = "n8n"
$global:volumeName = "n8n_data"
$global:containerEngine = Select-ContainerEngine
# Exit if no engine was selected
if (-not $global:containerEngine) {
	Write-Warning "No container engine selected. Exiting script."
	exit 1
}
if ($global:containerEngine -eq "docker") {
	Test-AdminPrivilege
	$global:enginePath = Get-DockerPath
	$global:pullOptions = @()  # No extra options needed for Docker.
	$global:imageName = "docker.n8n.io/n8nio/n8n:latest"
}
else {
	$global:enginePath = Get-PodmanPath
	$global:pullOptions = @("--tls-verify=false")
	# Use the Docker Hub version of n8n for Podman to avoid 403 errors.
	$global:imageName = "docker.io/n8nio/n8n:latest"
}

#==============================================================================
# Function: Get-n8nContainerConfig
#==============================================================================
<#
.SYNOPSIS
	Gets the current n8n container configuration, including environment variables, and prompts for external domain.
.DESCRIPTION
	Inspects the n8n container using the selected engine. Extracts the image name and relevant
	environment variables (starting with N8N_ or WEBHOOK_). Ensures community packages and tool usage
	are enabled by adding the respective environment variables if missing. Prompts the user via Read-Host
	to enter an external domain and adds N8N_HOST and WEBHOOK_URL environment variables if provided.
.OUTPUTS
	[PSCustomObject] Returns a custom object containing the extracted/updated configuration details
					 (Image, EnvVars) or $null if the container is not found or inspection fails.
.EXAMPLE
	$currentConfig = Get-n8nContainerConfig
	if ($currentConfig) { Write-Host "Current Image: $($currentConfig.Image)" }
.NOTES
	Uses 'engine inspect'. Modifies the extracted environment variables list.
	Requires user interaction via Read-Host for domain configuration.
#>
function Get-n8nContainerConfig {
	$containerInfo = & $global:enginePath inspect $global:containerName 2>$null | ConvertFrom-Json
	if (-not $containerInfo) {
		Write-Information "Container '$global:containerName' not found."
		return $null
	}

	# Extract environment variables
	$envVars = @()
	try {
		# Handle potential single vs multiple env vars
		$envList = @($containerInfo.Config.Env)
		foreach ($env in $envList) {
			# Only preserve n8n-specific environment variables
			if ($env -match "^(N8N_|WEBHOOK_)") {
				$envVars += $env
			}
		}
	}
	catch {
		Write-Warning "Could not parse existing environment variables: $_"
	}

	# Ensure N8N_COMMUNITY_PACKAGES_ENABLED is set to true
	$communityPackagesEnabled = $false
	$communityPackagesToolsEnabled = $false
	foreach ($env in $envVars) {
		if ($env -match "^N8N_COMMUNITY_PACKAGES_ENABLED=") {
			$communityPackagesEnabled = $true
		}
		if ($env -match "^N8N_COMMUNITY_PACKAGES_ALLOW_TOOL_USAGE=") {
			$communityPackagesToolsEnabled = $true
		}
	}
	if (-not $communityPackagesEnabled) {
		$envVars += "N8N_COMMUNITY_PACKAGES_ENABLED=true"
	}
	if (-not $communityPackagesToolsEnabled) {
		$envVars += "N8N_COMMUNITY_PACKAGES_ALLOW_TOOL_USAGE=true"
	}

	# Prompt user for external domain configuration.
	$externalDomain = Read-Host "Enter external domain for n8n container (e.g., n8n.example.com) or press Enter to skip"

	# If an external domain is provided, add environment variable options.
	if (-not [string]::IsNullOrWhiteSpace($externalDomain)) {
		$envVars += "N8N_HOST=$externalDomain"
		$envVars += "WEBHOOK_URL=https://$externalDomain"
	}

	# Return a custom object with the container information
	return [PSCustomObject]@{
		Image   = $containerInfo.Config.Image
		EnvVars = $envVars
	}
}

#==============================================================================
# Function: Remove-n8nContainer
#==============================================================================
<#
.SYNOPSIS
	Stops and removes the n8n container.
.DESCRIPTION
	Checks if a container named 'n8n' exists. If it does, it stops and removes it
	using the selected container engine. Supports -WhatIf.
.OUTPUTS
	[bool] Returns $true if the container is removed successfully or didn't exist.
		   Returns $false if removal fails or is skipped due to -WhatIf.
.EXAMPLE
	Remove-n8nContainer -WhatIf
.NOTES
	Uses 'engine ps', 'engine stop', and 'engine rm'.
	Explicitly notes that the 'n8n_data' volume is not removed by this function.
#>
function Remove-n8nContainer {
	[CmdletBinding(SupportsShouldProcess = $true)]
	[OutputType([bool])]
	param()

	$existingContainer = & $global:enginePath ps --all --filter "name=$global:containerName" --format "{{.ID}}"
	if (-not $existingContainer) {
		Write-Information "No n8n container found to remove."
		return $true
	}

	if ($PSCmdlet.ShouldProcess($global:containerName, "Stop and Remove Container")) {
		Write-Information "Stopping and removing n8n container..."
		Write-Information "NOTE: This only removes the container, not the volume with user data."

		& $global:enginePath stop $global:containerName 2>$null
		& $global:enginePath rm $global:containerName

		if ($LASTEXITCODE -eq 0) {
			return $true
		}
		else {
			Write-Error "Failed to remove n8n container."
			return $false
		}
	}
	else {
		return $false # Action skipped due to -WhatIf
	}
}

#==============================================================================
# Function: Start-n8nContainer
#==============================================================================
<#
.SYNOPSIS
	Starts a new n8n container with specified configuration.
.DESCRIPTION
	Runs a new container using the selected engine with the specified image.
	Configures standard n8n settings: detached mode, name 'n8n', mounts 'n8n_data' volume
	to '/home/node/.n8n', and maps host port 5678 to container port 5678.
	Applies any additional environment variables provided via the EnvVars parameter.
	After starting, waits 20 seconds and performs TCP and HTTP connectivity tests.
	Supports -WhatIf.
.PARAMETER Image
	The n8n container image to use (e.g., 'docker.n8n.io/n8nio/n8n:latest'). Mandatory.
.PARAMETER EnvVars
	Optional array of environment variables strings (e.g., @("N8N_HOST=n8n.example.com")).
.OUTPUTS
	[bool] Returns $true if the container starts successfully and connectivity tests pass.
		   Returns $false if start fails, tests fail, or action is skipped due to -WhatIf.
.EXAMPLE
	Start-n8nContainer -Image "docker.io/n8nio/n8n:latest" -EnvVars @("N8N_ENCRYPTION_KEY=secret")
.NOTES
	Relies on Test-TCPPort and Test-HTTPPort helper functions.
	Uses Write-Information for status messages.
#>
function Start-n8nContainer {
	[CmdletBinding(SupportsShouldProcess = $true)]
	[OutputType([bool])]
	param(
		[Parameter(Mandatory = $true)]
		[string]$Image,

		[Parameter(Mandatory = $false)]
		[array]$EnvVars = @()
	)

	# Build the run command
	$runOptions = @(
		"--detach", # Run container in background.
		"--publish", "5678:5678", # Map host port 5678 to container port 5678.
		"--volume", "$($global:volumeName):/home/node/.n8n", # Mount the named volume for persistent data.
		"--name", $global:containerName         # Assign a name to the container.
	)

	# Add all environment variables
	foreach ($env in $EnvVars) {
		$runOptions += "--env"
		$runOptions += $env
	}

	# Run the container
	if ($PSCmdlet.ShouldProcess($global:containerName, "Start Container with Image '$Image'")) {
		Write-Information "Starting n8n container with image: $Image"
		& $global:enginePath run $runOptions $Image

		if ($LASTEXITCODE -eq 0) {
			Write-Information "Waiting for container startup..."
			Start-Sleep -Seconds 20

			# Test connectivity
			$tcpTest = Test-TCPPort -ComputerName "localhost" -Port 5678 -serviceName "n8n"
			$httpTest = Test-HTTPPort -Uri "http://localhost:5678" -serviceName "n8n"

			if ($tcpTest -and $httpTest) {
				Write-Information "n8n is now running and accessible at http://localhost:5678"
				Write-Information "If accessing from another container, use 'http://host.docker.internal:5678' as the URL."
				return $true
			}
			else {
				Write-Warning "n8n container started but connectivity tests failed. Please check the container logs."
				return $false
			}
		}
		else {
			Write-Error "Failed to start n8n container."
			return $false
		}
	}
	else {
		return $false # Action skipped due to -WhatIf
	}
}

#==============================================================================
# Function: Install-n8nContainer
#==============================================================================
<#
.SYNOPSIS
	Installs and starts the n8n container.
.DESCRIPTION
	Ensures the 'n8n_data' volume exists using Confirm-ContainerVolume.
	Checks if the n8n image exists locally; if not, attempts to restore from backup using
	Test-AndRestoreBackup, falling back to pulling the image using Invoke-PullImage.
	Removes any existing 'n8n' container using Remove-n8nContainer.
	Defines default environment variables (enabling community packages/tools).
	Prompts the user for an optional external domain to set N8N_HOST and WEBHOOK_URL.
	Starts the new container using Start-n8nContainer with the determined image and environment variables.
.EXAMPLE
	Install-n8nContainer
.NOTES
	Orchestrates volume creation, image acquisition, cleanup, environment configuration, and container start.
	Relies on Confirm-ContainerVolume, Test-AndRestoreBackup, Invoke-PullImage,
	Remove-n8nContainer, and Start-n8nContainer helper functions.
	Requires user interaction via Read-Host for domain configuration.
	Uses Write-Information for status messages.
#>
function Install-n8nContainer {
	# Ensure the volume exists
	if (-not (Confirm-ContainerVolume -Engine $global:enginePath -VolumeName $global:volumeName)) {
		Write-Error "Failed to ensure volume '$global:volumeName' exists. Exiting..."
		return
	}
	Write-Information "IMPORTANT: Using volume '$global:volumeName' - existing user data will be preserved."

	# Check if the n8n image is already available, restore from backup, or pull new.
	$existingImage = & $global:enginePath images --filter "reference=$($global:imageName)" --format "{{.ID}}"
	if (-not $existingImage) {
		if (-not (Test-AndRestoreBackup -Engine $global:enginePath -ImageName $global:imageName)) {
			Write-Information "No backup restored. Pulling n8n image '$global:imageName'..."
			# Use shared pull function
			if (-not (Invoke-PullImage -Engine $global:enginePath -ImageName $global:imageName -PullOptions $global:pullOptions)) {
				Write-Error "Image pull failed. Exiting..."
				return
			}
		}
		else {
			Write-Information "Using restored backup image '$global:imageName'."
		}
	}
	else {
		Write-Information "n8n image already exists. Skipping pull."
	}

	# Remove any existing container
	Remove-n8nContainer # This function now supports ShouldProcess

	# Define environment variables
	$envVars = @()

	$envVars += "N8N_COMMUNITY_PACKAGES_ENABLED=true"
	$envVars += "N8N_COMMUNITY_PACKAGES_ALLOW_TOOL_USAGE=true"

	# Prompt user for external domain configuration.
	$externalDomain = Read-Host "Enter external domain for n8n container (e.g., n8n.example.com) or press Enter to skip"

	# If an external domain is provided, add environment variable options.
	if (-not [string]::IsNullOrWhiteSpace($externalDomain)) {
		$envVars += "N8N_HOST=$externalDomain"
		$envVars += "WEBHOOK_URL=https://$externalDomain"
	}

	# Start the container
	Start-n8nContainer -Image $global:imageName -EnvVars $envVars # This function now supports ShouldProcess
}

#==============================================================================
# Function: Uninstall-n8nContainer
#==============================================================================
<#
.SYNOPSIS
	Uninstalls the n8n container and optionally removes its data volume.
.DESCRIPTION
	Calls the Remove-ContainerAndVolume helper function, specifying 'n8n' as the container
	and 'n8n_data' as the volume. This will stop/remove the container and prompt the user
	about removing the volume. Supports -WhatIf.
.EXAMPLE
	Uninstall-n8nContainer -Confirm:$false
.NOTES
	Relies on Remove-ContainerAndVolume helper function.
#>
function Uninstall-n8nContainer {
	Remove-ContainerAndVolume -Engine $global:enginePath -ContainerName $global:containerName -VolumeName $global:volumeName # This function supports ShouldProcess
}

#==============================================================================
# Function: Backup-n8nContainer
#==============================================================================
<#
.SYNOPSIS
	Backs up the state of the running n8n container.
.DESCRIPTION
	Calls the Backup-ContainerState helper function, specifying 'n8n' as the container name.
	This commits the container state to an image and saves it as a tar file. Includes debug output.
.EXAMPLE
	Backup-n8nContainer
.NOTES
	Relies on Backup-ContainerState helper function. Supports -WhatIf via the helper function.
#>
function Backup-n8nContainer {
	Write-Information "Backing up n8n container..."

	# Debug output to verify the container name being passed
	Write-Information "DEBUG: Passing container name '$global:containerName' to Backup-ContainerState"

	# Call Backup-ContainerState with the explicit container name string
	if (Backup-ContainerState -Engine $global:enginePath -ContainerName $global:containerName) {
		# This function supports ShouldProcess
		Write-Information "n8n container backed up successfully."
		return $true
	}
	else {
		Write-Error "Failed to backup n8n container."
		return $false
	}
}

#==============================================================================
# Function: Restore-n8nContainer
#==============================================================================
<#
.SYNOPSIS
	Restores the n8n container and its data volume from backup, then starts the container.
.DESCRIPTION
	Calls Restore-ContainerState (with -RestoreVolumes) to load the image and restore volume data.
	If successful, removes any existing 'n8n' container.
	Retrieves existing configuration using Get-n8nContainerConfig (or defaults if none).
	Starts the container using Start-n8nContainer with the restored image and configuration.
.EXAMPLE
	Restore-n8nContainer
.NOTES
	Relies on Restore-ContainerState, Remove-n8nContainer, Get-n8nContainerConfig,
	and Start-n8nContainer helper functions. Supports -WhatIf via helper functions.
#>
function Restore-n8nContainer {
	Write-Information "Attempting to restore n8n container and its data volume from backup..."

	# First load the image from backup and restore volumes if available
	$imageName = Restore-ContainerState -Engine $global:enginePath -ContainerName $global:containerName -RestoreVolumes # This function supports ShouldProcess

	if (-not $imageName) {
		Write-Error "Failed to restore image for n8n container."
		return
	}

	# Remove any existing container
	Remove-n8nContainer # This function supports ShouldProcess

	# Get configuration from existing container or use defaults
	$config = Get-n8nContainerConfig
	if (-not $config) {
		Write-Information "No existing configuration found, using defaults."
		$config = [PSCustomObject]@{
			Image   = $imageName
			EnvVars = @(
				"N8N_COMMUNITY_PACKAGES_ENABLED=true",
				"N8N_COMMUNITY_PACKAGES_ALLOW_TOOL_USAGE=true"
			)
		}
	}
	else {
		# Update the image to use the restored one
		$config.Image = $imageName
	}

	# Start the container using the restored image and existing configuration
	if (Start-n8nContainer -Image $config.Image -EnvVars $config.EnvVars) {
		# This function supports ShouldProcess
		Write-Information "n8n container successfully restored and started."
	}
	else {
		Write-Error "Failed to start restored n8n container."
	}
}

#==============================================================================
# Function: Invoke-StartN8nForUpdate
#==============================================================================
<#
.SYNOPSIS
	Helper function called by Update-Container to start the n8n container after an update.
.DESCRIPTION
	This function encapsulates the specific logic required to start the n8n container after an update.
	It ensures the volume exists, retrieves existing configuration (like domain settings) using
	Get-n8nContainerConfig, and then calls Start-n8nContainer with the updated image name and
	preserved environment variables. It adheres to the parameter signature expected by the
	-RunFunction parameter of Update-Container.
.PARAMETER EnginePath
	Path to the container engine executable (passed by Update-Container).
.PARAMETER ContainerEngineType
	Type of the container engine ('docker' or 'podman'). (Passed by Update-Container, may not be used directly).
.PARAMETER ContainerName
	Name of the container being updated. (Passed by Update-Container, may not be used directly).
.PARAMETER VolumeName
	Name of the volume associated with the container. (Passed by Update-Container).
.PARAMETER ImageName
	The new image name/tag to use for the updated container (passed by Update-Container).
.OUTPUTS
	Throws an error if the container fails to start, which signals failure back to Update-Container.
.EXAMPLE
	# This function is intended to be called internally by Update-Container via -RunFunction
	# Update-Container -RunFunction ${function:Invoke-StartN8nForUpdate}
.NOTES
	Relies on Confirm-ContainerVolume, Get-n8nContainerConfig, Start-n8nContainer helper functions.
#>
function Invoke-StartN8nForUpdate {
	param(
		[string]$EnginePath,
		# The following parameters are part of the standard signature for Update-Container's script block,
		# but are not directly used in this specific implementation as it relies on global variables
		# or calls Start-n8nContainer which uses globals.
		[string]$ContainerEngineType,
		[string]$ContainerName,
		[string]$VolumeName,
		[string]$ImageName            # The updated image name passed by Update-Container
	)

	# Ensure the volume exists (important if it was removed manually)
	# Use the VolumeName parameter passed by Update-Container for consistency
	if (-not (Confirm-ContainerVolume -Engine $EnginePath -VolumeName $VolumeName)) {
		throw "Failed to ensure volume '$VolumeName' exists during update."
	}

	# Get existing config to preserve environment variables (like domain)
	$config = Get-n8nContainerConfig
	if (-not $config) {
		Write-Warning "Could not retrieve existing config during update. Using default environment variables."
		$envVars = @(
			"N8N_COMMUNITY_PACKAGES_ENABLED=true",
			"N8N_COMMUNITY_PACKAGES_ALLOW_TOOL_USAGE=true"
		)
	}
	else {
		$envVars = $config.EnvVars
	}

	# Start the container using the specific Start-n8nContainer function
	# Pass the updated image name received as a parameter
	$result = Start-n8nContainer -Image $ImageName -EnvVars $envVars
	if (-not $result) {
		# Throw an error to signal failure to Update-Container
		throw "Failed to start updated n8n container."
	}
}

#==============================================================================
# Function: Update-n8nContainer
#==============================================================================
<#
.SYNOPSIS
	Updates the n8n container to the latest image version using the generic update workflow.
.DESCRIPTION
	Calls the generic Update-Container helper function, providing the specific details for the
	n8n container (name, image name) and passing a reference to the
	Invoke-StartN8nForUpdate function via the -RunFunction parameter. This ensures the
	container is started correctly with preserved configuration after the image is pulled
	and the old container is removed. Supports -WhatIf.
.EXAMPLE
	Update-n8nContainer -WhatIf
.NOTES
	Relies on the Update-Container helper function and Invoke-StartN8nForUpdate.
#>
function Update-n8nContainer {
	[CmdletBinding(SupportsShouldProcess = $true)]
	param()

	# Check ShouldProcess before proceeding with the delegated update
	if (-not $PSCmdlet.ShouldProcess($global:containerName, "Update Container")) {
		return
	}

	# Previously, a script block was defined here and passed using .GetNewClosure().
	# .GetNewClosure() creates a copy of the script block that captures the current
	# state of variables in its scope, ensuring the generic Update-Container function
	# executes it with the correct context from this script.
	# We now use a dedicated function (Invoke-StartN8nForUpdate) instead for better structure.

	# Call the generic Update-Container function
	Update-Container -Engine $global:enginePath `
		-ContainerName $global:containerName `
		-ImageName $global:imageName `
		-RunFunction ${function:Invoke-StartN8nForUpdate} # Pass function reference
}

#==============================================================================
# Function: Update-n8nUserData
#==============================================================================
<#
.SYNOPSIS
	Placeholder function for updating user data in the n8n container.
.DESCRIPTION
	Currently, this function only displays a message indicating that the functionality
	is not implemented. Supports -WhatIf.
.EXAMPLE
	Update-n8nUserData
.NOTES
	This function needs implementation if specific user data update procedures are required.
#>
function Update-n8nUserData {
	[CmdletBinding(SupportsShouldProcess = $true)]
	param()

	if ($PSCmdlet.ShouldProcess("n8n container user data", "Update")) {
		# Placeholder for future implementation
		Write-Information "Update User Data functionality is not implemented for n8n container."
	}
}

#==============================================================================
# Function: Restart-n8nContainer
#==============================================================================
<#
.SYNOPSIS
	Restarts the n8n container, applying current configuration (e.g., enabling community packages).
.DESCRIPTION
	Retrieves the current container configuration using Get-n8nContainerConfig (which also ensures
	community packages are enabled in the config object).
	Removes the existing container using Remove-n8nContainer.
	Starts a new container using Start-n8nContainer with the retrieved configuration (image and env vars).
	This effectively restarts the container with potentially updated environment variables from Get-n8nContainerConfig.
.EXAMPLE
	Restart-n8nContainer
.NOTES
	Relies on Get-n8nContainerConfig, Remove-n8nContainer, Start-n8nContainer helper functions.
	Supports -WhatIf via helper functions.
#>
function Restart-n8nContainer {
	[CmdletBinding(SupportsShouldProcess = $true)]
	param()

	# Get current container configuration
	$config = Get-n8nContainerConfig
	if (-not $config) {
		Write-Information "No n8n container found to restart. Please install it first."
		return
	}

	# Remove the existing container
	if (-not (Remove-n8nContainer)) {
		# This function now supports ShouldProcess
		Write-Error "Failed to remove existing container or action skipped. Restart aborted."
		return
	}

	# Start a new container with the same image and configuration
	if (Start-n8nContainer -Image $config.Image -EnvVars $config.EnvVars) {
		# This function now supports ShouldProcess
		Write-Information "n8n container restarted successfully with community packages enabled!"
	}
	else {
		Write-Error "Failed to restart container or action skipped."
	}
}

################################################################################
# Main Menu Loop using Generic Function
################################################################################

# Define Menu Title and Items
$menuTitle = "n8n Container Menu"
$menuItems = [ordered]@{
	"1" = "Show Info & Test Connection"
	"2" = "Install container"
	"3" = "Uninstall container (preserves user data)"
	"4" = "Backup Live container"
	"5" = "Restore Live container"
	"6" = "Update System"
	"7" = "Update User Data"
	"8" = "Restart with Community Packages Enabled"
	"0" = "Exit menu"
}

# Define Menu Actions
$menuActions = @{
	"1" = {
		Show-ContainerStatus -ContainerName $global:containerName `
			-ContainerEngine $global:containerEngine `
			-EnginePath $global:enginePath `
			-DisplayName "n8n" `
			-TcpPort 5678 `
			-HttpPort 5678
	}
	"2" = { Install-n8nContainer }
	"3" = { Uninstall-n8nContainer }
	"4" = { Backup-n8nContainer }
	"5" = { Restore-n8nContainer }
	"6" = { Update-n8nContainer }
	"7" = { Update-n8nUserData }
	"8" = { Restart-n8nContainer }
	# Note: "0" action is handled internally by Invoke-MenuLoop
}

# Invoke the Menu Loop
Invoke-MenuLoop -MenuTitle $menuTitle -MenuItems $menuItems -ActionMap $menuActions -ExitChoice "0"
