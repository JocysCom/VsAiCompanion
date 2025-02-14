using namespace System
using namespace System.IO

# Dot-source the common functions file.
. "$PSScriptRoot\Setup_0.ps1"

# Ensure the script is running as Administrator and set the working directory.
Set-ScriptLocation

#############################################
# Setup Open WebUI Container Script with Docker/Podman Support
#############################################

$containerEngine = Select-ContainerEngine
if ($containerEngine -eq "docker") {
	Ensure-Elevated
    $enginePath = Get-DockerPath
} else {
    $enginePath = Get-PodmanPath
}

$imageName = "ghcr.io/open-webui/open-webui:main"
$containerName = "open-webui"

Write-Host "Pulling Open WebUI image '$imageName'..."
& $enginePath pull --platform linux/amd64 $imageName
if ($LASTEXITCODE -ne 0) {
    Write-Error "Pull failed. Check internet connection or image URL."
    exit 1
}

$existingContainer = & $enginePath ps -a --filter "name=$containerName" --format "{{.ID}}"
if ($existingContainer) {
    Write-Host "Removing existing container '$containerName'..."
    & $enginePath rm -f $containerName
}

Write-Host "Running container '$containerName'..."
& $enginePath run --platform linux/amd64 -d -p 3000:8080 -v open-webui:/app/backend/data --name $containerName $imageName
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to run container."
    exit 1
}

Write-Host "Waiting 20 seconds for container startup..."
Start-Sleep -Seconds 20

Test-HTTPPort -Uri "http://localhost:3000" -serviceName "OpenWebUI"
Test-TCPPort -ComputerName "localhost" -Port 3000 -serviceName "OpenWebUI"

Write-Host "Open WebUI is now running and accessible at http://localhost:3000"
Write-Host "Reminder: In Open WebUI settings, set the OpenAI API URL to 'http://host.docker.internal:9099' and API key to '0p3n-w3bu!' if integrating pipelines."