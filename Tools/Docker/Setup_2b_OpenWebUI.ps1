################################################################################
# File         : Setup_2b_OpenWebUI.ps1
# Description  : Script to set up, back up, restore, uninstall, and update the 
#                Open WebUI container using Docker/Podman support. The script 
#                provides a container menu (with install, backup, restore, uninstall,
#                update, and exit options).
# Usage        : Run as Administrator if using Docker.
################################################################################

using namespace System
using namespace System.IO

# Dot-source common helper functions.
. "$PSScriptRoot\Setup_0.ps1"

# Ensure the script working directory is set.
Set-ScriptLocation

#############################################
# Pick Container Engine and Set Global Variables
#############################################
$containerEngine = Select-ContainerEngine
if ($containerEngine -eq "docker") {
    Ensure-Elevated
    $enginePath = Get-DockerPath
}
else {
    $enginePath = Get-PodmanPath
}
$imageName     = "ghcr.io/open-webui/open-webui:main"
$containerName = "open-webui"

<#
.SYNOPSIS
    Runs the container using the provided container engine and parameters.
.DESCRIPTION
    This function encapsulates the duplicated code to run, wait, and test the 
    container startup.
.PARAMETER action
    A message prefix indicating the action (e.g. "Running container" or "Starting updated container").
.PARAMETER successMessage
    The message to print on successful startup.
#>
function Run-Container {
    param (
        [string]$action,
        [string]$successMessage
    )
    Write-Host "$action '$containerName'..."
    # Run the container.
    # run         Run a command in a new container.
    # --platform string    Specify the platform for selecting the image.
    # --detach             Run the container in the background and print its container ID.
    # --publish strings    Map a container's port to the host (3000:8080).
    # --volume string      Bind mount volume named 'open-webui' at /app/backend/data.
    # --name string        Assign a name to the container.
	# --add-host host.docker.internal:host-gateway  enables communication between the container and the host machine.
	# --restart always     The container will automatically restart under any circumstance.
    & $enginePath run --platform linux/amd64 --detach --publish 3000:8080 --volume open-webui:/app/backend/data --name $containerName $imageName
    & $enginePath run --platform linux/amd64 --detach --publish 3000:8080 --add-host host.docker.internal:host-gateway --restart always --volume open-webui:/app/backend/data --name $containerName $imageName
	
	
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to run container."
        return $false
    }
    Write-Host "Waiting 20 seconds for container startup..."
    Start-Sleep -Seconds 20
    Test-HTTPPort -Uri "http://localhost:3000" -serviceName "OpenWebUI"
    Test-TCPPort -ComputerName "localhost" -Port 3000 -serviceName "OpenWebUI"
	Test-WebSocketPort -Uri "ws://localhost:3000/api/v1/chat/completions" -serviceName "OpenWebUI WebSockets"
	New-NetFirewallRule -DisplayName "Allow WebSockets" -Direction Inbound -LocalPort 3000 -Protocol TCP -Action Allow
    Write-Host $successMessage
    return $true
}

<#
.SYNOPSIS
    Installs the Open WebUI container.
.DESCRIPTION
    Attempts to restore a backup image; if not available, pulls the latest image,
    removes any existing container, and then runs the container. A reminder regarding
    Open WebUI settings is printed after the container is running.
#>
function Install-OpenWebUIContainer {
    # Attempt to restore backup image; if not, pull latest image.
    if (-not (Check-AndRestoreBackup -Engine $enginePath -ImageName $imageName)) {
        Write-Host "No backup restored. Pulling Open WebUI image '$imageName'..."
        # Pull command:
        # pull       Pull an image from a registry.
        # --platform string Specify the platform to pull the image for.
        & $enginePath pull --platform linux/amd64 $imageName
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Pull failed. Check internet connection or image URL."
            return
        }
    }
    else {
        Write-Host "Using restored backup image '$imageName'."
    }
    # Remove any existing container.
    $existingContainer = & $enginePath ps -a --filter "name=^$containerName$" --format "{{.ID}}"
    if ($existingContainer) {
        Write-Host "Removing existing container '$containerName'..."
        # Remove container:
        # rm         Remove one or more containers.
        # --force    Force removal of a running container.
        & $enginePath rm --force $containerName
    }
    Run-Container -action "Running container" -successMessage "Open WebUI is now running and accessible at http://localhost:3000`nReminder: In Open WebUI settings, set the OpenAI API URL to 'http://host.docker.internal:9099' and API key to '0p3n-w3bu!' if integrating pipelines."
}

<#
.SYNOPSIS
    Uninstalls the Open WebUI container.
.DESCRIPTION
    Checks for the existence of the container and removes it using the engine's rm command.
#>
function Uninstall-OpenWebUIContainer {
    $existingContainer = & $enginePath ps -a --filter "name=^$containerName$" --format "{{.ID}}"
    if ($existingContainer) {
        Write-Host "Removing container '$containerName'..."
        & $enginePath rm --force $containerName
        if ($LASTEXITCODE -eq 0) {
            Write-Host "Container removed successfully."
        }
        else {
            Write-Error "Failed to remove container."
        }
    }
    else {
        Write-Host "No container found to remove."
    }
}

<#
.SYNOPSIS
    Backs up the live Open WebUI container.
.DESCRIPTION
    Uses the Backup-ContainerState helper function to back up the container.
#>
function Backup-OpenWebUIContainer {
    Backup-ContainerState -Engine $enginePath -ContainerName $containerName
}

<#
.SYNOPSIS
    Restores the Open WebUI container from backup.
.DESCRIPTION
    Uses the Restore-ContainerState helper function to restore the container.
#>
function Restore-OpenWebUIContainer {
    Restore-ContainerState -Engine $enginePath -ContainerName $containerName
}

#############################################
# Optional Functions
#############################################

<#
.SYNOPSIS
    Updates the Open WebUI container.
.DESCRIPTION
    Stops and removes any current container instance, pulls the latest image,
    and then starts the container using the Run-Container helper.
#>
function Update-OpenWebUIContainer {
    Write-Host "Initiating update for OpenWebUI container..."

    # Check and remove any current running instance of the container.
    $existingContainer = & $enginePath ps -a --filter "name=$containerName" --format "{{.ID}}"
    if ($existingContainer) {
        Write-Host "Removing existing container '$containerName' as part of the update..."
        # Remove container command:
        # rm         Remove one or more containers.
        # --force    Force removal of a running container.
        & $enginePath rm --force $containerName
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Failed to remove container '$containerName'. Update aborted."
            return
        }
    }
    
    Write-Host "Pulling latest image '$imageName'..."
    # pull       Pull an image from a registry.
    # --platform string    Specify the platform to pull the image for.
    & $enginePath pull --platform linux/amd64 $imageName
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to pull the latest image. Update aborted."
        return
    }
    
    Run-Container -action "Starting updated container" -successMessage "OpenWebUI container has been successfully updated and is running at http://localhost:3000"
}

<#
.SYNOPSIS
    Updates the user data for the Open WebUI container.
.DESCRIPTION
    This functionality is not implemented.
#>
function Update-OpenWebUIUserData {
    Write-Host "Update User Data functionality is not implemented for OpenWebUI container."
}

#############################################
# Main Menu Loop for OpenWebUI Container Management
#############################################
do {
    Write-Host "==========================================="
    Write-Host "Container Menu"
    Write-Host "==========================================="
    Write-Host "1. Install container"
    Write-Host "2. Uninstall container"
    Write-Host "3. Backup Live container"
    Write-Host "4. Restore Live container"
    Write-Host "5. Update System"
    Write-Host "6. Update User Data"
    $choice = Read-Host "Enter your choice (1, 2, 3, 4, 5, or 6)"
    switch ($choice) {
        "1" { Install-OpenWebUIContainer }
        "2" { Uninstall-OpenWebUIContainer }
        "3" { Backup-OpenWebUIContainer }
        "4" { Restore-OpenWebUIContainer }
        "5" { Update-OpenWebUIContainer }
        "6" { Update-OpenWebUIUserData }
        default { Write-Host "Invalid selection. Please enter 1, 2, 3, 4, 5, or 6." }
    }
    if ($choice -ne "6") {
         Write-Host "`nPress any key to continue..."
         $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
         Clear-Host
    }
} while ($choice -ne "6")