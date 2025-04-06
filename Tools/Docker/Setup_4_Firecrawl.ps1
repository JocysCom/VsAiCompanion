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
    # Step 1: Ensure Docker Network Exists
    #############################################
    if (-not (Confirm-ContainerNetwork -Engine $global:enginePath -NetworkName $global:networkName)) {
        Write-Error "Failed to ensure network '$($global:networkName)' exists. Exiting..."
        exit 1
    }

    #############################################
    # Step 2: Run the Redis Container with a Network Alias
    #############################################
    $existingRedis = & $global:enginePath ps --all --filter "name=^$global:redisContainerName$" --format "{{.ID}}"
    if ($existingRedis) {
        Write-Information "Removing existing Redis container '$global:redisContainerName'..."
        & $global:enginePath rm --force $global:redisContainerName
    }
    Write-Information "Starting Redis container '$global:redisContainerName' on network '$global:networkName' with alias 'redis'..."
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

    Write-Information "Waiting 10 seconds for Redis container to initialize..."
    Start-Sleep -Seconds 10

    Write-Information "Testing Redis container connectivity on port 6379 before installing Firecrawl..."
    if (-not (Test-TCPPort -ComputerName "localhost" -Port 6379 -serviceName "Firecrawl Redis")) {
        Write-Error "Redis connectivity test failed. Aborting Firecrawl installation."
        exit 1
    }

    #############################################
    # Step 3: Pull the Firecrawl Docker Image (or Restore)
    #############################################
    $existingImage = & $global:enginePath images --filter "reference=$($global:imageName)" --format "{{.ID}}"
    if (-not $existingImage) {
        if (-not (Test-AndRestoreBackup -Engine $global:enginePath -ImageName $global:imageName)) {
            Write-Information "No backup restored. Pulling Firecrawl Docker image '$global:imageName'..."
            # Use shared pull function
            if (-not (Invoke-PullImage -Engine $global:enginePath -ImageName $global:imageName)) { # No specific pull options needed
                Write-Error "Docker pull failed for image '$global:imageName'."
                exit 1
            }
        } else {
            Write-Information "Using restored backup image '$global:imageName'."
        }
    }
    else {
        Write-Information "Using restored backup image '$global:imageName'."
    }

    #############################################
    # Step 4: Remove Existing Firecrawl Container (if any)
    #############################################
    $existingFirecrawl = & $global:enginePath ps --all --filter "name=^$global:firecrawlName$" --format "{{.ID}}"
    if ($existingFirecrawl) {
        Write-Information "Removing existing Firecrawl container '$global:firecrawlName'..."
        & $global:enginePath rm --force $global:firecrawlName
    }

    #############################################
    # Step 5: Run the Firecrawl Container with Overridden Redis Settings
    #############################################
    Write-Information "Starting Firecrawl container '$global:firecrawlName'..."
    # Ensure the volume exists
    if (-not (Confirm-ContainerVolume -Engine $global:enginePath -VolumeName $global:volumeName)) {
        Write-Error "Failed to ensure volume '$($global:volumeName)' exists. Exiting..."
        exit 1
    }
    Write-Information "IMPORTANT: Using volume '$($global:volumeName)' - existing user data will be preserved."

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
    Write-Information "Waiting 20 seconds for containers to fully start..."
    Start-Sleep -Seconds 20

    Write-Information "Testing Firecrawl API connectivity on port 3002..."
    Test-TCPPort -ComputerName "localhost" -Port 3002 -serviceName "Firecrawl API"
    Test-HTTPPort -Uri "http://localhost:3002" -serviceName "Firecrawl API"

    Write-Information "Testing Redis container connectivity on port 6379..."
    Test-TCPPort -ComputerName "localhost" -Port 6379 -serviceName "Firecrawl Redis"

    Write-Information "Firecrawl is now running and accessible at http://localhost:3002"
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
        Write-Information "Removing Redis container '$global:redisContainerName'..."
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
    Uses the generic Update-Container function. The Redis container is left running.
    The RunFunction script block re-uses parts of the Install-FirecrawlContainer logic
    to start only the Firecrawl container, assuming Redis and the network are already present.
#>
function Update-FirecrawlContainer {
    [CmdletBinding(SupportsShouldProcess=$true)]
    param()

    # Check ShouldProcess before proceeding with the delegated update
    if (-not $PSCmdlet.ShouldProcess($global:firecrawlName, "Update Container")) {
        return
    }

    # Define the script block to run the updated Firecrawl container
    $runFirecrawlScriptBlock = {
        param(
            [string]$EnginePath,
            [string]$ContainerEngineType, # Not used
            [string]$ContainerName,       # Should be $global:firecrawlName
            [string]$VolumeName,          # Should be $global:volumeName
            [string]$ImageName            # The updated image name ($global:imageName)
        )

        # Assume Network and Redis are already running from the initial install

        # Ensure the volume exists (important if it was removed manually)
        if (-not (Confirm-ContainerVolume -Engine $EnginePath -VolumeName $VolumeName)) {
            throw "Failed to ensure volume '$VolumeName' exists during update."
        }

        Write-Information "Starting updated Firecrawl container '$ContainerName'..."

        # Define run options (same as in Install-FirecrawlContainer)
        $runOptions = @(
            "--detach",
            "--publish", "3002:3002",
            "--restart", "always",
            "--network", $global:networkName, # Use global network name
            "--name", $ContainerName,
            "--volume", "$($VolumeName):/app/data",
            "--env", "OPENAI_API_KEY=dummy",
            "--env", "REDIS_URL=redis://redis:6379",
            "--env", "REDIS_RATE_LIMIT_URL=redis://redis:6379",
            "--env", "REDIS_HOST=redis",
            "--env", "REDIS_PORT=6379",
            "--env", "POSTHOG_API_KEY="
        )

        # Execute the command
        & $EnginePath run @runOptions $ImageName
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to run updated Firecrawl container '$ContainerName'."
        }

        # Wait and Test Connectivity (same as in Install-FirecrawlContainer)
        Write-Information "Waiting 20 seconds for container to fully start..."
        Start-Sleep -Seconds 20
        Write-Information "Testing Firecrawl API connectivity on port 3002..."
        Test-TCPPort -ComputerName "localhost" -Port 3002 -serviceName "Firecrawl API"
        Test-HTTPPort -Uri "http://localhost:3002" -serviceName "Firecrawl API"
        Write-Information "Testing Redis container connectivity on port 6379..."
        Test-TCPPort -ComputerName "localhost" -Port 6379 -serviceName "Firecrawl Redis"
        Write-Information "Firecrawl container updated successfully."
    }

    # Call the generic Update-Container function
    Update-Container -Engine $global:enginePath `
                     -ContainerName $global:firecrawlName `
                     -ImageName $global:imageName `
                     -RunFunction $runFirecrawlScriptBlock.GetNewClosure() # Pass closure
}

<#
.SYNOPSIS
    Updates the user data for the Firecrawl container.
.DESCRIPTION
    This functionality is not implemented.
#>
function Update-FirecrawlUserData {
    [CmdletBinding(SupportsShouldProcess=$true)]
    param()

    if ($PSCmdlet.ShouldProcess("Firecrawl User Data", "Update")) {
        # No actions implemented yet
        Write-Information "Update User Data functionality is not implemented for Firecrawl container."
    }
}

<#
.SYNOPSIS
    Displays the main menu for Firecrawl container operations.
.DESCRIPTION
    Presents menu options for installing, uninstalling, backing up, restoring, updating the system,
    and updating user data. The exit option ("0") terminates the menu loop.
#>
function Show-ContainerMenu {
    Write-Output "==========================================="
    Write-Output "Firecrawl Container Menu"
    Write-Output "==========================================="
    Write-Output "1. Show Info & Test Connection"
    Write-Output "2. Install container (includes Redis)"
    Write-Output "3. Uninstall container (includes Redis)"
    Write-Output "4. Backup Live container"
    Write-Output "5. Restore Live container"
    Write-Output "6. Update System"
    Write-Output "7. Update User Data"
    Write-Output "0. Exit menu"
}

################################################################################
# Main Menu Loop using Generic Function
################################################################################
$menuActions = @{
    "1" = {
        # Pass the global variable directly to the restored -ContainerEngine parameter
        # Show status for Firecrawl itself
        Show-ContainerStatus -ContainerName $global:firecrawlName `
                             -ContainerEngine "docker" ` # This script hardcodes docker
                             -EnginePath $global:enginePath `
                             -DisplayName "Firecrawl API" `
                             -TcpPort 3002 `
                             -HttpPort 3002 `
                             -DelaySeconds 0

        # Show status for the associated Redis container
        Show-ContainerStatus -ContainerName $global:redisContainerName `
                             -ContainerEngine "docker" ` # This script hardcodes docker
                             -EnginePath $global:enginePath `
                             -DisplayName "Firecrawl Redis" `
                             -TcpPort 6379 `
                             -DelaySeconds 3
    }
    "2" = { Install-FirecrawlContainer }
    "3" = { Uninstall-FirecrawlContainer }
    "4" = { Backup-FirecrawlContainer }
    "5" = { Restore-FirecrawlContainer }
    "6" = { Update-FirecrawlContainer }
    "7" = { Update-FirecrawlUserData }
}

Invoke-MenuLoop -ShowMenuScriptBlock ${function:Show-ContainerMenu} -ActionMap $menuActions -ExitChoice "0"
