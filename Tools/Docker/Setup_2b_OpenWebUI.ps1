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

# Define the image and container names.
$imageName     = "ghcr.io/open-webui/open-webui:main"
$containerName = "open-webui"

#############################################
# Function: Install-OpenWebUIContainer
# Description: Pulls (or restores) the OpenWebUI image and runs the container using 
#              the appropriate port and volume mappings.
#############################################
function Install-OpenWebUIContainer {
    # Attempt to restore backup image first.
    if (-not (Check-AndRestoreBackup -Engine $enginePath -ImageName $imageName)) {
        Write-Host "No backup restored. Pulling Open WebUI image '$imageName'..."
        # Pull command:
        # pull       Pull an image or a repository from a registry.
        # --platform string Specify the platform in use.
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
    $existingContainer = & $enginePath ps -a --filter "name=$containerName" --format "{{.ID}}"
    if ($existingContainer) {
        Write-Host "Removing existing container '$containerName'..."
        # Remove container:
        # rm         Remove container(s).
        # --force    Force removal of a running container.
        & $enginePath rm --force $containerName
    }

    # Run the container.
    Write-Host "Running container '$containerName'..."
    # run         Run a command in a new container.
    # --platform string    Specify the platform for selecting the image.
    # --detach             Run the container in the background and print container ID.
    # --publish strings    Map a container's port to the host (3000:8080).
    # --volume string      Bind mount volume named 'open-webui' at /app/backend/data.
    # --name string        Assign a name to the container.
    & $enginePath run --platform linux/amd64 --detach --publish 3000:8080 --volume open-webui:/app/backend/data --name $containerName $imageName
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to run container."
        return
    }

    Write-Host "Waiting 20 seconds for container startup..."
    Start-Sleep -Seconds 20

    Test-HTTPPort -Uri "http://localhost:3000" -serviceName "OpenWebUI"
    Test-TCPPort -ComputerName "localhost" -Port 3000 -serviceName "OpenWebUI"

    Write-Host "Open WebUI is now running and accessible at http://localhost:3000"
    Write-Host "Reminder: In Open WebUI settings, set the OpenAI API URL to 'http://host.docker.internal:9099' and API key to '0p3n-w3bu!' if integrating pipelines."
}

#############################################
# Function: Update-OpenWebUIContainer
# Description: Updates the OpenWebUI container by stopping and removing any existing 
#              container, pulling the latest image, and starting a new container with 
#              the existing volume attached.
#############################################
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
    
    # Pull the latest image.
    Write-Host "Pulling latest image '$imageName'..."
    # pull       Pull an image from a registry.
    # --platform string    Specify the platform to pull the image for.
    & $enginePath pull --platform linux/amd64 $imageName
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to pull the latest image. Update aborted."
        return
    }
    
    # Run the updated container.
    Write-Host "Starting updated container '$containerName'..."
    # run         Run a command in a new container.
    # --platform string    Specify the platform for the image.
    # --detach             Run container in the background.
    # --publish strings    Map container port 8080 to host port 3000.
    # --volume string      Bind mount volume 'open-webui' at /app/backend/data.
    # --name string        Assign a name to the container.
    & $enginePath run --platform linux/amd64 --detach --publish 3000:8080 --volume open-webui:/app/backend/data --name $containerName $imageName
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to start the updated container."
        return
    }
    
    Write-Host "Waiting 20 seconds for the updated container to initialize..."
    Start-Sleep -Seconds 20

    Test-HTTPPort -Uri "http://localhost:3000" -serviceName "OpenWebUI"
    Test-TCPPort -ComputerName "localhost" -Port 3000 -serviceName "OpenWebUI"

    Write-Host "Open WebUI container has been successfully updated and is running at http://localhost:3000"
}

#############################################
# Function: Backup-OpenWebUIContainer
# Description: Backs up the live OpenWebUI container.
#############################################
function Backup-OpenWebUIContainer {
    Backup-ContainerState -Engine $enginePath -ContainerName $containerName
}

#############################################
# Function: Restore-OpenWebUIContainer
# Description: Restores the OpenWebUI container from a backup.
#############################################
function Restore-OpenWebUIContainer {
    Restore-ContainerState -Engine $enginePath -ContainerName $containerName
}

#############################################
# Function: Uninstall-OpenWebUIContainer
# Description: Uninstalls (removes) the OpenWebUI container.
#############################################
function Uninstall-OpenWebUIContainer {
    $existingContainer = & $enginePath ps -a --filter "name=$containerName" --format "{{.ID}}"
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

#############################################
# Function: Show-ContainerMenu
# Description: Displays the main menu for container options.
#############################################
function Show-ContainerMenu {
    Write-Host "==========================================="
    Write-Host "Container Menu"
    Write-Host "==========================================="
    Write-Host "1) Install Container"
    Write-Host "2) Backup live container"
    Write-Host "3) Restore container from backup"
    Write-Host "4) Uninstall Container"
    Write-Host "5) Update Container"
    Write-Host "6) Exit menu"
}

#############################################
# Main Menu Loop for OpenWebUI Container Management
#############################################
do {
    Show-ContainerMenu
    $choice = Read-Host "Enter your choice (1, 2, 3, 4, 5, or 6)"
    switch ($choice) {
        "1" { Install-OpenWebUIContainer }
        "2" { Backup-OpenWebUIContainer }
        "3" { Restore-OpenWebUIContainer }
        "4" { Uninstall-OpenWebUIContainer }
        "5" { Update-OpenWebUIContainer }
        "6" { Write-Host "Exiting menu." }
        default { Write-Host "Invalid selection. Please enter 1, 2, 3, 4, 5, or 6." }
    }
    if ($choice -ne "6") {
         Write-Host "`nPress any key to continue..."
         $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
         Clear-Host
    }
} while ($choice -ne "6")