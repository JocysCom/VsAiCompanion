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
# This script installs and runs a self-hosted instance of Firecrawl with a dedicated Redis container.
# It performs the following steps:
# 1. Creates (if necessary) a dedicated Docker network.
# 2. Runs a Redis container (using the "redis:alpine" image) on that network with a network alias.
# 3. Checks connectivity to Redis before installing Firecrawl.
# 4. Pulls the Firecrawl Docker image.
# 5. Runs the Firecrawl container with environment variables to override its Redis connection settings.
#
# The additional environment variables REDIS_HOST, REDIS_PORT, and REDIS_RATE_LIMIT_URL
# are set to ensure Firecrawl connects to the Redis container rather than attempting localhost.
#
# Firecrawl will be accessible at: http://localhost:3002
#
# NOTE:
#   If you intend to use actual OpenAI functionality, update OPENAI_API_KEY accordingly.

# Step 0: Define image names, container names, and network name.
$imageName          = "obeoneorg/firecrawl"
$redisImage         = "redis:alpine"
$firecrawlName      = "firecrawl"
$redisContainerName = "firecrawl-redis"
$networkName        = "firecrawl-net"

$dockerPath = Get-DockerPath

#############################################
# Step 1: Create Docker Network (if not present)
#############################################
$existingNetwork = & $dockerPath network ls --filter "name=^$networkName$" --format "{{.Name}}"
if ($existingNetwork -ne $networkName) {
    Write-Host "Creating Docker network '$networkName'..."
    & $dockerPath network create $networkName
} else {
    Write-Host "Docker network '$networkName' already exists."
}

#############################################
# Step 2: Run the Redis Container with a Network Alias
#############################################
$existingRedis = & $dockerPath ps -a --filter "name=^$redisContainerName$" --format "{{.ID}}"
if ($existingRedis) {
    Write-Host "Removing existing Redis container '$redisContainerName'..."
    & $dockerPath rm -f $redisContainerName
}
Write-Host "Starting Redis container '$redisContainerName' on network '$networkName' with alias 'redis'..."
& $dockerPath run -d --name $redisContainerName --network $networkName --network-alias redis -p 6379:6379 $redisImage
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to start Redis container."
    exit 1
}

# Wait for Redis to initialize before testing connectivity.
Write-Host "Waiting 10 seconds for Redis container to initialize..."
Start-Sleep -Seconds 10

# Check Redis connectivity before proceeding to install Firecrawl.
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
$existingFirecrawl = & $dockerPath ps -a --filter "name=^$firecrawlName$" --format "{{.ID}}"
if ($existingFirecrawl) {
    Write-Host "Removing existing Firecrawl container '$firecrawlName'..."
    & $dockerPath rm -f $firecrawlName
}

#############################################
# Step 5: Run the Firecrawl Container with Overridden Redis Settings
#############################################
# Provide a dummy OPENAI_API_KEY and override both the REDIS_URL and related settings.
# Notice that we now use the alias 'redis' so that the container can resolve the dedicated Redis container.
Write-Host "Starting Firecrawl container '$firecrawlName'..."
& $dockerPath run -d -p 3002:3002 --restart always --network $networkName --name $firecrawlName ` 
    -e OPENAI_API_KEY=dummy ` 
    -e REDIS_URL=redis://redis:6379 ` 
    -e REDIS_RATE_LIMIT_URL=redis://redis:6379 ` 
    -e REDIS_HOST=redis ` 
    -e REDIS_PORT=6379 ` 
    -e POSTHOG_API_KEY="" ` 
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

# Additionally, test Redis connectivity bound on port 6379.
Write-Host "Testing Redis container connectivity on port 6379..."
Test-TCPPort -ComputerName "localhost" -Port 6379 -serviceName "Firecrawl Redis"

Write-Host "Firecrawl is now running and accessible at http://localhost:3002"