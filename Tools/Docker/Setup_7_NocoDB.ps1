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
    Write-Output "Installing NocoDB container using image '$global:imageName'..." # Replaced Write-Host

    # Check if the volume for NocoDB data exists; if not, create it.
    $existingVolume = & $global:enginePath volume ls --filter "name=$global:volumeName" --format "{{.Name}}"
    if ([string]::IsNullOrWhiteSpace($existingVolume)) {
        Write-Output "Creating volume '$global:volumeName' for NocoDB data..." # Replaced Write-Host
        & $global:enginePath volume create $global:volumeName
    }
    else {
        Write-Output "Volume '$global:volumeName' already exists. Skipping creation." # Replaced Write-Host
    }

    # Check if the NocoDB image exists.
    $existingImage = & $global:enginePath images --format "{{.Repository}}:{{.Tag}}" | Where-Object { $_ -like "nocodb/nocodb:*" }
    if (-not $existingImage) {
        if (-not (Test-AndRestoreBackup -Engine $global:enginePath -ImageName $global:imageName)) { # Use renamed function
            Write-Output "No backup restored. Pulling NocoDB image '$global:imageName'..." # Replaced Write-Host
            $pullCmd = @("pull") + $global:pullOptions + $global:imageName
            & $global:enginePath @pullCmd
            if ($LASTEXITCODE -ne 0) {
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

    if ($PSCmdlet.ShouldProcess("NocoDB container", "Update")) {
        # Define Run Function for Update-Container
        $runContainerFunction = {
            Install-NocoDBContainer # Re-use install logic
        }
        # Use the shared update function (which supports ShouldProcess)
        Update-Container -Engine $global:enginePath -ContainerName $global:containerName -ImageName $global:imageName -RunFunction $runContainerFunction
    }
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
