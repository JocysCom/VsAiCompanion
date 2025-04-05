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
$global:volumeName    = "nocodb_data"
$global:containerEngine = Select-ContainerEngine
if ($global:containerEngine -eq "docker") {
    Test-AdminPrivileges # Use renamed function
    $global:enginePath = Get-DockerPath
    $global:pullOptions = @()  # No additional options for Docker.
}
else {
    $global:enginePath = Get-PodmanPath
    $global:pullOptions = @("--tls-verify=false")
}

# Set the NocoDB image name and container name.
$global:imageName = "nocodb/nocodb:latest"

#############################################
# Function: Install-NocoDBContainer
# Description: Installs (or reinstalls) the NocoDB container.
#              Checks/creates the necessary volume for data persistence,
#              removes any existing container, pulls the image if necessary,
#              runs the container, and tests connectivity.
#############################################
function Install-NocoDBContainer {
    Write-Output "Installing NocoDB container using image '$global:imageName'..."

    # Ensure the volume exists
    if (-not (Confirm-ContainerVolume -Engine $global:enginePath -VolumeName $global:volumeName)) { # Renamed function
        Write-Error "Failed to ensure volume '$($global:volumeName)' exists. Exiting..."
        return
    }
    Write-Output "IMPORTANT: Using volume '$($global:volumeName)' - existing user data will be preserved."

    # Check if image exists locally, restore from backup, or pull new
    $existingImage = & $global:enginePath images --filter "reference=$($global:imageName)" --format "{{.ID}}"
    if (-not $existingImage) {
        if (-not (Test-AndRestoreBackup -Engine $global:enginePath -ImageName $global:imageName)) {
            Write-Output "No backup restored. Pulling NocoDB image '$global:imageName'..."
            # Use shared pull function
            if (-not (Invoke-PullImage -Engine $global:enginePath -ImageName $global:imageName -PullOptions $global:pullOptions)) {
                Write-Error "Failed to pull NocoDB image. Exiting..."
                return
            }
        }
        else {
            Write-Output "Using restored backup image '$global:imageName'." # Replaced Write-Host
        }
    }
    else {
        Write-Output "NocoDB image already exists. Skipping pull." # Replaced Write-Host
    }

    # Remove any existing container with the same name.
    $existingContainer = & $global:enginePath ps --all --filter "name=^$global:containerName$" --format "{{.ID}}"
    if ($existingContainer) {
        Write-Output "Removing existing container '$global:containerName'..." # Replaced Write-Host
        & $global:enginePath rm --force $global:containerName
    }

    # Define run options for the container.
    $runOptions = @(
        "--detach",                             # Run container in background.
        "--publish", "8570:8080",               # Map host port 8570 to container port 8080.
        "--volume", "$($global:volumeName):/usr/app/data",# Bind mount volume for data persistence.
        "--name", $global:containerName         # Set container name.
    )

    Write-Output "Starting NocoDB container..." # Replaced Write-Host
    & $global:enginePath run $runOptions $global:imageName
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to run NocoDB container."
        return # Changed exit 1 to return for better menu flow
    }

    Write-Output "Waiting 20 seconds for container startup..." # Replaced Write-Host
    Start-Sleep -Seconds 20
    Test-TCPPort -ComputerName "localhost" -Port 8570 -serviceName $global:containerName
    Test-HTTPPort -Uri "http://localhost:8570" -serviceName $global:containerName
    Write-Output "NocoDB is now running and accessible at http://localhost:8570" # Replaced Write-Host
    Write-Output "If accessing NocoDB from another container (e.g. from n8n), use 'http://host.docker.internal:8570' as the URL." # Replaced Write-Host
}

#############################################
# Function: Uninstall-NocoDBContainer
# Description: Removes the NocoDB container and optionally the data volume.
#############################################
function Uninstall-NocoDBContainer {
    Remove-ContainerAndVolume -Engine $global:enginePath -ContainerName $global:containerName -VolumeName $global:volumeName # This function supports ShouldProcess
}

#############################################
# Function: Backup-NocoDBContainer
# Description: Backs up the live NocoDB container.
#############################################
function Backup-NocoDBContainer {
    Backup-ContainerState -Engine $global:enginePath -ContainerName $global:containerName # This function supports ShouldProcess
}

#############################################
# Function: Restore-NocoDBContainer
# Description: Restores the NocoDB container from a backup.
#############################################
function Restore-NocoDBContainer {
    Restore-ContainerState -Engine $global:enginePath -ContainerName $global:containerName # This function supports ShouldProcess
}

#############################################
# Function: Update-NocoDBContainer
# Description: Updates the NocoDB container by removing the existing one,
#              pulling the latest image, and reinstalling the container.
#############################################
function Update-NocoDBContainer {
    [CmdletBinding(SupportsShouldProcess=$true)] # Added SupportsShouldProcess
    param()

    # Check ShouldProcess before proceeding with the delegated update
    if (-not $PSCmdlet.ShouldProcess($global:containerName, "Update Container")) {
        return
    }

    # Define Run Function for Update-Container
    $runContainerFunction = {
        param(
            [string]$EnginePath,
            [string]$ContainerEngineType, # Not used
            [string]$ContainerName,       # Should be $global:containerName
            [string]$VolumeName,          # Should be $global:volumeName
            [string]$ImageName            # The updated image name ($global:imageName)
        )

        # Ensure the volume exists (important if it was removed manually)
        if (-not (Confirm-ContainerVolume -Engine $EnginePath -VolumeName $VolumeName)) { # Renamed function
            throw "Failed to ensure volume '$VolumeName' exists during update."
        }

        Write-Output "Starting updated NocoDB container '$ContainerName'..."

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
        Write-Output "Waiting 20 seconds for container startup..."
        Start-Sleep -Seconds 20
        Test-TCPPort -ComputerName "localhost" -Port 8570 -serviceName $ContainerName
        Test-HTTPPort -Uri "http://localhost:8570" -serviceName $ContainerName
        Write-Output "NocoDB container updated successfully."
    }

    # Use the shared update function (which supports ShouldProcess)
    # Note: The ShouldProcess check is handled internally by Update-Container
    Update-Container -Engine $global:enginePath `
                     -ContainerName $global:containerName `
                     -ImageName $global:imageName `
                     -RunFunction $runContainerFunction.GetNewClosure() # Pass closure
}

#############################################
# Function: Show-ContainerMenu
# Description: Displays the main menu for NocoDB container management.
#############################################
function Show-ContainerMenu {
    Write-Output "===========================================" # Replaced Write-Host
    Write-Output "NocoDB Container Management Menu" # Replaced Write-Host
    Write-Output "===========================================" # Replaced Write-Host
    Write-Output "1. Install container" # Replaced Write-Host
    Write-Output "2. Uninstall container" # Replaced Write-Host
    Write-Output "3. Backup container" # Replaced Write-Host
    Write-Output "4. Restore container" # Replaced Write-Host
    Write-Output "5. Update container" # Replaced Write-Host
    Write-Output "0. Exit menu" # Replaced Write-Host
}

################################################################################
# Main Menu Loop using Generic Function
################################################################################
$menuActions = @{
    "1" = { Install-NocoDBContainer }
    "2" = { Uninstall-NocoDBContainer }
    "3" = { Backup-NocoDBContainer }
    "4" = { Restore-NocoDBContainer }
    "5" = { Update-NocoDBContainer }
}

Invoke-MenuLoop -ShowMenuScriptBlock ${function:Show-ContainerMenu} -ActionMap $menuActions -ExitChoice "0"
