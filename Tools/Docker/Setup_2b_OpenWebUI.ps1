################################################################################
# File         : Setup_2b_OpenWebUI.ps1
# Description  : Script to set up, back up, restore, uninstall, and update the
#                Open WebUI container using Docker/Podman support. The script
#                provides a container menu (with install, backup, restore, uninstall,
#                update, and exit options).
# Usage        : Run as Administrator if using Docker.
################################################################################

using namespace System
using namespace System.IO

# Dot-source the necessary helper function files.
. "$PSScriptRoot\Setup_0_Core.ps1"
. "$PSScriptRoot\Setup_0_Network.ps1"
. "$PSScriptRoot\Setup_0_ContainerEngine.ps1"
. "$PSScriptRoot\Setup_0_BackupRestore.ps1"
. "$PSScriptRoot\Setup_0_ContainerMgmt.ps1"
. "$PSScriptRoot\Setup_0_WSL.ps1" # Needed for Check-WSLStatus

# Ensure the script working directory is set.
Set-ScriptLocation

#############################################
# Pick Container Engine and Set Global Variables
#############################################
$containerEngine = Select-ContainerEngine
if ($containerEngine -eq "docker") {
    Ensure-Elevated
    $enginePath = Get-DockerPath
}
else {
    $enginePath = Get-PodmanPath
}
$imageName     = "ghcr.io/open-webui/open-webui:main"
$containerName = "open-webui"
$volumeName    = "open-webui" # Assuming volume name matches container name

<#
.SYNOPSIS
    Gets the current Open WebUI container configuration.
.DESCRIPTION
    Retrieves container information including environment variables and volume mounts.
.OUTPUTS
    Returns a custom object with container information or $null if not found.
#>
function Get-OpenWebUIContainerConfig {
    $containerInfo = & $enginePath inspect $containerName 2>$null | ConvertFrom-Json
    if (-not $containerInfo) {
        Write-Host "Container '$containerName' not found." -ForegroundColor Yellow
        return $null
    }

    # Extract environment variables
    $envVars = @()
    try {
        foreach ($env in $containerInfo.Config.Env) {
            # Only preserve specific environment variables if needed
            if ($env -match "^(OPENWEBUI_|API_)") {
                $envVars += $env
            }
        }
    } catch {
        Write-Warning "Could not parse existing environment variables: $_"
    }

    # Extract volume mounts
    $volumeMounts = @()
    try {
        foreach ($mount in $containerInfo.Mounts) {
            if ($mount.Type -eq "volume") {
                $volumeMounts += "$($mount.Name):$($mount.Destination)"
            }
        }
    } catch {
        Write-Warning "Could not parse existing volume mounts: $_"
        # Default volume mount if parsing fails
        $volumeMounts = @("$($volumeName):/app/backend/data")
    }

    # Extract port mappings
    $ports = @()
    try {
        foreach ($portMapping in $containerInfo.NetworkSettings.Ports.PSObject.Properties) {
            foreach ($binding in $portMapping.Value) {
                $ports += "$($binding.HostPort):$($portMapping.Name.Split('/')[0])"
            }
        }
    } catch {
        Write-Warning "Could not parse existing port mappings: $_"
        # Default port mapping if parsing fails
        $ports = @("3000:8080")
    }

    # Return a custom object with the container information
    return [PSCustomObject]@{
        Image = $containerInfo.Config.Image
        EnvVars = $envVars
        VolumeMounts = $volumeMounts
        Ports = $ports
        Platform = "linux/amd64"  # Assuming this is the platform used
    }
}

<#
.SYNOPSIS
    Runs the container using the provided container engine and parameters.
.DESCRIPTION
    This function encapsulates the duplicated code to run, wait, and test the
    container startup.
.PARAMETER action
    A message prefix indicating the action (e.g. "Running container" or "Starting updated container").
.PARAMETER successMessage
    The message to print on successful startup.
.PARAMETER config
    Optional configuration object containing container settings. If not provided, default settings are used.
#>
function Run-Container {
    param (
        [string]$action,
        [string]$successMessage,
        [PSCustomObject]$config = $null
    )
    Write-Host "$action '$containerName'..."

    # Build the run command with either provided config or defaults
    $runOptions = @("--platform")

    if ($config) {
        $runOptions += $config.Platform
    } else {
        $runOptions += "linux/amd64"
    }

    $runOptions += @("--detach")

    # Add port mappings
    if ($config -and $config.Ports) {
        foreach ($port in $config.Ports) {
            $runOptions += "--publish"
            $runOptions += $port
        }
    } else {
        $runOptions += "--publish"
        $runOptions += "3000:8080"
    }

    # Add volume mounts
    if ($config -and $config.VolumeMounts) {
        foreach ($volume in $config.VolumeMounts) {
            $runOptions += "--volume"
            $runOptions += $volume
        }
    } else {
        $runOptions += "--volume"
        $runOptions += "$($volumeName):/app/backend/data"
    }

    # Add environment variables if provided
    if ($config -and $config.EnvVars) {
        foreach ($env in $config.EnvVars) {
            $runOptions += "--env"
            $runOptions += $env
        }
    }

    # Add host networking for Docker only
    if ($containerEngine -eq "docker") {
        $runOptions += "--add-host"
        $runOptions += "host.docker.internal:host-gateway"
    }

    # Add restart policy
    $runOptions += "--restart"
    $runOptions += "always"

    # Add container name
    $runOptions += "--name"
    $runOptions += $containerName

    # Add image name
    if ($config -and $config.Image) {
        $runOptions += $config.Image
    } else {
        $runOptions += $imageName
    }

    # Command: run
    #   --platform: Specify platform (linux/amd64).
    #   --detach: Run container in background.
    #   --publish: Map host port 3000 to container port 8080.
    #   --volume: Mount the named volume for persistent data.
    #   --add-host: (Docker only) Map host.docker.internal to host gateway IP.
    #   --restart always: Always restart the container unless explicitly stopped.
    #   --name: Assign a name to the container.
    # Run the container with all options
    & $enginePath run @runOptions

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to run container."
        return $false
    }

    Write-Host "Waiting 20 seconds for container startup..."
    Start-Sleep -Seconds 20

    # Test connectivity
    $httpTest = Test-HTTPPort -Uri "http://localhost:3000" -serviceName "OpenWebUI"
    $tcpTest = Test-TCPPort -ComputerName "localhost" -Port 3000 -serviceName "OpenWebUI"
    $wsTest = Test-WebSocketPort -Uri "ws://localhost:3000/api/v1/chat/completions" -serviceName "OpenWebUI WebSockets"

    # Create firewall rule if needed
    try {
        New-NetFirewallRule -DisplayName "Allow WebSockets" -Direction Inbound -LocalPort 3000 -Protocol TCP -Action Allow -ErrorAction SilentlyContinue
    } catch {
        Write-Warning "Could not create firewall rule. You may need to manually allow port 3000."
    }

    Write-Host $successMessage
    return $true
}

<#
.SYNOPSIS
    Installs the Open WebUI container.
.DESCRIPTION
    Attempts to restore a backup image; if not available, pulls the latest image,
    removes any existing container, and then runs the container. A reminder regarding
    Open WebUI settings is printed after the container is running.
#>
function Install-OpenWebUIContainer {
    # Attempt to restore backup image; if not, pull latest image.
    if (-not (Check-AndRestoreBackup -Engine $enginePath -ImageName $imageName)) {
        Write-Host "No backup restored. Pulling Open WebUI image '$imageName'..."
        # Pull command:
        # pull       Pull an image from a registry.
        # --platform string Specify the platform to pull the image for.
        & $enginePath pull --platform linux/amd64 $imageName
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Pull failed. Check internet connection or image URL."
            return
        }
    }
    else {
        Write-Host "Using restored backup image '$imageName'."
    }
    # Remove any existing container.
    $existingContainer = & $enginePath ps -a --filter "name=^$containerName$" --format "{{.ID}}"
    if ($existingContainer) {
        Write-Host "Removing existing container '$containerName'..."
        # Remove container:
        # rm         Remove one or more containers.
        # --force    Force removal of a running container.
        & $enginePath rm --force $containerName
    }
    Run-Container -action "Running container" -successMessage "Open WebUI is now running and accessible at http://localhost:3000`nReminder: In Open WebUI settings, set the OpenAI API URL to 'http://host.docker.internal:9099' and API key to '0p3n-w3bu!' if integrating pipelines."
}

<#
.SYNOPSIS
    Uninstalls the Open WebUI container and optionally the data volume.
.DESCRIPTION
    Uses the generic Remove-ContainerAndVolume function.
#>
function Uninstall-OpenWebUIContainer {
    Remove-ContainerAndVolume -Engine $enginePath -ContainerName $containerName -VolumeName $volumeName
}

<#
.SYNOPSIS
    Backs up the live Open WebUI container.
.DESCRIPTION
    Uses the Backup-ContainerState helper function to back up the container.
#>
function Backup-OpenWebUIContainer {
    Backup-ContainerState -Engine $enginePath -ContainerName $containerName
}

<#
.SYNOPSIS
    Restores the Open WebUI container from backup.
.DESCRIPTION
    Uses the Restore-ContainerState helper function to restore the container.
#>
function Restore-OpenWebUIContainer {
    Restore-ContainerState -Engine $enginePath -ContainerName $containerName
}

#############################################
# Optional Functions
#############################################

<#
.SYNOPSIS
    Updates the Open WebUI container.
.DESCRIPTION
    Uses the shared Update-Container function to update the Open WebUI container
    while preserving user data and configuration.
#>
function Update-OpenWebUIContainer {
    # Define a script block that knows how to run the container with the right options
    $runContainerFunction = {
        $config = Get-OpenWebUIContainerConfig
        if (-not $config) {
            throw "Failed to get container configuration"
        }

        # Update the image in the config to use the latest one
        $config.Image = $imageName

        $result = Run-Container -action "Starting updated container" -successMessage "OpenWebUI container has been successfully updated and is running at http://localhost:3000" -config $config
        if (-not $result) {
            throw "Failed to start updated container"
        }
    }

    # Use the shared update function
    Update-Container -Engine $enginePath -ContainerName $containerName -ImageName $imageName -RunFunction $runContainerFunction
}

<#
.SYNOPSIS
    Updates the user data for the Open WebUI container.
.DESCRIPTION
    This functionality is not implemented.
#>
function Update-OpenWebUIUserData {
    Write-Host "Update User Data functionality is not implemented for OpenWebUI container."

    # Provide some helpful information
    Write-Host "User data is stored in the 'open-webui' volume at '/app/backend/data' inside the container." -ForegroundColor Yellow
    Write-Host "To back up user data, you can use the 'Backup Live container' option." -ForegroundColor Yellow
    Write-Host "To modify user data directly, you would need to access the container with:" -ForegroundColor DarkYellow
    Write-Host "  $enginePath exec -it $containerName /bin/bash" -ForegroundColor DarkYellow
}

#############################################
# Main Menu Loop using Generic Function
#############################################
function Show-OpenWebUIMenu {
    Write-Host "==========================================="
    Write-Host "Open WebUI Container Menu"
    Write-Host "==========================================="
    Write-Host "1. Install container"
    Write-Host "2. Uninstall container"
    Write-Host "3. Backup Live container"
    Write-Host "4. Restore Live container"
    Write-Host "5. Update System"
    Write-Host "6. Update User Data"
    Write-Host "7. Check for Updates"
    Write-Host "0. Exit"
}

$menuActions = @{
    "1" = { Install-OpenWebUIContainer }
    "2" = { Uninstall-OpenWebUIContainer }
    "3" = { Backup-OpenWebUIContainer }
    "4" = { Restore-OpenWebUIContainer }
    "5" = { Update-OpenWebUIContainer }
    "6" = { Update-OpenWebUIUserData }
    "7" = { Check-ImageUpdateAvailable -Engine $enginePath -ImageName $imageName }
}

Invoke-MenuLoop -ShowMenuScriptBlock ${function:Show-OpenWebUIMenu} -ActionMap $menuActions -ExitChoice "0"
