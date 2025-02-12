using namespace System
using namespace System.IO

# Dot-source the common functions file.
. "$PSScriptRoot\Setup_0.ps1"

# Ensure the script is running as Administrator and set the working directory.
Ensure-Elevated
Set-ScriptLocation

#############################################
# Setup Open WebUI Container Script
#############################################
# This script performs the following:
# • Assumes Docker is already installed and running.
# • Pulls the Open WebUI Docker image.
# • Removes any existing container named "open-webui".
# • Runs the Open WebUI container.
# • Tests website accessibility using HTTP and TCP tests.
#
# Note: The Open WebUI Docker image is pulled from the official repository.
#############################################

$imageName = "ghcr.io/open-webui/open-webui:main"
$containerName = "open-webui"

# Retrieve Docker executable path using Setup_0.ps1's Get-DockerPath function.
$dockerPath = Get-DockerPath

Write-Host "Pulling Open WebUI Docker image '$imageName'..."
& $dockerPath pull --platform linux/amd64 $imageName
if ($LASTEXITCODE -ne 0) {
    Write-Error "Docker pull failed. Please check your internet connection or image URL."
    exit 1
}

$existingContainer = & $dockerPath ps -a --filter "name=$containerName" --format "{{.ID}}"
if ($existingContainer) {
    Write-Host "Removing existing container '$containerName'..."
    & $dockerPath rm -f $containerName
}

Write-Host "Running Docker container '$containerName'..."
& $dockerPath run --platform linux/amd64 -d -p 3000:8080 -v open-webui:/app/backend/data --name $containerName $imageName
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to run the Docker container."
    exit 1
}

Write-Host "Waiting 20 seconds for the container to fully start..."
Start-Sleep -Seconds 20

#############################################
# Test Open WebUI Service Connectivity
#############################################

# Test HTTP accessibility of the Open WebUI website.
Test-HTTPPort -Uri "http://localhost:3000" -serviceName "OpenWebUI"

# Test TCP connectivity to port 3000.
Test-TCPPort -ComputerName "localhost" -Port 3000 -serviceName "OpenWebUI"

Write-Host "If both tests succeeded, Open WebUI is now running and accessible at http://localhost:3000"
Write-Host "Reminder: In Open WebUI settings, set the OpenAI API URL to 'http://host.docker.internal:9099' and API key to '0p3n-w3bu!' if you plan to integrate pipelines."