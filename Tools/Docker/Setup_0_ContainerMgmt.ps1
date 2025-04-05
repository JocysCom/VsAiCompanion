################################################################################
# File         : Setup_0_ContainerMgmt.ps1
# Description  : Contains container management functions:
#                - Backup-ContainerState: Backup a running container's state.
#                - Restore-ContainerState: Restore a container from state backup.
#                - Check-ImageUpdateAvailable: Check for newer image versions.
#                - Update-Container: Generic container update function.
# Usage        : Dot-source this script and potentially Setup_0_BackupRestore.ps1
#                in other setup scripts:
#                . "$PSScriptRoot\Setup_0_BackupRestore.ps1" # For Restore-ContainerState dependency
#                . "$PSScriptRoot\Setup_0_ContainerMgmt.ps1"
################################################################################

#--------------------------------------
# Function: Backup-ContainerState
# Description: Creates a backup of a live running container by committing its state
#              to an image and saving that image as a tar file. Also backs up
#              associated volumes if specified.
# Parameters:
#   -Engine: Path to the container engine (docker or podman)
#   -ContainerName: Name of the container to backup
#   -BackupFolder: Folder to store the backup file (default ".\Backup")
#   -BackupVolumes: Whether to also backup volumes associated with the container
#--------------------------------------
function Backup-ContainerState {
    param(
        [Parameter(Mandatory=$true)]
        [string]$Engine,
        [Parameter(Mandatory=$true)]
        [string]$ContainerName,
        [string]$BackupFolder = ".\Backup"
    )

    if (-not (Test-Path $BackupFolder)) {
         New-Item -ItemType Directory -Path $BackupFolder -Force | Out-Null
         Write-Host "Created backup folder: $BackupFolder"
    }

    # Check if the container exists.
    $existingContainer = & $Engine ps -a --filter "name=^$ContainerName$" --format "{{.ID}}"
    if (-not $existingContainer) {
         Write-Error "Container '$ContainerName' does not exist. Cannot backup."
         return $false
    }

    # Debug output to verify container name
    Write-Host "DEBUG: Container name is '$ContainerName'"

    # Create a simple image tag without any container- prefix that might be causing issues
    $backupImageTag = "backup-$ContainerName"

    Write-Host "Committing container '$ContainerName' to image '$backupImageTag'..."
    # podman commit [OPTIONS] CONTAINER [REPOSITORY[:TAG]]
    # commit    Create a new image from a container's changes.
    & $Engine commit $ContainerName $backupImageTag
    if ($LASTEXITCODE -ne 0) {
         Write-Error "Failed to commit container '$ContainerName'."
         return $false
    }

    # Build backup tar file name.
    $safeName = $ContainerName -replace "[:/]", "_"
    if ($safeName -eq "") {
        $safeName = "unknown"
    }
    $backupFile = Join-Path $BackupFolder "$safeName-backup.tar"

    Write-Host "Saving backup image '$backupImageTag' to '$backupFile'..."
    # podman save [options] IMAGE
    # save      Save an image to a tar archive.
    # --output string   Specify the output file for saving the image.
    & $Engine save --output $backupFile $backupImageTag
    if ($LASTEXITCODE -eq 0) {
         Write-Host "Backup successfully saved to '$backupFile'."
         return $true
    } else {
         Write-Error "Failed to save backup image to '$backupFile'."
         return $false
    }
}

#--------------------------------------
# Function: Restore-ContainerState
# Description: Restores a container from a previously saved backup tar file.
#              Loads the backup image and runs a new container from it.
#              Also restores associated volumes if backup files exist.
# Parameters:
#   -Engine: Path to the container engine (docker or podman)
#   -ContainerName: Name of the container to restore
#   -BackupFolder: Folder where the backup file is located (default ".\Backup")
#   -RestoreVolumes: Whether to also restore volumes associated with the container
#--------------------------------------
function Restore-ContainerState {
    param(
        [Parameter(Mandatory=$true)]
        [string]$Engine,
        [Parameter(Mandatory=$true)]
        [string]$ContainerName,
        [string]$BackupFolder = ".\Backup",
        [switch]$RestoreVolumes = $true
    )

    # First try the container-specific backup format
    $safeName = $ContainerName -replace "[:/]", "_"
    $backupFile = Join-Path $BackupFolder "$safeName-backup.tar"

    # If container-specific backup not found, try to find a matching image backup
    if (-not (Test-Path $backupFile)) {
        Write-Host "Container-specific backup file '$backupFile' not found."
        Write-Host "Looking for image backups that might match this container..."

        # Get all tar files in the backup folder
        $tarFiles = Get-ChildItem -Path $BackupFolder -Filter "*.tar"

        # Try to find a matching image backup
        $matchingBackup = $null
        foreach ($file in $tarFiles) {
            # Extract potential image name from filename (remove .tar extension)
            $potentialImageName = $file.Name -replace '\.tar$', ''

            # Check if this backup file might be for the container we're looking for
            if ($potentialImageName -match $ContainerName) {
                $matchingBackup = $file.FullName
                Write-Host "Found potential matching backup: $matchingBackup"
                break
            }
        }

        if ($matchingBackup) {
            $backupFile = $matchingBackup
        } else {
            Write-Error "No backup file found for container '$ContainerName'."
            return $false
        }
    }

    Write-Host "Loading backup image from '$backupFile'..."
    # podman load [options]
    # load      Load an image from a tar archive.
    # --input string   Specify the input file containing the saved image.
    $loadOutput = & $Engine load --input $backupFile 2>&1
    if ($LASTEXITCODE -ne 0) {
         Write-Error "Failed to load backup image from '$backupFile'."
         return $false
    }

    # Parse the actual image name from the load output
    $imageName = $null
    if ($loadOutput -match "Loaded image: (.+)") {
        $imageName = $matches[1].Trim()
        Write-Host "Loaded image: $imageName"
    } else {
        Write-Error "Could not determine the loaded image name from output: $loadOutput"
        return $false
    }

    # If RestoreVolumes is true, check for volume backups
    if ($RestoreVolumes) {
        # For n8n, we know the volume name is "n8n_data"
        if ($ContainerName -eq "n8n") {
            $volumeName = "n8n_data"
            $volumeBackupFile = Join-Path $BackupFolder "$volumeName-data.tar"

            if (Test-Path $volumeBackupFile) {
                Write-Host "Found volume backup for '$volumeName': $volumeBackupFile"

                # Check if volume exists, create if not
                $volumeExists = & $Engine volume ls --filter "name=$volumeName" --format "{{.Name}}"
                if (-not $volumeExists) {
                    Write-Host "Creating volume '$volumeName'..."
                    & $Engine volume create $volumeName
                }

                # Ask for confirmation before restoring volume
                $restoreVolumeConfirm = Read-Host "Restore volume data for '$volumeName'? This will merge with existing data. (Y/N, default is Y)"
                if ($restoreVolumeConfirm -ne "N") {
                    Write-Host "Restoring volume '$volumeName' from '$volumeBackupFile'..."

                    # Create a temporary container to restore the volume data
                    $tempContainerName = "restore-volume-$volumeName-$(Get-Random)"

                    # Run a temporary container with the volume mounted and extract the backup
                    & $Engine run --rm --volume ${volumeName}:/target --volume ${BackupFolder}:/backup --name $tempContainerName alpine tar -xf /backup/$(Split-Path $volumeBackupFile -Leaf) -C /target

                    if ($LASTEXITCODE -eq 0) {
                        Write-Host "Successfully restored volume '$volumeName' from '$volumeBackupFile'" -ForegroundColor Green
                    } else {
                        Write-Error "Failed to restore volume '$volumeName'"
                    }
                } else {
                    Write-Host "Skipping volume restore as requested."
                }
            } else {
                Write-Host "No volume backup found for '$volumeName' at '$volumeBackupFile'."
                Write-Host "Will continue with container image restore only. Existing volume data will be preserved."
            }
        }
        # For other containers, we would need to determine volume names differently
    }

    # Return the loaded image name
    return $imageName
}

#############################################
# Function: Check-ImageUpdateAvailable
# Description: Checks if a newer version of a container image is available
#              from its registry. Works with multiple registries including
#              docker.io, ghcr.io, and others.
# Parameters:
#   -Engine: Path to the container engine (docker or podman)
#   -ImageName: Full image name including registry (e.g., ghcr.io/open-webui/open-webui:main)
# Returns: $true if an update is available, $false otherwise
#############################################
function Check-ImageUpdateAvailable {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)]
        [string]$Engine,

        [Parameter(Mandatory=$true)]
        [string]$ImageName
    )

    Write-Host "Checking for updates to $ImageName..."

    # First, check if we have the image locally
    $localImageInfo = & $Engine inspect $ImageName 2>$null | ConvertFrom-Json
    if (-not $localImageInfo) {
        Write-Host "Image '$ImageName' not found locally. Update is available." -ForegroundColor Yellow
        return $true
    }

    # Get local image digest
    $localDigest = $null
    try {
        if ($localImageInfo -is [array]) {
            $localDigest = $localImageInfo[0].Id
        } else {
            $localDigest = $localImageInfo.Id
        }
    } catch {
        Write-Warning "Could not determine local image digest: $_"
        # If we can't determine local digest, assume update is needed
        return $true
    }

    Write-Host "Local image digest: $localDigest"

    # Determine container engine type (docker or podman)
    $engineType = "docker"
    if ((Get-Item $Engine).Name -like "*podman*") {
        $engineType = "podman"
    }

    # Pull the image with latest tag but don't update the local image
    Write-Host "Checking remote registry for latest version..."

    # Different approach for Docker vs Podman
    if ($engineType -eq "docker") {
        # For Docker, we can use the manifest inspect command
        try {
            $remoteDigest = & $Engine manifest inspect $ImageName --verbose 2>$null | ConvertFrom-Json |
                Select-Object -ExpandProperty Descriptor -ErrorAction SilentlyContinue |
                Select-Object -ExpandProperty digest -ErrorAction SilentlyContinue
        } catch {
            $remoteDigest = $null
            Write-Warning "Error checking remote manifest: $_"
        }

        if (-not $remoteDigest) {
            Write-Warning "Could not determine remote image digest. Using fallback method."
            # Fallback method - pull image info
            & $Engine pull $ImageName 2>&1 | Out-Null
            $remoteImageInfo = & $Engine inspect $ImageName 2>$null | ConvertFrom-Json
            if ($remoteImageInfo -is [array]) {
                $remoteDigest = $remoteImageInfo[0].Id
            } else {
                $remoteDigest = $remoteImageInfo.Id
            }
        }
    } else {
        # For Podman, we need to pull the image to check its digest
        $tempTag = "temp-check-update-$(Get-Random):latest"

        # First try skopeo if available (more efficient)
        $skopeo = Get-Command skopeo -ErrorAction SilentlyContinue
        if ($skopeo) {
            try {
                # Convert docker:// or podman:// prefix if needed
                $skopeoUri = $ImageName
                if (-not $skopeoUri.StartsWith("docker://") -and -not $skopeoUri.StartsWith("podman://")) {
                    $skopeoUri = "docker://$skopeoUri"
                }

                $skopeoOutput = & skopeo inspect $skopeoUri --raw 2>$null
                $skopeoJson = $skopeoOutput | ConvertFrom-Json
                $remoteDigest = $skopeoJson.config.digest
            } catch {
                $remoteDigest = $null
                Write-Warning "Skopeo inspection failed: $_"
            }
        }

        # If skopeo failed or isn't available, fall back to podman pull
        if (-not $remoteDigest) {
            # Use --quiet to avoid downloading the entire image if possible
            & $Engine pull --quiet $ImageName 2>&1 | Out-Null

            # Tag it temporarily to avoid affecting the current image
            & $Engine tag $ImageName $tempTag 2>&1 | Out-Null

            # Get the digest
            $remoteImageInfo = & $Engine inspect $tempTag 2>$null | ConvertFrom-Json
            if ($remoteImageInfo -is [array]) {
                $remoteDigest = $remoteImageInfo[0].Id
            } else {
                $remoteDigest = $remoteImageInfo.Id
            }

            # Remove the temporary tag
            & $Engine rmi $tempTag 2>&1 | Out-Null
        }
    }

    if (-not $remoteDigest) {
        Write-Warning "Could not determine remote image digest. Assuming update is needed."
        return $true
    }

    Write-Host "Remote image digest: $remoteDigest"

    # Compare digests
    if ($localDigest -ne $remoteDigest) {
        Write-Host "Update available! Local and remote image digests differ." -ForegroundColor Green
        return $true
    } else {
        Write-Host "No update available. You have the latest version." -ForegroundColor Green
        return $false
    }
}

#############################################
# Function: Update-Container
# Description: Generic function to update a container while preserving its configuration
# Parameters:
#   -Engine: Path to the container engine (docker or podman)
#   -ContainerName: Name of the container to update
#   -ImageName: Full image name to update to
#   -Platform: Container platform (default: linux/amd64)
#   -RunFunction: A script block that runs the container with the appropriate options
# Returns: $true if successful, $false otherwise
#############################################
function Update-Container {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)]
        [string]$Engine,

        [Parameter(Mandatory=$true)]
        [string]$ContainerName,

        [Parameter(Mandatory=$true)]
        [string]$ImageName,

        [string]$Platform = "linux/amd64",

        [Parameter(Mandatory=$true)]
        [scriptblock]$RunFunction
    )

    Write-Host "Initiating update for container '$ContainerName'..."

    # Step 1: Check if container exists
    $containerInfo = & $Engine inspect $ContainerName 2>$null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Container '$ContainerName' not found. Nothing to update." -ForegroundColor Yellow
        return $false
    }

    # Step 2: Check if an update is available
    $updateAvailable = Check-ImageUpdateAvailable -Engine $Engine -ImageName $ImageName
    if (-not $updateAvailable) {
        $forceUpdate = Read-Host "No update available. Do you want to force an update anyway? (Y/N, default is N)"
        if ($forceUpdate -ne "Y") {
            Write-Host "Update canceled. No changes made."
            return $false
        }
        Write-Host "Proceeding with forced update..."
    }

    # Step 3: Optionally backup the container
    $createBackup = Read-Host "Create backup before updating? (Y/N, default is Y)"
    if ($createBackup -ne "N") {
        Write-Host "Creating backup of current container..."
        Backup-ContainerState -Engine $Engine -ContainerName $ContainerName
    }

    # Step 4: Remove the existing container
    Write-Host "Removing existing container '$ContainerName' as part of the update..."
    & $Engine rm --force $ContainerName
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to remove container '$ContainerName'. Update aborted."
        return $false
    }

    # Step 5: Pull the latest image
    Write-Host "Pulling latest image '$ImageName'..."
    & $Engine pull --platform $Platform $ImageName
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to pull the latest image. Update aborted."

        # Offer to restore from backup if one was created
        if ($createBackup -ne "N") {
            $restore = Read-Host "Would you like to restore from backup? (Y/N, default is Y)"
            if ($restore -ne "N") {
                Restore-ContainerState -Engine $Engine -ContainerName $ContainerName
            }
        }
        return $false
    }

    # Step 6: Run the container using the provided function
    Write-Host "Starting updated container..."
    try {
        & $RunFunction
        Write-Host "Container '$ContainerName' updated successfully!" -ForegroundColor Green
        return $true
    }
    catch {
        Write-Error "Failed to start updated container: $_"

        # Offer to restore from backup if one was created
        if ($createBackup -ne "N") {
            $restore = Read-Host "Would you like to restore from backup? (Y/N, default is Y)"
            if ($restore -ne "N") {
                Restore-ContainerState -Engine $Engine -ContainerName $ContainerName
            }
        }
        return $false
    }
}
