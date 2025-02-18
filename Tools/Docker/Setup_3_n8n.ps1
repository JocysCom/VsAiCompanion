################################################################################
# File         : Setup_3_n8n.ps1
# Description  : Script to set up and run the n8n container using Docker/Podman.
#                Verifies volume presence, pulls the n8n image if necessary,
#                and runs the container with port and volume mappings.
# Usage        : Run as Administrator if using Docker.
################################################################################

using namespace System
using namespace System.IO

# Dot-source the common functions file.
. "$PSScriptRoot\Setup_0.ps1"

Set-ScriptLocation

#############################################
# Setup n8n Container Script with Docker/Podman Support
#############################################

$containerEngine = Select-ContainerEngine

if ($containerEngine -eq "docker") {
    Ensure-Elevated
    $enginePath = Get-DockerPath
    $pullOptions = @()  # No extra options needed for Docker.
    $imageName = "docker.n8n.io/n8nio/n8n:latest"
} else {
    $enginePath = Get-PodmanPath
    $pullOptions = @("--tls-verify=false")
    # Use the Docker Hub version of n8n for Podman to avoid 403 errors.
    $imageName = "n8nio/n8n:latest"
}

# Check if volume 'n8n_data' already exists; if not, create it.
# Command: volume ls
#   --filter "name=n8n_data": filters volumes whose name matches “n8n_data”.
#   --format "{{.Name}}": outputs only the volume names.
$existingVolume = & $enginePath volume ls --filter "name=n8n_data" --format "{{.Name}}"
if ([string]::IsNullOrWhiteSpace($existingVolume)) {
    Write-Host "Creating volume 'n8n_data'..."
    # Command: volume create
    #   n8n_data: the name of the volume to create.
    & $enginePath volume create n8n_data
} else {
    Write-Host "Volume 'n8n_data' already exists. Skipping creation."
}

# Check if the n8n image is already available.
# Command: images
#   --format "{{.Repository}}:{{.Tag}}": prints the repository and tag of each image.
$existingImage = & $enginePath images --format "{{.Repository}}:{{.Tag}}" | Where-Object { $_ -match "n8n" }
if (-not $existingImage) {
    if (-not (Check-AndRestoreBackup -Engine $enginePath -ImageName $imageName)) {
         Write-Host "No backup restored. Pulling n8n image '$imageName'..."
         # Command: pull
         #   pull: downloads the specified image from the registry.
         #   Additional options (if any) are appended from $pullOptions.
         $pullCmd = @("pull") + $pullOptions + $imageName
         & $enginePath @pullCmd
         if ($LASTEXITCODE -ne 0) {
              Write-Error "Image pull failed. Exiting..."
              exit 1
         }
    } else {
         Write-Host "Using restored backup image '$imageName'."
    }
} else {
    Write-Host "n8n image already exists. Skipping pull."
}

# Check if a container with the name "n8n" already exists.
# Command: ps
#   --all: lists all containers (running and stopped).
#   --filter "name=n8n": filters for containers with the name “n8n”.
#   --format "{{.ID}}": outputs only the container ID.
$existingContainer = & $enginePath ps --all --filter "name=n8n" --format "{{.ID}}"
if ($existingContainer) {
    Write-Host "Removing existing container 'n8n'..."
    # Command: rm
    #   --force: forces removal of the container.
    & $enginePath rm --force n8n
}

# Define run options for starting the container using longer argument variants.
$runOptions = @(
    "--detach",                            # --detach: run container in background and print container ID.
    "--publish", "5678:5678",              # --publish: map host port 5678 to container port 5678.
    "--volume", "n8n_data:/home/node/.n8n",  # --volume: bind mount volume "n8n_data" to "/home/node/.n8n" in the container.
    "--name", "n8n"                        # --name: assign the container the name "n8n".
)

Write-Host "Starting n8n container..."
# Command: run
#   run [OPTIONS] IMAGE
#   The options provided include --detach, --publish, --volume, and --name.
& $enginePath run $runOptions $imageName
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to run n8n container. Exiting..."
    exit 1
}

Write-Host "Waiting 20 seconds for container startup..."
Start-Sleep -Seconds 20

# Test connectivity: check TCP and HTTP availability on port 5678.
Test-TCPPort -ComputerName "localhost" -Port 5678 -serviceName "n8n"
Test-HTTPPort -Uri "http://localhost:5678" -serviceName "n8n"

Write-Host "n8n is now running and accessible at http://localhost:5678"