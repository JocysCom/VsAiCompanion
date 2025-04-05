################################################################################
# File         : Setup_0_BackupRestore.ps1
# Description  : Contains container image backup/restore functions:
#                - Backup-ContainerImage: Backup a single image.
#                - Backup-ContainerImages: Backup all images.
#                - Restore-ContainerImage: Restore a single image.
#                - Run-RestoredContainer: Helper to run a restored image.
#                - Restore-ContainerImages: Restore all images from a folder.
#                - Check-AndRestoreBackup: Check for backup and prompt to restore.
# Usage        : Dot-source this script in other setup scripts:
#                . "$PSScriptRoot\Setup_0_BackupRestore.ps1"
################################################################################

#------------------------------
# Function: Backup-ContainerImage
# Description: Backs up a single container image to a tar file.
# Parameters:
#   -Engine: Path to the container engine (docker or podman)
#   -ImageName: Name of the image to backup
#   -BackupFolder: Folder to store the backup file (default ".\Backup")
#------------------------------
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
        Write-Output "Created backup folder: $BackupFolder"
    }

    # Replace characters not allowed in file names (':' and '/' become '_')
    $safeName = $ImageName -replace "[:/]", "_"
    $backupFile = Join-Path $BackupFolder "$safeName.tar"

    Write-Output "Backing up image '$ImageName' to '$backupFile'..."
    # podman save [options] IMAGE
    # save      Save an image to a tar archive.
    # --output string   Specify the output file for saving the image.
    & $Engine save --output $backupFile $ImageName

    if ($LASTEXITCODE -eq 0) {
        Write-Output "Successfully backed up image '$ImageName'"
        return $true
    }
    else {
        Write-Error "Failed to backup image '$ImageName'"
        return $false
    }
}

#------------------------------
# Function: Backup-ContainerImages
# Description: Backs up all container images to tar files.
# Parameters:
#   -Engine: Path to the container engine (docker or podman)
#   -BackupFolder: Folder to store the backup files (default ".\Backup")
#------------------------------
function Backup-ContainerImages {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory=$true)]
        [string]$Engine,

        [string]$BackupFolder = ".\Backup"
    )

    Write-Output "Retrieving list of images for $Engine..."
    $images = & $Engine images --format "{{.Repository}}:{{.Tag}}" | Where-Object { $_ -ne "<none>:<none>" }

    if (-not $images) {
        Write-Output "No images found for $Engine."
        return $false
    }

    $successCount = 0
    foreach ($image in $images) {
        if (Backup-ContainerImage -Engine $Engine -ImageName $image -BackupFolder $BackupFolder) {
            $successCount++
        }
    }

    Write-Output "Backed up $successCount out of $($images.Count) images."
    return ($successCount -gt 0)
}

#------------------------------
# Function: Restore-ContainerImage
# Description: Restores a container image from a tar file.
# Parameters:
#   -Engine: Path to the container engine (docker or podman)
#   -BackupFile: Path to the backup tar file
#   -RunContainer: Whether to run a container from the restored image
#------------------------------
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

    Write-Output "Restoring image from '$BackupFile'..."
    # podman load [options]
    # load       Load an image from a tar archive.
    # --input string   Specify the input file containing the saved image.
    $output = & $Engine load --input $BackupFile

    if ($LASTEXITCODE -eq 0) {
        Write-Output "Successfully restored image from '$BackupFile'."

        # Attempt to parse the image name from the load output
        # Expected output example: "Loaded image: docker.io/open-webui/pipelines:custom"
        $imageName = $null
        if ($output -match "Loaded image:\s*(\S+)") {
            $imageName = $matches[1].Trim()
            Write-Output "Parsed image name: $imageName"

            if ($RunContainer) {
                Start-RestoredContainer -Engine $Engine -ImageName $imageName
            }
            return $true
        }
        else {
            Write-Output "Could not parse image name from the load output."
            return $true
        }
    }
    else {
        Write-Error "Failed to restore image from '$BackupFile'."
        return $false
    }
}

#------------------------------
# Function: Start-RestoredContainer
# Description: Starts a container from a restored image.
# Parameters:
#   -Engine: Path to the container engine (docker or podman)
#   -ImageName: Name of the image to run
#------------------------------
function Start-RestoredContainer {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory=$true)]
        [string]$Engine,

        [Parameter(Mandatory=$true)]
        [string]$ImageName
    )

    # Generate a container name by replacing ':' and '/' with underscores
    $containerName = ($ImageName -replace "[:/]", "_") + "_container"

    Write-Output "Starting container from image '$ImageName' with container name '$containerName'..."
    # podman run [options] IMAGE [COMMAND [ARG...]]
    # run         Run a command in a new container.
    # --detach    Run container in background and print container ID.
    # --name      Assign a name to the container.
    & $Engine run --detach --name $containerName $ImageName

    if ($LASTEXITCODE -eq 0) {
        Write-Output "Container '$containerName' started successfully."
        return $true
    }
    else {
        Write-Error "Failed to start container from image '$ImageName'."
        return $false
    }
}

#------------------------------
# Function: Restore-ContainerImages
# Description: Restores all container images from tar files in a folder.
# Parameters:
#   -Engine: Path to the container engine (docker or podman)
#   -BackupFolder: Folder containing the backup tar files (default ".\Backup")
#   -RunContainers: Whether to run containers from the restored images
#------------------------------
function Restore-ContainerImages {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory=$true)]
        [string]$Engine,

        [string]$BackupFolder = ".\Backup",

        [switch]$RunContainers = $false
    )

    if (-not (Test-Path $BackupFolder)) {
        Write-Output "Backup folder '$BackupFolder' does not exist. Nothing to restore."
        return $false
    }

    $tarFiles = Get-ChildItem -Path $BackupFolder -Filter "*.tar"
    if (-not $tarFiles) {
        Write-Output "No backup tar files found in '$BackupFolder'."
        return $false
    }

    $successCount = 0
    foreach ($file in $tarFiles) {
        if (Restore-ContainerImage -Engine $Engine -BackupFile $file.FullName -RunContainer:$RunContainers) {
            $successCount++
        }
    }

    Write-Output "Restored $successCount out of $($tarFiles.Count) images."
    return ($successCount -gt 0)
}

#------------------------------
# Function: Test-AndRestoreBackup
# Description: Checks if a backup exists for an image and offers to restore it.
# Parameters:
#   -Engine: Path to the container engine (docker or podman)
#   -ImageName: Name of the image to check for backup
#   -BackupFolder: Folder containing the backup tar files (default ".\Backup")
#------------------------------
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
        Write-Output "No backup file found for image '$ImageName' in folder '$BackupFolder'."
        return $false
    }

    Write-Output "Backup file found for image '$ImageName': $backupFile"
    $choice = Read-Host "Do you want to restore the backup for '$ImageName'? (Y/N, default N)"
    if ($choice -and $choice.ToUpper() -eq "Y") {
        return (Restore-ContainerImage -Engine $Engine -BackupFile $backupFile)
    }
    else {
        Write-Output "User opted not to restore backup for image '$ImageName'."
        return $false
    }
}
