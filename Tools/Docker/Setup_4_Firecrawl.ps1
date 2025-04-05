################################################################################
# File         : Setup_4_Firecrawl.ps1
# Description  : Script to set up and run a Firecrawl container with a dedicated Redis container.
#                Creates a Docker network, launches Redis, pulls Firecrawl image, and runs Firecrawl
#                with environment variable overrides directing it to use the dedicated Redis.
# Usage        : Run as Administrator.
################################################################################

using namespace System
using namespace System.IO

# Dot-source the necessary helper function files.
. "$PSScriptRoot\Setup_0_Core.ps1"
. "$PSScriptRoot\Setup_0_Network.ps1"
. "$PSScriptRoot\Setup_0_ContainerEngine.ps1"
. "$PSScriptRoot\Setup_0_BackupRestore.ps1"
. "$PSScriptRoot\Setup_0_ContainerMgmt.ps1"

# Ensure the script is running as Administrator and set the working directory.
# Note: This script currently only supports Docker due to network alias usage.
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
$global:enginePath         = Get-DockerPath # Explicitly use Docker
$global:volumeName         = "firecrawl_data" # Define a volume name

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
    $existingNetwork = & $global:enginePath network ls --filter "name=^$global:networkName$" --format "{{.Name}}"
    if ($existingNetwork -ne $global:networkName) {
        Write-Host "Creating Docker network '$global:networkName'..."
        & $global:enginePath network create $global:networkName
    }
    else {
        Write-Host "Docker network '$global:networkName' already exists."
    }

    #############################################
    # Step 2: Run the Redis Container with a Network Alias
    #############################################
    $existingRedis = & $global:enginePath ps --all --filter "name=^$global:redisContainerName$" --format "{{.ID}}"
    if ($existingRedis) {
        Write-Host "Removing existing Redis container '$global:redisContainerName'..."
        & $global:enginePath rm --force $global:redisContainerName
    }
    Write-Host "Starting Redis container '$global:redisContainerName' on network '$global:networkName' with alias 'redis'..."
    # Command: run (for Redis)
    #   --detach: Run container in background.
    #   --name: Assign a name to the container.
    #   --network: Connect container to the specified network.
    #   --network-alias: Assign an alias ('redis') for use within the network.
    #   --publish: Map host port 6379 to container port 6379.
    & $global:enginePath run --detach --name $global:redisContainerName --network $global:networkName --network-alias redis --publish 6379:6379 $global:redisImage
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to start Redis container."
        exit 1
    }

    Write-Host "Waiting 10 seconds for Redis container to initialize..."
    Start-Sleep -Seconds 10

    Write-Host "Testing Redis container connectivity on port 6379 before installing Firecrawl..."
    if (-not (Test-TCPPort -ComputerName "localhost" -Port 6379 -serviceName "Firecrawl Redis")) {
        Write-Error "Redis connectivity test failed. Aborting Firecrawl installation."
        exit 1
    }

    #############################################
    # Step 3: Pull the Firecrawl Docker Image
    #############################################
    if (-not (Check-AndRestoreBackup -Engine $global:enginePath -ImageName $global:imageName)) {
        Write-Host "No backup restored. Pulling Firecrawl Docker image '$global:imageName'..."
        & $global:enginePath pull $global:imageName
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
    $existingFirecrawl = & $global:enginePath ps --all --filter "name=^$global:firecrawlName$" --format "{{.ID}}"
    if ($existingFirecrawl) {
        Write-Host "Removing existing Firecrawl container '$global:firecrawlName'..."
        & $global:enginePath rm --force $global:firecrawlName
    }

    #############################################
    # Step 5: Run the Firecrawl Container with Overridden Redis Settings
    #############################################
    Write-Host "Starting Firecrawl container '$global:firecrawlName'..."
    # Create volume if it doesn't exist
    $existingVolume = & $global:enginePath volume ls --filter "name=$global:volumeName" --format "{{.Name}}"
    if ([string]::IsNullOrWhiteSpace($existingVolume)) {
        Write-Host "Creating volume '$global:volumeName' for Firecrawl data..."
        & $global:enginePath volume create $global:volumeName
    }

    # Define run options as an array
    $runOptions = @(
        "--detach",                             # Run container in background.
        "--publish", "3002:3002",               # Map host port 3002 to container port 3002.
        "--restart", "always",                  # Always restart the container unless explicitly stopped.
        "--network", $global:networkName,       # Attach the container to the specified Docker network.
        "--name", $global:firecrawlName,        # Assign the container the name 'firecrawl'.
        "--volume", "$($global:volumeName):/app/data", # Mount the named volume for persistent data.
        "--env", "OPENAI_API_KEY=dummy",        # Set dummy OpenAI key.
        "--env", "REDIS_URL=redis://redis:6379", # Point to the Redis container using network alias.
        "--env", "REDIS_RATE_LIMIT_URL=redis://redis:6379",
        "--env", "REDIS_HOST=redis",
        "--env", "REDIS_PORT=6379",
        "--env", "POSTHOG_API_KEY="             # Disable PostHog analytics.
    )

    # Execute the command using splatting
    & $global:enginePath run @runOptions $global:imageName
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to run Firecrawl container '$global:firecrawlName'."
        exit 1
    }

    #############################################
    # Step 6: Wait and Test Connectivity
    #############################################
    Write-Host "Waiting 20 seconds for containers to fully start..."
    Start-Sleep -Seconds 20

    Write-Host "Testing Firecrawl API connectivity on port 3002..."
    Test-TCPPort -ComputerName "localhost" -Port 3002 -serviceName "Firecrawl API"
    Test-HTTPPort -Uri "http://localhost:3002" -serviceName "Firecrawl API"

    Write-Host "Testing Redis container connectivity on port 6379..."
    Test-TCPPort -ComputerName "localhost" -Port 6379 -serviceName "Firecrawl Redis"

    Write-Host "Firecrawl is now running and accessible at http://localhost:3002"
}

<#
.SYNOPSIS
    Uninstalls the Firecrawl container and optionally the data volume.
.DESCRIPTION
    Uses the generic Remove-ContainerAndVolume function. Also removes the dedicated Redis container.
#>
function Uninstall-FirecrawlContainer {
    # Remove Firecrawl container and potentially its volume
    Remove-ContainerAndVolume -Engine $global:enginePath -ContainerName $global:firecrawlName -VolumeName $global:volumeName

    # Remove the dedicated Redis container
    $existingRedis = & $global:enginePath ps -a --filter "name=^$global:redisContainerName$" --format "{{.ID}}"
    if ($existingRedis) {
        Write-Host "Removing Redis container '$global:redisContainerName'..."
        & $global:enginePath rm --force $global:redisContainerName
    }
}

<#
.SYNOPSIS
    Backs up the live Firecrawl container.
.DESCRIPTION
    Uses the Backup-ContainerState helper function to back up the Firecrawl container.
#>
function Backup-FirecrawlContainer {
    Backup-ContainerState -Engine $global:enginePath -ContainerName $global:firecrawlName
}

<#
.SYNOPSIS
    Restores the Firecrawl container from backup.
.DESCRIPTION
    Uses the Restore-ContainerState helper function to restore the Firecrawl container.
    Note: This does not restore the Redis container state.
#>
function Restore-FirecrawlContainer {
    Restore-ContainerState -Engine $global:enginePath -ContainerName $global:firecrawlName
    # Consider adding logic to restart Redis if needed after restore
}

<#
.SYNOPSIS
    Updates the Firecrawl container.
.DESCRIPTION
    Removes the existing Firecrawl container, pulls the latest Firecrawl image, and reinstalls the container.
    The Redis container is left running.
#>
function Update-FirecrawlContainer {
    $existingContainer = & $global:enginePath ps --all --filter "name=^$global:firecrawlName$" --format "{{.ID}}"
    if ($existingContainer) {
        Write-Host "Removing existing Firecrawl container..."
        & $global:enginePath rm --force $global:firecrawlName
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Failed to remove existing Firecrawl container. Update aborted."
            return
        }
    }
    Write-Host "Pulling latest Firecrawl image '$global:imageName'..."
    & $global:enginePath pull $global:imageName
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to pull latest image. Update aborted."
        return
    }
    # Re-run install logic, which will start Firecrawl and connect to existing Redis
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
    Write-Host "1. Install container (includes Redis)"
    Write-Host "2. Uninstall container (includes Redis)"
    Write-Host "3. Backup Live container"
    Write-Host "4. Restore Live container"
    Write-Host "5. Update System"
    Write-Host "6. Update User Data"
    Write-Host "0. Exit menu"
}

################################################################################
# Main Menu Loop using Generic Function
################################################################################
$menuActions = @{
    "1" = { Install-FirecrawlContainer }
    "2" = { Uninstall-FirecrawlContainer }
    "3" = { Backup-FirecrawlContainer }
    "4" = { Restore-FirecrawlContainer }
    "5" = { Update-FirecrawlContainer }
    "6" = { Update-FirecrawlUserData }
}

Invoke-MenuLoop -ShowMenuScriptBlock ${function:Show-ContainerMenu} -ActionMap $menuActions -ExitChoice "0"
