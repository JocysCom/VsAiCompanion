################################################################################
# File         : Setup_2a_Pipelines.ps1
# Description  : Script to set up, back up, restore, and uninstall the Pipelines
#                container using Docker or Podman. This version installs Pipelines
#                from scratch by cloning the repository (without converting LF to CRLF)
#                and running the container with the default configuration.
# Usage        : Run as Administrator if using Docker.
################################################################################

using namespace System
using namespace System.IO

# Dot-source the common functions file (assumed to provide functions like Check-Git, Set-ScriptLocation,
# Select-ContainerEngine, Get-DockerPath, Get-PodmanPath, Backup-ContainerState, Restore-ContainerState,
# Test-TCPPort, and Test-HTTPPort).
. "$PSScriptRoot\Setup_0.ps1"

# Ensure the working directory is set.
Set-ScriptLocation

# Global variables used across functions.
$global:containerName   = "pipelines"
$global:pipelinesFolder = ".\pipelines"
$global:downloadFolder = "./downloads"
$global:enginePath      = $null
$global:containerEngine = $null

################################################################################
# Function: Install-PipelinesContainer
# Description : Installs (or reinstalls) the Pipelines container from scratch.
# Steps include:
#  - Checking for Git and cloning the repository with LF line endings preserved.
#  - Using the existing Dockerfile with default configuration, or creating one if missing.
#  - Building the custom image.
#  - Running the container with the appropriate environment variables.
################################################################################
function Install-PipelinesContainer {
    Write-Host "Installing Pipelines using pre-built image from ghcr.io/open-webui/pipelines:main"

    # Set the custom image tag to the official pre-built image
    $customPipelineImageTag = "ghcr.io/open-webui/pipelines:main"

    # (Optional) Remove any existing container with the same name
    $existingContainer = & $global:enginePath ps -a --filter "name=$global:containerName" --format "{{.ID}}"
    if ($existingContainer) {
        Write-Host "Pipelines container already exists. Removing it..."
        & $global:enginePath rm --force $global:containerName
    }
	
    Write-Host "Running Pipelines container..."
	$runArgs = @(
        '--detach',                                           # -d : run in background
        '--publish', '9099:9099',                             # -p 9099:9099 : port mapping
        '--add-host', 'host.docker.internal:host-gateway',    # add host mapping for networking
        '--volume', 'pipelines:/app/pipelines',               # volume mapping for persistent data
        '--restart', 'always',                                # restart policy
        '--name', $global:containerName,                      # container name (e.g. "pipelines")
        $customPipelineImageTag                               # official image tag
    )
	
    & $global:enginePath run @runArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to run the Pipelines container."
        return
    }
    Write-Host "Pipelines container is now running."

    # Wait for the container to initialize, then test connectivity
    Start-Sleep -Seconds 20
    Test-TCPPort -ComputerName "localhost" -Port 9099 -serviceName $global:containerName
    Test-HTTPPort -Uri "http://localhost:9099" -serviceName $global:containerName
}


################################################################################
# Function: Backup-PipelinesContainer
# Description : Backs up the live Pipelines container.
################################################################################
function Backup-PipelinesContainer {
    if (-not $global:enginePath) {
        Write-Error "Engine path not set. Please install the Pipelines container first."
        return
    }
    Backup-ContainerState -Engine $global:enginePath -ContainerName $global:containerName
}

################################################################################
# Function: Restore-PipelinesContainer
# Description : Restores the Pipelines container from backup.
################################################################################
function Restore-PipelinesContainer {
    if (-not $global:enginePath) {
        Write-Error "Engine path not set. Please install the Pipelines container first."
        return
    }
    Restore-ContainerState -Engine $global:enginePath -ContainerName $global:containerName
}

################################################################################
# Function: Uninstall-PipelinesContainer
# Description : Uninstalls (removes) the Pipelines container.
################################################################################
function Uninstall-PipelinesContainer {
    if (-not $global:enginePath) {
        Write-Error "Engine path not set. Nothing to uninstall."
        return
    }
    $existingContainer = & $global:enginePath ps -a --filter "name=$global:containerName" --format "{{.ID}}"
    if ($existingContainer) {
        Write-Host "Removing Pipelines container '$global:containerName'..."
        & $global:enginePath rm --force $global:containerName
        if ($LASTEXITCODE -eq 0) {
            Write-Host "Pipelines container removed successfully."
        } else {
            Write-Error "Failed to remove Pipelines container."
        }
    }
    else {
        Write-Host "No Pipelines container found to remove."
    }
}

################################################################################
# Function: Add-PipelineToContainer
# Description : Add azure_openai_pipeline.py to container.
################################################################################
function Add-PipelineToContainer {
    param(
        # URL of the pipeline file (use the raw URL)
        [string]$PipelineUrl = "https://raw.githubusercontent.com/open-webui/pipelines/main/examples/pipelines/providers/azure_openai_pipeline.py",
        # Destination directory inside the container
        [string]$DestinationDir = "/app/pipelines",
        # Container name (defaults to the global container name)
        [string]$ContainerName = $global:containerName
    )

    $fileName = "azure_openai_pipeline.py"
    # Create a temporary file path for the download (assume $global:downloadFolder is a Windows path)
    $tempFile = Join-Path $global:downloadFolder $fileName
    Write-Host "Downloading pipeline from $PipelineUrl to $tempFile..."
    Invoke-WebRequest -Uri $PipelineUrl -OutFile $tempFile -UseBasicParsing

    # If using Podman, convert the Windows path to WSL path
    if ($global:containerEngine -eq "podman") {
        $hostPath = ConvertTo-WSLPath -winPath $tempFile
    }
    else {
        $hostPath = $tempFile
    }

    Write-Host "Host Path: $hostPath"

    #Write-Host "Removing any existing copy of $fileName in container '$ContainerName'..."
    #& $global:enginePath exec $ContainerName rm -f "$DestinationDir/$fileName"

    Write-Host "Copying downloaded pipeline into container '$ContainerName' at '$DestinationDir'..."
    & $global:enginePath machine ssh "podman cp '$hostPath' '$($ContainerName):$DestinationDir'"

    Write-Host "Restarting container '$ContainerName' to load the new pipeline..."
    & $global:enginePath restart $ContainerName

    # Clean up the temporary file
    Remove-Item $tempFile -Force
    Write-Host "Pipeline added successfully."
	
    Write-Host "Reminder: In Open WebUI settings, set the OpenAI API URL to 'http://host.docker.internal:9099' and API key to '0p3n-w3bu!' if integrating pipelines."
}

# Helper function to convert a Windows absolute path to a WSL path
function ConvertTo-WSLPath {
    param(
        [string]$winPath
    )
    # Ensure the path is absolute using Resolve-Path
    $absPath = (Resolve-Path $winPath).Path
    if ($absPath -match '^([A-Z]):\\(.*)$') {
        $drive = $matches[1].ToLower()
        $pathWithoutDrive = $matches[2]
        # Replace backslashes with forward slashes
        $unixPath = $pathWithoutDrive -replace '\\', '/'
        # Prepend the /mnt/<drive> directory
        return "/mnt/$drive/$unixPath"
    }
    else {
        Write-Warning "Path '$winPath' does not match the expected Windows absolute path format."
        return $absPath
    }
}

################################################################################
# Function: Show-ContainerMenu
# Description : Displays the main menu for Pipelines container operations.
################################################################################
function Show-ContainerMenu {
    Write-Host "==========================================="
    Write-Host "Pipelines Container Menu"
    Write-Host "==========================================="
    Write-Host "1) Install Container"
    Write-Host "2) Backup live container"
    Write-Host "3) Restore container from backup"
    Write-Host "4) Uninstall Container"
    Write-Host "5) Add Azure Pipeline to Container"
    Write-Host "6) Exit menu"
}

################################################################################
# Pick Container Engine BEFORE showing the menu.
################################################################################
$global:containerEngine = Select-ContainerEngine
if ($global:containerEngine -eq "docker") {
    Ensure-Elevated
    $global:enginePath = Get-DockerPath
} else {
    $global:enginePath = Get-PodmanPath
}

################################################################################
# Main Script Execution - Menu Option Loop.
################################################################################
do {
    Show-ContainerMenu
    $choice = Read-Host "Enter your choice (1, 2, 3, 4, 5, or 6)"
    switch ($choice) {
        "1" { Install-PipelinesContainer }
        "2" { Backup-PipelinesContainer }
        "3" { Restore-PipelinesContainer }
        "4" { Uninstall-PipelinesContainer }
		"5" { Add-PipelineToContainer }
        "6" { Write-Host "Exiting menu." }
        default { Write-Host "Invalid selection. Enter 1, 2, 3, 4, 5, or 6." }
    }
    if ($choice -ne "5") {
         Write-Host "`nPress any key to continue..."
         $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
         Clear-Host
    }
} while ($choice -ne "5")