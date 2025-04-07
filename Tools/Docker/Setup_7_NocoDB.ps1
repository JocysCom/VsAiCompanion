################################################################################
# File         : Setup_7_NocoDB.ps1
# Description  : Script to set up, backup, restore, uninstall, and update the
#                NocoDB container using Docker/Podman support. Provides a container
#                menu for container operations.
#                NocoDB is an AirTable alternative.
# Usage        : Run as Administrator if using Docker.
################################################################################

using namespace System
using namespace System.IO

# Dot-source the necessary helper function files.
. "$PSScriptRoot\Setup_0_Core.ps1"
. "$PSScriptRoot\Setup_0_Network.ps1"
. "$PSScriptRoot\Setup_0_ContainerEngine.ps1"
. "$PSScriptRoot\Setup_0_BackupRestore.ps1"
. "$PSScriptRoot\Setup_0_ContainerMgmt.ps1"

# Ensure the script's working directory is set.
Set-ScriptLocation

#############################################
# Global Variables and Container Engine Setup
#############################################
# Note: PSAvoidGlobalVars warnings are ignored here as these are used across menu actions.
$global:containerName = "nocodb"
$global:volumeName = "nocodb_data"
$global:containerEngine = Select-ContainerEngine
if ($global:containerEngine -eq "docker") {
	Test-AdminPrivilege
	$global:enginePath = Get-DockerPath
	$global:pullOptions = @()  # No additional options for Docker.
}
else {
	$global:enginePath = Get-PodmanPath
	$global:pullOptions = @("--tls-verify=false")
}

# Set the NocoDB image name and container name.
$global:imageName = "nocodb/nocodb:latest"

#==============================================================================
# Function: Install-NocoDBContainer
#==============================================================================
<#
.SYNOPSIS
	Installs and starts the NocoDB container.
.DESCRIPTION
	Ensures the 'nocodb_data' volume exists using Confirm-ContainerVolume.
	Checks if the NocoDB image exists locally; if not, attempts to restore from backup using
	Test-AndRestoreBackup, falling back to pulling the image using Invoke-PullImage.
	Removes any existing 'nocodb' container.
	Runs the NocoDB container using the selected engine, mapping port 8570 to 8080,
	and mounting the 'nocodb_data' volume to '/usr/app/data'.
	Waits 20 seconds after starting and performs TCP/HTTP connectivity tests.
.EXAMPLE
	Install-NocoDBContainer
.NOTES
	Orchestrates volume creation, image acquisition, cleanup, and container start.
	Relies on Confirm-ContainerVolume, Test-AndRestoreBackup, Invoke-PullImage,
	Test-TCPPort, Test-HTTPPort helper functions.
	Uses Write-Host for status messages.
#>
function Install-NocoDBContainer {
	Write-Host "Installing NocoDB container using image '$global:imageName'..."

	# Ensure the volume exists
	if (-not (Confirm-ContainerVolume -Engine $global:enginePath -VolumeName $global:volumeName)) {
		Write-Error "Failed to ensure volume '$($global:volumeName)' exists. Exiting..."
		return
	}
	Write-Host "IMPORTANT: Using volume '$($global:volumeName)' - existing user data will be preserved."

	# Check if image exists locally, restore from backup, or pull new
	$existingImage = & $global:enginePath images --filter "reference=$($global:imageName)" --format "{{.ID}}"
	if (-not $existingImage) {
		if (-not (Test-AndRestoreBackup -Engine $global:enginePath -ImageName $global:imageName)) {
			Write-Host "No backup restored. Pulling NocoDB image '$global:imageName'..."
			# Use shared pull function
			if (-not (Invoke-PullImage -Engine $global:enginePath -ImageName $global:imageName -PullOptions $global:pullOptions)) {
				Write-Error "Failed to pull NocoDB image. Exiting..."
				return
			}
		}
		else {
			Write-Host "Using restored backup image '$global:imageName'."
		}
	}
	else {
		Write-Host "NocoDB image already exists. Skipping pull."
	}

	# Remove any existing container with the same name.
	$existingContainer = & $global:enginePath ps --all --filter "name=^$global:containerName$" --format "{{.ID}}"
	if ($existingContainer) {
		Write-Host "Removing existing container '$global:containerName'..."
		& $global:enginePath rm --force $global:containerName
	}

	# Define run options for the container.
	$runOptions = @(
		"--detach", # Run container in background.
		"--publish", "8570:8080", # Map host port 8570 to container port 8080.
		"--volume", "$($global:volumeName):/usr/app/data", # Bind mount volume for data persistence.
		"--name", $global:containerName         # Set container name.
	)

	Write-Host "Starting NocoDB container..."
	& $global:enginePath run $runOptions $global:imageName
	if ($LASTEXITCODE -ne 0) {
		Write-Error "Failed to run NocoDB container."
		return
	}

	Write-Host "Waiting 20 seconds for container startup..."
	Start-Sleep -Seconds 20
	Test-TCPPort -ComputerName "localhost" -Port 8570 -serviceName $global:containerName
	Test-HTTPPort -Uri "http://localhost:8570" -serviceName $global:containerName
	Write-Host "NocoDB is now running and accessible at http://localhost:8570"
	Write-Host "If accessing NocoDB from another container (e.g. from n8n), use 'http://host.docker.internal:8570' as the URL."
}

#==============================================================================
# Function: Uninstall-NocoDBContainer
#==============================================================================
<#
.SYNOPSIS
	Uninstalls the NocoDB container and optionally removes its data volume.
.DESCRIPTION
	Calls the Remove-ContainerAndVolume helper function, specifying 'nocodb' as the container
	and 'nocodb_data' as the volume. This will stop/remove the container and prompt the user
	about removing the volume. Supports -WhatIf.
.EXAMPLE
	Uninstall-NocoDBContainer -Confirm:$false
.NOTES
	Relies on Remove-ContainerAndVolume helper function.
#>
function Uninstall-NocoDBContainer {
	Remove-ContainerAndVolume -Engine $global:enginePath -ContainerName $global:containerName -VolumeName $global:volumeName
}

#==============================================================================
# Function: Backup-NocoDBContainer
#==============================================================================
<#
.SYNOPSIS
	Backs up the state of the running NocoDB container.
.DESCRIPTION
	Calls the Backup-ContainerState helper function, specifying 'nocodb' as the container name.
	This commits the container state to an image and saves it as a tar file.
.EXAMPLE
	Backup-NocoDBContainer
.NOTES
	Relies on Backup-ContainerState helper function. Supports -WhatIf via the helper function.
#>
function Backup-NocoDBContainer {
	Backup-ContainerState -Engine $global:enginePath -ContainerName $global:containerName
}

#==============================================================================
# Function: Restore-NocoDBContainer
#==============================================================================
<#
.SYNOPSIS
	Restores the NocoDB container image from a backup tar file.
.DESCRIPTION
	Calls the Restore-ContainerState helper function, specifying 'nocodb' as the container name.
	This loads the image from the backup tar file. Note: This only restores the NocoDB image,
	it does not automatically start the container.
.EXAMPLE
	Restore-NocoDBContainer
.NOTES
	Relies on Restore-ContainerState helper function. Does not handle volume restore.
#>
function Restore-NocoDBContainer {
	Restore-ContainerState -Engine $global:enginePath -ContainerName $global:containerName
}

#==============================================================================
# Function: Invoke-StartNocoDBForUpdate
#==============================================================================
<#
.SYNOPSIS
	Helper function called by Update-Container to start the NocoDB container after an update.
.DESCRIPTION
	This function encapsulates the specific logic required to start the NocoDB container after an update.
	It ensures the volume exists, runs the container with the correct ports, volume mount, and the
	updated image name, waits, and performs connectivity tests.
	It adheres to the parameter signature expected by the -RunFunction parameter of Update-Container.
.PARAMETER EnginePath
	Path to the container engine executable (passed by Update-Container).
.PARAMETER ContainerEngineType
	Type of the container engine ('docker' or 'podman'). (Passed by Update-Container, not directly used).
.PARAMETER ContainerName
	Name of the container being updated (e.g., 'nocodb') (passed by Update-Container).
.PARAMETER VolumeName
	Name of the volume associated with the container (e.g., 'nocodb_data') (passed by Update-Container).
.PARAMETER ImageName
	The new image name/tag to use for the updated container (passed by Update-Container).
.OUTPUTS
	Throws an error if the container fails to start, which signals failure back to Update-Container.
.EXAMPLE
	# This function is intended to be called internally by Update-Container via -RunFunction
	# Update-Container -RunFunction ${function:Invoke-StartNocoDBForUpdate}
.NOTES
	Relies on Confirm-ContainerVolume, Test-TCPPort, Test-HTTPPort helper functions.
	Uses Write-Host for status messages.
#>
function Invoke-StartNocoDBForUpdate {
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

	Write-Host "Starting updated NocoDB container '$ContainerName'..."

	# Define run options (same as in Install-NocoDBContainer)
	$runOptions = @(
		"--detach",
		"--publish", "8570:8080",
		"--volume", "$($VolumeName):/usr/app/data",
		"--name", $ContainerName
	)

	# Execute the command
	& $EnginePath run @runOptions $ImageName
	if ($LASTEXITCODE -ne 0) {
		throw "Failed to run updated NocoDB container '$ContainerName'."
	}

	# Wait and Test Connectivity (same as in Install-NocoDBContainer)
	Write-Host "Waiting 20 seconds for container startup..."
	Start-Sleep -Seconds 20
	Test-TCPPort -ComputerName "localhost" -Port 8570 -serviceName $ContainerName
	Test-HTTPPort -Uri "http://localhost:8570" -serviceName $ContainerName
	Write-Host "NocoDB container updated successfully."
}

#==============================================================================
# Function: Update-NocoDBContainer
#==============================================================================
<#
.SYNOPSIS
	Updates the NocoDB container to the latest image version using the generic update workflow.
.DESCRIPTION
	Calls the generic Update-Container helper function, providing the specific details for the
	NocoDB container (name, image name) and passing a reference to the
	Invoke-StartNocoDBForUpdate function via the -RunFunction parameter. This ensures the
	container is started correctly after the image is pulled and the old container is removed.
	Supports -WhatIf.
.EXAMPLE
	Update-NocoDBContainer -WhatIf
.NOTES
	Relies on the Update-Container helper function and Invoke-StartNocoDBForUpdate.
#>
function Update-NocoDBContainer {
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
	# We now use a dedicated function (Invoke-StartNocoDBForUpdate) instead for better structure.

	# Use the shared update function (which supports ShouldProcess)
	Update-Container -Engine $global:enginePath `
		-ContainerName $global:containerName `
		-ImageName $global:imageName `
		-RunFunction ${function:Invoke-StartNocoDBForUpdate} # Pass function reference
}

################################################################################
# Main Menu Loop using Generic Function
################################################################################

# Define Menu Title and Items
$menuTitle = "NocoDB Container Management Menu"
$menuItems = [ordered]@{
	"1" = "Show Info & Test Connection"
	"2" = "Install container"
	"3" = "Uninstall container"
	"4" = "Backup container"
	"5" = "Restore container"
	"6" = "Update container"
	"0" = "Exit menu"
}

# Define Menu Actions
$menuActions = @{
	"1" = {
		Show-ContainerStatus -ContainerName $global:containerName `
			-ContainerEngine $global:containerEngine `
			-EnginePath $global:enginePath `
			-DisplayName "NocoDB" `
			-TcpPort 8570 `
			-HttpPort 8570
	}
	"2" = { Install-NocoDBContainer }
	"3" = { Uninstall-NocoDBContainer }
	"4" = { Backup-NocoDBContainer }
	"5" = { Restore-NocoDBContainer }
	"6" = { Update-NocoDBContainer }
	# Note: "0" action is handled internally by Invoke-MenuLoop
}

# Invoke the Menu Loop
Invoke-MenuLoop -MenuTitle $menuTitle -MenuItems $menuItems -ActionMap $menuActions -ExitChoice "0"
