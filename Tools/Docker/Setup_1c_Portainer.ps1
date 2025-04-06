################################################################################
# File         : Setup_1c_Portainer.ps1
# Description  : Script to set up and run Portainer container using Docker/Podman.
#                Provides installation, uninstallation, backup, restore, and
#                update functionality for Portainer - a lightweight web UI for
#                managing Docker and Podman environments.
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

# Ensure the script working directory is set.
Set-ScriptLocation

#############################################
# Global Variables
#############################################
# Note: PSAvoidGlobalVars warnings are ignored here as these are used across menu actions.
$global:containerEngine = Select-ContainerEngine
if ($global:containerEngine -eq "docker") {
    Test-AdminPrivilege
    $global:enginePath    = Get-DockerPath
    $global:pullOptions   = @()  # No extra options needed for Docker.
    $global:imageName     = "portainer/portainer-ce:latest"
}
else {
    $global:enginePath    = Get-PodmanPath
    $global:pullOptions   = @("--tls-verify=false")
    $global:imageName     = "portainer/portainer-ce:latest"
}

# Default ports for Portainer
$global:httpPort = 9000
$global:httpsPort = 9443

#==============================================================================
# Function: Get-PortainerContainerConfig
#==============================================================================
<#
.SYNOPSIS
    Gets the current Portainer container configuration.
.DESCRIPTION
    Retrieves container information including the image name and Portainer-specific
    environment variables by inspecting the 'portainer' container using the selected engine.
.OUTPUTS
    [PSCustomObject] Returns a custom object with 'Image' and 'EnvVars' properties,
                     or $null if the container is not found or inspection fails.
.EXAMPLE
    $config = Get-PortainerContainerConfig
    if ($config) { Write-Host "Image: $($config.Image)" }
.NOTES
    Uses 'engine inspect'. Filters environment variables to keep only those starting with 'PORTAINER_'.
#>
function Get-PortainerContainerConfig {
    $containerInfo = & $global:enginePath inspect portainer 2>$null | ConvertFrom-Json
    if (-not $containerInfo) {
        Write-Information "Container 'portainer' not found."
        return $null
    }

    # Extract environment variables
    $envVars = @()
    try {
        # Handle potential single vs multiple env vars
        $envList = @($containerInfo.Config.Env)
        foreach ($env in $envList) {
            # Only preserve Portainer-specific environment variables
            if ($env -match "^(PORTAINER_)") {
                $envVars += $env
            }
        }
    } catch {
        Write-Warning "Could not parse existing environment variables: $_"
    }

    # Return a custom object with the container information
    return [PSCustomObject]@{
        Image = $containerInfo.Config.Image
        EnvVars = $envVars
    }
}

#==============================================================================
# Function: Remove-PortainerContainer
#==============================================================================
<#
.SYNOPSIS
    Stops and removes the Portainer container.
.DESCRIPTION
    Checks if a container named 'portainer' exists. If it does, it stops and removes it
    using the selected container engine. Supports -WhatIf.
.OUTPUTS
    [bool] Returns $true if the container is removed successfully or didn't exist.
           Returns $false if removal fails or is skipped due to -WhatIf.
.EXAMPLE
    Remove-PortainerContainer -WhatIf
.NOTES
    Uses 'engine ps', 'engine stop', and 'engine rm'.
    Explicitly notes that the 'portainer_data' volume is not removed by this function.
#>
function Remove-PortainerContainer {
    [CmdletBinding(SupportsShouldProcess=$true)]
    [OutputType([bool])]
    param()

    $existingContainer = & $global:enginePath ps --all --filter "name=portainer" --format "{{.ID}}"
    if (-not $existingContainer) {
        Write-Information "No Portainer container found to remove."
        return $true
    }

    if ($PSCmdlet.ShouldProcess("portainer", "Stop and Remove Container")) {
        Write-Information "Stopping and removing Portainer container..."
        Write-Information "NOTE: This only removes the container, not the volume with user data."

        & $global:enginePath stop portainer 2>$null
        & $global:enginePath rm portainer

        if ($LASTEXITCODE -eq 0) {
            return $true
        } else {
            Write-Error "Failed to remove Portainer container."
            return $false
        }
    } else {
        return $false # Action skipped due to -WhatIf
    }
}

#==============================================================================
# Function: Start-PortainerContainer
#==============================================================================
<#
.SYNOPSIS
    Starts a new Portainer container with specified or default configuration.
.DESCRIPTION
    Runs a new container using the selected engine with the specified image.
    Configures standard Portainer settings: detached mode, name 'portainer',
    mounts 'portainer_data' volume, maps HTTP/HTTPS ports, and mounts the
    appropriate engine socket (/var/run/docker.sock or /run/podman/podman.sock).
    Applies any additional environment variables provided.
    After starting, waits 10 seconds and performs TCP and HTTP connectivity tests.
    Supports -WhatIf.
.PARAMETER Image
    The Portainer container image to use (e.g., 'portainer/portainer-ce:latest'). Mandatory.
.PARAMETER EnvVars
    Optional array of environment variables strings (e.g., @("MY_VAR=value")).
.PARAMETER HttpPort
    Optional. The host port to map to container port 9000. Defaults to global $httpPort (9000).
.PARAMETER HttpsPort
    Optional. The host port to map to container port 9443. Defaults to global $httpsPort (9443).
.OUTPUTS
    [bool] Returns $true if the container starts successfully and connectivity tests pass.
           Returns $false if start fails, tests fail, or action is skipped due to -WhatIf.
.EXAMPLE
    Start-PortainerContainer -Image "portainer/portainer-ce:latest"
.EXAMPLE
    Start-PortainerContainer -Image "portainer/portainer-ce:latest" -HttpPort 8080 -HttpsPort 8443
.NOTES
    Relies on Test-TCPPort and Test-HTTPPort helper functions.
    Uses Write-Information for status messages.
#>
function Start-PortainerContainer {
    [CmdletBinding(SupportsShouldProcess=$true)]
    param(
        [Parameter(Mandatory=$true)]
        [string]$Image,

        [Parameter(Mandatory=$false)]
        [array]$EnvVars = @(),

        [Parameter(Mandatory=$false)]
        [int]$HttpPort = $global:httpPort,

        [Parameter(Mandatory=$false)]
        [int]$HttpsPort = $global:httpsPort
    )

    # Build the run command
    $runOptions = @(
        "--detach",                             # Run container in background.
        "--publish", "${HttpPort}:9000",        # Map host HTTP port to container port 9000.
        "--publish", "${HttpsPort}:9443",       # Map host HTTPS port to container port 9443.
        "--volume", "portainer_data:/data"      # Mount the named volume for persistent data.
    )

    # Add socket volume based on container engine
    if ($global:containerEngine -eq "docker") {
        # Mount the Docker socket for container management.
        $runOptions += "--volume"
        $runOptions += "/var/run/docker.sock:/var/run/docker.sock"
    } else {
        # Mount the Podman socket for container management (read-only).
        $runOptions += "--volume"
        $runOptions += "/run/podman/podman.sock:/var/run/docker.sock:ro"
    }

    # Assign a name to the container.
    $runOptions += "--name"
    $runOptions += "portainer"

    # Add all environment variables (if any).
    foreach ($env in $EnvVars) {
        $runOptions += "--env"
        $runOptions += $env
    }

    # Run the container
    if ($PSCmdlet.ShouldProcess("portainer", "Start Container with Image '$Image'")) {
        Write-Information "Starting Portainer container with image: $Image"
        & $global:enginePath run $runOptions $Image

        if ($LASTEXITCODE -eq 0) {
            Write-Information "Waiting for container startup..."
            Start-Sleep -Seconds 10

            # Test connectivity
            $tcpTest = Test-TCPPort -ComputerName "localhost" -Port $HttpPort -serviceName "Portainer"
            $httpTest = Test-HTTPPort -Uri "http://localhost:$HttpPort" -serviceName "Portainer"

            if ($tcpTest -and $httpTest) {
                Write-Information "Portainer is now running and accessible at:"
                Write-Information "  HTTP:  http://localhost:$HttpPort"
                Write-Information "  HTTPS: https://localhost:$HttpsPort"
                Write-Information "On first connection, you'll need to create an admin account."
                return $true
            } else {
                Write-Warning "Portainer container started but connectivity tests failed. Please check the container logs."
                return $false
            }
        } else {
            Write-Error "Failed to start Portainer container."
            return $false
        }
    } else {
        return $false # Action skipped due to -WhatIf
    }
}

#==============================================================================
# Function: Install-PortainerContainer
#==============================================================================
<#
.SYNOPSIS
    Installs and starts the Portainer container.
.DESCRIPTION
    Ensures the 'portainer_data' volume exists using Confirm-ContainerVolume.
    Checks if the Portainer image exists locally; if not, attempts to restore from backup using
    Test-AndRestoreBackup, falling back to pulling the image using Invoke-PullImage.
    Removes any existing 'portainer' container using Remove-PortainerContainer.
    Starts the new container using Start-PortainerContainer.
.EXAMPLE
    Install-PortainerContainer
.NOTES
    Orchestrates volume creation, image acquisition, cleanup, and container start.
    Relies on Confirm-ContainerVolume, Test-AndRestoreBackup, Invoke-PullImage,
    Remove-PortainerContainer, and Start-PortainerContainer helper functions.
    Uses Write-Information for status messages.
#>
function Install-PortainerContainer {
    # Ensure the volume exists
    if (-not (Confirm-ContainerVolume -Engine $global:enginePath -VolumeName "portainer_data")) {
        Write-Error "Failed to ensure volume 'portainer_data' exists. Exiting..."
        return
    }
    Write-Information "IMPORTANT: Using volume 'portainer_data' - existing user data will be preserved."

    # Check if the Portainer image is already available.
    $existingImage = & $global:enginePath images --filter "reference=$($global:imageName)" --format "{{.ID}}"
    if (-not $existingImage) {
        if (-not (Test-AndRestoreBackup -Engine $global:enginePath -ImageName $global:imageName)) {
            Write-Information "No backup restored. Pulling Portainer image '$global:imageName'..."
            # Use the shared Invoke-PullImage function
            if (-not (Invoke-PullImage -Engine $global:enginePath -ImageName $global:imageName -PullOptions $global:pullOptions)) {
                Write-Error "Image pull failed. Exiting..."
                return
            }
        }
    }

    # Remove existing container before starting new one
    Remove-PortainerContainer # This now supports ShouldProcess

    # Start the container
    Start-PortainerContainer -Image $global:imageName # This now supports ShouldProcess
}

#==============================================================================
# Function: Uninstall-PortainerContainer
#==============================================================================
<#
.SYNOPSIS
    Uninstalls the Portainer container and optionally removes its data volume.
.DESCRIPTION
    Calls the Remove-ContainerAndVolume helper function, specifying 'portainer' as the container
    and 'portainer_data' as the volume. This will stop/remove the container and prompt the user
    about removing the volume. Supports -WhatIf.
.EXAMPLE
    Uninstall-PortainerContainer -Confirm:$false
.NOTES
    Relies on Remove-ContainerAndVolume helper function.
#>
function Uninstall-PortainerContainer {
    Remove-ContainerAndVolume -Engine $global:enginePath -ContainerName "portainer" -VolumeName "portainer_data" # This now supports ShouldProcess
}

#==============================================================================
# Function: Backup-PortainerContainer
#==============================================================================
<#
.SYNOPSIS
    Backs up the state of the running Portainer container.
.DESCRIPTION
    Calls the Backup-ContainerState helper function, specifying 'portainer' as the container name.
    This commits the container state to an image and saves it as a tar file.
.EXAMPLE
    Backup-PortainerContainer
.NOTES
    Relies on Backup-ContainerState helper function.
#>
function Backup-PortainerContainer {
    Backup-ContainerState -Engine $global:enginePath -ContainerName "portainer"
}

#==============================================================================
# Function: Restore-PortainerContainer
#==============================================================================
<#
.SYNOPSIS
    Restores the Portainer container image from a backup tar file.
.DESCRIPTION
    Calls the Restore-ContainerState helper function, specifying 'portainer' as the container name.
    This loads the image from the backup tar file. Note: This only restores the image,
    it does not automatically start a container from it.
.EXAMPLE
    Restore-PortainerContainer
.NOTES
    Relies on Restore-ContainerState helper function. Does not handle volume restore.
#>
function Restore-PortainerContainer {
    Restore-ContainerState -Engine $global:enginePath -ContainerName "portainer"
}

#==============================================================================
# Function: Update-PortainerContainer
#==============================================================================
<#
.SYNOPSIS
    Updates the Portainer container to the latest image version while preserving data.
.DESCRIPTION
    Performs an update workflow:
    1. Gets the current container config using Get-PortainerContainerConfig.
    2. Prompts the user whether to create a backup before updating.
    3. Removes the existing container using Remove-PortainerContainer.
    4. Pulls the latest image using Invoke-PullImage.
    5. Starts a new container using Start-PortainerContainer with the latest image and preserved environment variables.
    6. Offers to restore from backup if pull or start fails (and backup was made).
    Supports -WhatIf for backup, remove, pull, and start actions.
.EXAMPLE
    Update-PortainerContainer -WhatIf
.NOTES
    Relies on Get-PortainerContainerConfig, Backup-PortainerContainer, Remove-PortainerContainer,
    Invoke-PullImage, Start-PortainerContainer, Restore-PortainerContainer helper functions.
    User interaction handled via Read-Host for backup confirmation.
#>
function Update-PortainerContainer {
    [CmdletBinding(SupportsShouldProcess=$true)]
    param()

    # Step 1: Check if container exists and get its configuration
    $config = Get-PortainerContainerConfig
    if (-not $config) {
        Write-Information "No Portainer container found to update. Please install it first."
        return
    }

    # Step 2: Optionally backup the container
    $createBackup = Read-Host "Create backup before updating? (Y/N, default is Y)"
    if ($createBackup -ne "N") {
        if ($PSCmdlet.ShouldProcess("portainer", "Backup Container State")) {
            Write-Information "Creating backup of current container..."
            Backup-PortainerContainer
        }
    }

    # Step 3: Remove the existing container
    if (-not (Remove-PortainerContainer)) { # This function now supports ShouldProcess
        Write-Error "Failed to remove existing container or action skipped. Update aborted."
        return
    }

    # Step 4: Pull the latest image
    if ($PSCmdlet.ShouldProcess($global:imageName, "Pull Latest Image")) {
        # Use the shared Invoke-PullImage function
        if (-not (Invoke-PullImage -Engine $global:enginePath -ImageName $global:imageName -PullOptions $global:pullOptions)) {
            Write-Error "Failed to pull latest image. Update aborted."

            # Offer to restore from backup if one was created
            if ($createBackup -ne "N") {
                $restore = Read-Host "Would you like to restore from backup? (Y/N, default is Y)"
                if ($restore -ne "N") {
                    if ($PSCmdlet.ShouldProcess("portainer", "Restore Container State after Failed Update")) {
                        Restore-PortainerContainer
                    }
                }
            }
            return
        }
    } else {
         Write-Information "Skipping image pull due to -WhatIf."
    }

    # Step 5: Start a new container with the latest image and preserved configuration
    if (Start-PortainerContainer -Image $global:imageName -EnvVars $config.EnvVars) { # This function now supports ShouldProcess
        Write-Information "Portainer container updated successfully!"
    } else {
        Write-Error "Failed to start updated container or action skipped."

        # Offer to restore from backup if one was created
        if ($createBackup -ne "N") {
            $restore = Read-Host "Would you like to restore from backup? (Y/N, default is Y)"
            if ($restore -ne "N") {
                 if ($PSCmdlet.ShouldProcess("portainer", "Restore Container State after Failed Start")) {
                    Restore-PortainerContainer
                 }
            }
        }
    }
}

#==============================================================================
# Function: Show-ContainerMenu
#==============================================================================
<#
.SYNOPSIS
    Displays the main menu options for Portainer container management.
.DESCRIPTION
    Writes the available menu options (Show Info, Install, Uninstall, Backup, Restore, Update, Exit)
    to the console using Write-Output.
.EXAMPLE
    Show-ContainerMenu
.NOTES
    Uses Write-Output for direct console display.
#>
function Show-ContainerMenu {
    Write-Output "==========================================="
    Write-Output "Portainer Container Menu"
    Write-Output "==========================================="
    Write-Output "1. Show Info & Test Connection"
    Write-Output "2. Install container"
    Write-Output "3. Uninstall container (preserves user data)"
    Write-Output "4. Backup Live container"
    Write-Output "5. Restore Live container"
    Write-Output "6. Update container"
    Write-Output "0. Exit menu"
}

################################################################################
# Main Menu Loop using Generic Function
################################################################################
$menuActions = @{
    "1" = {
        # Pass the global variable directly to the restored -ContainerEngine parameter
        Show-ContainerStatus -ContainerName "portainer" `
                             -ContainerEngine $global:containerEngine `
                             -EnginePath $global:enginePath `
                             -DisplayName "Portainer" `
                             -TcpPort $global:httpPort `
                             -HttpPort $global:httpPort
    }
    "2" = { Install-PortainerContainer }
    "3" = { Uninstall-PortainerContainer }
    "4" = { Backup-PortainerContainer }
    "5" = { Restore-PortainerContainer }
    "6" = { Update-PortainerContainer }
}

Invoke-MenuLoop -ShowMenuScriptBlock ${function:Show-ContainerMenu} -ActionMap $menuActions -ExitChoice "0"
