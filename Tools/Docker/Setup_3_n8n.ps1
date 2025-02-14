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
    Write-Host "Pulling n8n image '$imageName'..."
    $pullCmd = @("pull") + $pullOptions + $imageName
    & $enginePath @pullCmd
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Image pull failed. Exiting..."
        exit 1
    }
} else {
    Write-Host "n8n image already exists. Skipping pull."
}

$existingContainer = & $enginePath ps -a --filter "name=n8n" --format "{{.ID}}"
if ($existingContainer) {
    Write-Host "Removing existing container 'n8n'..."
    & $enginePath rm -f n8n
}

$runOptions = @(
    "-d",                                  # Run in detached mode.
    "-p", "5678:5678",                     # Map host port 5678 to container port 5678.
    "-v", "n8n_data:/home/node/.n8n",       # Mount volume to persist data.
    "--name", "n8n"                        # Container name.
)

Write-Host "Starting n8n container..."
& $enginePath run $runOptions $imageName
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to run n8n container. Exiting..."
    exit 1
}

Write-Host "Waiting 20 seconds for container startup..."
Start-Sleep -Seconds 20

Test-TCPPort -ComputerName "localhost" -Port 5678 -serviceName "n8n"
Test-HTTPPort -Uri "http://localhost:5678" -serviceName "n8n"

Write-Host "n8n is now running and accessible at http://localhost:5678"