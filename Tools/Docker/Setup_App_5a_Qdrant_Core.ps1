################################################################################
# Description  : Script to set up and run the Qdrant container with Docker/Podman support.
#                Validates backup, pulls the Qdrant image if necessary, removes existing containers,
#                and runs Qdrant with proper port mapping.
# Usage        : Run as Administrator if necessary.
################################################################################

using namespace System
using namespace System.IO

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
$global:imageName = "qdrant/qdrant"
$global:containerName = "qdrant"
$global:volumeName = $global:containerName # Default: same as container name.

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
	Write-Host "Using Docker..."
	# For Docker, set DOCKER_HOST pointing to the Docker service pipe.
	$env:DOCKER_HOST = "npipe:////./pipe/docker_engine"
}
else {
 # Assumes podman
	Write-Host "Using Podman..."
	# If additional Podman-specific environment settings are needed, add them here.
}
# Get the engine path after setting specific options
$global:enginePath = Get-EnginePath -EngineName $global:containerEngine
Write-Host "Engine executable: $global:enginePath"

#==============================================================================
# Function: Install-QdrantContainer
#==============================================================================
<#
.SYNOPSIS
	Installs and starts the Qdrant container.
.DESCRIPTION
	Ensures the 'qdrant_storage' volume exists using Confirm-ContainerVolume.
	Checks if the Qdrant image exists locally; if not, attempts to restore from backup using
	Test-AndRestoreBackup, falling back to pulling the image using Invoke-PullImage.
	Removes any existing 'qdrant' container.
	Runs the Qdrant container using the selected engine, mapping ports 6333 (HTTP) and 6334 (gRPC),
	and mounting the 'qdrant_storage' volume.
	Waits 20 seconds after starting and performs TCP/HTTP connectivity tests.
.EXAMPLE
	Install-QdrantContainer
.NOTES
	Orchestrates volume creation, image acquisition, cleanup, and container start.
	Relies on Confirm-ContainerVolume, Test-AndRestoreBackup, Invoke-PullImage,
	Test-TCPPort, Test-HTTPPort helper functions.
	Uses Write-Host for status messages.
#>
function Install-QdrantContainer {
	# Check if image exists locally, restore from backup, or pull new
	$existingImage = & $global:enginePath images --filter "reference=$($global:imageName)" --format "{{.ID}}"
	if (-not $existingImage) {
		if (-not (Test-AndRestoreBackup -Engine $global:enginePath -ImageName $global:imageName)) {
			Write-Host "No backup restored. Pulling Qdrant image '$global:imageName' using $global:containerEngine..."
			# Use shared pull function
			if (-not (Invoke-PullImage -Engine $global:enginePath -ImageName $global:imageName)) {
				# No specific pull options needed
				Write-Error "$global:containerEngine pull failed for Qdrant. Please check your internet connection or the image name."
				exit 1
			}
		}
		else {
			Write-Host "Using restored backup image '$global:imageName'."
		}
	}
	else {
		Write-Host "Qdrant image '$global:imageName' already exists locally."
	}

	# Remove Existing Container
	$existingContainer = & $global:enginePath ps --all --filter "name=$global:containerName" --format "{{.ID}}"
	if ($existingContainer) {
		Write-Host "Removing existing container '$global:containerName'..."
		& $global:enginePath rm --force $global:containerName
	}

	# Run Container
	Write-Host "Starting Qdrant container..."
	$runOptions = @(
		"--detach", # Run container in background.
		"--name", $global:containerName, # Assign the container a name.
		"--publish", "6333:6333", # Map host HTTP port to container port 6333.
		"--publish", "6334:6334", # Map host gRPC port to container port 6334.
		"--volume", "$($global:volumeName):/qdrant/storage" # Mount the named volume for persistent data.
	)
	& $global:enginePath run @runOptions $global:imageName
	if ($LASTEXITCODE -ne 0) {
		Write-Error "Failed to start the Qdrant container."
		exit 1
	}

	# Wait and Test
	Write-Host "Waiting 20 seconds for the Qdrant container to fully start..."
	Start-Sleep -Seconds 20
	Test-TCPPort -ComputerName "localhost" -Port 6333 -serviceName "Qdrant HTTP"
	Test-HTTPPort -Uri "http://localhost:6333" -serviceName "Qdrant HTTP"
	Test-TCPPort -ComputerName "localhost" -Port 6334 -serviceName "Qdrant gRPC"
	Write-Host "Qdrant is now running and accessible at http://localhost:6333"
}

# Note: Uninstall-QdrantContainer, Backup-QdrantContainer, Restore-QdrantContainer functions removed. Shared functions called directly from menu.

#==============================================================================
# Function: Invoke-StartQdrantForUpdate
#==============================================================================
<#
.SYNOPSIS
	Helper function called by Update-Container to start the Qdrant container after an update.
.DESCRIPTION
	This function encapsulates the specific logic required to start the Qdrant container after an update.
	It ensures the volume exists, runs the container with the correct ports, volume mount, and the
	updated image name, waits, and performs connectivity tests.
	It adheres to the parameter signature expected by the -RunFunction parameter of Update-Container.
.PARAMETER EnginePath
	Path to the container engine executable (passed by Update-Container).
.PARAMETER ContainerEngineType
	Type of the container engine ('docker' or 'podman'). (Passed by Update-Container, not directly used).
.PARAMETER ContainerName
	Name of the container being updated (e.g., 'qdrant') (passed by Update-Container).
.PARAMETER VolumeName
	Name of the volume associated with the container (e.g., 'qdrant_storage') (passed by Update-Container).
.PARAMETER ImageName
	The new image name/tag to use for the updated container (passed by Update-Container).
.OUTPUTS
	Throws an error if the container fails to start, which signals failure back to Update-Container.
.EXAMPLE
	# This function is intended to be called internally by Update-Container via -RunFunction
	# Update-Container -RunFunction ${function:Invoke-StartQdrantForUpdate}
.NOTES
	Relies on Confirm-ContainerVolume, Test-TCPPort, Test-HTTPPort helper functions.
	Uses Write-Host for status messages.
#>
function Invoke-StartQdrantForUpdate {
	param(
		[string]$EnginePath,
		[string]$ContainerEngineType, # Not used
		[string]$ContainerName, # Should be $global:containerName
		[string]$VolumeName, # Should be $global:volumeName
		[string]$ImageName            # The updated image name ($global:imageName)
	)

	# Ensure the volume exists (important if it was removed manually)
	if (-not (Confirm-ContainerResource -Engine $EnginePath -ResourceType "volume" -ResourceName $VolumeName)) {
		throw "Failed to ensure volume '$VolumeName' exists during update."
	}

	Write-Host "Starting updated Qdrant container '$ContainerName'..."

	# Define run options (same as in Install-QdrantContainer)
	$runOptions = @(
		"--detach",
		"--name", $ContainerName,
		"--publish", "6333:6333",
		"--publish", "6334:6334",
		"--volume", "$($VolumeName):/qdrant/storage"
	)

	# Execute the command
	& $EnginePath run @runOptions $ImageName
	if ($LASTEXITCODE -ne 0) {
		throw "Failed to run updated Qdrant container '$ContainerName'."
	}

	# Wait and Test Connectivity (same as in Install-QdrantContainer)
	Write-Host "Waiting 20 seconds for the Qdrant container to fully start..."
	Start-Sleep -Seconds 20
	Test-TCPPort -ComputerName "localhost" -Port 6333 -serviceName "Qdrant HTTP"
	Test-HTTPPort -Uri "http://localhost:6333" -serviceName "Qdrant HTTP"
	Test-TCPPort -ComputerName "localhost" -Port 6334 -serviceName "Qdrant gRPC"
	Write-Host "Qdrant container updated successfully."
}

#==============================================================================
# Function: Update-QdrantContainer
#==============================================================================
<#
.SYNOPSIS
	Updates the Qdrant container to the latest image version using the generic update workflow.
.DESCRIPTION
	Orchestrates the update process:
	1. Prompts the user to optionally back up the current container state.
	2. Calls the simplified generic Update-Container function (handles update check, removal, pull).
	3. If core update steps succeed, calls Invoke-StartQdrantForUpdate to start the new container.
	4. Offers to restore from backup if the start fails (and a backup was made).
.EXAMPLE
	Update-QdrantContainer -WhatIf
.NOTES
	Relies on Backup-QdrantContainer, Update-Container, Invoke-StartQdrantForUpdate,
	Restore-QdrantContainer helper functions.
	User interaction handled via Read-Host for backup confirmation.
#>
function Update-QdrantContainer {
	[CmdletBinding(SupportsShouldProcess = $true)] # Keep ShouldProcess for overall control
	param()

	# Check ShouldProcess before proceeding
	if (-not $PSCmdlet.ShouldProcess($global:containerName, "Update Container")) {
		return
	}

	Write-Host "Initiating update for Qdrant..."
	$backupMade = $false
	# Check if container actually exists before prompting for backup
	$existingContainer = & $global:enginePath ps -a --filter "name=$($global:containerName)" --format "{{.ID}}"
	if ($existingContainer) {
		$createBackup = Read-Host "Create backup before updating? (Y/N, default is Y)"
		if ($createBackup -ne "N") {
			if (Backup-QdrantContainer) {
				# Calls Backup-ContainerState
				$backupMade = $true
			}
		}
	}
	else {
		Write-Warning "Container '$($global:containerName)' not found. Skipping backup prompt."
	}

	# Call simplified Update-Container (handles check, remove, pull)
	# Pass volume name for removal step
	if (Update-Container -Engine $global:enginePath -ContainerName $global:containerName -VolumeName $global:volumeName -ImageName $global:imageName) {
		Write-Host "Core update steps successful. Starting new container..."
		# Start the new container using the dedicated start function
		try {
			# Invoke-StartQdrantForUpdate expects these params, pass globals/literals
			Invoke-StartQdrantForUpdate -EnginePath $global:enginePath `
				-ContainerEngineType $global:containerEngine `
				-ContainerName $global:containerName `
				-VolumeName $global:volumeName `
				-ImageName $global:imageName
			# Success message is handled within Invoke-StartQdrantForUpdate
		}
		catch {
			Write-Error "Failed to start updated Qdrant container: $_"
			if ($backupMade) {
				$restore = Read-Host "Would you like to restore from backup? (Y/N, default is Y)"
				if ($restore -ne "N") {
					Restore-QdrantContainer # Calls Restore-ContainerState
				}
			}
		}
	}
	else {
		Write-Error "Update process failed during check, removal, or pull."
		if ($backupMade) {
			$restore = Read-Host "Would you like to restore from backup? (Y/N, default is Y)"
			if ($restore -ne "N") {
				Restore-QdrantContainer # Calls Restore-ContainerState
			}
		}
	}
}

#==============================================================================
# Function: Update-QdrantUserData
#==============================================================================
<#
.SYNOPSIS
	Placeholder function for updating user data in the Qdrant container.
.DESCRIPTION
	Currently, this function only displays a message indicating that the functionality
	is not implemented. Supports -WhatIf.
.EXAMPLE
	Update-QdrantUserData
.NOTES
	This function needs implementation if specific user data update procedures are required.
#>
function Update-QdrantUserData {
	[CmdletBinding(SupportsShouldProcess = $true)]
	param()

	if ($PSCmdlet.ShouldProcess("Qdrant Container User Data", "Update user data")) {
		# No actions to wrap with ShouldProcess as it's not implemented
		Write-Host "Update User Data functionality is not implemented for Qdrant container."
	}
}

################################################################################
# Main Menu Loop using Generic Function
################################################################################

# Define Menu Title and Items
$menuTitle = "Qdrant Container Menu"
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
	"0" = "Exit menu"
}

# Define Menu Actions
$menuActions = @{
	"1" = {
		Show-ContainerStatus -ContainerName $global:containerName `
			-ContainerEngine $global:containerEngine `
			-EnginePath $global:enginePath `
			-DisplayName "Qdrant" `
			-TcpPort 6333 `
			-HttpPort 6333 `
			-AdditionalInfo @{ "gRPC Port" = 6334 }
	}
	"2" = { Install-QdrantContainer }
	"3" = { Remove-ContainerAndVolume -Engine $global:enginePath -ContainerName $global:containerName -VolumeName $global:volumeName } # Call shared function directly
	"4" = { Backup-ContainerImage -Engine $global:enginePath -ContainerName $global:containerName } # Call shared function directly
	"5" = { Restore-ContainerImage -Engine $global:enginePath -ContainerName $global:containerName } # Call shared function directly
	"6" = { Update-QdrantContainer } # Calls the dedicated update function
	"7" = { $null = Backup-ContainerVolume -EngineType $global:containerEngine -VolumeName $global:volumeName } # Call shared function directly
	"8" = {
		$null = Restore-ContainerVolume -EngineType $global:containerEngine -VolumeName $global:volumeName
		& $global:enginePath restart $global:containerName
	}
	"9" = { Test-ImageUpdateAvailable -Engine $global:enginePath -ImageName $global:imageName }
	# Note: "0" action is handled internally by Invoke-MenuLoop
}

# Invoke the Menu Loop
Invoke-MenuLoop -MenuTitle $menuTitle -MenuItems $menuItems -ActionMap $menuActions -ExitChoice "0"
