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
    Test-AdminPrivileges
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
    Write-Information "Installing NocoDB container using image '$global:imageName'..."

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
            Write-Information "No backup restored. Pulling NocoDB image '$global:imageName'..."
            # Use shared pull function
            if (-not (Invoke-PullImage -Engine $global:enginePath -ImageName $global:imageName -PullOptions $global:pullOptions)) {
                Write-Error "Failed to pull NocoDB image. Exiting..."
                return
            }
        }
        else {
            Write-Information "Using restored backup image '$global:imageName'."
        }
    }
    else {
        Write-Information "NocoDB image already exists. Skipping pull."
    }

    # Remove any existing container with the same name.
    $existingContainer = & $global:enginePath ps --all --filter "name=^$global:containerName$" --format "{{.ID}}"
    if ($existingContainer) {
        Write-Information "Removing existing container '$global:containerName'..."
        & $global:enginePath rm --force $global:containerName
    }

    # Define run options for the container.
    $runOptions = @(
        "--detach",                             # Run container in background.
        "--publish", "8570:8080",               # Map host port 8570 to container port 8080.
        "--volume", "$($global:volumeName):/usr/app/data",# Bind mount volume for data persistence.
        "--name", $global:containerName         # Set container name.
    )

    Write-Information "Starting NocoDB container..."
    & $global:enginePath run $runOptions $global:imageName
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to run NocoDB container."
        return
    }

    Write-Information "Waiting 20 seconds for container startup..."
    Start-Sleep -Seconds 20
    Test-TCPPort -ComputerName "localhost" -Port 8570 -serviceName $global:containerName
    Test-HTTPPort -Uri "http://localhost:8570" -serviceName $global:containerName
    Write-Information "NocoDB is now running and accessible at http://localhost:8570"
    Write-Information "If accessing NocoDB from another container (e.g. from n8n), use 'http://host.docker.internal:8570' as the URL."
}

#############################################
# Function: Uninstall-NocoDBContainer
# Description: Removes the NocoDB container and optionally the data volume.
#############################################
function Uninstall-NocoDBContainer {
    Remove-ContainerAndVolume -Engine $global:enginePath -ContainerName $global:containerName -VolumeName $global:volumeName
}

#############################################
# Function: Backup-NocoDBContainer
# Description: Backs up the live NocoDB container.
#############################################
function Backup-NocoDBContainer {
    Backup-ContainerState -Engine $global:enginePath -ContainerName $global:containerName
}

#############################################
# Function: Restore-NocoDBContainer
# Description: Restores the NocoDB container from a backup.
#############################################
function Restore-NocoDBContainer {
    Restore-ContainerState -Engine $global:enginePath -ContainerName $global:containerName
}

#############################################
# Function: Update-NocoDBContainer
# Description: Updates the NocoDB container by removing the existing one,
#              pulling the latest image, and reinstalling the container.
#############################################
function Update-NocoDBContainer {
    [CmdletBinding(SupportsShouldProcess=$true)]
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
        if (-not (Confirm-ContainerVolume -Engine $EnginePath -VolumeName $VolumeName)) {
            throw "Failed to ensure volume '$VolumeName' exists during update."
        }

        Write-Information "Starting updated NocoDB container '$ContainerName'..."

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
        Write-Information "Waiting 20 seconds for container startup..."
        Start-Sleep -Seconds 20
        Test-TCPPort -ComputerName "localhost" -Port 8570 -serviceName $ContainerName
        Test-HTTPPort -Uri "http://localhost:8570" -serviceName $ContainerName
        Write-Information "NocoDB container updated successfully."
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
    Write-Output "==========================================="
    Write-Output "NocoDB Container Management Menu"
    Write-Output "==========================================="
    Write-Output "1. Show Info & Test Connection"
    Write-Output "2. Install container"
    Write-Output "3. Uninstall container"
    Write-Output "4. Backup container"
    Write-Output "5. Restore container"
    Write-Output "6. Update container"
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
                             -DisplayName "NocoDB" `
                             -TcpPort 8570 `
                             -HttpPort 8570
    }
    "2" = { Install-NocoDBContainer }
    "3" = { Uninstall-NocoDBContainer }
    "4" = { Backup-NocoDBContainer }
    "5" = { Restore-NocoDBContainer }
    "6" = { Update-NocoDBContainer }
}

Invoke-MenuLoop -ShowMenuScriptBlock ${function:Show-ContainerMenu} -ActionMap $menuActions -ExitChoice "0"
