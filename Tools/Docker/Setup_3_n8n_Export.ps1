################################################################################
# File         : Setup_3_n8n_Export.ps1
# Description  : Exports n8n workflows and credentials by copying data from the
#                running n8n container's volume to a local directory.
# Usage        : Run with appropriate permissions. Requires the n8n container
#                to be running. Run as Administrator if using Docker.
################################################################################

# Dot-source the necessary helper function files.
. "$PSScriptRoot\Setup_0_Core.ps1"
. "$PSScriptRoot\Setup_0_ContainerEngine.ps1"

# Ensure the script working directory is set.
Set-ScriptLocation

#############################################
# Global Variables
#############################################
$global:containerName = "n8n"
$global:containerEngine = Select-ContainerEngine
# Exit if no engine was selected
if (-not $global:containerEngine) {
    Write-Warning "No container engine selected. Exiting script."
    exit 1
}
if ($global:containerEngine -eq "docker") {
    Test-AdminPrivilege # Docker often requires elevation
    $global:enginePath = Get-DockerPath
}
else {
    $global:enginePath = Get-PodmanPath
}

# Define source and destination paths
$containerSourcePath = "$($global:containerName):/home/node/.n8n/." # Trailing /. copies content
$localDestinationPath = Join-Path -Path $PSScriptRoot -ChildPath "downloads\n8n_data"

#==============================================================================
# Function: Export-n8nData
#==============================================================================
<#
.SYNOPSIS
    Exports n8n data (workflows, credentials, etc.) from the running container.
.DESCRIPTION
    Checks if the n8n container specified by $global:containerName is running using the
    selected container engine ($global:enginePath). If running, it creates the
    local destination directory ($localDestinationPath) if it doesn't exist.
    Then, it uses the container engine's 'cp' command to copy the contents of
    '/home/node/.n8n' from the container to the local destination directory.
.PARAMETER ContainerEnginePath
    The full path to the container engine executable (docker.exe or podman.exe).
.PARAMETER ContainerName
    The name of the n8n container (e.g., "n8n").
.PARAMETER SourcePathInContainer
    The path inside the container to copy data from (e.g., "n8n:/home/node/.n8n/.").
.PARAMETER DestinationPathLocal
    The local directory path where the data should be exported.
.OUTPUTS
    [void] This function does not return a value but writes status messages.
.EXAMPLE
    Export-n8nData -ContainerEnginePath $global:enginePath -ContainerName $global:containerName -SourcePathInContainer $containerSourcePath -DestinationPathLocal $localDestinationPath
.NOTES
    Requires the target n8n container to be running.
    Uses Write-Host for status messages and Write-Error for errors.
#>
function Export-n8nData {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory = $true)]
        [string]$ContainerEnginePath,

        [Parameter(Mandatory = $true)]
        [string]$ContainerName,

        [Parameter(Mandatory = $true)]
        [string]$SourcePathInContainer,

        [Parameter(Mandatory = $true)]
        [string]$DestinationPathLocal
    )

    # Check if container is running
    Write-Host "Checking status of container '$ContainerName'..."
    $containerId = & $ContainerEnginePath ps --filter "name=$ContainerName" --filter "status=running" --format "{{.ID}}"
    if (-not $containerId) {
        Write-Error "Container '$ContainerName' is not running. Export cannot proceed."
        return
    }
    Write-Host "Container '$ContainerName' is running (ID: $containerId)."

    # Ensure destination directory exists
    if (-not (Test-Path -Path $DestinationPathLocal -PathType Container)) {
        Write-Host "Creating destination directory: $DestinationPathLocal"
        try {
            New-Item -Path $DestinationPathLocal -ItemType Directory -Force -ErrorAction Stop | Out-Null
        }
        catch {
            Write-Error "Failed to create destination directory '$DestinationPathLocal': $_"
            return
        }
    }
    else {
         Write-Host "Destination directory already exists: $DestinationPathLocal"
    }

    # Perform the copy operation
    $targetDescription = "contents of '$SourcePathInContainer' to '$DestinationPathLocal'"
    if ($PSCmdlet.ShouldProcess($targetDescription, "Copy Data from Container")) {
        Write-Host "Attempting to copy data..."
        try {
            & $ContainerEnginePath cp $SourcePathInContainer $DestinationPathLocal 2>&1 | Write-Host # Show output/errors from cp
            if ($LASTEXITCODE -ne 0) {
                 # Throw an exception to be caught below if cp command fails
                throw "Container engine 'cp' command failed with exit code $LASTEXITCODE."
            }
            Write-Host "Successfully exported n8n data to '$DestinationPathLocal'." -ForegroundColor Green
        }
        catch {
            Write-Error "Failed to export n8n data: $_"
            Write-Error "Please ensure the container '$ContainerName' is running and the path '$SourcePathInContainer' is correct."
        }
    }
    else {
        Write-Host "Skipped copying data due to -WhatIf."
    }
}

################################################################################
# Main Script Body
################################################################################

Write-Host "Starting n8n data export..."
Export-n8nData -ContainerEnginePath $global:enginePath `
    -ContainerName $global:containerName `
    -SourcePathInContainer $containerSourcePath `
    -DestinationPathLocal $localDestinationPath

Write-Host "Export script finished."
