################################################################################
# File         : Setup_5_Qdrant.ps1
# Description  : Script to set up and run the Qdrant container with Docker/Podman support.
#                Validates backup, pulls the Qdrant image if necessary, removes existing containers,
#                and runs Qdrant with proper port mapping.
# Usage        : Run as Administrator if necessary.
################################################################################

using namespace System
using namespace System.IO

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
$global:imageName = "qdrant/qdrant"
$global:containerName = "qdrant"
$global:volumeName = "qdrant_storage" # Define a volume name
$global:containerEngine = Select-ContainerEngine
if ($global:containerEngine -eq "docker") {
	Test-AdminPrivilege
	$global:enginePath = Get-DockerPath
	Write-Information "Using Docker with executable: $global:enginePath"
	# For Docker, set DOCKER_HOST pointing to the Docker service pipe.
	$env:DOCKER_HOST = "npipe:////./pipe/docker_engine"
}
else {
	$global:enginePath = Get-PodmanPath
	Write-Information "Using Podman with executable: $global:enginePath"
	# If additional Podman-specific environment settings are needed, add them here.
}

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
	Uses Write-Information for status messages.
#>
function Install-QdrantContainer {
	# Ensure the volume exists
	if (-not (Confirm-ContainerVolume -Engine $global:enginePath -VolumeName $global:volumeName)) {
		Write-Error "Failed to ensure volume '$($global:volumeName)' exists. Exiting..."
		return
	}
	Write-Information "IMPORTANT: Using volume '$($global:volumeName)' - existing user data will be preserved."

	# Check if image exists locally, restore from backup, or pull new
	$existingImage = & $global:enginePath images --filter "reference=$($global:imageName)" --format "{{.ID}}"
	if (-not $existingImage) {
		if (-not (Test-AndRestoreBackup -Engine $global:enginePath -ImageName $global:imageName)) {
			Write-Information "No backup restored. Pulling Qdrant image '$global:imageName' using $global:containerEngine..."
			# Use shared pull function
			if (-not (Invoke-PullImage -Engine $global:enginePath -ImageName $global:imageName)) {
				# No specific pull options needed
				Write-Error "$global:containerEngine pull failed for Qdrant. Please check your internet connection or the image name."
				exit 1
			}
		}
		else {
			Write-Information "Using restored backup image '$global:imageName'."
		}
	}
	else {
		Write-Information "Using restored backup image '$global:imageName'." # This line was duplicated in the original, keeping it for consistency unless told otherwise.
	}

	# Remove Existing Container
	$existingContainer = & $global:enginePath ps --all --filter "name=$global:containerName" --format "{{.ID}}"
	if ($existingContainer) {
		Write-Information "Removing existing container '$global:containerName'..."
		& $global:enginePath rm --force $global:containerName
	}

	# Run Container
	Write-Information "Starting Qdrant container..."
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
	Write-Information "Waiting 20 seconds for the Qdrant container to fully start..."
	Start-Sleep -Seconds 20
	Test-TCPPort -ComputerName "localhost" -Port 6333 -serviceName "Qdrant HTTP"
	Test-HTTPPort -Uri "http://localhost:6333" -serviceName "Qdrant HTTP"
	Test-TCPPort -ComputerName "localhost" -Port 6334 -serviceName "Qdrant gRPC"
	Write-Information "Qdrant is now running and accessible at http://localhost:6333"
}

#==============================================================================
# Function: Uninstall-QdrantContainer
#==============================================================================
<#
.SYNOPSIS
	Uninstalls the Qdrant container and optionally removes its data volume.
.DESCRIPTION
	Calls the Remove-ContainerAndVolume helper function, specifying 'qdrant' as the container
	and 'qdrant_storage' as the volume. This will stop/remove the container and prompt the user
	about removing the volume. Supports -WhatIf.
.EXAMPLE
	Uninstall-QdrantContainer -Confirm:$false
.NOTES
	Relies on Remove-ContainerAndVolume helper function.
#>
function Uninstall-QdrantContainer {
	Remove-ContainerAndVolume -Engine $global:enginePath -ContainerName $global:containerName -VolumeName $global:volumeName # This function supports ShouldProcess
}

#==============================================================================
# Function: Backup-QdrantContainer
#==============================================================================
<#
.SYNOPSIS
	Backs up the state of the running Qdrant container.
.DESCRIPTION
	Calls the Backup-ContainerState helper function, specifying 'qdrant' as the container name.
	This commits the container state to an image and saves it as a tar file.
.EXAMPLE
	Backup-QdrantContainer
.NOTES
	Relies on Backup-ContainerState helper function. Supports -WhatIf via the helper function.
#>
function Backup-QdrantContainer {
	Backup-ContainerState -Engine $global:enginePath -ContainerName $global:containerName # This function supports ShouldProcess
}

#==============================================================================
# Function: Restore-QdrantContainer
#==============================================================================
<#
.SYNOPSIS
	Restores the Qdrant container image from a backup tar file.
.DESCRIPTION
	Calls the Restore-ContainerState helper function, specifying 'qdrant' as the container name.
	This loads the image from the backup tar file. Note: This only restores the Qdrant image,
	it does not automatically start the container.
.EXAMPLE
	Restore-QdrantContainer
.NOTES
	Relies on Restore-ContainerState helper function. Does not handle volume restore.
#>
function Restore-QdrantContainer {
	Restore-ContainerState -Engine $global:enginePath -ContainerName $global:containerName # This function supports ShouldProcess
}

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
	Uses Write-Information for status messages.
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
	if (-not (Confirm-ContainerVolume -Engine $EnginePath -VolumeName $VolumeName)) {
		throw "Failed to ensure volume '$VolumeName' exists during update."
	}

	Write-Information "Starting updated Qdrant container '$ContainerName'..."

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
	Write-Information "Waiting 20 seconds for the Qdrant container to fully start..."
	Start-Sleep -Seconds 20
	Test-TCPPort -ComputerName "localhost" -Port 6333 -serviceName "Qdrant HTTP"
	Test-HTTPPort -Uri "http://localhost:6333" -serviceName "Qdrant HTTP"
	Test-TCPPort -ComputerName "localhost" -Port 6334 -serviceName "Qdrant gRPC"
	Write-Information "Qdrant container updated successfully."
}

#==============================================================================
# Function: Update-QdrantContainer
#==============================================================================
<#
.SYNOPSIS
	Updates the Qdrant container to the latest image version using the generic update workflow.
.DESCRIPTION
	Calls the generic Update-Container helper function, providing the specific details for the
	Qdrant container (name, image name) and passing a reference to the
	Invoke-StartQdrantForUpdate function via the -RunFunction parameter. This ensures the
	container is started correctly after the image is pulled and the old container is removed.
	Supports -WhatIf.
.EXAMPLE
	Update-QdrantContainer -WhatIf
.NOTES
	Relies on the Update-Container helper function and Invoke-StartQdrantForUpdate.
#>
function Update-QdrantContainer {
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
	# We now use a dedicated function (Invoke-StartQdrantForUpdate) instead for better structure.

	# Use the shared update function (which supports ShouldProcess)
	Update-Container -Engine $global:enginePath `
		-ContainerName $global:containerName `
		-ImageName $global:imageName `
		-RunFunction ${function:Invoke-StartQdrantForUpdate} # Pass function reference
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
		Write-Information "Update User Data functionality is not implemented for Qdrant container."
	}
}

#==============================================================================
# Function: Show-ContainerMenu
#==============================================================================
<#
.SYNOPSIS
	Displays the main menu options for Qdrant container management.
.DESCRIPTION
	Writes the available menu options (Show Info, Install, Uninstall, Backup, Restore, Update System,
	Update User Data, Exit) to the console using Write-Output.
.EXAMPLE
	Show-ContainerMenu
.NOTES
	Uses Write-Output for direct console display.
#>
function Show-ContainerMenu {
	Write-Output "==========================================="
	Write-Output "Qdrant Container Menu"
	Write-Output "==========================================="
	Write-Output "1. Show Info & Test Connection"
	Write-Output "2. Install container"
	Write-Output "3. Uninstall container"
	Write-Output "4. Backup Live container"
	Write-Output "5. Restore Live container"
	Write-Output "6. Update System"
	Write-Output "7. Update User Data"
	Write-Output "0. Exit menu"
}

################################################################################
# Main Menu Loop using Generic Function
################################################################################
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
	"3" = { Uninstall-QdrantContainer }
	"4" = { Backup-QdrantContainer }
	"5" = { Restore-QdrantContainer }
	"6" = { Update-QdrantContainer }
	"7" = { Update-QdrantUserData }
}

Invoke-MenuLoop -ShowMenuScriptBlock ${function:Show-ContainerMenu} -ActionMap $menuActions -ExitChoice "0"
