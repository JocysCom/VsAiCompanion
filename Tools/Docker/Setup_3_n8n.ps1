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

# Dot-source the common functions file.
. "$PSScriptRoot\Setup_0.ps1"

# Ensure the script working directory is set.
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
$existingVolume = & $enginePath volume ls --filter "name=n8n_data" --format "{{.Name}}"
if ([string]::IsNullOrWhiteSpace($existingVolume)) {
    Write-Host "Creating volume 'n8n_data'..."
    & $enginePath volume create n8n_data
} else {
    Write-Host "Volume 'n8n_data' already exists. Skipping creation."
}

# Check if the n8n image is already available.
$existingImage = & $enginePath images --format "{{.Repository}}:{{.Tag}}" | Where-Object { $_ -match "n8n" }
if (-not $existingImage) {
    if (-not (Check-AndRestoreBackup -Engine $enginePath -ImageName $imageName)) {
         Write-Host "No backup restored. Pulling n8n image '$imageName'..."
         # Build pull command array with options.
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

# Check if a container with the name "n8n" already exists and remove it.
$existingContainer = & $enginePath ps --all --filter "name=n8n" --format "{{.ID}}"
if ($existingContainer) {
    Write-Host "Removing existing container 'n8n'..."
    & $enginePath rm --force n8n
}

# Prompt user for external domain configuration.
$externalDomain = Read-Host "Enter external domain for n8n container (e.g., n8n.example.com) or press Enter to skip"

# Define run options for starting the container using longer argument variants.
$runOptions = @(
    "--detach",                            # --detach: Run container in background and print container ID.
    "--publish", "5678:5678",              # --publish: Map host port 5678 to container port 5678.
    "--volume", "n8n_data:/home/node/.n8n",  # --volume: Bind mount volume "n8n_data" to /home/node/.n8n in the container.
    "--name", "n8n"                        # --name: Assign the container the name "n8n".
)

# If user provided an external domain, add environment variables to specify external host settings.
if (-not [string]::IsNullOrWhiteSpace($externalDomain)) {
    $envOptions = @(
        "--env", "N8N_HOST=$externalDomain",          # --env: Set N8N_HOST to the provided domain.
        "--env", "WEBHOOK_URL=https://$externalDomain"  # --env: Set WEBHOOK_URL to use HTTPS with the provided domain.
    )
    # Append external domain environment options to run options.
    $runOptions += $envOptions
}

Write-Host "Starting n8n container..."
# podman run [options] IMAGE [COMMAND [ARG...]]
# run         Execute a command in a new container.
# --detach             Run container in the background and print container ID.
# --env string         Set an environment variable in the container.
# --publish strings    Map a container's port or range of ports to the host.
# --volume stringArray Bind mount a volume into the container.
# --name string        Assign a name to the container.
& $enginePath run $runOptions $imageName
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to run n8n container. Exiting..."
    exit 1
}

Write-Host "Waiting 20 seconds for container startup..."
Start-Sleep -Seconds 20

# Test connectivity: Check TCP and HTTP availability on port 5678.
Test-TCPPort -ComputerName "localhost" -Port 5678 -serviceName "n8n"
Test-HTTPPort -Uri "http://localhost:5678" -serviceName "n8n"

Write-Host "n8n is now running and accessible at http://localhost:5678"