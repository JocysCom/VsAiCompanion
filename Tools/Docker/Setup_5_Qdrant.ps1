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
    Test-AdminPrivileges # Use renamed function
    $global:enginePath = Get-DockerPath
    Write-Output "Using Docker with executable: $global:enginePath" # Replaced Write-Host
    # For Docker, set DOCKER_HOST pointing to the Docker service pipe.
    $env:DOCKER_HOST = "npipe:////./pipe/docker_engine"
}
else {
    $global:enginePath = Get-PodmanPath
    Write-Output "Using Podman with executable: $global:enginePath" # Replaced Write-Host
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
        Write-Output "Creating volume '$global:volumeName' for Qdrant data..." # Replaced Write-Host
        & $global:enginePath volume create $global:volumeName
    } else {
        Write-Output "Volume '$global:volumeName' already exists." # Replaced Write-Host
    }

    # Check/Restore/Pull Image
    if (-not (Test-AndRestoreBackup -Engine $global:enginePath -ImageName $global:imageName)) { # Use renamed function
        Write-Output "No backup restored. Pulling Qdrant image '$global:imageName' using $global:containerEngine..." # Replaced Write-Host
        & $global:enginePath pull $global:imageName
        if ($LASTEXITCODE -ne 0) {
             Write-Error "$global:containerEngine pull failed for Qdrant. Please check your internet connection or the image name."
             exit 1
        }
    }
    else {
        Write-Output "Using restored backup image '$global:imageName'." # Replaced Write-Host
    }

    # Remove Existing Container
    $existingContainer = & $global:enginePath ps --all --filter "name=$global:containerName" --format "{{.ID}}"
    if ($existingContainer) {
        Write-Output "Removing existing container '$global:containerName'..." # Replaced Write-Host
        & $global:enginePath rm --force $global:containerName
    }

    # Run Container
    Write-Output "Starting Qdrant container..." # Replaced Write-Host
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
    Write-Output "Waiting 20 seconds for the Qdrant container to fully start..." # Replaced Write-Host
    Start-Sleep -Seconds 20
    Test-TCPPort -ComputerName "localhost" -Port 6333 -serviceName "Qdrant HTTP"
    Test-HTTPPort -Uri "http://localhost:6333" -serviceName "Qdrant HTTP"
    Test-TCPPort -ComputerName "localhost" -Port 6334 -serviceName "Qdrant gRPC"
    Write-Output "Qdrant is now running and accessible at http://localhost:6333" # Replaced Write-Host
}

<#
.SYNOPSIS
    Uninstalls the Qdrant container and optionally the data volume.
.DESCRIPTION
    Uses the generic Remove-ContainerAndVolume function.
#>
function Uninstall-QdrantContainer {
    Remove-ContainerAndVolume -Engine $global:enginePath -ContainerName $global:containerName -VolumeName $global:volumeName # This function supports ShouldProcess
}

<#
.SYNOPSIS
    Backs up the live Qdrant container.
.DESCRIPTION
    Uses the Backup-ContainerState helper function to create a backup of the Qdrant container.
#>
function Backup-QdrantContainer {
    Backup-ContainerState -Engine $global:enginePath -ContainerName $global:containerName # This function supports ShouldProcess
}

<#
.SYNOPSIS
    Restores the Qdrant container from backup.
.DESCRIPTION
    Uses the Restore-ContainerState helper function to restore the Qdrant container.
#>
function Restore-QdrantContainer {
    Restore-ContainerState -Engine $global:enginePath -ContainerName $global:containerName # This function supports ShouldProcess
}

<#
.SYNOPSIS
    Updates the Qdrant container.
.DESCRIPTION
    Removes any existing Qdrant container, pulls the latest image from the registry, and reinstalls the container.
#>
function Update-QdrantContainer {
    [CmdletBinding(SupportsShouldProcess=$true)] # Added SupportsShouldProcess
    param()

    # Define Run Function for Update-Container
     $runContainerFunction = {
        # Re-use install logic, but it needs access to global vars
        # This assumes Install-QdrantContainer handles removing old container if needed
        # and uses the latest pulled image implicitly.
        # A more robust approach might pass config, but Install is simple here.
         Install-QdrantContainer
     }

    # Use the shared update function (which supports ShouldProcess)
    if ($PSCmdlet.ShouldProcess("Qdrant Container", "Update to the latest version")) {
        Update-Container -Engine $global:enginePath -ContainerName $global:containerName -ImageName $global:imageName -RunFunction $runContainerFunction
    }
}

<#
.SYNOPSIS
    Updates user data for the Qdrant container.
.DESCRIPTION
    This functionality is not implemented.
#>
function Update-QdrantUserData {
    [CmdletBinding(SupportsShouldProcess=$true)] # Added SupportsShouldProcess
    param()

    if ($PSCmdlet.ShouldProcess("Qdrant Container User Data", "Update user data")) {
        # No actions to wrap with ShouldProcess as it's not implemented
        Write-Output "Update User Data functionality is not implemented for Qdrant container." # Replaced Write-Host
    }
}

<#
.SYNOPSIS
    Displays the main menu for Qdrant container operations.
.DESCRIPTION
    Presents menu options for installing, uninstalling, backing up, restoring, updating (system and user data).
    The exit option ("0") terminates the menu loop.
#>
function Show-ContainerMenu {
    Write-Output "===========================================" # Replaced Write-Host
    Write-Output "Qdrant Container Menu" # Replaced Write-Host
    Write-Output "===========================================" # Replaced Write-Host
    Write-Output "1. Install container" # Replaced Write-Host
    Write-Output "2. Uninstall container" # Replaced Write-Host
    Write-Output "3. Backup Live container" # Replaced Write-Host
    Write-Output "4. Restore Live container" # Replaced Write-Host
    Write-Output "5. Update System" # Replaced Write-Host
    Write-Output "6. Update User Data" # Replaced Write-Host
    Write-Output "0. Exit menu" # Replaced Write-Host
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
