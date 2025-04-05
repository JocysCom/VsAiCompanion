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
         Write-Output "Created backup folder: $BackupFolder"
    }

    # Check if the container exists.
    $existingContainer = & $Engine ps -a --filter "name=^$ContainerName$" --format "{{.ID}}"
    if (-not $existingContainer) {
         Write-Error "Container '$ContainerName' does not exist. Cannot backup."
         return $false
    }

    # Debug output to verify container name
    Write-Output "DEBUG: Container name is '$ContainerName'"

    # Create a simple image tag without any container- prefix that might be causing issues
    $backupImageTag = "backup-$ContainerName"

    Write-Output "Committing container '$ContainerName' to image '$backupImageTag'..."
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

    Write-Output "Saving backup image '$backupImageTag' to '$backupFile'..."
    # podman save [options] IMAGE
    # save      Save an image to a tar archive.
    # --output string   Specify the output file for saving the image.
    & $Engine save --output $backupFile $backupImageTag
    if ($LASTEXITCODE -eq 0) {
         Write-Output "Backup successfully saved to '$backupFile'."
         return $true
    } else {
         Write-Error "Failed to save backup image to '$backupFile'."
        return $false
    }
}

#############################################
# Function: Remove-ContainerAndVolume
# Description: Stops and removes a container, and optionally its associated volume.
# Parameters:
#   -Engine: Path to the container engine executable.
#   -ContainerName: Name of the container to remove.
#   -VolumeName: Name of the associated data volume to potentially remove.
# Returns: $true if container removal was successful, $false otherwise.
#############################################
function Remove-ContainerAndVolume {
    [CmdletBinding(SupportsShouldProcess=$true)]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory=$true)]
        [string]$Engine,

        [Parameter(Mandatory=$true)]
        [string]$ContainerName,

        [Parameter(Mandatory=$true)]
        [string]$VolumeName
    )

    # Check if container exists
    $existingContainer = & $Engine ps -a --filter "name=^$ContainerName$" --format "{{.ID}}"
    if (-not $existingContainer) {
        Write-Output "Container '$ContainerName' not found. Nothing to remove." # Removed ForegroundColor Yellow
        return $true # Indicate success as there's nothing to do
    }

    if ($PSCmdlet.ShouldProcess($ContainerName, "Stop Container")) {
        Write-Output "Stopping container '$ContainerName'..."
        & $Engine stop $ContainerName 2>$null | Out-Null
    }

    if ($PSCmdlet.ShouldProcess($ContainerName, "Remove Container")) {
        Write-Output "Removing container '$ContainerName'..."
        & $Engine rm --force $ContainerName
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Failed to remove container '$ContainerName'."
            return $false
        }
        Write-Output "Container '$ContainerName' removed successfully."
    }

    # Check if volume exists
    $existingVolume = & $Engine volume ls --filter "name=^$VolumeName$" --format "{{.Name}}"
    if ($existingVolume) {
        Write-Output "Data volume '$VolumeName' exists." # Removed ForegroundColor Yellow
        $removeVolume = Read-Host "Do you want to remove the data volume '$VolumeName' as well? (Y/N, default N)"
        if ($removeVolume -eq 'Y') {
            if ($PSCmdlet.ShouldProcess($VolumeName, "Remove Volume")) {
                Write-Output "Removing volume '$VolumeName'..."
                & $Engine volume rm $VolumeName
                if ($LASTEXITCODE -eq 0) {
                    Write-Output "Volume '$VolumeName' removed successfully." # Removed ForegroundColor Green
                } else {
                    Write-Error "Failed to remove volume '$VolumeName'."
                    # Continue even if volume removal fails, as container was removed
                }
            }
        } else {
            Write-Output "Volume '$VolumeName' was not removed."
        }
    } else {
        Write-Output "Volume '$VolumeName' not found."
    }

    return $true
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
        [switch]$RestoreVolumes = $false # Changed default to $false
    )

    # First try the container-specific backup format
    $safeName = $ContainerName -replace "[:/]", "_"
    $backupFile = Join-Path $BackupFolder "$safeName-backup.tar"

    # If container-specific backup not found, try to find a matching image backup
    if (-not (Test-Path $backupFile)) {
        Write-Output "Container-specific backup file '$backupFile' not found."
        Write-Output "Looking for image backups that might match this container..."

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
                Write-Output "Found potential matching backup: $matchingBackup"
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

    Write-Output "Loading backup image from '$backupFile'..."
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
        Write-Output "Loaded image: $imageName"
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
                Write-Output "Found volume backup for '$volumeName': $volumeBackupFile"

                # Check if volume exists, create if not
                $volumeExists = & $Engine volume ls --filter "name=$volumeName" --format "{{.Name}}"
                if (-not $volumeExists) {
                    Write-Output "Creating volume '$volumeName'..."
                    & $Engine volume create $volumeName
                }

                # Ask for confirmation before restoring volume
                $restoreVolumeConfirm = Read-Host "Restore volume data for '$volumeName'? This will merge with existing data. (Y/N, default is Y)"
                if ($restoreVolumeConfirm -ne "N") {
                    Write-Output "Restoring volume '$volumeName' from '$volumeBackupFile'..."

                    # Create a temporary container to restore the volume data
                    $tempContainerName = "restore-volume-$volumeName-$(Get-Random)"

                    # Run a temporary container with the volume mounted and extract the backup
                    & $Engine run --rm --volume ${volumeName}:/target --volume ${BackupFolder}:/backup --name $tempContainerName alpine tar -xf /backup/$(Split-Path $volumeBackupFile -Leaf) -C /target

                    if ($LASTEXITCODE -eq 0) {
                        Write-Output "Successfully restored volume '$volumeName' from '$volumeBackupFile'" # Removed ForegroundColor Green
                    } else {
                        Write-Error "Failed to restore volume '$volumeName'"
                    }
                } else {
                    Write-Output "Skipping volume restore as requested."
                }
            } else {
                Write-Output "No volume backup found for '$volumeName' at '$volumeBackupFile'."
                Write-Output "Will continue with container image restore only. Existing volume data will be preserved."
            }
        }
        # For other containers, we would need to determine volume names differently
    }

    # Return the loaded image name
    return $imageName
}

#############################################
# Function: Test-ImageUpdateAvailable
# Description: Checks if a newer version of a container image is available
#              from its registry. Works with multiple registries including
#              docker.io, ghcr.io, and others.
# Parameters:
#   -Engine: Path to the container engine (docker or podman)
#   -ImageName: Full image name including registry (e.g., ghcr.io/open-webui/open-webui:main)
# Returns: $true if an update is available, $false otherwise
#############################################
function Test-ImageUpdateAvailable { # Renamed function
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory=$true)]
        [string]$Engine,

        [Parameter(Mandatory=$true)]
        [string]$ImageName
    )

    Write-Output "Checking for updates to $ImageName..."

    # First, check if we have the image locally
    $localImageInfo = & $Engine inspect $ImageName 2>$null | ConvertFrom-Json
    if (-not $localImageInfo) {
        Write-Output "Image '$ImageName' not found locally. Update is available." # Removed ForegroundColor Yellow
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

    Write-Output "Local image digest: $localDigest"

    # Determine container engine type (docker or podman)
    $engineType = "docker"
    if ((Get-Item $Engine).Name -like "*podman*") {
        $engineType = "podman"
    }

    # Pull the image with latest tag but don't update the local image
    Write-Output "Checking remote registry for latest version..."

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

    Write-Output "Remote image digest: $remoteDigest"

    # Compare digests
    if ($localDigest -ne $remoteDigest) {
        Write-Output "Update available! Local and remote image digests differ." # Removed ForegroundColor Green
        return $true
    } else {
        Write-Output "No update available. You have the latest version." # Removed ForegroundColor Green
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
    [CmdletBinding(SupportsShouldProcess=$true)]
    [OutputType([bool])]
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

    Write-Output "Initiating update for container '$ContainerName'..."

    # Step 1: Check if container exists
    # $containerInfo = & $Engine inspect $ContainerName 2>$null # Unused variable removed
    & $Engine inspect $ContainerName 2>$null | Out-Null # Check existence without storing info
    if ($LASTEXITCODE -ne 0) {
        Write-Output "Container '$ContainerName' not found. Nothing to update." # Removed ForegroundColor Yellow
        return $false
    }

    # Step 2: Check if an update is available
    $updateAvailable = Test-ImageUpdateAvailable -Engine $Engine -ImageName $ImageName # Use renamed function
    if (-not $updateAvailable) {
        $forceUpdate = Read-Host "No update available. Do you want to force an update anyway? (Y/N, default is N)"
        if ($forceUpdate -ne "Y") {
            Write-Output "Update canceled. No changes made."
            return $false
        }
        Write-Output "Proceeding with forced update..."
    }

    # Step 3: Optionally backup the container
    $createBackup = Read-Host "Create backup before updating? (Y/N, default is Y)"
    if ($createBackup -ne "N") {
        if ($PSCmdlet.ShouldProcess($ContainerName, "Backup Container State")) {
            Write-Output "Creating backup of current container..."
            Backup-ContainerState -Engine $Engine -ContainerName $ContainerName
        }
    }

    # Step 4: Remove the existing container
    if ($PSCmdlet.ShouldProcess($ContainerName, "Remove Container for Update")) {
        Write-Output "Removing existing container '$ContainerName' as part of the update..."
        & $Engine rm --force $ContainerName
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Failed to remove container '$ContainerName'. Update aborted."
            return $false
        }
    }

    # Step 5: Pull the latest image
    if ($PSCmdlet.ShouldProcess($ImageName, "Pull Latest Image")) {
        Write-Output "Pulling latest image '$ImageName'..."
        & $Engine pull --platform $Platform $ImageName
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Failed to pull the latest image. Update aborted."

            # Offer to restore from backup if one was created
            if ($createBackup -ne "N") {
                $restore = Read-Host "Would you like to restore from backup? (Y/N, default is Y)"
                if ($restore -ne "N") {
                    if ($PSCmdlet.ShouldProcess($ContainerName, "Restore Container State after Failed Update")) {
                        Restore-ContainerState -Engine $Engine -ContainerName $ContainerName
                    }
                }
            }
            return $false
        }
    }

    # Step 6: Run the container using the provided function
    if ($PSCmdlet.ShouldProcess($ContainerName, "Start Updated Container")) {
        Write-Output "Starting updated container..."
        try {
            & $RunFunction
            Write-Output "Container '$ContainerName' updated successfully!" # Removed ForegroundColor Green
            return $true
        }
        catch {
            Write-Error "Failed to start updated container: $_"

            # Offer to restore from backup if one was created
            if ($createBackup -ne "N") {
                $restore = Read-Host "Would you like to restore from backup? (Y/N, default is Y)"
                if ($restore -ne "N") {
                    if ($PSCmdlet.ShouldProcess($ContainerName, "Restore Container State after Failed Start")) {
                        Restore-ContainerState -Engine $Engine -ContainerName $ContainerName
                    }
                }
            }
            return $false
        }
    } else {
        # If ShouldProcess returned false for starting the container
        Write-Output "Update process completed (image pulled, old container removed), but new container start was skipped due to -WhatIf."
        return $true # Consider it a success in terms of -WhatIf
    }
}
