################################################################################
# File         : Setup_4_Firecrawl.ps1
# Description  : Script to set up and run a Firecrawl container with a dedicated Redis container.
#                Creates a Docker network, launches Redis, pulls Firecrawl image, and runs Firecrawl
#                with environment variable overrides directing it to use the dedicated Redis.
# Usage        : Run as Administrator.
################################################################################

using namespace System
using namespace System.IO

# Dot-source the common functions file.
. "$PSScriptRoot\Setup_0.ps1"

# Ensure the script is running as Administrator and set the working directory.
Ensure-Elevated
Set-ScriptLocation

#############################################
# Setup Firecrawl with Dedicated Redis Support
#############################################
# Define the image names, container names, and network name.
$imageName          = "obeoneorg/firecrawl"
$redisImage         = "redis:alpine"
$firecrawlName      = "firecrawl"
$redisContainerName = "firecrawl-redis"
$networkName        = "firecrawl-net"

$dockerPath = Get-DockerPath

#############################################
# Step 1: Create Docker Network (if not present)
#############################################
# Command: network ls
#   --filter "name=^$networkName$": filters networks with an exact match of the network name.
#   --format "{{.Name}}": outputs only the network names.
$existingNetwork = & $dockerPath network ls --filter "name=^$networkName$" --format "{{.Name}}"
if ($existingNetwork -ne $networkName) {
    Write-Host "Creating Docker network '$networkName'..."
    # Command: network create
    #   network create NETWORK: creates a new Docker network with the given name.
    & $dockerPath network create $networkName
} else {
    Write-Host "Docker network '$networkName' already exists."
}

#############################################
# Step 2: Run the Redis Container with a Network Alias
#############################################
# Command: ps
#   --all: lists all containers.
#   --filter "name=^$redisContainerName$": filters containers matching the exact Redis container name.
#   --format "{{.ID}}": outputs the container ID.
$existingRedis = & $dockerPath ps --all --filter "name=^$redisContainerName$" --format "{{.ID}}"
if ($existingRedis) {
    Write-Host "Removing existing Redis container '$redisContainerName'..."
    # Command: rm
    #   --force: forces the removal of the container.
    & $dockerPath rm --force $redisContainerName
}
Write-Host "Starting Redis container '$redisContainerName' on network '$networkName' with alias 'redis'..."
# Command: run
#   --detach: run container in background.
#   --name: assign a name to the container.
#   --network: connect container to the specified network.
#   --network-alias: assign an alias (here, "redis") for use within the network.
#   --publish: map host port 6379 to container port 6379.
& $dockerPath run --detach --name $redisContainerName --network $networkName --network-alias redis --publish 6379:6379 $redisImage
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to start Redis container."
    exit 1
}

# Wait for Redis to initialize before testing connectivity.
Write-Host "Waiting 10 seconds for Redis container to initialize..."
Start-Sleep -Seconds 10

# Check Redis connectivity before proceeding.
Write-Host "Testing Redis container connectivity on port 6379 before installing Firecrawl..."
if (-not (Test-TCPPort -ComputerName "localhost" -Port 6379 -serviceName "Firecrawl Redis")) {
    Write-Error "Redis connectivity test failed. Aborting Firecrawl installation."
    exit 1
}

#############################################
# Step 3: Pull the Firecrawl Docker Image
#############################################
if (-not (Check-AndRestoreBackup -Engine $dockerPath -ImageName $imageName)) {
    Write-Host "No backup restored. Pulling Firecrawl Docker image '$imageName'..."
    # Command: pull
    #   pull: downloads the specified image from the Docker registry.
    & $dockerPath pull $imageName
    if ($LASTEXITCODE -ne 0) {
         Write-Error "Docker pull failed for image '$imageName'."
         exit 1
    }
} else {
    Write-Host "Using restored backup image '$imageName'."
}

#############################################
# Step 4: Remove Existing Firecrawl Container (if any)
#############################################
# Command: ps
#   --all: lists all containers.
#   --filter "name=^$firecrawlName$": filters for the Firecrawl container.
#   --format "{{.ID}}": outputs the container ID.
$existingFirecrawl = & $dockerPath ps --all --filter "name=^$firecrawlName$" --format "{{.ID}}"
if ($existingFirecrawl) {
    Write-Host "Removing existing Firecrawl container '$firecrawlName'..."
    # Command: rm
    #   --force: forces removal of the container.
    & $dockerPath rm --force $firecrawlName
}

#############################################
# Step 5: Run the Firecrawl Container with Overridden Redis Settings
#############################################
Write-Host "Starting Firecrawl container '$firecrawlName'..."
# Command: run
#   --detach: run container in background.
#   --publish: map container port 3002 to host port 3002.
#   --restart always: always restart the container unless explicitly stopped.
#   --network: attach the container to the specified Docker network.
#   --name: assign the container the name “firecrawl”.
#   --env: set environment variables within the container.
& $dockerPath run --detach --publish 3002:3002 --restart always --network $networkName --name $firecrawlName `
    --env OPENAI_API_KEY=dummy `
    --env REDIS_URL=redis://redis:6379 `
    --env REDIS_RATE_LIMIT_URL=redis://redis:6379 `
    --env REDIS_HOST=redis `
    --env REDIS_PORT=6379 `
    --env POSTHOG_API_KEY="" `
    $imageName
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to run Firecrawl container '$firecrawlName'."
    exit 1
}

#############################################
# Step 6: Wait and Test Connectivity
#############################################
Write-Host "Waiting 20 seconds for containers to fully start..."
Start-Sleep -Seconds 20

# Test Firecrawl API connectivity on port 3002.
Write-Host "Testing Firecrawl API connectivity on port 3002..."
Test-TCPPort -ComputerName "localhost" -Port 3002 -serviceName "Firecrawl API"
Test-HTTPPort -Uri "http://localhost:3002" -serviceName "Firecrawl API"

# Additionally, test Redis connectivity on port 6379.
Write-Host "Testing Redis container connectivity on port 6379..."
Test-TCPPort -ComputerName "localhost" -Port 6379 -serviceName "Firecrawl Redis"

Write-Host "Firecrawl is now running and accessible at http://localhost:3002"