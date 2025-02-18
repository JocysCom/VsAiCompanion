################################################################################
# File         : Setup_5_Qdrant.ps1
# Description  : Script to set up and run the Qdrant container with Docker/Podman support.
#                Validates backup, pulls the Qdrant image if necessary, removes existing containers,
#                and runs Qdrant with proper port mapping.
# Usage        : Run as Administrator if necessary.
################################################################################

using namespace System
using namespace System.IO

# Dot-source the common functions file.
. "$PSScriptRoot\Setup_0.ps1"

# Ensure the script is running as Administrator and set the working directory.
Set-ScriptLocation

#############################################
# Setup Qdrant Container with Container Engine Selection
#############################################

$imageName = "qdrant/qdrant"
$containerName = "qdrant"

# Prompt user to choose container engine (Docker or Podman)
$containerEngine = Select-ContainerEngine
if ($containerEngine -eq "docker") {
    Ensure-Elevated
    $enginePath = Get-DockerPath
    Write-Host "Using Docker with executable: $enginePath"
    # For Docker, set DOCKER_HOST pointing to the Docker service pipe.
    $env:DOCKER_HOST = "npipe:////./pipe/docker_engine"
}
else {
    $enginePath = Get-PodmanPath
    Write-Host "Using Podman with executable: $enginePath"
    # If additional Podman-specific environment settings are needed, add them here.
}

if (-not (Check-AndRestoreBackup -Engine $enginePath -ImageName $imageName)) {
    Write-Host "No backup restored. Pulling Qdrant image '$imageName' using $containerEngine..."
    # Command: pull
    #   pull: downloads the specified image from the registry.
    & $enginePath pull $imageName
    if ($LASTEXITCODE -ne 0) {
         Write-Error "$containerEngine pull failed for Qdrant. Please check your internet connection or the image name."
         exit 1
    }
} else {
    Write-Host "Using restored backup image '$imageName'."
}

# Check if a container with the same name already exists.
# Command: ps
#   --all: lists all containers.
#   --filter "name=$containerName": filters for the container by name.
#   --format "{{.ID}}": outputs only the container ID.
$existingContainer = & $enginePath ps --all --filter "name=$containerName" --format "{{.ID}}"
if ($existingContainer) {
    Write-Host "Removing existing container '$containerName'..."
    # Command: rm
    #   --force: forces removal of the container.
    & $enginePath rm --force $containerName
}

# Run the Qdrant container with port mapping.
Write-Host "Starting Qdrant container..."
# Command: run
#   --detach: run container in background.
#   --name: assign the container a name.
#   --publish: map host port 6333 to container port 6333.
& $enginePath run --detach --name $containerName --publish 6333:6333 $imageName
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