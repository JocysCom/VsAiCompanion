################################################################################
# File         : Setup_7_NocoDB.ps1
# Description  : Script to set up, backup, restore, uninstall, and update the 
#                NocoDB container using Docker/Podman support. Provides a container 
#                menu for container operations.
#                NocoDB is an AirTable alternative.
# Usage        : Run as Administrator if using Docker.
################################################################################

using namespace System
using namespace System.IO

# Dot-source the common functions file.
. "$PSScriptRoot\Setup_0.ps1"

# Ensure the script's working directory is set.
Set-ScriptLocation

#############################################
# Global Variables and Container Engine Setup
#############################################
$global:containerEngine = Select-ContainerEngine
if ($global:containerEngine -eq "docker") {
    Ensure-Elevated
    $global:enginePath = Get-DockerPath
    $global:pullOptions = @()  # No additional options for Docker.
}
else {
    $global:enginePath = Get-PodmanPath
    $global:pullOptions = @("--tls-verify=false")
}

# Set the NocoDB image name and container name.
$global:imageName = "nocodb/nocodb:latest"
$global:containerName = "nocodb"

#############################################
# Function: Install-NocoDBContainer
# Description: Installs (or reinstalls) the NocoDB container.
#              Checks/creates the necessary volume for data persistence,
#              removes any existing container, pulls the image if necessary,
#              runs the container, and tests connectivity.
#############################################
function Install-NocoDBContainer {
    Write-Host "Installing NocoDB container using image '$global:imageName'..."

    # Check if the volume for NocoDB data exists; if not, create it.
    $volumeName = "nocodb_data"
    $existingVolume = & $global:enginePath volume ls --filter "name=$volumeName" --format "{{.Name}}"
    if ([string]::IsNullOrWhiteSpace($existingVolume)) {
        Write-Host "Creating volume '$volumeName' for NocoDB data..."
        & $global:enginePath volume create $volumeName
    }
    else {
        Write-Host "Volume '$volumeName' already exists. Skipping creation."
    }

    # Check if the NocoDB image exists.
    $existingImage = & $global:enginePath images --format "{{.Repository}}:{{.Tag}}" | Where-Object { $_ -like "nocodb/nocodb:*" }
    if (-not $existingImage) {
        if (-not (Check-AndRestoreBackup -Engine $global:enginePath -ImageName $global:imageName)) {
            Write-Host "No backup restored. Pulling NocoDB image '$global:imageName'..."
            $pullCmd = @("pull") + $global:pullOptions + $global:imageName
            & $global:enginePath @pullCmd
            if ($LASTEXITCODE -ne 0) {
                Write-Error "Failed to pull NocoDB image. Exiting..."
                return
            }
        }
        else {
            Write-Host "Using restored backup image '$global:imageName'."
        }
    }
    else {
        Write-Host "NocoDB image already exists. Skipping pull."
    }

    # Remove any existing container with the same name.
    $existingContainer = & $global:enginePath ps --all --filter "name=^$global:containerName$" --format "{{.ID}}"
    if ($existingContainer) {
        Write-Host "Removing existing container '$global:containerName'..."
        & $global:enginePath rm --force $global:containerName
    }

    # Define run options for the container.
    $runOptions = @(
        "--detach",                             # Run container in background.
        "--publish", "8570:8080",               # Map host port 8570 to container port 8080.
        "--volume", "$($volumeName):/usr/app/data",# Bind mount volume for data persistence.
        "--name", $global:containerName         # Set container name.
    )

    Write-Host "Starting NocoDB container..."
    & $global:enginePath run $runOptions $global:imageName
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to run NocoDB container."
        return
    }

    Write-Host "Waiting 20 seconds for container startup..."
    Start-Sleep -Seconds 20
    Test-TCPPort -ComputerName "localhost" -Port 8570 -serviceName $global:containerName
    Test-HTTPPort -Uri "http://localhost:8570" -serviceName $global:containerName
    Write-Host "NocoDB is now running and accessible at http://localhost:8570"
}

#############################################
# Function: Uninstall-NocoDBContainer
# Description: Removes the NocoDB container if it exists.
#############################################
function Uninstall-NocoDBContainer {
    $existingContainer = & $global:enginePath ps --all --filter "name=^$global:containerName$" --format "{{.ID}}"
    if ($existingContainer) {
        Write-Host "Removing NocoDB container '$global:containerName'..."
        & $global:enginePath rm --force $global:containerName
        if ($LASTEXITCODE -eq 0) {
            Write-Host "NocoDB container removed successfully."
        }
        else {
            Write-Error "Failed to remove NocoDB container."
        }
    }
    else {
        Write-Host "No NocoDB container found to remove."
    }
}

#############################################
# Function: Backup-NocoDBContainer
# Description: Backs up the live NocoDB container.
#############################################
function Backup-NocoDBContainer {
    Backup-ContainerState -Engine $global:enginePath -ContainerName $global:containerName
}

#############################################
# Function: Restore-NocoDBContainer
# Description: Restores the NocoDB container from a backup.
#############################################
function Restore-NocoDBContainer {
    Restore-ContainerState -Engine $global:enginePath -ContainerName $global:containerName
}

#############################################
# Function: Update-NocoDBContainer
# Description: Updates the NocoDB container by removing the existing one,
#              pulling the latest image, and reinstalling the container.
#############################################
function Update-NocoDBContainer {
    Write-Host "Updating NocoDB container..."
    $existingContainer = & $global:enginePath ps --all --filter "name=^$global:containerName$" --format "{{.ID}}"
    if ($existingContainer) {
        Write-Host "Removing existing container '$global:containerName'..."
        & $global:enginePath rm --force $global:containerName
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Failed to remove existing NocoDB container. Update aborted."
            return
        }
    }
    Write-Host "Pulling latest NocoDB image '$global:imageName'..."
    $pullCmd = @("pull") + $global:pullOptions + $global:imageName
    & $global:enginePath @pullCmd
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to pull the latest NocoDB image. Update aborted."
        return
    }
    Install-NocoDBContainer
}

#############################################
# Function: Show-ContainerMenu
# Description: Displays the main menu for NocoDB container management.
#############################################
function Show-ContainerMenu {
    Write-Host "==========================================="
    Write-Host "NocoDB Container Management Menu"
    Write-Host "==========================================="
    Write-Host "1. Install container"
    Write-Host "2. Uninstall container"
    Write-Host "3. Backup container"
    Write-Host "4. Restore container"
    Write-Host "5. Update container"
    Write-Host "0. Exit menu"
}

################################################################################
# Main Menu Loop for NocoDB Container Management
################################################################################
do {
    Show-ContainerMenu
    $choice = Read-Host "Enter your choice (1, 2, 3, 4, 5, or 0)"
    switch ($choice) {
        "1" { Install-NocoDBContainer }
        "2" { Uninstall-NocoDBContainer }
        "3" { Backup-NocoDBContainer }
        "4" { Restore-NocoDBContainer }
        "5" { Update-NocoDBContainer }
        "0" { Write-Host "Exiting menu." }
        default { Write-Host "Invalid selection. Please enter 1, 2, 3, 4, 5, or 0." }
    }
    if ($choice -ne "0") {
         Write-Host "`nPress any key to continue..."
         $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
         Clear-Host
    }
} while ($choice -ne "0")h