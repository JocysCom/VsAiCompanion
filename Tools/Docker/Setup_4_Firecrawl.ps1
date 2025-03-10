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
# Global Variables
#############################################
$global:imageName          = "obeoneorg/firecrawl"
$global:redisImage         = "redis:alpine"
$global:firecrawlName      = "firecrawl"
$global:redisContainerName = "firecrawl-redis"
$global:networkName        = "firecrawl-net"
$global:dockerPath         = Get-DockerPath

<#
.SYNOPSIS
    Installs the Firecrawl container with dedicated Redis.
.DESCRIPTION
    Performs the following steps:
    1. Creates a Docker network if it does not exist.
    2. Removes any existing Redis container for Firecrawl, then starts a new Redis container 
       on the specified network with alias 'redis'.
    3. Waits for Redis to initialize and tests connectivity.
    4. Pulls the Firecrawl Docker image (or restores from backup).
    5. Removes any existing Firecrawl container.
    6. Runs the Firecrawl container with environment variable overrides to use the dedicated Redis.
    7. Waits and tests connectivity for the Firecrawl API and Redis.
    All original command arguments and workarounds are preserved.
#>
function Install-FirecrawlContainer {
    #############################################
    # Step 1: Create Docker Network (if not present)
    #############################################
    # Command: network ls
    #   --filter "name=^$networkName$": filters networks with an exact match of the network name.
    #   --format "{{.Name}}": outputs only the network names.
    $existingNetwork = & $global:dockerPath network ls --filter "name=^$global:networkName$" --format "{{.Name}}"
    if ($existingNetwork -ne $global:networkName) {
        Write-Host "Creating Docker network '$global:networkName'..."
        # Command: network create
        #   network create NETWORK: creates a new Docker network with the given name.
        & $global:dockerPath network create $global:networkName
    }
    else {
        Write-Host "Docker network '$global:networkName' already exists."
    }
    
    #############################################
    # Step 2: Run the Redis Container with a Network Alias
    #############################################
    # Command: ps
    #   --all: lists all containers.
    #   --filter "name=^$redisContainerName$": filters containers matching the exact Redis container name.
    #   --format "{{.ID}}": outputs the container ID.
    $existingRedis = & $global:dockerPath ps --all --filter "name=^$global:redisContainerName$" --format "{{.ID}}"
    if ($existingRedis) {
        Write-Host "Removing existing Redis container '$global:redisContainerName'..."
        # Command: rm
        #   --force: forces the removal of the container.
        & $global:dockerPath rm --force $global:redisContainerName
    }
    Write-Host "Starting Redis container '$global:redisContainerName' on network '$global:networkName' with alias 'redis'..."
    # Command: run
    #   --detach: run container in background.
    #   --name: assign a name to the container.
    #   --network: connect container to the specified network.
    #   --network-alias: assign an alias (here, 'redis') for use within the network.
    #   --publish: map host port 6379 to container port 6379.
    & $global:dockerPath run --detach --name $global:redisContainerName --network $global:networkName --network-alias redis --publish 6379:6379 $global:redisImage
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
    if (-not (Check-AndRestoreBackup -Engine $global:dockerPath -ImageName $global:imageName)) {
        Write-Host "No backup restored. Pulling Firecrawl Docker image '$global:imageName'..."
        # Command: pull
        #   pull: downloads the specified image from the Docker registry.
        & $global:dockerPath pull $global:imageName
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Docker pull failed for image '$global:imageName'."
            exit 1
        }
    }
    else {
        Write-Host "Using restored backup image '$global:imageName'."
    }
    
    #############################################
    # Step 4: Remove Existing Firecrawl Container (if any)
    #############################################
    # Command: ps
    #   --all: lists all containers.
    #   --filter "name=^$firecrawlName$": filters for the Firecrawl container.
    #   --format "{{.ID}}": outputs the container ID.
    $existingFirecrawl = & $global:dockerPath ps --all --filter "name=^$global:firecrawlName$" --format "{{.ID}}"
    if ($existingFirecrawl) {
        Write-Host "Removing existing Firecrawl container '$global:firecrawlName'..."
        # Command: rm
        #   --force: forces removal of the container.
        & $global:dockerPath rm --force $global:firecrawlName
    }
    
    #############################################
    # Step 5: Run the Firecrawl Container with Overridden Redis Settings
    #############################################
    Write-Host "Starting Firecrawl container '$global:firecrawlName'..."
    # Command: run
    #   --detach: run container in background.
    #   --publish: map container port 3002 to host port 3002.
    #   --restart always: always restart the container unless explicitly stopped.
    #   --network: attach the container to the specified Docker network.
    #   --name: assign the container the name “firecrawl”.
    #   --env: set environment variables within the container.
    & $global:dockerPath run --detach --publish 3002:3002 --restart always --network $global:networkName --name $global:firecrawlName `
        --env OPENAI_API_KEY=dummy `
        --env REDIS_URL=redis://redis:6379 `
        --env REDIS_RATE_LIMIT_URL=redis://redis:6379 `
        --env REDIS_HOST=redis `
        --env REDIS_PORT=6379 `
        --env POSTHOG_API_KEY="" `
        $global:imageName
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to run Firecrawl container '$global:firecrawlName'."
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
}

<#
.SYNOPSIS
    Uninstalls the Firecrawl container.
.DESCRIPTION
    Removes the Firecrawl container if it exists.
#>
function Uninstall-FirecrawlContainer {
    $existingContainer = & $global:dockerPath ps --all --filter "name=^$global:firecrawlName$" --format "{{.ID}}"
    if ($existingContainer) {
        Write-Host "Removing Firecrawl container '$global:firecrawlName'..."
        & $global:dockerPath rm --force $global:firecrawlName
        if ($LASTEXITCODE -eq 0) {
            Write-Host "Firecrawl container removed successfully."
        }
        else {
            Write-Error "Failed to remove Firecrawl container."
        }
    }
    else {
        Write-Host "No Firecrawl container found to remove."
    }
}

<#
.SYNOPSIS
    Backs up the live Firecrawl container.
.DESCRIPTION
    Uses the Backup-ContainerState helper function to back up the Firecrawl container.
#>
function Backup-FirecrawlContainer {
    Backup-ContainerState -Engine $global:dockerPath -ContainerName $global:firecrawlName
}

<#
.SYNOPSIS
    Restores the Firecrawl container from backup.
.DESCRIPTION
    Uses the Restore-ContainerState helper function to restore the Firecrawl container.
#>
function Restore-FirecrawlContainer {
    Restore-ContainerState -Engine $global:dockerPath -ContainerName $global:firecrawlName
}

<#
.SYNOPSIS
    Updates the Firecrawl container.
.DESCRIPTION
    Removes the existing Firecrawl container, pulls the latest Firecrawl image, and reinstalls the container.
#>
function Update-FirecrawlContainer {
    $existingContainer = & $global:dockerPath ps --all --filter "name=^$global:firecrawlName$" --format "{{.ID}}"
    if ($existingContainer) {
        Write-Host "Removing existing Firecrawl container..."
        & $global:dockerPath rm --force $global:firecrawlName
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Failed to remove existing Firecrawl container. Update aborted."
            return
        }
    }
    Write-Host "Pulling latest Firecrawl image '$global:imageName'..."
    & $global:dockerPath pull $global:imageName
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to pull latest image. Update aborted."
        return
    }
    Install-FirecrawlContainer
}

<#
.SYNOPSIS
    Updates the user data for the Firecrawl container.
.DESCRIPTION
    This functionality is not implemented.
#>
function Update-FirecrawlUserData {
    Write-Host "Update User Data functionality is not implemented for Firecrawl container."
}

<#
.SYNOPSIS
    Displays the main menu for Firecrawl container operations.
.DESCRIPTION
    Presents menu options for installing, uninstalling, backing up, restoring, updating the system,
    and updating user data. The exit option ("0") terminates the menu loop.
#>
function Show-ContainerMenu {
    Write-Host "==========================================="
    Write-Host "Firecrawl Container Menu"
    Write-Host "==========================================="
    Write-Host "1. Install container"
    Write-Host "2. Uninstall container"
    Write-Host "3. Backup Live container"
    Write-Host "4. Restore Live container"
    Write-Host "5. Update System"
    Write-Host "6. Update User Data"
    Write-Host "0. Exit menu"
}

################################################################################
# Main Menu Loop for Firecrawl Container Management
################################################################################
do {
    Show-ContainerMenu
    $choice = Read-Host "Enter your choice (1, 2, 3, 4, 5, 6, or 0)"
    switch ($choice) {
        "1" { Install-FirecrawlContainer }
        "2" { Uninstall-FirecrawlContainer }
        "3" { Backup-FirecrawlContainer }
        "4" { Restore-FirecrawlContainer }
        "5" { Update-FirecrawlContainer }
        "6" { Update-FirecrawlUserData }
        "0" { Write-Host "Exiting menu." }
        default { Write-Host "Invalid selection. Please enter 1, 2, 3, 4, 5, 6, or 0." }
    }
    if ($choice -ne "0") {
         Write-Host "`nPress any key to continue..."
         $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
         Clear-Host
    }
} while ($choice -ne "0")