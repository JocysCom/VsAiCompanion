################################################################################
# File         : Setup_3_Portainer.ps1
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
$global:containerEngine = Select-ContainerEngine
if ($global:containerEngine -eq "docker") {
    Ensure-Elevated
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

#############################################
# Reusable Helper Functions
#############################################

<#
.SYNOPSIS
    Gets the current Portainer container configuration.
.DESCRIPTION
    Retrieves container information including environment variables and port mappings.
.OUTPUTS
    Returns a custom object with container information or $null if not found.
#>
function Get-PortainerContainerConfig {
    $containerInfo = & $global:enginePath inspect portainer 2>$null | ConvertFrom-Json
    if (-not $containerInfo) {
        Write-Host "Container 'portainer' not found." -ForegroundColor Yellow
        return $null
    }
    
    # Extract environment variables
    $envVars = @()
    try {
        foreach ($env in $containerInfo.Config.Env) {
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

<#
.SYNOPSIS
    Stops and removes the Portainer container.
.DESCRIPTION
    Safely stops and removes the Portainer container while preserving user data.
.OUTPUTS
    Returns $true if successful, $false otherwise.
#>
function Remove-PortainerContainer {
    $existingContainer = & $global:enginePath ps --all --filter "name=portainer" --format "{{.ID}}"
    if (-not $existingContainer) {
        Write-Host "No Portainer container found to remove." -ForegroundColor Yellow
        return $true
    }
    
    Write-Host "Stopping and removing Portainer container..."
    Write-Host "NOTE: This only removes the container, not the volume with user data." -ForegroundColor Yellow
    
    & $global:enginePath stop portainer 2>$null
    & $global:enginePath rm portainer
    
    if ($LASTEXITCODE -eq 0) {
        return $true
    } else {
        Write-Error "Failed to remove Portainer container."
        return $false
    }
}

<#
.SYNOPSIS
    Starts a new Portainer container with the specified configuration.
.DESCRIPTION
    Creates a new Portainer container with the provided image and environment variables.
.PARAMETER Image
    The image to use for the container.
.PARAMETER EnvVars
    Array of environment variables to set in the container.
.OUTPUTS
    Returns $true if successful, $false otherwise.
#>
function Start-PortainerContainer {
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
    Write-Host "Starting Portainer container with image: $Image"
    & $global:enginePath run $runOptions $Image

    if ($LASTEXITCODE -eq 0) {
        Write-Host "Waiting for container startup..."
        Start-Sleep -Seconds 10
        
        # Test connectivity
        $tcpTest = Test-TCPPort -ComputerName "localhost" -Port $HttpPort -serviceName "Portainer"
        $httpTest = Test-HTTPPort -Uri "http://localhost:$HttpPort" -serviceName "Portainer"
        
        if ($tcpTest -and $httpTest) {
            Write-Host "Portainer is now running and accessible at:" -ForegroundColor Green
            Write-Host "  HTTP:  http://localhost:$HttpPort" -ForegroundColor Green
            Write-Host "  HTTPS: https://localhost:$HttpsPort" -ForegroundColor Green
            Write-Host "On first connection, you'll need to create an admin account." -ForegroundColor Cyan
            return $true
        } else {
            Write-Warning "Portainer container started but connectivity tests failed. Please check the container logs."
            return $false
        }
    } else {
        Write-Error "Failed to start Portainer container."
        return $false
    }
}

<#
.SYNOPSIS
    Pulls the latest Portainer image.
.DESCRIPTION
    Downloads the latest version of the Portainer image.
.OUTPUTS
    Returns $true if successful, $false otherwise.
#>
function Pull-PortainerImage {
    Write-Host "Pulling latest Portainer image '$global:imageName'..."
    $pullCmd = @("pull") + $global:pullOptions + $global:imageName
    & $global:enginePath @pullCmd
    
    if ($LASTEXITCODE -eq 0) {
        return $true
    } else {
        Write-Error "Failed to pull latest Portainer image."
        return $false
    }
}

<#
.SYNOPSIS
    Installs the Portainer container.
.DESCRIPTION
    Creates (if necessary) the volume 'portainer_data', pulls the Portainer image if not found
    (or restores from backup), removes any pre-existing container named "portainer",
    prompts for port configuration, builds run options, runs the container,
    waits for startup, and tests connectivity.
#>
function Install-PortainerContainer {
    # Check if volume 'portainer_data' already exists; if not, create it.
    $existingVolume = & $global:enginePath volume ls --filter "name=portainer_data" --format "{{.Name}}"
    if ([string]::IsNullOrWhiteSpace($existingVolume)) {
        Write-Host "Creating volume 'portainer_data'..."
        & $global:enginePath volume create portainer_data
    }
    else {
        Write-Host "Volume 'portainer_data' already exists. Skipping creation."
        Write-Host "IMPORTANT: Using existing volume - all previous user data will be preserved." -ForegroundColor Green
    }

    # Check if the Portainer image is already available.
    $existingImage = & $global:enginePath images --format "{{.Repository}}:{{.Tag}}" | Where-Object { $_ -match "portainer" }
    if (-not $existingImage) {
        if (-not (Check-AndRestoreBackup -Engine $global:enginePath -ImageName $global:imageName)) {
            Write-Host "No backup restored. Pulling Portainer image '$global:imageName'..."
            if (-not (Pull-PortainerImage)) {
                Write-Error "Image pull failed. Exiting..."
                return
            }
        }
    }
}

<#
.SYNOPSIS
    Uninstalls the Portainer container and optionally the data volume.
.DESCRIPTION
    Uses the generic Remove-ContainerAndVolume function.
#>
function Uninstall-PortainerContainer {
    Remove-ContainerAndVolume -Engine $global:enginePath -ContainerName "portainer" -VolumeName "portainer_data"
}

<#
.SYNOPSIS
    Backs up the live Portainer container.
.DESCRIPTION
    Uses the Backup-ContainerState helper function to create a backup of the container.
#>
function Backup-PortainerContainer {
    Backup-ContainerState -Engine $global:enginePath -ContainerName "portainer"
}

<#
.SYNOPSIS
    Restores the Portainer container from backup.
.DESCRIPTION
    Uses the Restore-ContainerState helper function to restore the container from a backup.
#>
function Restore-PortainerContainer {
    Restore-ContainerState -Engine $global:enginePath -ContainerName "portainer"
}

<#
.SYNOPSIS
    Updates the Portainer container without resetting user data.
.DESCRIPTION
    Updates the Portainer container to the latest version while preserving all user data and configuration.
#>
function Update-PortainerContainer {
    # Step 1: Check if container exists and get its configuration
    $config = Get-PortainerContainerConfig
    if (-not $config) {
        Write-Host "No Portainer container found to update. Please install it first." -ForegroundColor Yellow
        return
    }
    
    # Step 2: Optionally backup the container
    $createBackup = Read-Host "Create backup before updating? (Y/N, default is Y)"
    if ($createBackup -ne "N") {
        Write-Host "Creating backup of current container..."
        Backup-PortainerContainer
    }
    
    # Step 3: Remove the existing container
    if (-not (Remove-PortainerContainer)) {
        Write-Error "Failed to remove existing container. Update aborted."
        return
    }
    
    # Step 4: Pull the latest image
    if (-not (Pull-PortainerImage)) {
        Write-Error "Failed to pull latest image. Update aborted."
        
        # Offer to restore from backup if one was created
        if ($createBackup -ne "N") {
            $restore = Read-Host "Would you like to restore from backup? (Y/N, default is Y)"
            if ($restore -ne "N") {
                Restore-PortainerContainer
            }
        }
        return
    }
    
    # Step 5: Start a new container with the latest image and preserved configuration
    if (Start-PortainerContainer -Image $global:imageName -EnvVars $config.EnvVars) {
        Write-Host "Portainer container updated successfully!" -ForegroundColor Green
    } else {
        Write-Error "Failed to start updated container."
        
        # Offer to restore from backup if one was created
        if ($createBackup -ne "N") {
            $restore = Read-Host "Would you like to restore from backup? (Y/N, default is Y)"
            if ($restore -ne "N") {
                Restore-PortainerContainer
            }
        }
    }
}

<#
.SYNOPSIS
    Displays the main menu for Portainer container operations.
.DESCRIPTION
    Presents menu options (Install, Uninstall, Backup, Restore, Update);hhhh
    the exit option ("0") terminates the menu loop.
#>
function Show-ContainerMenu {
    Write-Host "==========================================="
    Write-Host "Portainer Container Menu"
    Write-Host "==========================================="
    Write-Host "1. Install container"
    Write-Host "2. Uninstall container (preserves user data)"
    Write-Host "3. Backup Live container"
    Write-Host "4. Restore Live container"
    Write-Host "5. Update container"
    Write-Host "0. Exit menu"
}

################################################################################
# Main Menu Loop using Generic Function
################################################################################
$menuActions = @{
    "1" = { Install-PortainerContainer }
    "2" = { Uninstall-PortainerContainer }
    "3" = { Backup-PortainerContainer }
    "4" = { Restore-PortainerContainer }
    "5" = { Update-PortainerContainer }
}

Invoke-MenuLoop -ShowMenuScriptBlock ${function:Show-ContainerMenu} -ActionMap $menuActions -ExitChoice "0"
