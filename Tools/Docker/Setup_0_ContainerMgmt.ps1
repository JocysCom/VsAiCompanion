################################################################################
# File         : Setup_0_ContainerMgmt.ps1
# Description  : Contains container management functions:
#                - Backup-ContainerState: Backup a running container's state.
#                - Restore-ContainerState: Restore a container from state backup.
#                - Check-ImageUpdateAvailable: Check for newer image versions.
#                - Update-Container: Generic container update function.
# Usage        : Dot-source this script and potentially Setup_0_BackupRestore.ps1
#                and Setup_0_Network.ps1 in other setup scripts:
#                . "$PSScriptRoot\Setup_0_Network.ps1" # For Show-ContainerStatus dependency
#                . "$PSScriptRoot\Setup_0_BackupRestore.ps1" # For Restore-ContainerState dependency
#                . "$PSScriptRoot\Setup_0_ContainerMgmt.ps1"
################################################################################

#############################################
# Function: Ensure-ContainerNetwork
# Description: Checks if a container network exists and creates it if it doesn't.
# Parameters:
#   -Engine: Path to the container engine executable.
#   -NetworkName: Name of the network to check/create.
# Returns: $true if network exists or was created, $false otherwise.
#############################################
function Confirm-ContainerNetwork {
    [CmdletBinding(SupportsShouldProcess=$true)]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory=$true)]
        [string]$Engine,

        [Parameter(Mandatory=$true)]
        [string]$NetworkName
    )

    $existingNetwork = & $Engine network ls --filter "name=^$NetworkName$" --format "{{.Name}}"
    if ($existingNetwork -ne $NetworkName) {
        if ($PSCmdlet.ShouldProcess($NetworkName, "Create Network")) {
            # Use Write-Information for status messages
            Write-Information "Creating container network '$NetworkName'..."
            & $Engine network create $NetworkName
            if ($LASTEXITCODE -eq 0) {
                # Use Write-Information for status messages
                Write-Information "Network '$NetworkName' created successfully."
                return $true
            } else {
                Write-Error "Failed to create network '$NetworkName'."
                return $false
            }
        } else {
            Write-Warning "Network creation skipped due to -WhatIf."
            return $false # Indicate network doesn't exist if creation skipped
        }
    }
    else {
        # Use Write-Information for status messages
        Write-Information "Network '$NetworkName' already exists. Skipping creation."
        return $true
    }
}

#############################################
# Function: Ensure-ContainerVolume
# Description: Checks if a volume exists and creates it if it doesn't.
# Parameters:
#   -Engine: Path to the container engine executable.
#   -VolumeName: Name of the volume to check/create.
# Returns: $true if volume exists or was created, $false otherwise.
#############################################
function Confirm-ContainerVolume {
    [CmdletBinding(SupportsShouldProcess=$true)]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory=$true)]
        [string]$Engine,

        [Parameter(Mandatory=$true)]
        [string]$VolumeName
    )

    $existingVolume = & $Engine volume ls --filter "name=^$VolumeName$" --format "{{.Name}}"
    if ([string]::IsNullOrWhiteSpace($existingVolume)) {
        if ($PSCmdlet.ShouldProcess($VolumeName, "Create Volume")) {
            # Use Write-Information for status messages
            Write-Information "Creating volume '$VolumeName'..."
            & $Engine volume create $VolumeName
            if ($LASTEXITCODE -eq 0) {
                # Use Write-Information for status messages
                Write-Information "Volume '$VolumeName' created successfully."
                return $true
            } else {
                Write-Error "Failed to create volume '$VolumeName'."
                return $false
            }
        } else {
            Write-Warning "Volume creation skipped due to -WhatIf."
            return $false # Indicate volume doesn't exist if creation skipped
        }
    }
    else {
        # Use Write-Information for status messages
        Write-Information "Volume '$VolumeName' already exists. Skipping creation."
        return $true
    }
}

#############################################
# Function: Invoke-PullImage
# Description: Pulls a container image using the specified engine.
# Parameters:
#   -Engine: Path to the container engine executable.
#   -ImageName: Full name of the image to pull.
#   -PullOptions: Optional array of additional options for the pull command (e.g., --platform, --tls-verify).
# Returns: $true if pull was successful, $false otherwise.
#############################################
function Invoke-PullImage {
    [CmdletBinding(SupportsShouldProcess=$true)]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory=$true)]
        [string]$Engine,

        [Parameter(Mandatory=$true)]
        [string]$ImageName,

        [Parameter(Mandatory=$false)]
        [array]$PullOptions = @()
    )

    if ($PSCmdlet.ShouldProcess($ImageName, "Pull Image")) {
        # Use Write-Information for status messages
        Write-Information "Pulling image '$ImageName'..."
        $pullCmd = @("pull") + $PullOptions + $ImageName
        & $Engine @pullCmd

        if ($LASTEXITCODE -eq 0) {
            # Use Write-Information for status messages
            Write-Information "Image '$ImageName' pulled successfully."
            return $true
        } else {
            Write-Error "Failed to pull image '$ImageName'."
            return $false
        }
    } else {
        Write-Warning "Image pull skipped due to -WhatIf."
        return $false # Indicate failure if skipped
    }
}

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
         # Use Write-Information for status messages
         Write-Information "Created backup folder: $BackupFolder"
    }

    # Check if the container exists.
    $existingContainer = & $Engine ps -a --filter "name=^$ContainerName$" --format "{{.ID}}"
    if (-not $existingContainer) {
         Write-Error "Container '$ContainerName' does not exist. Cannot backup."
         return $false
     }

    # Use Write-Information for status messages
    Write-Information "DEBUG: Container name is '$ContainerName'"

    # Create a simple image tag without any container- prefix that might be causing issues
    $backupImageTag = "backup-$ContainerName"

    # Use Write-Information for status messages
    Write-Information "Committing container '$ContainerName' to image '$backupImageTag'..."
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

    # Use Write-Information for status messages
    Write-Information "Saving backup image '$backupImageTag' to '$backupFile'..."
    # podman save [options] IMAGE
    # save      Save an image to a tar archive.
    # --output string   Specify the output file for saving the image.
    & $Engine save --output $backupFile $backupImageTag
    if ($LASTEXITCODE -eq 0) {
         # Use Write-Information for status messages
         Write-Information "Backup successfully saved to '$backupFile'."
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
        # Use Write-Information for status messages
        Write-Information "Container '$ContainerName' not found. Nothing to remove."
        return $true # Indicate success as there's nothing to do
    }

    if ($PSCmdlet.ShouldProcess($ContainerName, "Stop Container")) {
        # Use Write-Information for status messages
        Write-Information "Stopping container '$ContainerName'..."
        & $Engine stop $ContainerName 2>$null | Out-Null
    }

    if ($PSCmdlet.ShouldProcess($ContainerName, "Remove Container")) {
        # Use Write-Information for status messages
        Write-Information "Removing container '$ContainerName'..."
        & $Engine rm --force $ContainerName
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Failed to remove container '$ContainerName'."
            return $false
        }
        # Use Write-Information for status messages
        Write-Information "Container '$ContainerName' removed successfully."
    }

    # Check if volume exists
    $existingVolume = & $Engine volume ls --filter "name=^$VolumeName$" --format "{{.Name}}"
    if ($existingVolume) {
        # Use Write-Information for status messages
        Write-Information "Data volume '$VolumeName' exists."
        $removeVolume = Read-Host "Do you want to remove the data volume '$VolumeName' as well? (Y/N, default N)"
        if ($removeVolume -eq 'Y') {
            if ($PSCmdlet.ShouldProcess($VolumeName, "Remove Volume")) {
                # Use Write-Information for status messages
                Write-Information "Removing volume '$VolumeName'..."
                & $Engine volume rm $VolumeName
                if ($LASTEXITCODE -eq 0) {
                    # Use Write-Information for status messages
                    Write-Information "Volume '$VolumeName' removed successfully."
                } else {
                    Write-Error "Failed to remove volume '$VolumeName'."
                    # Continue even if volume removal fails, as container was removed
                }
            }
        } else {
            # Use Write-Information for status messages
            Write-Information "Volume '$VolumeName' was not removed."
        }
    } else {
        # Use Write-Information for status messages
        Write-Information "Volume '$VolumeName' not found."
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
        [switch]$RestoreVolumes = $false
    )

    # First try the container-specific backup format
    $safeName = $ContainerName -replace "[:/]", "_"
    $backupFile = Join-Path $BackupFolder "$safeName-backup.tar"

    # If container-specific backup not found, try to find a matching image backup
    if (-not (Test-Path $backupFile)) {
        # Use Write-Information for status messages
        Write-Information "Container-specific backup file '$backupFile' not found."
        # Use Write-Information for status messages
        Write-Information "Looking for image backups that might match this container..."

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
                # Use Write-Information for status messages
                Write-Information "Found potential matching backup: $matchingBackup"
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

    # Use Write-Information for status messages
    Write-Information "Loading backup image from '$backupFile'..."
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
        # Use Write-Information for status messages
        Write-Information "Loaded image: $imageName"
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
                # Use Write-Information for status messages
                Write-Information "Found volume backup for '$volumeName': $volumeBackupFile"

                # Check if volume exists, create if not
                $volumeExists = & $Engine volume ls --filter "name=$volumeName" --format "{{.Name}}"
                if (-not $volumeExists) {
                    # Use Write-Information for status messages
                    Write-Information "Creating volume '$volumeName'..."
                    & $Engine volume create $volumeName
                }

                # Ask for confirmation before restoring volume
                $restoreVolumeConfirm = Read-Host "Restore volume data for '$volumeName'? This will merge with existing data. (Y/N, default is Y)"
                if ($restoreVolumeConfirm -ne "N") {
                    # Use Write-Information for status messages
                    Write-Information "Restoring volume '$volumeName' from '$volumeBackupFile'..."

                    # Create a temporary container to restore the volume data
                    $tempContainerName = "restore-volume-$volumeName-$(Get-Random)"

                    # Run a temporary container with the volume mounted and extract the backup
                    & $Engine run --rm --volume ${volumeName}:/target --volume ${BackupFolder}:/backup --name $tempContainerName alpine tar -xf /backup/$(Split-Path $volumeBackupFile -Leaf) -C /target

                    if ($LASTEXITCODE -eq 0) {
                        # Use Write-Information for status messages
                        Write-Information "Successfully restored volume '$volumeName' from '$volumeBackupFile'"
                    } else {
                        Write-Error "Failed to restore volume '$volumeName'"
                    }
                } else {
                    # Use Write-Information for status messages
                    Write-Information "Skipping volume restore as requested."
                }
            } else {
                # Use Write-Information for status messages
                Write-Information "No volume backup found for '$volumeName' at '$volumeBackupFile'."
                # Use Write-Information for status messages
                Write-Information "Will continue with container image restore only. Existing volume data will be preserved."
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
function Test-ImageUpdateAvailable {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory=$true)]
        [string]$Engine,

        [Parameter(Mandatory=$true)]
        [string]$ImageName
    )

    # Use Write-Information for status messages
    Write-Information "Checking for updates to $ImageName..."

    # First, check if we have the image locally
    $localImageInfo = & $Engine inspect $ImageName 2>$null | ConvertFrom-Json
    if (-not $localImageInfo) {
        # Use Write-Information for status messages
        Write-Information "Image '$ImageName' not found locally. Update is available."
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

    # Use Write-Information for status messages
    Write-Information "Local image digest: $localDigest"

    # Determine container engine type (docker or podman)
    $engineType = "docker"
    if ((Get-Item $Engine).Name -like "*podman*") {
        $engineType = "podman"
    }

    # Pull the image with latest tag but don't update the local image
    # Use Write-Information for status messages
    Write-Information "Checking remote registry for latest version..."

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

    # Use Write-Information for status messages
    Write-Information "Remote image digest: $remoteDigest"

    # Compare digests
    if ($localDigest -ne $remoteDigest) {
        # Use Write-Information for status messages
        Write-Information "Update available! Local and remote image digests differ."
        return $true
    } else {
        # Use Write-Information for status messages
        Write-Information "No update available. You have the latest version."
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

    # Use Write-Information for status messages
    Write-Information "Initiating update for container '$ContainerName'..."

    # Step 1: Check if container exists
    & $Engine inspect $ContainerName 2>$null | Out-Null # Check existence without storing info
    if ($LASTEXITCODE -ne 0) {
        # Use Write-Information for status messages
        Write-Information "Container '$ContainerName' not found. Nothing to update."
        return $false
    }

    # Step 2: Check if an update is available
    $updateAvailable = Test-ImageUpdateAvailable -Engine $Engine -ImageName $ImageName
    if (-not $updateAvailable) {
        $forceUpdate = Read-Host "No update available. Do you want to force an update anyway? (Y/N, default is N)"
        if ($forceUpdate -ne "Y") {
            # Use Write-Information for status messages
            Write-Information "Update canceled. No changes made."
            return $false
        }
        # Use Write-Information for status messages
        Write-Information "Proceeding with forced update..."
    }

    # Step 3: Optionally backup the container
    $createBackup = Read-Host "Create backup before updating? (Y/N, default is Y)"
    if ($createBackup -ne "N") {
        if ($PSCmdlet.ShouldProcess($ContainerName, "Backup Container State")) {
            # Use Write-Information for status messages
            Write-Information "Creating backup of current container..."
            Backup-ContainerState -Engine $Engine -ContainerName $ContainerName
        }
    }

    # Step 4: Remove the existing container
    if ($PSCmdlet.ShouldProcess($ContainerName, "Remove Container for Update")) {
        # Use Write-Information for status messages
        Write-Information "Removing existing container '$ContainerName' as part of the update..."
        & $Engine rm --force $ContainerName
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Failed to remove container '$ContainerName'. Update aborted."
            return $false
        }
    }

    # Step 5: Pull the latest image
    if ($PSCmdlet.ShouldProcess($ImageName, "Pull Latest Image")) {
        # Use Write-Information for status messages
        Write-Information "Pulling latest image '$ImageName'..."
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
        # Use Write-Information for status messages
        Write-Information "Starting updated container..."
        try {
            & $RunFunction
            # Use Write-Information for status messages
            Write-Information "Container '$ContainerName' updated successfully!"
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
        # Use Write-Information for status messages
        Write-Information "Update process completed (image pulled, old container removed), but new container start was skipped due to -WhatIf."
        return $true # Consider it a success in terms of -WhatIf
    }
}

#############################################
# Function: Show-ContainerStatus
# Description: Displays container configuration, checks runtime status,
#              performs network connectivity tests, and pauses briefly.
#              Requires Setup_0_Network.ps1 to be dot-sourced for network tests.
# Parameters:
#   -ContainerName: Name of the container.
#   -ContainerEngine: Name of the engine (e.g., "docker", "podman").
#   -EnginePath: Full path to the container engine executable.
#   -DisplayName: Optional, friendly name for display (defaults to ContainerName).
#   -ContainerUrl: Optional, base URL for HTTP/WS tests (e.g., http://localhost:8080).
#   -TcpPort: Optional, port number for TCP connectivity test.
#   -HttpPort: Optional, port number for HTTP connectivity test.
#   -HttpPath: Optional, path for HTTP test (defaults to '/').
#   -WsPort: Optional, port number for WebSocket connectivity test.
#   -WsPath: Optional, path for WebSocket test.
#   -DelaySeconds: Optional, seconds to pause after displaying info (default 3).
#   -AdditionalInfo: Optional, hashtable of extra key-value pairs to display.
#############################################
function Show-ContainerStatus {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)]
        [string]$ContainerName,

        [Parameter(Mandatory=$true)]
        [string]$ContainerEngine,

        [Parameter(Mandatory=$true)]
        [string]$EnginePath,

        [Parameter(Mandatory=$false)]
        [string]$DisplayName = $ContainerName,

        [Parameter(Mandatory=$false)]
        [string]$ContainerUrl,

        [Parameter(Mandatory=$false)]
        [int]$TcpPort,

        [Parameter(Mandatory=$false)]
        [int]$HttpPort,

        [Parameter(Mandatory=$false)]
        [string]$HttpPath = '/',

        [Parameter(Mandatory=$false)]
        [int]$WsPort,

        [Parameter(Mandatory=$false)]
        [string]$WsPath,

        [Parameter(Mandatory=$false)]
        [int]$DelaySeconds = 3,

        [Parameter(Mandatory=$false)]
        [hashtable]$AdditionalInfo
    )

    Write-Host "==========================================="
    Write-Host "Status for: $DisplayName"
    Write-Host "==========================================="
    Write-Host "Container Name : $ContainerName"
    Write-Host "Engine         : $ContainerEngine ($EnginePath)"

    # Display additional info if provided
    if ($AdditionalInfo) {
        Write-Host "-------------------------------------------"
        Write-Host "Additional Configuration:"
        foreach ($key in $AdditionalInfo.Keys) {
            Write-Host "$($key.PadRight(15)) : $($AdditionalInfo[$key])"
        }
        Write-Host "-------------------------------------------"
    }

    # Check container status
    Write-Host "Checking container status..."
    $containerInfo = & $EnginePath ps -a --filter "name=^$ContainerName$" --format "{{.Status}}"
    $containerId = & $EnginePath ps -a --filter "name=^$ContainerName$" --format "{{.ID}}"

    if (-not $containerId) {
        Write-Warning "Container '$ContainerName' not found."
    } else {
        Write-Host "Container Status : $containerInfo"

        # Perform network tests only if container is running
        if ($containerInfo -like "Up*") {
            # TCP Test
            if ($TcpPort -gt 0) {
                Test-TCPPort -ComputerName "localhost" -Port $TcpPort -ServiceName $DisplayName
            }

            # HTTP Test
            if ($HttpPort -gt 0) {
                $httpUri = $ContainerUrl # Use provided URL if available
                if ([string]::IsNullOrWhiteSpace($httpUri)) {
                    $httpUri = "http://localhost:$HttpPort" # Construct default URL
                }
                # Ensure path starts with /
                if (-not $HttpPath.StartsWith('/')) {
                    $HttpPath = "/$HttpPath"
                }
                $httpUri += $HttpPath
                Test-HTTPPort -Uri $httpUri -ServiceName $DisplayName
            }

            # WebSocket Test
            if ($WsPort -gt 0) {
                $wsUri = $ContainerUrl # Use provided URL if available
                if ([string]::IsNullOrWhiteSpace($wsUri)) {
                    $wsUri = "ws://localhost:$WsPort" # Construct default URL
                }
                # Ensure path starts with / if provided
                if (-not [string]::IsNullOrWhiteSpace($WsPath) -and -not $WsPath.StartsWith('/')) {
                    $WsPath = "/$WsPath"
                }
                $wsUri += $WsPath
                # Check if Test-WebSocketPort function exists before calling
                if (Get-Command Test-WebSocketPort -ErrorAction SilentlyContinue) {
                    Test-WebSocketPort -Uri $wsUri -ServiceName $DisplayName
                } else {
                    Write-Warning "Test-WebSocketPort function not found (is Setup_0_Network.ps1 sourced?). Skipping WebSocket test."
                }
            }
        } else {
            Write-Warning "Container is not running. Skipping network tests."
        }
    }

    Write-Host "==========================================="

    # Pause
    if ($DelaySeconds -gt 0) {
        Write-Host "Pausing for $DelaySeconds seconds..."
        Start-Sleep -Seconds $DelaySeconds
    }
}
