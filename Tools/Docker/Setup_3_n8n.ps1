################################################################################
# File         : Setup_3_n8n.ps1
# Description  : Script to set up and run the n8n container using Docker/Podman.
#                Verifies volume presence, pulls the n8n image if necessary,
#                and runs the container with port and volume mappings.
#                Additionally, prompts for an external domain to set N8N_HOST
#                and WEBHOOK_URL if needed.
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
    $global:imageName     = "docker.n8n.io/n8nio/n8n:latest"
}
else {
    $global:enginePath    = Get-PodmanPath
    $global:pullOptions   = @("--tls-verify=false")
    # Use the Docker Hub version of n8n for Podman to avoid 403 errors.
    $global:imageName     = "docker.io/n8nio/n8n:latest"
}

#############################################
# Reusable Helper Functions
#############################################

<#
.SYNOPSIS
    Gets the current n8n container configuration.
.DESCRIPTION
    Retrieves container information including environment variables.
.OUTPUTS
    Returns a custom object with container information or $null if not found.
#>
function Get-n8nContainerConfig {
    $containerInfo = & $global:enginePath inspect n8n 2>$null | ConvertFrom-Json
    if (-not $containerInfo) {
        Write-Host "Container 'n8n' not found." -ForegroundColor Yellow
        return $null
    }
    
    # Extract environment variables
    $envVars = @()
    try {
        foreach ($env in $containerInfo.Config.Env) {
            # Only preserve n8n-specific environment variables
            if ($env -match "^(N8N_|WEBHOOK_)") {
                $envVars += $env
            }
        }
    } catch {
        Write-Warning "Could not parse existing environment variables: $_"
    }
    
    # Ensure N8N_COMMUNITY_PACKAGES_ENABLED is set to true
    $communityPackagesEnabled = $false
    $communityPackagesToolsEnabled = $false
    foreach ($env in $envVars) {
        if ($env -match "^N8N_COMMUNITY_PACKAGES_ENABLED=") {
            $communityPackagesEnabled = $true
        }
        if ($env -match "^N8N_COMMUNITY_PACKAGES_ALLOW_TOOL_USAGE=") {
            $communityPackagesToolsEnabled = $true
        }
    }
    if (-not $communityPackagesEnabled) {
        $envVars += "N8N_COMMUNITY_PACKAGES_ENABLED=true"
    }
    if (-not $communityPackagesToolsEnabled) {
        $envVars += "N8N_COMMUNITY_PACKAGES_ALLOW_TOOL_USAGE=true"
    }
	
    # Prompt user for external domain configuration.
    $externalDomain = Read-Host "Enter external domain for n8n container (e.g., n8n.example.com) or press Enter to skip"
	
    # If an external domain is provided, add environment variable options.
    if (-not [string]::IsNullOrWhiteSpace($externalDomain)) {
        $envVars += "N8N_HOST=$externalDomain"
        $envVars += "WEBHOOK_URL=https://$externalDomain"
    }
    
    # Return a custom object with the container information
    return [PSCustomObject]@{
        Image = $containerInfo.Config.Image
        EnvVars = $envVars
    }
}

<#
.SYNOPSIS
    Stops and removes the n8n container.
.DESCRIPTION
    Safely stops and removes the n8n container while preserving user data.
.OUTPUTS
    Returns $true if successful, $false otherwise.
#>
function Remove-n8nContainer {
    $existingContainer = & $global:enginePath ps --all --filter "name=n8n" --format "{{.ID}}"
    if (-not $existingContainer) {
        Write-Host "No n8n container found to remove." -ForegroundColor Yellow
        return $true
    }
    
    Write-Host "Stopping and removing n8n container..."
    Write-Host "NOTE: This only removes the container, not the volume with user data." -ForegroundColor Yellow
    
    & $global:enginePath stop n8n 2>$null
    & $global:enginePath rm n8n
    
    if ($LASTEXITCODE -eq 0) {
        return $true
    } else {
        Write-Error "Failed to remove n8n container."
        return $false
    }
}

<#
.SYNOPSIS
    Starts a new n8n container with the specified configuration.
.DESCRIPTION
    Creates a new n8n container with the provided image and environment variables.
.PARAMETER Image
    The image to use for the container.
.PARAMETER EnvVars
    Array of environment variables to set in the container.
.OUTPUTS
    Returns $true if successful, $false otherwise.
#>
function Start-n8nContainer {
    param(
        [Parameter(Mandatory=$true)]
        [string]$Image,
        
        [Parameter(Mandatory=$false)]
        [array]$EnvVars = @()
    )
    
    # Build the run command
    $runOptions = @(
        "--detach",
        "--publish", "5678:5678",
        "--volume", "n8n_data:/home/node/.n8n",
        "--name", "n8n"
    )
    
    # Add all environment variables
    foreach ($env in $EnvVars) {
        $runOptions += "--env"
        $runOptions += $env
    }
    
    # Run the container
    Write-Host "Starting n8n container with image: $Image"
    & $global:enginePath run $runOptions $Image
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Waiting for container startup..."
        Start-Sleep -Seconds 20
        
        # Test connectivity
        $tcpTest = Test-TCPPort -ComputerName "localhost" -Port 5678 -serviceName "n8n"
        $httpTest = Test-HTTPPort -Uri "http://localhost:5678" -serviceName "n8n"
        
        if ($tcpTest -and $httpTest) {
            Write-Host "n8n is now running and accessible at http://localhost:5678" -ForegroundColor Green
            Write-Host "If accessing from another container, use 'http://host.docker.internal:5678' as the URL." -ForegroundColor Cyan
            return $true
        } else {
            Write-Warning "n8n container started but connectivity tests failed. Please check the container logs."
            return $false
        }
    } else {
        Write-Error "Failed to start n8n container."
        return $false
    }
}

<#
.SYNOPSIS
    Pulls the latest n8n image.
.DESCRIPTION
    Downloads the latest version of the n8n image.
.OUTPUTS
    Returns $true if successful, $false otherwise.
#>
function Pull-n8nImage {
    Write-Host "Pulling latest n8n image '$global:imageName'..."
    $pullCmd = @("pull") + $global:pullOptions + $global:imageName
    & $global:enginePath @pullCmd
    
    if ($LASTEXITCODE -eq 0) {
        return $true
    } else {
        Write-Error "Failed to pull latest n8n image."
        return $false
    }
}

<#
.SYNOPSIS
    Installs the n8n container.
.DESCRIPTION
    Creates (if necessary) the volume 'n8n_data', pulls the n8n image if not found
    (or restores from backup), removes any pre-existing container named "n8n",
    prompts for an external domain, builds run options, runs the container,
    waits for startup, and tests connectivity on port 5678.
    Core argument values are preserved.
#>
function Install-n8nContainer {
    # Check if volume 'n8n_data' already exists; if not, create it.
    $existingVolume = & $global:enginePath volume ls --filter "name=n8n_data" --format "{{.Name}}"
    if ([string]::IsNullOrWhiteSpace($existingVolume)) {
        Write-Host "Creating volume 'n8n_data'..."
        & $global:enginePath volume create n8n_data
    }
    else {
        Write-Host "Volume 'n8n_data' already exists. Skipping creation."
        Write-Host "IMPORTANT: Using existing volume - all previous user data will be preserved." -ForegroundColor Green
    }

    # Check if the n8n image is already available.
    $existingImage = & $global:enginePath images --format "{{.Repository}}:{{.Tag}}" | Where-Object { $_ -match "n8n" }
    if (-not $existingImage) {
        if (-not (Check-AndRestoreBackup -Engine $global:enginePath -ImageName $global:imageName)) {
            Write-Host "No backup restored. Pulling n8n image '$global:imageName'..."
            if (-not (Pull-n8nImage)) {
                Write-Error "Image pull failed. Exiting..."
                return
            }
        }
        else {
            Write-Host "Using restored backup image '$global:imageName'."
        }
    }
    else {
        Write-Host "n8n image already exists. Skipping pull."
    }

    # Remove any existing container
    Remove-n8nContainer

    # Define environment variables
    $envVars = @()
	
	$envVars += "N8N_COMMUNITY_PACKAGES_ENABLED=true"
	$envVars += "N8N_COMMUNITY_PACKAGES_ALLOW_TOOL_USAGE=true"

    # Prompt user for external domain configuration.
    $externalDomain = Read-Host "Enter external domain for n8n container (e.g., n8n.example.com) or press Enter to skip"

    # If an external domain is provided, add environment variable options.
    if (-not [string]::IsNullOrWhiteSpace($externalDomain)) {
        $envVars += "N8N_HOST=$externalDomain"
        $envVars += "WEBHOOK_URL=https://$externalDomain"
    }

    # Start the container
    Start-n8nContainer -Image $global:imageName -EnvVars $envVars
}

<#
.SYNOPSIS
    Uninstalls the n8n container.
.DESCRIPTION
    Checks for an existing container named "n8n" and removes it using the container engine's rm command.
    IMPORTANT: This only removes the container, not the volume with user data.
#>
function Uninstall-n8nContainer {
    $existingContainer = & $global:enginePath ps --all --filter "name=n8n" --format "{{.ID}}"
    if ($existingContainer) {
        Write-Host "Removing n8n container..."
        Write-Host "IMPORTANT: This only removes the container, NOT the volume with user data." -ForegroundColor Yellow
        Write-Host "           Your workflows and credentials remain safe in the 'n8n_data' volume." -ForegroundColor Yellow
        
        $confirmation = Read-Host "Continue with container removal? (Y/N)"
        if ($confirmation -ne "Y") {
            Write-Host "Container removal cancelled."
            return
        }
        
        if (Remove-n8nContainer) {
            Write-Host "n8n container removed successfully."
            Write-Host "To completely remove all user data, you would need to manually delete the volume with:" -ForegroundColor DarkYellow
            Write-Host "  $global:enginePath volume rm n8n_data" -ForegroundColor DarkYellow
        }
    }
    else {
        Write-Host "No n8n container found to remove."
    }
}

<#
.SYNOPSIS
    Backs up the live n8n container.
.DESCRIPTION
    Uses the Backup-ContainerState helper function to create a backup of the container.
#>
function Backup-n8nContainer {
    Write-Host "Backing up n8n container..."
    
    # Debug output to verify the container name being passed
    Write-Host "DEBUG: Passing container name 'n8n' to Backup-ContainerState"
    
    # Call Backup-ContainerState with the explicit container name string
    if (Backup-ContainerState -Engine $global:enginePath -ContainerName 'n8n') {
        Write-Host "n8n container backed up successfully." -ForegroundColor Green
        return $true
    } else {
        Write-Error "Failed to backup n8n container."
        return $false
    }
}

<#
.SYNOPSIS
    Restores the n8n container from backup.
.DESCRIPTION
    Uses the Restore-ContainerState helper function to restore the container from a backup.
#>
function Restore-n8nContainer {
    Write-Host "Attempting to restore n8n container and its data volume from backup..."
    
    # First load the image from backup and restore volumes if available
    $imageName = Restore-ContainerState -Engine $global:enginePath -ContainerName "n8n" -RestoreVolumes
    
    if (-not $imageName) {
        Write-Error "Failed to restore image for n8n container."
        return
    }
    
    # Remove any existing container
    Remove-n8nContainer
    
    # Get configuration from existing container or use defaults
    $config = Get-n8nContainerConfig
    if (-not $config) {
        Write-Host "No existing configuration found, using defaults."
        $config = [PSCustomObject]@{
            Image = $imageName
            EnvVars = @(
                "N8N_COMMUNITY_PACKAGES_ENABLED=true",
                "N8N_COMMUNITY_PACKAGES_ALLOW_TOOL_USAGE=true"
            )
        }
    } else {
        # Update the image to use the restored one
        $config.Image = $imageName
    }
    
    # Start the container using the restored image and existing configuration
    if (Start-n8nContainer -Image $config.Image -EnvVars $config.EnvVars) {
        Write-Host "n8n container successfully restored and started." -ForegroundColor Green
    } else {
        Write-Error "Failed to start restored n8n container."
    }
}

<#
.SYNOPSIS
    Updates the n8n container without resetting user data.
.DESCRIPTION
    Updates the n8n container to the latest version while preserving all user data and configuration.
#>
function Update-n8nContainer {
    # Step 1: Check if container exists and get its configuration
    $config = Get-n8nContainerConfig
    if (-not $config) {
        Write-Host "No n8n container found to update. Please install it first." -ForegroundColor Yellow
        return
    }
    
    # Step 2: Optionally backup the container
    $createBackup = Read-Host "Create backup before updating? (Y/N, default is Y)"
    if ($createBackup -ne "N") {
        Write-Host "Creating backup of current container..."
        Backup-n8nContainer
    }
    
    # Step 3: Remove the existing container
    if (-not (Remove-n8nContainer)) {
        Write-Error "Failed to remove existing container. Update aborted."
        return
    }
    
    # Step 4: Pull the latest image
    if (-not (Pull-n8nImage)) {
        Write-Error "Failed to pull latest image. Update aborted."
        
        # Offer to restore from backup if one was created
        if ($createBackup -ne "N") {
            $restore = Read-Host "Would you like to restore from backup? (Y/N, default is Y)"
            if ($restore -ne "N") {
                Restore-n8nContainer
            }
        }
        return
    }
    
    # Step 5: Start a new container with the latest image and preserved configuration
    if (Start-n8nContainer -Image $global:imageName -EnvVars $config.EnvVars) {
        Write-Host "n8n container updated successfully!" -ForegroundColor Green
    } else {
        Write-Error "Failed to start updated container."
        
        # Offer to restore from backup if one was created
        if ($createBackup -ne "N") {
            $restore = Read-Host "Would you like to restore from backup? (Y/N, default is Y)"
            if ($restore -ne "N") {
                Restore-n8nContainer
            }
        }
    }
}

<#
.SYNOPSIS
    Updates user data for the n8n container.
.DESCRIPTION
    This functionality is not yet implemented.
#>
function Update-n8nUserData {
    Write-Host "Update User Data functionality is not implemented for n8n container."
}

<#
.SYNOPSIS
    Restarts the n8n container with updated environment variables.
.DESCRIPTION
    Safely stops and removes the existing container, then creates a new one with
    the same image and volume but updated environment variables. This preserves
    all user data while allowing configuration changes.
#>
function Restart-n8nContainer {
    # Get current container configuration
    $config = Get-n8nContainerConfig
    if (-not $config) {
        Write-Host "No n8n container found to restart. Please install it first." -ForegroundColor Yellow
        return
    }
    
    # Remove the existing container
    if (-not (Remove-n8nContainer)) {
        Write-Error "Failed to remove existing container. Restart aborted."
        return
    }
    
    # Start a new container with the same image and configuration
    if (Start-n8nContainer -Image $config.Image -EnvVars $config.EnvVars) {
        Write-Host "n8n container restarted successfully with community packages enabled!" -ForegroundColor Green
    } else {
        Write-Error "Failed to restart container."
    }
}

<#
.SYNOPSIS
    Displays the main menu for n8n container operations.
.DESCRIPTION
    Presents menu options (Install, Uninstall, Backup, Restore, Update System, and Update User Data);
    the exit option ("0") terminates the menu loop.
#>
function Show-ContainerMenu {
    Write-Host "==========================================="
    Write-Host "n8n Container Menu"
    Write-Host "==========================================="
    Write-Host "1. Install container"
    Write-Host "2. Uninstall container (preserves user data)"
    Write-Host "3. Backup Live container"
    Write-Host "4. Restore Live container"
    Write-Host "5. Update System"
    Write-Host "6. Update User Data"
    Write-Host "7. Restart with Community Packages Enabled"
    Write-Host "0. Exit menu"
}

################################################################################
# Main Menu Loop for n8n Container Management
################################################################################
do {
    Show-ContainerMenu
    $choice = Read-Host "Enter your choice (1-7, or 0 to exit)"
    switch ($choice) {
        "1" { Install-n8nContainer }
        "2" { Uninstall-n8nContainer }
        "3" { Backup-n8nContainer }
        "4" { Restore-n8nContainer }
        "5" { Update-n8nContainer }
        "6" { Update-n8nUserData }
        "7" { Restart-n8nContainer }
        "0" { Write-Host "Exiting menu." }
        default { Write-Host "Invalid selection. Please enter a number between 0 and 7." }
    }
    if ($choice -ne "0") {
         Write-Host "`nPress any key to continue..."
         $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
         Clear-Host
    }
} while ($choice -ne "0")
