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
$global:imageName     = "qdrant/qdrant"
$global:containerName = "qdrant"
$global:containerEngine = Select-ContainerEngine
if ($global:containerEngine -eq "docker") {
    Ensure-Elevated
    $global:enginePath = Get-DockerPath
    Write-Host "Using Docker with executable: $global:enginePath"
    # For Docker, set DOCKER_HOST pointing to the Docker service pipe.
    $env:DOCKER_HOST = "npipe:////./pipe/docker_engine"
}
else {
    $global:enginePath = Get-PodmanPath
    Write-Host "Using Podman with executable: $global:enginePath"
    # If additional Podman-specific environment settings are needed, add them here.
}

<#
.SYNOPSIS
    Installs the Qdrant container.
.DESCRIPTION
    Checks if a backup can be restored using the Check-AndRestoreBackup helper. If not,
    pulls the Qdrant image from the registry using the selected container engine.
    Then, if a container with the specified name exists, it is removed.
    Finally, the Qdrant container is launched with proper port mapping.
    All original command arguments and workarounds are preserved.
#>
function Install-QdrantContainer {
    if (-not (Check-AndRestoreBackup -Engine $global:enginePath -ImageName $global:imageName)) {
        Write-Host "No backup restored. Pulling Qdrant image '$global:imageName' using $global:containerEngine..."
        # Command: pull
        #   pull: downloads the specified image from the registry.
        & $global:enginePath pull $global:imageName
        if ($LASTEXITCODE -ne 0) {
             Write-Error "$global:containerEngine pull failed for Qdrant. Please check your internet connection or the image name."
             exit 1
        }
    }
    else {
        Write-Host "Using restored backup image '$global:imageName'."
    }
    
    # Check if a container with the same name already exists.
    # Command: ps
    #   --all: lists all containers.
    #   --filter "name=$containerName": filters for the container by name.
    #   --format "{{.ID}}": outputs only the container ID.
    $existingContainer = & $global:enginePath ps --all --filter "name=$global:containerName" --format "{{.ID}}"
    if ($existingContainer) {
        Write-Host "Removing existing container '$global:containerName'..."
        # Command: rm
        #   --force: forces removal of the container.
        & $global:enginePath rm --force $global:containerName
    }
    
    Write-Host "Starting Qdrant container..."
    # Command: run
    #   --detach: run container in background.
    #   --name: assign the container a name.
    #   --publish: map host port 6333 to container port 6333.
    & $global:enginePath run --detach --name $global:containerName --publish 6333:6333 $global:imageName
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to start the Qdrant container."
        exit 1
    }
    Write-Host "Waiting 20 seconds for the Qdrant container to fully start..."
    Start-Sleep -Seconds 20
    # Test connectivity to Qdrant service.
    Test-TCPPort -ComputerName "localhost" -Port 6333 -serviceName "Qdrant"
    Test-HTTPPort -Uri "http://localhost:6333" -serviceName "Qdrant"
    Write-Host "Qdrant is now running and accessible at http://localhost:6333"
}

<#
.SYNOPSIS
    Uninstalls the Qdrant container.
.DESCRIPTION
    Checks if a container with the specified name exists and removes it using the container engine's rm command.
#>
function Uninstall-QdrantContainer {
    $existingContainer = & $global:enginePath ps --all --filter "name=$global:containerName" --format "{{.ID}}"
    if ($existingContainer) {
        Write-Host "Removing existing container '$global:containerName'..."
        & $global:enginePath rm --force $global:containerName
        if ($LASTEXITCODE -eq 0) {
             Write-Host "Container removed successfully."
        }
        else {
             Write-Error "Failed to remove Qdrant container."
        }
    }
    else {
        Write-Host "No Qdrant container found to remove."
    }
}

<#
.SYNOPSIS
    Backs up the live Qdrant container.
.DESCRIPTION
    Uses the Backup-ContainerState helper function to create a backup of the Qdrant container.
#>
function Backup-QdrantContainer {
    Backup-ContainerState -Engine $global:enginePath -ContainerName $global:containerName
}

<#
.SYNOPSIS
    Restores the Qdrant container from backup.
.DESCRIPTION
    Uses the Restore-ContainerState helper function to restore the Qdrant container.
#>
function Restore-QdrantContainer {
    Restore-ContainerState -Engine $global:enginePath -ContainerName $global:containerName
}

<#
.SYNOPSIS
    Updates the Qdrant container.
.DESCRIPTION
    Removes any existing Qdrant container, pulls the latest image from the registry, and reinstalls the container.
#>
function Update-QdrantContainer {
    $existingContainer = & $global:enginePath ps --all --filter "name=$global:containerName" --format "{{.ID}}"
    if ($existingContainer) {
        Write-Host "Removing existing container '$global:containerName'..."
        & $global:enginePath rm --force $global:containerName
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Failed to remove existing Qdrant container. Update aborted."
            return
        }
    }
    Write-Host "Pulling latest Qdrant image '$global:imageName'..."
    & $global:enginePath pull $global:imageName
    if ($LASTEXITCODE -ne 0) {
         Write-Error "Failed to pull latest image. Update aborted."
         return
    }
    Install-QdrantContainer
}

<#
.SYNOPSIS
    Updates user data for the Qdrant container.
.DESCRIPTION
    This functionality is not implemented.
#>
function Update-QdrantUserData {
    Write-Host "Update User Data functionality is not implemented for Qdrant container."
}

<#
.SYNOPSIS
    Displays the main menu for Qdrant container operations.
.DESCRIPTION
    Presents menu options for installing, uninstalling, backing up, restoring, updating (system and user data).
    The exit option ("0") terminates the menu loop.
#>
function Show-ContainerMenu {
    Write-Host "==========================================="
    Write-Host "Qdrant Container Menu"
    Write-Host "==========================================="
    Write-Host "1. Install container"
    Write-Host "2. Uninstall container"
    Write-Host "3. Backup Live container"
    Write-Host "4. Restore Live container"
    Write-Host "5. Update System"
    Write-Host "6. Update User Data"
    Write-Host "0. Exit menu"
}

################################################################################
# Main Menu Loop for Qdrant Container Management
################################################################################
do {
    Show-ContainerMenu
    $choice = Read-Host "Enter your choice (1, 2, 3, 4, 5, 6, or 0)"
    switch ($choice) {
        "1" { Install-QdrantContainer }
        "2" { Uninstall-QdrantContainer }
        "3" { Backup-QdrantContainer }
        "4" { Restore-QdrantContainer }
        "5" { Update-QdrantContainer }
        "6" { Update-QdrantUserData }
        "0" { Write-Host "Exiting menu." }
        default { Write-Host "Invalid selection. Please enter 1, 2, 3, 4, 5, 6, or 0." }
    }
    if ($choice -ne "0") {
         Write-Host "`nPress any key to continue..."
         $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
         Clear-Host
    }
} while ($choice -ne "0")
