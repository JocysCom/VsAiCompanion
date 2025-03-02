################################################################################
# File         : Setup_2a_Pipelines.ps1
# Description  : Script to set up, update, backup, restore, uninstall the Pipelines
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
$global:downloadFolder  = ".\downloads"
$global:enginePath      = $null
$global:containerEngine = $null

# After the dot-sourcing of Setup_0.ps1, add this code to ensure the engine is selected:
$global:containerEngine = Select-ContainerEngine
if ($global:containerEngine -eq "docker") {
    $global:enginePath = Get-DockerPath
} elseif ($global:containerEngine -eq "podman") {
    $global:enginePath = Get-PodmanPath
} else {
    Write-Error "No container engine (Docker or Podman) found. Please install one before running this script."
    exit 1
}

# Validate that we have a valid engine path
if (-not $global:enginePath) {
    Write-Error "Failed to get path to container engine executable. Exiting."
    exit 1
}

<#
.SYNOPSIS
   Converts a Windows path into a WSL (Linux) path.
.DESCRIPTION
   This function takes an absolute Windows path and converts it to the corresponding WSL
   path by replacing the drive letter and backslashes with the Linux mount point format.
   IMPORTANT: This workaround is CRUCIAL for successfully copying a file from the local 
   machine to Podman.
.PARAMETER winPath
   The Windows path to convert.
#>
function ConvertTo-WSLPath {
    param(
        [Parameter(Mandatory=$true)]
        [string]$winPath
    )
    $absPath = (Resolve-Path $winPath).Path
    if ($absPath -match '^([A-Z]):\\(.*)$') {
        $drive = $matches[1].ToLower()
        $pathWithoutDrive = $matches[2]
        $unixPath = $pathWithoutDrive -replace '\\', '/'
        return "/mnt/$drive/$unixPath"
    }
    else {
        Write-Warning "Path '$winPath' does not match the expected Windows absolute path format."
        return $absPath
    }
}

<#
.SYNOPSIS
    Installs (or reinstalls) the Pipelines container from scratch.
.DESCRIPTION
    Installs Pipelines using the pre-built image from ghcr.io/open-webui/pipelines:main.
    Optionally removes any existing container with the same name.
    If running Docker, adds the '--add-host' parameter for host resolution; for Podman, skips it.
    After running the container, waits for startup and tests connectivity.
#>
function Install-PipelinesContainer {
    Write-Host "Installing Pipelines using pre-built image from ghcr.io/open-webui/pipelines:main"

    # Set the custom image tag to the official pre-built image
    $customPipelineImageTag = "ghcr.io/open-webui/pipelines:main"

    # (Optional) Remove any existing container with the same name
    $existingContainer = & $global:enginePath ps -a --filter "name=$($global:containerName)" --format "{{.ID}}"
    if ($existingContainer) {
        Write-Host "Pipelines container already exists. Removing it..."
        & $global:enginePath rm --force $global:containerName
    }
    
    Write-Host "Running Pipelines container..."

    # Conditionally set the --add-host parameter if using Docker
    if ($global:containerEngine -eq "docker") {
        $addHostParams = @('--add-host', 'host.docker.internal:host-gateway')
    }
    else {
        # For Podman, skip the --add-host parameter (or add an alternative if required)
        $addHostParams = @()
    }

    # Build the run arguments array
    $runArgs = @(
        '--detach',                                      # run in background
        '--publish', '9099:9099'                          # port mapping
    ) + $addHostParams + @(
        '--volume', 'pipelines:/app/pipelines',          # volume mapping for persistent data
        '--restart', 'always',                           # restart policy
        '--name', $global:containerName,                 # container name
        $customPipelineImageTag                          # pre-built image tag
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

<#
.SYNOPSIS
    Backs up the live Pipelines container.
.DESCRIPTION
    Uses the Backup-ContainerState helper function to backup the container.
    Aborts if the engine path has not been set.
#>
function Backup-PipelinesContainer {
    if (-not $global:enginePath) {
        Write-Error "Engine path not set. Please install the Pipelines container first."
        return
    }
    Backup-ContainerState -Engine $global:enginePath -ContainerName $global:containerName
}

<#
.SYNOPSIS
    Restores the Pipelines container from backup.
.DESCRIPTION
    Uses the Restore-ContainerState helper function to restore the container.
    Aborts if the engine path has not been set.
#>
function Restore-PipelinesContainer {
    if (-not $global:enginePath) {
        Write-Error "Engine path not set. Please install the Pipelines container first."
        return
    }
    Restore-ContainerState -Engine $global:enginePath -ContainerName $global:containerName
}

<#
.SYNOPSIS
    Uninstalls (removes) the Pipelines container.
.DESCRIPTION
    Checks if the engine path is set and removes the container if it exists.
#>
function Uninstall-PipelinesContainer {
    if (-not $global:enginePath) {
        Write-Error "Engine path not set. Nothing to uninstall."
        return
    }
    $existingContainer = & $global:enginePath ps -a --filter "name=$($global:containerName)" --format "{{.ID}}"
    if ($existingContainer) {
        Write-Host "Removing Pipelines container '$($global:containerName)'..."
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

<#
.SYNOPSIS
    Adds the Azure pipeline file to the container.
.PARAMETER PipelineUrl
    URL of the pipeline file (use the raw URL).
.PARAMETER DestinationDir
    Destination directory inside the container.
.PARAMETER ContainerName
    Container name. Defaults to the global container name.
.DESCRIPTION
    Downloads the azure_openai_pipeline.py file, converts the Windows path to a WSL path when using Podman,
    copies the file into the container, restarts the container, and cleans up the temporary file.
    IMPORTANT: Workaround for copying file to Podman is preserved.
#>
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

<#
.SYNOPSIS
    Updates the Pipelines container.
.DESCRIPTION
    Stops and removes any existing container, pulls the latest image from ghcr.io/open-webui/pipelines:main,
    and then reinstalls the container.
#>
function Update-PipelinesContainer {
    Write-Host "Initiating update for Pipelines container..."

    # Check and remove any current running instance of the container.
    $existingContainer = & $global:enginePath ps -a --filter "name=$($global:containerName)" --format "{{.ID}}"
    if ($existingContainer) {
        Write-Host "Removing existing container '$($global:containerName)' as part of the update..."
        # Remove container command:
        # rm         Remove one or more containers.
        # --force    Force removal of a running container.
        & $global:enginePath rm --force $global:containerName
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Failed to remove container '$($global:containerName)'. Update aborted."
            return
        }
    }
    
    Write-Host "Pulling the latest image..."
    & $global:enginePath pull ghcr.io/open-webui/pipelines:main
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to pull the latest Pipelines image. Update aborted."
        return
    }
    
    Install-PipelinesContainer
}

<#
.SYNOPSIS
    Updates the user data for the Pipelines container.
.DESCRIPTION
    This functionality is not implemented.
#>
function Update-PipelinesUserData {
    Write-Host "Update User Data functionality is not implemented for Pipelines container."
}

<#
.SYNOPSIS
    Displays the main menu for container operations.
.DESCRIPTION
    Shows required menu items (1 to 4) and optional items (A, B, C), plus an exit option (0).
#>
function Show-ContainerMenu {
    Write-Host "==========================================="
    Write-Host "Container Menu"
    Write-Host "==========================================="
    Write-Host "1. Install container"
    Write-Host "2. Uninstall container"
    Write-Host "3. Backup Live container"
    Write-Host "4. Restore Live container"
    Write-Host "A. Add Azure Pipeline to Container"
    Write-Host "B. Update System"
    Write-Host "C. Update User Data"
    Write-Host "0. Exit menu"
}

################################################################################
# Main Menu Loop for Pipelines Container Management
################################################################################
do {
    Show-ContainerMenu
    $choice = Read-Host "Enter your choice (1, 2, 3, 4, 5, or 6)"
    switch ($choice) {
        "1" { Install-PipelinesContainer }
        "2" { Uninstall-PipelinesContainer }
        "3" { Backup-PipelinesContainer }
        "4" { Restore-PipelinesContainer }
        "A" { Add-PipelineToContainer }
        "B" { Update-PipelinesContainer }
        "C" { Update-PipelinesUserData }
        "0" { Write-Host "Exiting menu." }
        default { Write-Host "Invalid selection. Enter 1, 2, 3, 4, 5, or 6." }
    }
    if ($choice -ne "0") {
         Write-Host "`nPress any key to continue..."
         $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
         Clear-Host
    }
} while ($choice -ne "0")
