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
$global:volumeName    = "qdrant_storage" # Define a volume name
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
    Finally, the Qdrant container is launched with proper port mapping and volume.
    All original command arguments and workarounds are preserved.
#>
function Install-QdrantContainer {
    # Check/Create Volume
    $existingVolume = & $global:enginePath volume ls --filter "name=$global:volumeName" --format "{{.Name}}"
    if ([string]::IsNullOrWhiteSpace($existingVolume)) {
        Write-Host "Creating volume '$global:volumeName' for Qdrant data..."
        & $global:enginePath volume create $global:volumeName
    } else {
        Write-Host "Volume '$global:volumeName' already exists."
    }

    # Check/Restore/Pull Image
    if (-not (Check-AndRestoreBackup -Engine $global:enginePath -ImageName $global:imageName)) {
        Write-Host "No backup restored. Pulling Qdrant image '$global:imageName' using $global:containerEngine..."
        & $global:enginePath pull $global:imageName
        if ($LASTEXITCODE -ne 0) {
             Write-Error "$global:containerEngine pull failed for Qdrant. Please check your internet connection or the image name."
             exit 1
        }
    }
    else {
        Write-Host "Using restored backup image '$global:imageName'."
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
        "--detach",                             # Run container in background.
        "--name", $global:containerName,        # Assign the container a name.
        "--publish", "6333:6333",               # Map host HTTP port to container port 6333.
        "--publish", "6334:6334",               # Map host gRPC port to container port 6334.
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

<#
.SYNOPSIS
    Uninstalls the Qdrant container and optionally the data volume.
.DESCRIPTION
    Uses the generic Remove-ContainerAndVolume function.
#>
function Uninstall-QdrantContainer {
    Remove-ContainerAndVolume -Engine $global:enginePath -ContainerName $global:containerName -VolumeName $global:volumeName
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
    # Define Run Function for Update-Container
     $runContainerFunction = {
        # Re-use install logic, but it needs access to global vars
        # This assumes Install-QdrantContainer handles removing old container if needed
        # and uses the latest pulled image implicitly.
        # A more robust approach might pass config, but Install is simple here.
         Install-QdrantContainer
     }

    # Use the shared update function
     Update-Container -Engine $global:enginePath -ContainerName $global:containerName -ImageName $global:imageName -RunFunction $runContainerFunction
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
# Main Menu Loop using Generic Function
################################################################################
$menuActions = @{
    "1" = { Install-QdrantContainer }
    "2" = { Uninstall-QdrantContainer }
    "3" = { Backup-QdrantContainer }
    "4" = { Restore-QdrantContainer }
    "5" = { Update-QdrantContainer }
    "6" = { Update-QdrantUserData }
}

Invoke-MenuLoop -ShowMenuScriptBlock ${function:Show-ContainerMenu} -ActionMap $menuActions -ExitChoice "0"
