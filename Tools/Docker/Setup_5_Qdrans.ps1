# Setup_5_Qdrant.ps1 - Qdrant Container Deployment Script
# This script installs and runs Qdrant, a vector database for storing and searching embeddings.
# Qdrant enhances your AI system’s capabilities by enabling advanced semantic search and recommendations.
# It performs the following:
#   1. Pulls the Qdrant Docker image from Docker Hub.
#   2. Removes any existing container named "qdrant".
#   3. Runs a new container mapping port 6333 (used for Qdrant's REST API).
#   4. Tests connectivity to ensure that Qdrant is accessible.

using namespace System
using namespace System.IO

# Dot-source the common functions file.
. "$PSScriptRoot\Setup_0.ps1"

# Ensure the script is running as Administrator and set the working directory.
Ensure-Elevated
Set-ScriptLocation

#############################################
# Setup Qdrant Container
#############################################

$imageName = "qdrant/qdrant"
$containerName = "qdrant"

# Retrieve Docker executable path using Setup_0.ps1's Get-DockerPath function.
$dockerPath = Get-DockerPath

# Pull the Qdrant Docker image.
Write-Host "Pulling Qdrant Docker image '$imageName'..."
& $dockerPath pull $imageName
if ($LASTEXITCODE -ne 0) {
    Write-Error "Docker pull failed for Qdrant. Please check your internet connection or the image name."
    exit 1
}

# Remove any existing container named 'qdrant'
$existingContainer = & $dockerPath ps -a --filter "name=$containerName" --format "{{.ID}}"
if ($existingContainer) {
    Write-Host "Removing existing container '$containerName'..."
    & $dockerPath rm -f $containerName
}

# Run the Qdrant container with port mapping.
Write-Host "Starting Qdrant container..."
& $dockerPath run -d --name $containerName -p 6333:6333 $imageName
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