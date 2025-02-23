################################################################################
# File         : Setup_3_n8n.ps1
# Description  : Script to set up and run the n8n container using Docker/Podman.
#                Verifies volume presence, pulls the n8n image if necessary,
#                and runs the container with port and volume mappings.
#                Additionally, prompts for an external domain to set N8N_HOST
#                and WEBHOOK_URL if needed.
# Usage        : Run as Administrator if using Docker.
################################################################################

using namespace System
using namespace System.IO

# Dot-source the common functions file.
. "$PSScriptRoot\Setup_0.ps1"

# Ensure the script working directory is set.
Set-ScriptLocation

#############################################
# Global Variables
#############################################
$global:containerEngine = Select-ContainerEngine
if ($global:containerEngine -eq "docker") {
    Ensure-Elevated
    $global:enginePath    = Get-DockerPath
    $global:pullOptions   = @()  # No extra options needed for Docker.
    $global:imageName     = "docker.n8n.io/n8nio/n8n:latest"
}
else {
    $global:enginePath    = Get-PodmanPath
    $global:pullOptions   = @("--tls-verify=false")
    # Use the Docker Hub version of n8n for Podman to avoid 403 errors.
    $global:imageName     = "n8nio/n8n:latest"
}

<#
.SYNOPSIS
    Installs the n8n container.
.DESCRIPTION
    Creates (if necessary) the volume 'n8n_data', pulls the n8n image if not found
    (or restores from backup), removes any pre-existing container named "n8n",
    prompts for an external domain, builds run options, runs the container,
    waits for startup, and tests connectivity on port 5678.
    Core argument values are preserved.
#>
function Install-n8nContainer {
    # Check if volume 'n8n_data' already exists; if not, create it.
    $existingVolume = & $global:enginePath volume ls --filter "name=n8n_data" --format "{{.Name}}"
    if ([string]::IsNullOrWhiteSpace($existingVolume)) {
        Write-Host "Creating volume 'n8n_data'..."
        & $global:enginePath volume create n8n_data
    }
    else {
        Write-Host "Volume 'n8n_data' already exists. Skipping creation."
    }

    # Check if the n8n image is already available.
    $existingImage = & $global:enginePath images --format "{{.Repository}}:{{.Tag}}" | Where-Object { $_ -match "n8n" }
    if (-not $existingImage) {
        if (-not (Check-AndRestoreBackup -Engine $global:enginePath -ImageName $global:imageName)) {
            Write-Host "No backup restored. Pulling n8n image '$global:imageName'..."
            $pullCmd = @("pull") + $global:pullOptions + $global:imageName
            & $global:enginePath @pullCmd
            if ($LASTEXITCODE -ne 0) {
                Write-Error "Image pull failed. Exiting..."
                return
            }
        }
        else {
            Write-Host "Using restored backup image '$global:imageName'."
        }
    }
    else {
        Write-Host "n8n image already exists. Skipping pull."
    }

    # Check if a container with the name "n8n" already exists; if so, remove it.
    $existingContainer = & $global:enginePath ps --all --filter "name=n8n" --format "{{.ID}}"
    if ($existingContainer) {
        Write-Host "Removing existing container 'n8n'..."
        & $global:enginePath rm --force n8n
    }

    # Prompt user for external domain configuration.
    $externalDomain = Read-Host "Enter external domain for n8n container (e.g., n8n.example.com) or press Enter to skip"

    # Define run options for starting the container.
    $runOptions = @(
        "--detach",                            # Run container in background.
        "--publish", "5678:5678",              # Map host port 5678 to container port 5678.
        "--volume", "n8n_data:/home/node/.n8n",  # Bind mount volume "n8n_data" to /home/node/.n8n in the container.
        "--name", "n8n"                        # Assign the container the name "n8n".
    )

    # If an external domain is provided, add environment variable options.
    if (-not [string]::IsNullOrWhiteSpace($externalDomain)) {
        $envOptions = @(
            "--env", "N8N_HOST=$externalDomain",          # Set N8N_HOST to the provided domain.
            "--env", "WEBHOOK_URL=https://$externalDomain"  # Set WEBHOOK_URL using HTTPS.
        )
        $runOptions += $envOptions
    }

    Write-Host "Starting n8n container..."
    & $global:enginePath run $runOptions $global:imageName
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to run n8n container. Exiting..."
        return
    }
    Write-Host "Waiting 20 seconds for container startup..."
    Start-Sleep -Seconds 20
    Test-TCPPort -ComputerName "localhost" -Port 5678 -serviceName "n8n"
    Test-HTTPPort -Uri "http://localhost:5678" -serviceName "n8n"
    Write-Host "n8n is now running and accessible at http://localhost:5678"
}

<#
.SYNOPSIS
    Uninstalls the n8n container.
.DESCRIPTION
    Checks for an existing container named "n8n" and removes it using the container engine's rm command.
#>
function Uninstall-n8nContainer {
    $existingContainer = & $global:enginePath ps --all --filter "name=n8n" --format "{{.ID}}"
    if ($existingContainer) {
        Write-Host "Removing n8n container..."
        & $global:enginePath rm --force n8n
        if ($LASTEXITCODE -eq 0) {
            Write-Host "n8n container removed successfully."
        }
        else {
            Write-Error "Failed to remove n8n container."
        }
    }
    else {
        Write-Host "No n8n container found to remove."
    }
}

<#
.SYNOPSIS
    Backs up the live n8n container.
.DESCRIPTION
    Uses the Backup-ContainerState helper function to create a backup of the container.
#>
function Backup-n8nContainer {
    Backup-ContainerState -Engine $global:enginePath -ContainerName "n8n"
}

<#
.SYNOPSIS
    Restores the n8n container from backup.
.DESCRIPTION
    Uses the Restore-ContainerState helper function to restore the container from a backup.
#>
function Restore-n8nContainer {
    Restore-ContainerState -Engine $global:enginePath -ContainerName "n8n"
}

<#
.SYNOPSIS
    Updates the n8n container.
.DESCRIPTION
    Removes any existing container, pulls the latest n8n image, and reinstalls the container.
#>
function Update-n8nContainer {
    $existingContainer = & $global:enginePath ps --all --filter "name=n8n" --format "{{.ID}}"
    if ($existingContainer) {
        Write-Host "Removing existing n8n container..."
        & $global:enginePath rm --force n8n
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Failed to remove existing n8n container. Update aborted."
            return
        }
    }
    Write-Host "Pulling latest n8n image '$global:imageName'..."
    $pullCmd = @("pull") + $global:pullOptions + $global:imageName
    & $global:enginePath @pullCmd
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to pull latest image. Update aborted."
        return
    }
    Install-n8nContainer
}

<#
.SYNOPSIS
    Updates user data for the n8n container.
.DESCRIPTION
    This functionality is not yet implemented.
#>
function Update-n8nUserData {
    Write-Host "Update User Data functionality is not implemented for n8n container."
}

<#
.SYNOPSIS
    Displays the main menu for n8n container operations.
.DESCRIPTION
    Presents menu options (Install, Uninstall, Backup, Restore, Update System, and Update User Data);
    the exit option ("0") terminates the menu loop.
#>
function Show-ContainerMenu {
    Write-Host "==========================================="
    Write-Host "n8n Container Menu"
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
# Main Menu Loop for n8n Container Management
################################################################################
do {
    Show-ContainerMenu
    $choice = Read-Host "Enter your choice (1, 2, 3, 4, 5, 6, or 0)"
    switch ($choice) {
        "1" { Install-n8nContainer }
        "2" { Uninstall-n8nContainer }
        "3" { Backup-n8nContainer }
        "4" { Restore-n8nContainer }
        "5" { Update-n8nContainer }
        "6" { Update-n8nUserData }
        "0" { Write-Host "Exiting menu." }
        default { Write-Host "Invalid selection. Please enter 1, 2, 3, 4, 5, 6, or 0." }
    }
    if ($choice -ne "0") {
         Write-Host "`nPress any key to continue..."
         $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
         Clear-Host
    }
} while ($choice -ne "0")