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

#==============================================================================
# Function: Confirm-ContainerNetwork
#==============================================================================
<#
.SYNOPSIS
	Checks if a container network exists and creates it if it doesn't.
.DESCRIPTION
	Uses the provided container engine to check if a network with the specified name exists.
	If it doesn't exist, it attempts to create the network. Supports -WhatIf.
.PARAMETER Engine
	Path to the container engine executable (e.g., 'docker' or 'podman'). Mandatory.
.PARAMETER NetworkName
	The name of the container network to check or create. Mandatory.
.OUTPUTS
	[bool] Returns $true if the network exists or was successfully created.
		   Returns $false if creation failed or was skipped due to -WhatIf.
.EXAMPLE
	Confirm-ContainerNetwork -Engine "podman" -NetworkName "my-app-network"
.NOTES
	Relies on 'engine network ls' and 'engine network create'.
#>
function Confirm-ContainerNetwork {
	[CmdletBinding(SupportsShouldProcess = $true)]
	[OutputType([bool])]
	param(
		[Parameter(Mandatory = $true)]
		[string]$Engine,

		[Parameter(Mandatory = $true)]
		[string]$NetworkName
	)

	$existingNetwork = & $Engine network ls --filter "name=^$NetworkName$" --format "{{.Name}}"
	if ($existingNetwork -ne $NetworkName) {
		if ($PSCmdlet.ShouldProcess($NetworkName, "Create Network")) {
			# Use Write-Host for status messages
			Write-Host "Creating container network '$NetworkName'..."
			& $Engine network create $NetworkName
			if ($LASTEXITCODE -eq 0) {
				# Use Write-Host for status messages
				Write-Host "Network '$NetworkName' created successfully."
				return $true
			}
			else {
				Write-Error "Failed to create network '$NetworkName'."
				return $false
			}
		}
		else {
			Write-Warning "Network creation skipped due to -WhatIf."
			return $false # Indicate network doesn't exist if creation skipped
		}
	}
	else {
		# Use Write-Host for status messages
		Write-Host "Network '$NetworkName' already exists. Skipping creation."
		return $true
	}
}

#==============================================================================
# Function: Confirm-ContainerVolume
#==============================================================================
<#
.SYNOPSIS
	Checks if a container volume exists and creates it if it doesn't.
.DESCRIPTION
	Uses the provided container engine to check if a volume with the specified name exists.
	If it doesn't exist, it attempts to create the volume. Supports -WhatIf.
.PARAMETER Engine
	Path to the container engine executable (e.g., 'docker' or 'podman'). Mandatory.
.PARAMETER VolumeName
	The name of the container volume to check or create. Mandatory.
.OUTPUTS
	[bool] Returns $true if the volume exists or was successfully created.
		   Returns $false if creation failed or was skipped due to -WhatIf.
.EXAMPLE
	Confirm-ContainerVolume -Engine "docker" -VolumeName "my-db-data"
.NOTES
	Relies on 'engine volume ls' and 'engine volume create'.
#>
function Confirm-ContainerVolume {
	[CmdletBinding(SupportsShouldProcess = $true)]
	[OutputType([bool])]
	param(
		[Parameter(Mandatory = $true)]
		[string]$Engine,

		[Parameter(Mandatory = $true)]
		[string]$VolumeName
	)

	$existingVolume = & $Engine volume ls --filter "name=^$VolumeName$" --format "{{.Name}}"
	if ([string]::IsNullOrWhiteSpace($existingVolume)) {
		if ($PSCmdlet.ShouldProcess($VolumeName, "Create Volume")) {
			# Use Write-Host for status messages
			Write-Host "Creating volume '$VolumeName'..."
			& $Engine volume create $VolumeName
			if ($LASTEXITCODE -eq 0) {
				# Use Write-Host for status messages
				Write-Host "Volume '$VolumeName' created successfully."
				return $true
			}
			else {
				Write-Error "Failed to create volume '$VolumeName'."
				return $false
			}
		}
		else {
			Write-Warning "Volume creation skipped due to -WhatIf."
			return $false # Indicate volume doesn't exist if creation skipped
		}
	}
	else {
		# Use Write-Host for status messages
		Write-Host "Volume '$VolumeName' already exists. Skipping creation."
		return $true
	}
}

#==============================================================================
# Function: Invoke-PullImage
#==============================================================================
<#
.SYNOPSIS
	Pulls a container image using the specified engine and optional arguments.
.DESCRIPTION
	Executes the container engine's 'pull' command for the specified image name.
	Allows passing additional command-line options via the PullOptions parameter.
	Supports -WhatIf.
.PARAMETER Engine
	Path to the container engine executable (e.g., 'docker' or 'podman'). Mandatory.
.PARAMETER ImageName
	The full name and tag of the container image to pull (e.g., 'nginx:latest'). Mandatory.
.PARAMETER PullOptions
	An optional array of strings representing additional arguments to pass to the pull command
	(e.g., @("--platform", "linux/arm64")). Defaults to an empty array.
.OUTPUTS
	[bool] Returns $true if the image pull command executes successfully (exit code 0).
		   Returns $false if the pull fails or is skipped due to -WhatIf.
.EXAMPLE
	Invoke-PullImage -Engine "podman" -ImageName "alpine:latest"
.EXAMPLE
	Invoke-PullImage -Engine "docker" -ImageName "mysql:8.0" -PullOptions @("--platform", "linux/amd64")
.NOTES
	Uses splatting (@pullCmd) to pass arguments to the engine.
#>
function Invoke-PullImage {
	[CmdletBinding(SupportsShouldProcess = $true)]
	[OutputType([bool])]
	param(
		[Parameter(Mandatory = $true)]
		[string]$Engine,

		[Parameter(Mandatory = $true)]
		[string]$ImageName,

		[Parameter(Mandatory = $false)]
		[array]$PullOptions = @()
	)

	if ($PSCmdlet.ShouldProcess($ImageName, "Pull Image")) {
		# Use Write-Host for status messages
		Write-Host "Pulling image '$ImageName'..."
		$pullCmd = @("pull") + $PullOptions + $ImageName
		& $Engine @pullCmd

		if ($LASTEXITCODE -eq 0) {
			# Use Write-Host for status messages
			Write-Host "Image '$ImageName' pulled successfully."
			return $true
		}
		else {
			Write-Error "Failed to pull image '$ImageName'."
			return $false
		}
	}
	else {
		Write-Warning "Image pull skipped due to -WhatIf."
		return $false # Indicate failure if skipped
	}
}

#==============================================================================
# Function: Backup-ContainerAndData
#==============================================================================
<#
.SYNOPSIS
	Backs up a container's data volume (if applicable, e.g., 'n8n_data' for 'n8n') and its image state.
.DESCRIPTION
	For the 'n8n' container: Stops the container, exports the 'n8n_data' volume (user data) to
	'<BackupFolder>/n8n_data-data.tar' using 'podman volume export', and restarts the container.
	For all containers (including 'n8n'): Commits the current container state to a new image
	tagged 'backup-<ContainerName>' and saves this image to '<BackupFolder>/<ContainerName>-backup.tar'.
.PARAMETER Engine
	Path to the container engine executable (e.g., 'docker' or 'podman'). Mandatory.
.PARAMETER ContainerName
	The name of the running container to back up. Mandatory.
.PARAMETER BackupFolder
	The directory where the backup .tar files will be saved. Defaults to '.\Backup'.
.OUTPUTS
	[bool] For 'n8n', returns $true if the volume export was successful. For others, returns $true if image save was successful.
.EXAMPLE
	Backup-ContainerAndData -Engine "docker" -ContainerName "my-web-app" -BackupFolder "C:\ContainerBackups"
.EXAMPLE
	Backup-ContainerAndData -Engine "podman" -ContainerName "n8n"
.NOTES
	Volume backup logic is currently specific to 'n8n' and its 'n8n_data' volume.
	Prioritizes volume backup success for 'n8n'. Image backup is secondary for 'n8n'.
	Uses $LASTEXITCODE to check the success of engine commands.
#>
function Backup-ContainerAndData {
	[CmdletBinding(SupportsShouldProcess = $true)] # Added SupportsShouldProcess
	param(
		[Parameter(Mandatory = $true)]
		[string]$Engine,
		[Parameter(Mandatory = $true)]
		[string]$ContainerName,
		[string]$BackupFolder = ".\Backup"
	)

	if (-not (Test-Path $BackupFolder)) {
		if ($PSCmdlet.ShouldProcess($BackupFolder, "Create Directory")) {
			New-Item -ItemType Directory -Path $BackupFolder -Force | Out-Null
			Write-Host "Created backup folder: $BackupFolder"
		}
		else {
			Write-Warning "Backup folder creation skipped. Cannot proceed."
			return $false
		}
	}

	# Check if the container exists.
	$existingContainer = & $Engine ps -a --filter "name=^$ContainerName$" --format "{{.ID}}"
	if (-not $existingContainer) {
		Write-Error "Container '$ContainerName' does not exist. Cannot backup."
		return $false
	}

	# --- Volume Backup Logic (Specific to n8n for now) ---
	$volumeBackupSuccess = $true # Assume success unless volume backup fails or is skipped
	if ($ContainerName -eq "n8n") {
		$volumeName = "n8n_data"
		$volumeBackupFile = Join-Path $BackupFolder "$volumeName-data.tar"

		# Check if volume exists
		$existingVolume = & $Engine volume ls --filter "name=^$volumeName$" --format "{{.Name}}"
		if ($existingVolume) {
			Write-Host "Attempting to back up volume '$volumeName' for container '$ContainerName'."
			Write-Warning "Container '$ContainerName' will be stopped temporarily for volume backup."

			# Stop the container
			if ($PSCmdlet.ShouldProcess($ContainerName, "Stop Container for Volume Backup")) {
				& $Engine stop $ContainerName 2>$null | Out-Null
				if ($LASTEXITCODE -ne 0) {
					Write-Warning "Failed to stop container '$ContainerName'. Skipping volume backup."
					$volumeBackupSuccess = $false
				}
				else {
					# Export the volume
					if ($PSCmdlet.ShouldProcess($volumeName, "Export Volume to '$volumeBackupFile'")) {
						Write-Host "Exporting volume '$volumeName' to '$volumeBackupFile'..."
						# podman volume export [options] VOLUME
						# export    Export volume contents to an external tar file.
						# --output, -o string   Specify the output file.
						& $Engine volume export --output $volumeBackupFile $volumeName
						if ($LASTEXITCODE -eq 0) {
							Write-Host "Successfully exported volume '$volumeName' to '$volumeBackupFile'."
						}
						else {
							Write-Error "Failed to export volume '$volumeName'."
							$volumeBackupSuccess = $false
						}
					}
					else {
						Write-Warning "Volume export skipped due to -WhatIf."
						$volumeBackupSuccess = $false # Treat skip as failure for overall success
					}

					# Restart the container regardless of export success/failure
					if ($PSCmdlet.ShouldProcess($ContainerName, "Restart Container after Volume Backup Attempt")) {
						Write-Host "Restarting container '$ContainerName'..."
						& $Engine start $ContainerName 2>$null | Out-Null
						if ($LASTEXITCODE -ne 0) {
							Write-Warning "Failed to restart container '$ContainerName' after volume backup attempt."
						}
					}
					else {
						Write-Warning "Container restart skipped due to -WhatIf."
					}
				}
			}
			else {
				Write-Warning "Container stop skipped due to -WhatIf. Cannot back up volume."
				$volumeBackupSuccess = $false
			}
		}
		else {
			Write-Warning "Volume '$volumeName' not found. Skipping volume backup."
			# Not necessarily a failure if volume doesn't exist, but backup isn't complete
			$volumeBackupSuccess = $false
		}
	}
	# --- End Volume Backup Logic ---

	# --- Image Backup Logic (Existing - Run for all containers as secondary/fallback) ---
	$imageBackupSuccess = $false
	Write-Host "Proceeding with container image state backup (commit/save)..."
	$backupImageTag = "backup-$ContainerName"

	if ($PSCmdlet.ShouldProcess($ContainerName, "Commit Container State to Image '$backupImageTag'")) {
		Write-Host "Committing container '$ContainerName' to image '$backupImageTag'..."
		& $Engine commit $ContainerName $backupImageTag
		if ($LASTEXITCODE -ne 0) {
			Write-Error "Failed to commit container '$ContainerName'."
			# Don't return yet, report overall success later
		}
		else {
			# Build backup tar file name for the image
			$safeName = $ContainerName -replace "[:/]", "_"
			if ($safeName -eq "") { $safeName = "unknown" }
			$imageBackupFile = Join-Path $BackupFolder "$safeName-backup.tar"

			if ($PSCmdlet.ShouldProcess($backupImageTag, "Save Image to '$imageBackupFile'")) {
				Write-Host "Saving backup image '$backupImageTag' to '$imageBackupFile'..."
				& $Engine save --output $imageBackupFile $backupImageTag
				if ($LASTEXITCODE -eq 0) {
					Write-Host "Image backup successfully saved to '$imageBackupFile'."
					$imageBackupSuccess = $true
				}
				else {
					Write-Error "Failed to save backup image to '$imageBackupFile'."
				}
			}
			else {
				Write-Warning "Image save skipped due to -WhatIf."
			}
		}
	}
	else {
		Write-Warning "Container commit skipped due to -WhatIf."
	}
	# --- End Image Backup Logic ---

	# Return true only if the essential part (volume backup for n8n) succeeded.
	# For other containers, return true if image backup succeeded.
	if ($ContainerName -eq "n8n") {
		if (-not $volumeBackupSuccess) {
			Write-Warning "Volume backup for n8n did not complete successfully."
		}
		return $volumeBackupSuccess # Prioritize volume backup success for n8n
	}
	else {
		# For other containers, image backup is the primary method here
		if (-not $imageBackupSuccess) {
			Write-Warning "Image backup did not complete successfully."
		}
		return $imageBackupSuccess
	}
}

#==============================================================================
# Function: Remove-ContainerAndVolume
#==============================================================================
<#
.SYNOPSIS
	Stops and removes a container, and optionally prompts to remove an associated volume.
.DESCRIPTION
	Checks if the specified container exists. If it does, it stops and removes the container.
	It then checks if the specified volume exists. If the volume exists, it prompts the user
	via Read-Host whether to remove the volume as well. Supports -WhatIf for container/volume
	stop/remove actions.
.PARAMETER Engine
	Path to the container engine executable (e.g., 'docker' or 'podman'). Mandatory.
.PARAMETER ContainerName
	The name of the container to stop and remove. Mandatory.
.PARAMETER VolumeName
	The name of the associated data volume to check and potentially remove. Mandatory.
.OUTPUTS
	[bool] Returns $true if the container is successfully removed (or didn't exist initially).
		   Returns $false if the container removal fails. Volume removal status does not affect the return value.
.EXAMPLE
	Remove-ContainerAndVolume -Engine "podman" -ContainerName "old-app" -VolumeName "old-app-data"
.NOTES
	User interaction for volume removal is handled via Read-Host.
	Uses $LASTEXITCODE to check the success of engine commands.
#>
function Remove-ContainerAndVolume {
	[CmdletBinding(SupportsShouldProcess = $true)]
	[OutputType([bool])]
	param(
		[Parameter(Mandatory = $true)]
		[string]$Engine,

		[Parameter(Mandatory = $true)]
		[string]$ContainerName,

		[Parameter(Mandatory = $true)]
		[string]$VolumeName
	)

	# Check if container exists
	$existingContainer = & $Engine ps -a --filter "name=^$ContainerName$" --format "{{.ID}}"
	if (-not $existingContainer) {
		# Use Write-Host for status messages
		Write-Host "Container '$ContainerName' not found. Nothing to remove."
		return $true # Indicate success as there's nothing to do
	}

	if ($PSCmdlet.ShouldProcess($ContainerName, "Stop Container")) {
		# Use Write-Host for status messages
		Write-Host "Stopping container '$ContainerName'..."
		& $Engine stop $ContainerName 2>$null | Out-Null
	}

	if ($PSCmdlet.ShouldProcess($ContainerName, "Remove Container")) {
		# Use Write-Host for status messages
		Write-Host "Removing container '$ContainerName'..."
		& $Engine rm --force $ContainerName
		if ($LASTEXITCODE -ne 0) {
			Write-Error "Failed to remove container '$ContainerName'."
			return $false
		}
		# Use Write-Host for status messages
		Write-Host "Container '$ContainerName' removed successfully."
	}

	# Check if volume exists
	$existingVolume = & $Engine volume ls --filter "name=^$VolumeName$" --format "{{.Name}}"
	if ($existingVolume) {
		# Use Write-Host for status messages
		Write-Host "Data volume '$VolumeName' exists."
		$removeVolume = Read-Host "Do you want to remove the data volume '$VolumeName' as well? (Y/N, default N)"
		if ($removeVolume -eq 'Y') {
			if ($PSCmdlet.ShouldProcess($VolumeName, "Remove Volume")) {
				# Use Write-Host for status messages
				Write-Host "Removing volume '$VolumeName'..."
				& $Engine volume rm $VolumeName
				if ($LASTEXITCODE -eq 0) {
					# Use Write-Host for status messages
					Write-Host "Volume '$VolumeName' removed successfully."
				}
				else {
					Write-Error "Failed to remove volume '$VolumeName'."
					# Continue even if volume removal fails, as container was removed
				}
			}
		}
		else {
			# Use Write-Host for status messages
			Write-Host "Volume '$VolumeName' was not removed."
		}
	}
	else {
		# Use Write-Host for status messages
		Write-Host "Volume '$VolumeName' not found."
	}

	return $true
}

#==============================================================================
# Function: Restore-ContainerAndData
#==============================================================================
<#
.SYNOPSIS
	Restores a container image from backup and optionally restores associated volume data.
.DESCRIPTION
	Loads a container image from '<BackupFolder>/<ContainerName>-backup.tar' (or a matching image backup).
	If -RestoreVolumes is specified and ContainerName is 'n8n': Looks for '<BackupFolder>/n8n_data-data.tar',
	creates the 'n8n_data' volume if needed, prompts the user, and restores the volume data (user data)
	using a temporary container and tar extraction.
.PARAMETER Engine
	Path to the container engine executable (e.g., 'docker' or 'podman'). Mandatory.
.PARAMETER ContainerName
	The name of the container being restored. Used to find backup files and identify volumes. Mandatory.
.PARAMETER BackupFolder
	The directory containing the backup .tar file(s). Defaults to '.\Backup'.
.PARAMETER RestoreVolumes
	Switch parameter. If present, attempts to restore associated volume data (currently specific to 'n8n').
.OUTPUTS
	[string] Returns the name of the loaded image if successful.
	[bool] Returns $false if the image backup file is not found or the image load fails. Volume restore status doesn't affect return value directly, but errors are reported.
.EXAMPLE
	$loadedImage = Restore-ContainerAndData -Engine "docker" -ContainerName "my-app"
	if ($loadedImage) { docker run --name my-app $loadedImage }
.EXAMPLE
	Restore-ContainerAndData -Engine "podman" -ContainerName "n8n" -RestoreVolumes
.NOTES
	Volume restore logic is currently specific to 'n8n' and its 'n8n_data' volume.
	Relies on parsing output from 'engine load'.
	Uses $LASTEXITCODE to check the success of engine commands.
#>
function Restore-ContainerAndData {
	param(
		[Parameter(Mandatory = $true)]
		[string]$Engine,
		[Parameter(Mandatory = $true)]
		[string]$ContainerName,
		[string]$BackupFolder = ".\Backup",
		[switch]$RestoreVolumes = $false
	)

	# First try the container-specific backup format
	$safeName = $ContainerName -replace "[:/]", "_"
	$backupFile = Join-Path $BackupFolder "$safeName-backup.tar"

	# If container-specific backup not found, try to find a matching image backup
	if (-not (Test-Path $backupFile)) {
		# Use Write-Host for status messages
		Write-Host "Container-specific backup file '$backupFile' not found."
		# Use Write-Host for status messages
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
				# Use Write-Host for status messages
				Write-Host "Found potential matching backup: $matchingBackup"
				break
			}
		}

		if ($matchingBackup) {
			$backupFile = $matchingBackup
		}
		else {
			Write-Error "No backup file found for container '$ContainerName'."
			return $false
		}
	}

	# Use Write-Host for status messages
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
		# Use Write-Host for status messages
		Write-Host "Loaded image: $imageName"
	}
	else {
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
				# Use Write-Host for status messages
				Write-Host "Found volume backup for '$volumeName': $volumeBackupFile"

				# Check if volume exists, create if not
				$volumeExists = & $Engine volume ls --filter "name=$volumeName" --format "{{.Name}}"
				if (-not $volumeExists) {
					# Use Write-Host for status messages
					Write-Host "Creating volume '$volumeName'..."
					& $Engine volume create $volumeName
				}

				# Ask for confirmation before restoring volume
				$restoreVolumeConfirm = Read-Host "Restore volume data for '$volumeName'? This will merge with existing data. (Y/N, default is Y)"
				if ($restoreVolumeConfirm -ne "N") {
					# Use Write-Host for status messages
					Write-Host "Restoring volume '$volumeName' from '$volumeBackupFile'..."

					# Create a temporary container to restore the volume data
					$tempContainerName = "restore-volume-$volumeName-$(Get-Random)"

					# Run a temporary container with the volume mounted and extract the backup
					& $Engine run --rm --volume ${volumeName}:/target --volume ${BackupFolder}:/backup --name $tempContainerName alpine tar -xf /backup/$(Split-Path $volumeBackupFile -Leaf) -C /target

					if ($LASTEXITCODE -eq 0) {
						# Use Write-Host for status messages
						Write-Host "Successfully restored volume '$volumeName' from '$volumeBackupFile'"
					}
					else {
						Write-Error "Failed to restore volume '$volumeName'"
					}
				}
				else {
					# Use Write-Host for status messages
					Write-Host "Skipping volume restore as requested."
				}
			}
			else {
				# Use Write-Host for status messages
				Write-Host "No volume backup found for '$volumeName' at '$volumeBackupFile'."
				# Use Write-Host for status messages
				Write-Host "Will continue with container image restore only. Existing volume data will be preserved."
			}
		}
		# For other containers, we would need to determine volume names differently
	}

	# Return the loaded image name
	return $imageName
}

#==============================================================================
# Function: Test-ImageUpdateAvailable
#==============================================================================
<#
.SYNOPSIS
	Checks if a newer version of a container image is available in its remote registry.
.DESCRIPTION
	Compares the digest of the locally available image (if any) with the digest of the image
	in the remote registry. Handles both Docker and Podman engines, using different techniques
	(docker manifest inspect, skopeo inspect, or podman pull/inspect fallback) to get the remote digest.
.PARAMETER Engine
	Path to the container engine executable (e.g., 'docker' or 'podman'). Mandatory.
.PARAMETER ImageName
	The full name and tag of the container image to check (e.g., 'ghcr.io/open-webui/open-webui:main'). Mandatory.
.OUTPUTS
	[bool] Returns $true if the image is not found locally, if digests cannot be determined, or if the remote digest differs from the local digest.
		   Returns $false if the local and remote digests match.
.EXAMPLE
	if (Test-ImageUpdateAvailable -Engine "podman" -ImageName "docker.io/library/alpine:latest") { Invoke-PullImage ... }
.NOTES
	Attempts multiple methods to get remote digest for robustness (docker manifest, skopeo, podman pull).
	Assumes update needed if digests cannot be reliably determined.
#>
function Test-ImageUpdateAvailable {
	[CmdletBinding()]
	[OutputType([bool])]
	param(
		[Parameter(Mandatory = $true)]
		[string]$Engine,

		[Parameter(Mandatory = $true)]
		[string]$ImageName
	)

	# Use Write-Host for status messages
	Write-Host "Checking for updates to $ImageName..."

	# First, check if we have the image locally
	$localImageInfo = & $Engine inspect $ImageName 2>$null | ConvertFrom-Json
	if (-not $localImageInfo) {
		# Use Write-Host for status messages
		Write-Host "Image '$ImageName' not found locally. Update is available."
		return $true
	}

	# Get local image digest
	$localDigest = $null
	try {
		if ($localImageInfo -is [array]) {
			$localDigest = $localImageInfo[0].Id
		}
		else {
			$localDigest = $localImageInfo.Id
		}
	}
	catch {
		Write-Warning "Could not determine local image digest: $_"
		# If we can't determine local digest, assume update is needed
		return $true
	}

	# Use Write-Host for status messages
	Write-Host "Local image digest: $localDigest"

	# Determine container engine type (docker or podman)
	$engineType = "docker"
	if ((Get-Item $Engine).Name -like "*podman*") {
		$engineType = "podman"
	}

	# Pull the image with latest tag but don't update the local image
	# Use Write-Host for status messages
	Write-Host "Checking remote registry for latest version..."

	# Different approach for Docker vs Podman
	if ($engineType -eq "docker") {
		# For Docker, we can use the manifest inspect command
		try {
			$remoteDigest = & $Engine manifest inspect $ImageName --verbose 2>$null | ConvertFrom-Json |
			Select-Object -ExpandProperty Descriptor -ErrorAction SilentlyContinue |
			Select-Object -ExpandProperty digest -ErrorAction SilentlyContinue
		}
		catch {
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
			}
			else {
				$remoteDigest = $remoteImageInfo.Id
			}
		}
	}
	else {
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
			}
			catch {
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
			}
			else {
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

	# Use Write-Host for status messages
	Write-Host "Remote image digest: $remoteDigest"

	# Compare digests
	if ($localDigest -ne $remoteDigest) {
		# Use Write-Host for status messages
		Write-Host "Update available! Local and remote image digests differ."
		return $true
	}
	else {
		# Use Write-Host for status messages
		Write-Host "No update available. You have the latest version."
		return $false
	}
}

#==============================================================================
# Function: Update-Container
#==============================================================================
<#
.SYNOPSIS
	Provides a generic workflow to update a running container to the latest image version.
.DESCRIPTION
	Performs the following steps:
	1. Checks if the container exists.
	2. Checks if a remote image update is available using Test-ImageUpdateAvailable (prompts to force if not).
	3. Optionally prompts to back up the current container state using Backup-ContainerState.
	4. Removes the existing container.
	5. Pulls the latest version of the specified image.
	6. Executes a provided script block (`RunFunction`) to start the new container with the correct configuration.
	7. Offers to restore from backup if the pull or start fails (and a backup was made).
	Supports -WhatIf for backup, remove, pull, and start actions.
.PARAMETER Engine
	Path to the container engine executable (e.g., 'docker' or 'podman'). Mandatory.
.PARAMETER ContainerName
	The name of the container to update. Mandatory.
.PARAMETER ImageName
	The full name and tag of the container image to update to (e.g., 'nginx:latest'). Mandatory.
.PARAMETER Platform
	The target platform for the image pull (e.g., 'linux/amd64'). Defaults to 'linux/amd64'.
.PARAMETER RunFunction
	A script block that contains the specific 'engine run' command needed to start the container
	with its required volumes, ports, environment variables, etc. Mandatory.
.OUTPUTS
	[bool] Returns $true if the update process completes successfully (including the new container start).
		   Returns $false if any critical step fails or the update is canceled.
.EXAMPLE
	$runMyApp = { & $enginePath run --name my-app -p 8080:80 -v my-app-data:/data $imageName }
	Update-Container -Engine "docker" -ContainerName "my-app" -ImageName "my-registry/my-app:latest" -RunFunction $runMyApp
.NOTES
	Relies on several other functions: Test-ImageUpdateAvailable, Backup-ContainerState, Restore-ContainerState.
	User interaction handled via Read-Host for backup and force update prompts.
#>
function Update-Container {
	[CmdletBinding(SupportsShouldProcess = $true)]
	[OutputType([bool])]
	param(
		[Parameter(Mandatory = $true)]
		[string]$Engine,

		[Parameter(Mandatory = $true)]
		[string]$ContainerName,

		[Parameter(Mandatory = $true)]
		[string]$ImageName,

		[string]$Platform = "linux/amd64"
	)

	# Use Write-Host for status messages
	Write-Host "Initiating update for container '$ContainerName'..."

	# Step 1: Check if container exists
	& $Engine inspect $ContainerName 2>$null | Out-Null # Check existence without storing info
	if ($LASTEXITCODE -ne 0) {
		# Use Write-Host for status messages
		Write-Host "Container '$ContainerName' not found. Nothing to update."
		return $false
	}

	# Step 2: Check if an update is available
	$updateAvailable = Test-ImageUpdateAvailable -Engine $Engine -ImageName $ImageName
	if (-not $updateAvailable) {
		$forceUpdate = Read-Host "No update available. Do you want to force an update anyway? (Y/N, default is N)"
		if ($forceUpdate -ne "Y") {
			# Use Write-Host for status messages
			Write-Host "Update canceled. No changes made."
			return $false
		}
		# Use Write-Host for status messages
		Write-Host "Proceeding with forced update..."
	}

	# Step 3: Optionally backup the container
	$createBackup = Read-Host "Create backup before updating? (Y/N, default is Y)"
	if ($createBackup -ne "N") {
		if ($PSCmdlet.ShouldProcess($ContainerName, "Backup Container State")) {
			# Use Write-Host for status messages
			Write-Host "Creating backup of current container..."
			Backup-ContainerState -Engine $Engine -ContainerName $ContainerName
		}
	}

	# Step 4: Remove the existing container
	if ($PSCmdlet.ShouldProcess($ContainerName, "Remove Container for Update")) {
		# Use Write-Host for status messages
		Write-Host "Removing existing container '$ContainerName' as part of the update..."
		& $Engine rm --force $ContainerName
		if ($LASTEXITCODE -ne 0) {
			Write-Error "Failed to remove container '$ContainerName'. Update aborted."
			return $false
		}
	}

	# Step 5: Pull the latest image
	if ($PSCmdlet.ShouldProcess($ImageName, "Pull Latest Image")) {
		# Use Write-Host for status messages
		Write-Host "Pulling latest image '$ImageName'..."
		& $Engine pull --platform $Platform $ImageName
		if ($LASTEXITCODE -ne 0) {
			Write-Error "Failed to pull the latest image. Update aborted."

			# Offer to restore from backup if one was created
			if ($createBackup -ne "N") {
				$restore = Read-Host "Would you like to restore from backup? (Y/N, default is Y)"
				if ($restore -ne "N") {
					if ($PSCmdlet.ShouldProcess($ContainerName, "Restore Container State after Failed Update")) {
						# Call the renamed function
						Restore-ContainerAndData -Engine $Engine -ContainerName $ContainerName
					}
				}
			}
			return $false
		}
		# Use Write-Host for status messages
		Write-Host "Image '$ImageName' pulled successfully."
		# Indicate success if image pull was successful
		return $true
	}
	else {
		# If ShouldProcess returned false for pulling the image
		Write-Warning "Image pull skipped due to -WhatIf."
		# Consider it a success in terms of -WhatIf, as the pre-update steps completed
		return $true
	}
}

#==============================================================================
# Function: Show-ContainerStatus
#==============================================================================
<#
.SYNOPSIS
	Displays status information and performs connectivity tests for a specified container.
.DESCRIPTION
	Shows basic information like container name, engine, and any additional configuration provided.
	Checks the container's running status using 'engine ps'.
	If the container is running, performs optional network connectivity tests:
	- TCP port check using Test-TCPPort.
	- HTTP endpoint check using Test-HTTPPort.
	- WebSocket endpoint check using Test-WebSocketPort (requires Setup_0_Network.ps1).
	Pauses for a specified number of seconds after displaying the information.
.PARAMETER ContainerName
	The name of the container to check. Mandatory.
.PARAMETER ContainerEngine
	The name of the container engine being used (e.g., "docker", "podman"). Mandatory.
.PARAMETER EnginePath
	The full path to the container engine executable. Mandatory.
.PARAMETER DisplayName
	An optional friendly name for the container to display in the output. Defaults to ContainerName.
.PARAMETER ContainerUrl
	An optional base URL (e.g., 'http://localhost:8080') used for constructing HTTP/WS test URIs if specific ports aren't provided.
.PARAMETER TcpPort
	Optional. The TCP port number on localhost to test connectivity to.
.PARAMETER HttpPort
	Optional. The HTTP port number on localhost to test connectivity to. If ContainerUrl is not set, defaults to http://localhost:<HttpPort>.
.PARAMETER HttpPath
	Optional. The path component for the HTTP test URI. Defaults to '/'.
.PARAMETER WsPort
	Optional. The WebSocket port number on localhost to test connectivity to. If ContainerUrl is not set, defaults to ws://localhost:<WsPort>.
.PARAMETER WsPath
	Optional. The path component for the WebSocket test URI.
.PARAMETER DelaySeconds
	Optional. The number of seconds to pause after displaying the status. Defaults to 3.
.PARAMETER AdditionalInfo
	Optional. A hashtable containing extra key-value pairs to display under 'Additional Configuration'.
.EXAMPLE
	Show-ContainerStatus -ContainerName "webserver" -ContainerEngine "docker" -EnginePath "docker" -HttpPort 80 -TcpPort 80
.EXAMPLE
	$info = @{ "Volume" = "data:/var/www"; "Network" = "web-net" }
	Show-ContainerStatus -ContainerName "app-db" -ContainerEngine "podman" -EnginePath "podman" -DisplayName "Application Database" -TcpPort 5432 -AdditionalInfo $info -DelaySeconds 5
.NOTES
	Relies on Test-TCPPort, Test-HTTPPort (from Setup_0_Network.ps1).
	Relies on Test-WebSocketPort (from Setup_0_Network.ps1). Checks for its existence before calling.
#>
function Show-ContainerStatus {
	[CmdletBinding()]
	param(
		[Parameter(Mandatory = $true)]
		[string]$ContainerName,

		[Parameter(Mandatory = $true)]
		[string]$ContainerEngine,

		[Parameter(Mandatory = $true)]
		[string]$EnginePath,

		[Parameter(Mandatory = $false)]
		[string]$DisplayName = $ContainerName,

		[Parameter(Mandatory = $false)]
		[string]$ContainerUrl,

		[Parameter(Mandatory = $false)]
		[int]$TcpPort,

		[Parameter(Mandatory = $false)]
		[int]$HttpPort,

		[Parameter(Mandatory = $false)]
		[string]$HttpPath = '/',

		[Parameter(Mandatory = $false)]
		[int]$WsPort,

		[Parameter(Mandatory = $false)]
		[string]$WsPath,

		[Parameter(Mandatory = $false)]
		[int]$DelaySeconds = 3,

		[Parameter(Mandatory = $false)]
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
	}
	else {
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
				}
				else {
					Write-Warning "Test-WebSocketPort function not found (is Setup_0_Network.ps1 sourced?). Skipping WebSocket test."
				}
			}
		}
		else {
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
