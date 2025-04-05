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

# Dot-source the necessary helper function files.
. "$PSScriptRoot\Setup_0_Core.ps1"
. "$PSScriptRoot\Setup_0_Network.ps1"
. "$PSScriptRoot\Setup_0_ContainerEngine.ps1"
. "$PSScriptRoot\Setup_0_BackupRestore.ps1"
. "$PSScriptRoot\Setup_0_ContainerMgmt.ps1"

# Ensure the working directory is set.
Set-ScriptLocation

# Global variables used across functions.
# Note: PSAvoidGlobalVars warnings are ignored here as these are used across menu actions.
$global:containerName   = "pipelines"
$global:volumeName      = "pipelines" # Assuming volume name matches container name
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
    Write-Output "Installing Pipelines using pre-built image from ghcr.io/open-webui/pipelines:main" # Replaced Write-Host

    # Set the custom image tag to the official pre-built image
    $customPipelineImageTag = "ghcr.io/open-webui/pipelines:main"

    # (Optional) Remove any existing container with the same name
    $existingContainer = & $global:enginePath ps -a --filter "name=$($global:containerName)" --format "{{.ID}}"
    if ($existingContainer) {
        Write-Output "Pipelines container already exists. Removing it..." # Replaced Write-Host
        & $global:enginePath rm --force $global:containerName
    }

    Write-Output "Running Pipelines container..." # Replaced Write-Host

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
        '--publish', '9099:9099',                         # port mapping
        '--volume', "$($global:volumeName):/app/pipelines", # volume mapping for persistent data
        '--restart', 'always',                           # restart policy
        '--name', $global:containerName,                 # container name
        $customPipelineImageTag                          # pre-built image tag
    ) + $addHostParams # Add conditional params at the end

    # Command: run
    #   --detach: Run container in background.
    #   --publish: Map host port 9099 to container port 9099.
    #   --add-host: (Docker only) Map host.docker.internal to host gateway IP.
    #   --volume: Mount the named volume for persistent pipeline data.
    #   --restart always: Always restart the container unless explicitly stopped.
    #   --name: Assign a name to the container.
    & $global:enginePath run @runArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to run the Pipelines container."
        return
    }
    Write-Output "Pipelines container is now running." # Replaced Write-Host

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
    Backup-ContainerState -Engine $global:enginePath -ContainerName $global:containerName # This function supports ShouldProcess
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
    Restore-ContainerState -Engine $global:enginePath -ContainerName $global:containerName # This function supports ShouldProcess
}

<#
.SYNOPSIS
    Uninstalls the Pipelines container and optionally the data volume.
.DESCRIPTION
    Uses the generic Remove-ContainerAndVolume function.
#>
function Uninstall-PipelinesContainer {
    Remove-ContainerAndVolume -Engine $global:enginePath -ContainerName $global:containerName -VolumeName $global:volumeName # This function supports ShouldProcess
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
    Write-Output "Downloading pipeline from $PipelineUrl to $tempFile..." # Replaced Write-Host
    Invoke-WebRequest -Uri $PipelineUrl -OutFile $tempFile -UseBasicParsing

    # If using Podman, convert the Windows path to WSL path
    if ($global:containerEngine -eq "podman") {
        $hostPath = ConvertTo-WSLPath -winPath $tempFile
    }
    else {
        $hostPath = $tempFile
    }

    Write-Output "Host Path: $hostPath" # Replaced Write-Host

    #Write-Output "Removing any existing copy of $fileName in container '$ContainerName'..." # Replaced Write-Host
    #& $global:enginePath exec $ContainerName rm -f "$DestinationDir/$fileName"

    Write-Output "Copying downloaded pipeline into container '$ContainerName' at '$DestinationDir'..." # Replaced Write-Host
    & $global:enginePath machine ssh "podman cp '$hostPath' '$($ContainerName):$DestinationDir'"

    Write-Output "Restarting container '$ContainerName' to load the new pipeline..." # Replaced Write-Host
    & $global:enginePath restart $ContainerName

    # Clean up the temporary file
    Remove-Item $tempFile -Force
    Write-Output "Pipeline added successfully." # Replaced Write-Host

    Write-Output "Reminder: In Open WebUI settings, set the OpenAI API URL to 'http://host.docker.internal:9099' and API key to '0p3n-w3bu!' if integrating pipelines." # Replaced Write-Host
}

<#
.SYNOPSIS
    Updates the Pipelines container.
.DESCRIPTION
    Stops and removes any existing container, pulls the latest image from ghcr.io/open-webui/pipelines:main,
    and then reinstalls the container.
#>
function Update-PipelinesContainer {
    [CmdletBinding(SupportsShouldProcess=$true)] # Added SupportsShouldProcess
    param()

    Write-Output "Initiating update for Pipelines container..." # Replaced Write-Host

    # Check and remove any current running instance of the container.
    $existingContainer = & $global:enginePath ps -a --filter "name=$($global:containerName)" --format "{{.ID}}"
    if ($existingContainer) {
        if ($PSCmdlet.ShouldProcess($global:containerName, "Remove Existing Container")) {
            Write-Output "Removing existing container '$($global:containerName)' as part of the update..." # Replaced Write-Host
            # Remove container command:
            # rm         Remove one or more containers.
            # --force    Force removal of a running container.
            & $global:enginePath rm --force $global:containerName
            if ($LASTEXITCODE -ne 0) {
                Write-Error "Failed to remove container '$($global:containerName)'. Update aborted."
                return
            }
        } else {
             Write-Output "Skipping removal of existing container due to -WhatIf."
             # Decide if update should proceed without removal? For now, let's abort.
             Write-Warning "Update cannot proceed without removing the existing container."
             return
        }
    }

    if ($PSCmdlet.ShouldProcess("ghcr.io/open-webui/pipelines:main", "Pull Latest Image")) {
        Write-Output "Pulling the latest image..." # Replaced Write-Host
        & $global:enginePath pull ghcr.io/open-webui/pipelines:main
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Failed to pull the latest Pipelines image. Update aborted."
            return
        }
    } else {
        Write-Output "Skipping image pull due to -WhatIf."
        # Decide if update should proceed without pulling? For now, let's abort.
        Write-Warning "Update cannot proceed without pulling the latest image."
        return
    }

    # Reinstall container (Install function handles ShouldProcess internally if needed)
    Install-PipelinesContainer
}

<#
.SYNOPSIS
    Updates the user data for the Pipelines container.
.DESCRIPTION
    This functionality is not implemented.
#>
function Update-PipelinesUserData {
    [CmdletBinding(SupportsShouldProcess=$true)] # Added SupportsShouldProcess
    param()

    # No actions to wrap with ShouldProcess as it's not implemented
    Write-Output "Update User Data functionality is not implemented for Pipelines container." # Replaced Write-Host
}

<#
.SYNOPSIS
    Displays the main menu for container operations.
.DESCRIPTION
    Shows required menu items (1 to 4) and optional items (A, B, C), plus an exit option (0).
#>
function Show-ContainerMenu {
    Write-Output "===========================================" # Replaced Write-Host
    Write-Output "Pipelines Container Menu" # Replaced Write-Host
    Write-Output "===========================================" # Replaced Write-Host
    Write-Output "1. Install container" # Replaced Write-Host
    Write-Output "2. Uninstall container" # Replaced Write-Host
    Write-Output "3. Backup Live container" # Replaced Write-Host
    Write-Output "4. Restore Live container" # Replaced Write-Host
    Write-Output "A. Add Azure Pipeline to Container" # Replaced Write-Host
    Write-Output "B. Update System" # Replaced Write-Host
    Write-Output "C. Update User Data" # Replaced Write-Host
    Write-Output "0. Exit menu" # Replaced Write-Host
}

################################################################################
# Main Menu Loop using Generic Function
################################################################################
$menuActions = @{
    "1" = { Install-PipelinesContainer }
    "2" = { Uninstall-PipelinesContainer }
    "3" = { Backup-PipelinesContainer }
    "4" = { Restore-PipelinesContainer }
    "A" = { Add-PipelineToContainer }
    "B" = { Update-PipelinesContainer }
    "C" = { Update-PipelinesUserData }
}

Invoke-MenuLoop -ShowMenuScriptBlock ${function:Show-ContainerMenu} -ActionMap $menuActions -ExitChoice "0"
