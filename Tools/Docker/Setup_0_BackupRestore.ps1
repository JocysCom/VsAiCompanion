################################################################################
# File         : Setup_0_BackupRestore.ps1
# Description  : Contains container image backup/restore functions:
#                - Backup-ContainerImage: Backup a single image.
#                - Invoke-ContainerImageBackup: Backup all images. (Formerly Backup-ContainerImages)
#                - Restore-ContainerImage: Restore a single image.
#                - Start-RestoredContainer: Helper to run a restored image.
#                - Invoke-ContainerImageRestore: Restore all images from a folder. (Formerly Restore-ContainerImages)
#                - Test-AndRestoreBackup: Check for backup and prompt to restore.
# Usage        : Dot-source this script in other setup scripts:
#                . "$PSScriptRoot\Setup_0_BackupRestore.ps1"
################################################################################

#==============================================================================
# Function: Backup-ContainerImage
#==============================================================================
<#
.SYNOPSIS
    Backs up a single container image to a tar file.
.DESCRIPTION
    Saves a specified container image using the provided container engine (docker or podman)
    to a .tar file in the specified backup folder. The filename is derived from the image name,
    replacing ':' and '/' with '_'. Creates the backup folder if it doesn't exist.
.PARAMETER Engine
    Path to the container engine executable (e.g., 'docker' or 'podman'). Mandatory.
.PARAMETER ImageName
    The full name and tag of the container image to back up (e.g., 'nginx:latest'). Mandatory.
.PARAMETER BackupFolder
    The directory where the backup .tar file will be saved. Defaults to '.\Backup'.
.OUTPUTS
    [bool] Returns $true on success, $false on failure.
.EXAMPLE
    Backup-ContainerImage -Engine "podman" -ImageName "docker.io/library/alpine:latest" -BackupFolder "C:\MyBackups"
.NOTES
    Uses Write-Information for status messages.
    Uses $LASTEXITCODE to check the success of the engine's save command.
#>
function Backup-ContainerImage {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory=$true)]
        [string]$Engine,

        [Parameter(Mandatory=$true)]
        [string]$ImageName,

        [string]$BackupFolder = ".\Backup"
    )

    if (-not (Test-Path $BackupFolder)) {
        New-Item -ItemType Directory -Force -Path $BackupFolder | Out-Null
        # Use Write-Information for status messages
        Write-Information "Created backup folder: $BackupFolder"
    }

    # Replace characters not allowed in file names (':' and '/' become '_')
    $safeName = $ImageName -replace "[:/]", "_"
    $backupFile = Join-Path $BackupFolder "$safeName.tar"

    # Use Write-Information for status messages
    Write-Information "Backing up image '$ImageName' to '$backupFile'..."
    # podman save [options] IMAGE
    # save      Save an image to a tar archive.
    # --output string   Specify the output file for saving the image.
    & $Engine save --output $backupFile $ImageName

    if ($LASTEXITCODE -eq 0) {
        # Use Write-Information for status messages
        Write-Information "Successfully backed up image '$ImageName'"
        return $true
    }
    else {
        Write-Error "Failed to backup image '$ImageName'"
        return $false
    }
}

#==============================================================================
# Function: Invoke-ContainerImageBackup
#==============================================================================
<#
.SYNOPSIS
    Backs up all available container images using the specified engine.
.DESCRIPTION
    Retrieves a list of all images known to the container engine (excluding <none>:<none>).
    For each image found, it calls the Backup-ContainerImage function to save it to a .tar file
    in the specified backup folder.
.PARAMETER Engine
    Path to the container engine executable (e.g., 'docker' or 'podman'). Mandatory.
.PARAMETER BackupFolder
    The directory where the backup .tar files will be saved. Defaults to '.\Backup'.
.OUTPUTS
    [bool] Returns $true if at least one image was successfully backed up, $false otherwise.
.EXAMPLE
    Invoke-ContainerImageBackup -Engine "docker" -BackupFolder "D:\DockerBackups"
.NOTES
    Uses Write-Information for status messages.
    Relies on the output format of 'engine images --format "{{.Repository}}:{{.Tag}}"''.
    Formerly named Backup-ContainerImages.
#>
function Invoke-ContainerImageBackup {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory=$true)]
        [string]$Engine,

        [string]$BackupFolder = ".\Backup"
    )

    # Use Write-Information for status messages
    Write-Information "Retrieving list of images for $Engine..."
    $images = & $Engine images --format "{{.Repository}}:{{.Tag}}" | Where-Object { $_ -ne "<none>:<none>" }

    if (-not $images) {
        # Use Write-Information for status messages
        Write-Information "No images found for $Engine."
        return $false
    }

    $successCount = 0
    foreach ($image in $images) {
        # Call the singular version
        if (Backup-ContainerImage -Engine $Engine -ImageName $image -BackupFolder $BackupFolder) {
            $successCount++
        }
    }

    # Use Write-Information for status messages
    Write-Information "Backed up $successCount out of $($images.Count) images."
    return ($successCount -gt 0)
}

#==============================================================================
# Function: Restore-ContainerImage
#==============================================================================
<#
.SYNOPSIS
    Restores a container image from a backup .tar file.
.DESCRIPTION
    Loads a container image from the specified .tar backup file using the provided container engine.
    If the load is successful and the -RunContainer switch is provided, it attempts to parse the
    loaded image name from the engine's output and then calls Start-RestoredContainer.
.PARAMETER Engine
    Path to the container engine executable (e.g., 'docker' or 'podman'). Mandatory.
.PARAMETER BackupFile
    The full path to the .tar file containing the image backup. Mandatory.
.PARAMETER RunContainer
    Switch parameter. If present, attempts to start a container from the restored image. Defaults to $false.
.OUTPUTS
    [bool] Returns $true if the image was loaded successfully, $false otherwise. Note: Success is based on loading,
           not necessarily on parsing the name or starting the container.
.EXAMPLE
    Restore-ContainerImage -Engine "podman" -BackupFile "C:\MyBackups\alpine_latest.tar"
.EXAMPLE
    Restore-ContainerImage -Engine "docker" -BackupFile "D:\DockerBackups\nginx_latest.tar" -RunContainer
.NOTES
    Uses Write-Information for status messages.
    Relies on the output format of 'engine load --input ...' to parse the image name (e.g., "Loaded image: ...").
    Uses $LASTEXITCODE to check the success of the engine's load command.
#>
function Restore-ContainerImage {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory=$true)]
        [string]$Engine,

        [Parameter(Mandatory=$true)]
        [string]$BackupFile,

        [switch]$RunContainer = $false
    )

    if (-not (Test-Path $BackupFile)) {
        Write-Error "Backup file '$BackupFile' not found."
        return $false
    }

    # Use Write-Information for status messages
    Write-Information "Restoring image from '$BackupFile'..."
    # podman load [options]
    # load       Load an image from a tar archive.
    # --input string   Specify the input file containing the saved image.
    $output = & $Engine load --input $BackupFile

    if ($LASTEXITCODE -eq 0) {
        # Use Write-Information for status messages
        Write-Information "Successfully restored image from '$BackupFile'."

        # Attempt to parse the image name from the load output
        # Expected output example: "Loaded image: docker.io/open-webui/pipelines:custom"
        $imageName = $null
        if ($output -match "Loaded image:\s*(\S+)") {
            $imageName = $matches[1].Trim()
            # Use Write-Information for status messages
            Write-Information "Parsed image name: $imageName"

            if ($RunContainer) {
                Start-RestoredContainer -Engine $Engine -ImageName $imageName
            }
            return $true
        }
        else {
            # Use Write-Information for status messages
            Write-Information "Could not parse image name from the load output."
            # Still return true as the image was loaded, just couldn't parse name
            return $true
        }
    }
    else {
        Write-Error "Failed to restore image from '$BackupFile'."
        return $false
    }
}

#==============================================================================
# Function: Start-RestoredContainer
#==============================================================================
<#
.SYNOPSIS
    Starts a detached container from a specified image.
.DESCRIPTION
    Runs a new container in detached mode using the provided engine and image name.
    Generates a container name based on the image name (replacing ':' and '/' with '_')
    and appending '_container'. Supports -WhatIf via CmdletBinding.
.PARAMETER Engine
    Path to the container engine executable (e.g., 'docker' or 'podman'). Mandatory.
.PARAMETER ImageName
    The full name and tag of the container image to run (e.g., 'nginx:latest'). Mandatory.
.OUTPUTS
    [bool] Returns $true if the container starts successfully (or if skipped due to -WhatIf), $false on failure.
.EXAMPLE
    Start-RestoredContainer -Engine "podman" -ImageName "docker.io/library/alpine:latest"
.NOTES
    Uses Write-Information for status messages.
    Uses $LASTEXITCODE to check the success of the engine's run command.
#>
function Start-RestoredContainer {
    [CmdletBinding(SupportsShouldProcess=$true)]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory=$true)]
        [string]$Engine,

        [Parameter(Mandatory=$true)]
        [string]$ImageName
    )

    # Generate a container name by replacing ':' and '/' with underscores
    $containerName = ($ImageName -replace "[:/]", "_") + "_container"

    # Use Write-Information for status messages
    Write-Information "Starting container from image '$ImageName' with container name '$containerName'..."

    # Check if the action should be performed
    if ($PSCmdlet.ShouldProcess("container '$containerName' from image '$ImageName'", "Start")) {
        # podman run [options] IMAGE [COMMAND [ARG...]]
        # run         Run a command in a new container.
        # --detach    Run container in background and print container ID.
        # --name      Assign a name to the container.
        & $Engine run --detach --name $containerName $ImageName

        if ($LASTEXITCODE -eq 0) {
            # Use Write-Information for status messages
            Write-Information "Container '$containerName' started successfully."
            return $true
        }
        else {
            Write-Error "Failed to start container from image '$ImageName'."
            return $false
        }
    } # Closing brace for ShouldProcess block
    else {
        # If ShouldProcess returns false (e.g., user chose "No" or used -WhatIf)
        Write-Information "Skipped starting container '$containerName' due to ShouldProcess."
        return $false
    }
} # Closing brace for function Start-RestoredContainer

#==============================================================================
# Function: Invoke-ContainerImageRestore
#==============================================================================
<#
.SYNOPSIS
    Restores all container images found in .tar files within a specified backup folder.
.DESCRIPTION
    Searches the specified backup folder for files ending in '.tar'. For each file found,
    it calls the Restore-ContainerImage function. If the -RunContainers switch is provided,
    it's passed along to Restore-ContainerImage.
.PARAMETER Engine
    Path to the container engine executable (e.g., 'docker' or 'podman'). Mandatory.
.PARAMETER BackupFolder
    The directory containing the .tar backup files. Defaults to '.\Backup'.
.PARAMETER RunContainers
    Switch parameter. If present, attempts to start containers from the restored images. Defaults to $false.
.OUTPUTS
    [bool] Returns $true if at least one image was successfully restored, $false otherwise.
.EXAMPLE
    Invoke-ContainerImageRestore -Engine "docker" -BackupFolder "D:\DockerBackups"
.EXAMPLE
    Invoke-ContainerImageRestore -Engine "podman" -RunContainers
.NOTES
    Uses Write-Information for status messages.
    Formerly named Restore-ContainerImages.
#>
function Invoke-ContainerImageRestore {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory=$true)]
        [string]$Engine,

        [string]$BackupFolder = ".\Backup",

        [switch]$RunContainers = $false
    )

    if (-not (Test-Path $BackupFolder)) {
        # Use Write-Information for status messages
        Write-Information "Backup folder '$BackupFolder' does not exist. Nothing to restore."
        return $false
    }

    $tarFiles = Get-ChildItem -Path $BackupFolder -Filter "*.tar"
    if (-not $tarFiles) {
        # Use Write-Information for status messages
        Write-Information "No backup tar files found in '$BackupFolder'."
        return $false
    }

    $successCount = 0
    foreach ($file in $tarFiles) {
        # Call the singular version
        if (Restore-ContainerImage -Engine $Engine -BackupFile $file.FullName -RunContainer:$RunContainers) {
            $successCount++
        }
    }

    # Use Write-Information for status messages
    Write-Information "Restored $successCount out of $($tarFiles.Count) images."
    return ($successCount -gt 0)
}

#==============================================================================
# Function: Test-AndRestoreBackup
#==============================================================================
<#
.SYNOPSIS
    Checks if a backup file exists for a specific image and prompts the user to restore it.
.DESCRIPTION
    Constructs the expected backup filename based on the ImageName (replacing ':' and '/' with '_')
    within the specified BackupFolder. If the file exists, it prompts the user via Read-Host
    whether they want to restore it. If the user confirms ('Y'), it calls Restore-ContainerImage.
.PARAMETER Engine
    Path to the container engine executable (e.g., 'docker' or 'podman'). Mandatory.
.PARAMETER ImageName
    The full name and tag of the container image to check for a backup (e.g., 'nginx:latest'). Mandatory.
.PARAMETER BackupFolder
    The directory where the backup .tar file is expected. Defaults to '.\Backup'.
.OUTPUTS
    [bool] Returns $true if the user chose to restore AND the restore was successful, $false otherwise
           (backup not found, user declined, or restore failed).
.EXAMPLE
    Test-AndRestoreBackup -Engine "podman" -ImageName "docker.io/library/alpine:latest"
.NOTES
    Uses Write-Information for status messages.
    User interaction is handled via Read-Host.
#>
function Test-AndRestoreBackup {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Engine,

        [Parameter(Mandatory = $true)]
        [string]$ImageName,

        [string]$BackupFolder = ".\Backup"
    )

    # Compute the safe backup file name by replacing ':' and '/' with '_'
    $safeName = $ImageName -replace "[:/]", "_"
    $backupFile = Join-Path $BackupFolder "$safeName.tar"

    if (-not (Test-Path $backupFile)) {
        # Use Write-Information for status messages
        Write-Information "No backup file found for image '$ImageName' in folder '$BackupFolder'."
        return $false
    }

    # Use Write-Information for status messages
    Write-Information "Backup file found for image '$ImageName': $backupFile"
    $choice = Read-Host "Do you want to restore the backup for '$ImageName'? (Y/N, default N)"
    if ($choice -and $choice.ToUpper() -eq "Y") {
        # Call the singular version
        return (Restore-ContainerImage -Engine $Engine -BackupFile $backupFile)
    }
    else {
        # Use Write-Information for status messages
        Write-Information "User opted not to restore backup for image '$ImageName'."
        return $false
    }
}
