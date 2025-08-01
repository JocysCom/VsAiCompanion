################################################################################
# Description  : Contains container image backup/restore functions:
#                - Backup-ContainerImage: Backup a single image.
#                - Invoke-ContainerImageBackup: Backup all images. (Formerly Backup-ContainerImages)
#                - Restore-ContainerImage: Restore a single image.
#                - Start-RestoredContainer: Helper to run a restored image.
#                - Invoke-ContainerImageRestore: Restore all images from a folder. (Formerly Restore-ContainerImages)
#                - Test-AndRestoreBackup: Check for backup and prompt to restore.
################################################################################

# Global Variables
$global:backupFolder = ".\Backup"
$global:dockerImagesFolder = "docker_images"
$global:dockerVolumesFolder = "docker_volumes"

#==============================================================================
# Function: ConvertTo-WSLPath
#==============================================================================
<#
.SYNOPSIS
   Converts a Windows path into a WSL (Linux) path.
.DESCRIPTION
   This function takes an absolute Windows path and converts it to the corresponding WSL
   path by replacing the drive letter and backslashes with the Linux mount point format
   (e.g., C:\Users\Me becomes /mnt/c/Users/Me).
   IMPORTANT: This workaround is CRUCIAL for successfully copying a file from the local
   machine to Podman using 'podman machine ssh "podman cp ..."'.
.PARAMETER winPath
   The Windows path to convert. Mandatory.
.OUTPUTS
   [string] The converted WSL path, or the original path if conversion fails.
.EXAMPLE
   $wslPath = ConvertTo-WSLPath -winPath "C:\MyFolder\MyFile.txt"
   # $wslPath will be "/mnt/c/MyFolder/MyFile.txt"
.NOTES
   Uses Resolve-Path to get the absolute path first.
   Uses regex matching and replacement.
   Copied from Setup_2a_Pipelines.ps1
#>
function ConvertTo-WSLPath {
	param(
		[Parameter(Mandatory = $true)]
		[string]$winPath
	)
	# Ensure the path exists before trying to resolve (needed for destination paths)
	$itemExists = Test-Path -Path $winPath
	if (-not $itemExists) {
		# If it doesn't exist, try resolving the parent directory
		$parentDir = Split-Path -Path $winPath -Parent
		if (Test-Path -Path $parentDir) {
			$resolvedParent = (Resolve-Path $parentDir).Path
			$filename = Split-Path -Path $winPath -Leaf
			$absPath = Join-Path -Path $resolvedParent -ChildPath $filename
		}
		else {
			Write-Warning "Cannot resolve path or its parent: '$winPath'. Using original path for conversion attempt."
			$absPath = $winPath # Fallback
		}
	}
	else {
		$absPath = (Resolve-Path $winPath).Path
	}

	if ($absPath -match '^([A-Z]):\\(.*)$') {
		$drive = $matches[1].ToLower()
		$pathWithoutDrive = $matches[2]
		$unixPath = $pathWithoutDrive -replace '\\', '/'
		return "/mnt/$drive/$unixPath"
	}
	else {
		Write-Warning "Path '$winPath' does not match the expected Windows absolute path format."
		return $absPath # Return original path on failure
	}
}

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
	Uses $LASTEXITCODE to check the success of the engine's save command.
#>
function Backup-ContainerImage {
	[CmdletBinding()]
	[OutputType([bool])]
	param(
		[Parameter(Mandatory = $true)]
		[string]$Engine,
		[Parameter(Mandatory = $true)]
		[string]$ImageName, # Changed from ContainerName to ImageName
		[string]$BackupFolder = $global:backupFolder
	)

	$imageBackupPath = Join-Path $BackupFolder $global:dockerImagesFolder
	Write-Host "Saving image '$ImageName' to '$imageBackupPath'..."

	# Ensure backup folder exists
	if (-not (Test-Path $imageBackupPath)) {
		New-Item -ItemType Directory -Force -Path $imageBackupPath | Out-Null
		Write-Host "Created image backup folder: $imageBackupPath"
	}

	# Generate timestamped filename using .tar extension
	$timestamp = Get-Date -Format "yyyyMMdd-HHmm"
	# Use a safe name for the file, replacing problematic characters
	$safeImageName = $ImageName -replace "[:/]", "_"
	$backupFileName = "$safeImageName-image-$timestamp.tar"
	$hostWinFilePath = Join-Path $imageBackupPath $backupFileName

	# podman save [options] IMAGE
	# save      Save an image to a tar archive.
	# --output string   Specify the output file for saving the image.
	Write-Host "& $Engine save --output '$hostWinFilePath' '$ImageName'"
	& $Engine save --output $hostWinFilePath $ImageName
	$success = $LASTEXITCODE -eq 0
	if ($success) {
		Write-Host "Successfully backed up image '$ImageName'" -ForegroundColor Green
	}
	else {
		Write-Error "Failed to backup image '$ImageName'"
	}
	return $success
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
	Relies on the output format of 'engine load --input ...' to parse the image name (e.g., "Loaded image: ...").
	Uses $LASTEXITCODE to check the success of the engine's load command.
#>
function Restore-ContainerImage {
	[CmdletBinding()]
	[OutputType([bool])]
	param(
		[Parameter(Mandatory = $true)]
		[string]$Engine,
		[Parameter(Mandatory = $true)]
		[string]$BackupFile, # Changed to accept BackupFile directly
		[Parameter(Mandatory = $false)]
		[switch]$RunContainer = $false # Added RunContainer parameter
	)

	Write-Host "Loading image from '$BackupFile'..."

	if (-not (Test-Path $BackupFile)) {
		Write-Error "Backup file '$BackupFile' not found."
		return $false
	}

	# No need to inspect container or get image name from it.
	# The image name will be parsed from the 'load' command output.

	$hostWinFilePath = $BackupFile # BackupFile is already the full path

	$output = & $Engine load --input $hostWinFilePath

	if ($LASTEXITCODE -eq 0) {
		# Use Write-Host for status messages
		Write-Host "Successfully restored image from '$hostWinFilePath'."

		# Attempt to parse the image name from the load output
		# Expected output example: "Loaded image: docker.io/open-webui/pipelines:custom"
		$imageName = $null
		if ($output -match "Loaded image:\s*(\S+)") {
			$imageName = $matches[1].Trim()
			# Use Write-Host for status messages
			Write-Host "Parsed image name: $imageName"

			if ($RunContainer) {
				Start-RestoredContainer -Engine $Engine -ImageName $imageName
			}
			return $true
		}
		else {
			# Use Write-Host for status messages
			Write-Host "Could not parse image name from the load output."
			# Still return true as the image was loaded, just couldn't parse name
			return $true
		}
	}
	else {
		Write-Error "Failed to restore image from '$hostWinFilePath'."
		return $false
	}
}

#==============================================================================
# Function: Backup-ContainerVolume
#==============================================================================
<#
.SYNOPSIS
	Backs up a single container volume to a timestamped tar file.
.DESCRIPTION
	Exports the specified container volume using the provided container engine (docker or podman)
	to a .tar file in the specified backup folder.
.PARAMETER EngineType
	The type of the container engine ('docker' or 'podman').
.PARAMETER VolumeName
	The name of the container volume to back up. Mandatory.
.PARAMETER BackupFolder
	The directory where the backup .tar file will be saved. Defaults to '.\Backup'.
.OUTPUTS
	[string] Returns the full path to the created backup file on success, $null on failure.
.EXAMPLE
	Backup-ContainerVolume -EngineType "podman" -VolumeName "my_data" -BackupFolder "C:\MyBackups"
.EXAMPLE
	Backup-ContainerVolume -EngineType "docker" -VolumeName "app_data"
#>
function Backup-ContainerVolume {
	[OutputType([string])]
	param(
		[Parameter(Mandatory = $true)]
		[ValidateSet("docker", "podman")]
		[string]$EngineType,

		[Parameter(Mandatory = $true)]
		[string]$VolumeName,

		[string]$BackupFolder = $global:backupFolder
	)

	$volumeBackupPath = Join-Path $BackupFolder $global:dockerVolumesFolder
	Write-Host "Saving volume '$VolumeName' to '$volumeBackupPath'..."

	# Ensure backup folder exists
	if (-not (Test-Path $volumeBackupPath)) {
		New-Item -ItemType Directory -Force -Path $volumeBackupPath | Out-Null
		Write-Host "Created volume backup folder: $volumeBackupPath"
	}

	# Generate timestamped filename using .tar extension
	$timestamp = Get-Date -Format "yyyyMMdd-HHmm"
	$backupFileName = "$VolumeName-volume-$timestamp.tar"
	$hostWinFilePath = Join-Path $volumeBackupPath $backupFileName
	$hostWslFilePath = ConvertTo-WSLPath -winPath $hostWinFilePath

	# Command runs inside the container engine's context.
	Write-Host "$EngineType machine ssh ""$EngineType volume export '$VolumeName' --output '$hostWslFilePath'"""
	& $EngineType machine ssh "$EngineType volume export '$VolumeName' --output '$hostWslFilePath'"
	$success = $LASTEXITCODE -eq 0
	if ($success) {
		Write-Host "Successfully backed up volume '$VolumeName' to '$hostWinFilePath'" -ForegroundColor Green
	}
	else {
		Write-Error "Failed to backup volume '$VolumeName'"
	}
	return $hostWinFilePath
}

#==============================================================================
# Function: Restore-ContainerVolume
#==============================================================================
<#
.SYNOPSIS
	Restores a container volume from a backup .tar file, prompting user for selection.
.DESCRIPTION
	Finds backup files matching '{VolumeName}-volume-*.tar' in the specified backup folder.
	Presents a numbered list sorted by date (newest first) and prompts the user to select one.
	Restores the selected backup into the specified volume using a temporary alpine container
	and the 'tar' command. Creates the volume if it doesn't exist.
.PARAMETER EngineType
	The type of the container engine ('docker' or 'podman'). Mandatory. Used to select the correct command.
.PARAMETER VolumeName
	The name of the container volume to restore into. Mandatory.
.PARAMETER BackupFolder
	The directory where the backup .tar files are located. Defaults to '.\Backup'.
.OUTPUTS
	[bool] Returns $true on success, $false on failure or cancellation.
.EXAMPLE
	Restore-ContainerVolume -Engine "podman" -EngineType "podman" -VolumeName "my_data"
.EXAMPLE
	Restore-ContainerVolume -Engine "docker" -EngineType "docker" -VolumeName "app_data" -BackupFolder "C:\MyBackups"
.NOTES
	Uses $LASTEXITCODE to check command success. Requires 'alpine' image.
	Will overwrite existing volume content.
#>
function Restore-ContainerVolume {
	[OutputType([bool])]
	param(
		[Parameter(Mandatory = $true)]
		[ValidateSet("docker", "podman")]
		[string]$EngineType,

		[Parameter(Mandatory = $true)]
		[string]$VolumeName,

		[string]$BackupFolder = $global:backupFolder
	)

	$volumeBackupPath = Join-Path $BackupFolder $global:dockerVolumesFolder
	Write-Host "Restoring volume '$VolumeName' from '$volumeBackupPath'..."

	# Find available backup files for the specified volume
	$backupPattern = "$VolumeName-volume-*.tar"
	$backupFiles = Get-ChildItem -Path $volumeBackupPath -Filter $backupPattern | Sort-Object LastWriteTime -Descending

	if (-not $backupFiles) {
		Write-Error "No backup files found for volume '$VolumeName' in folder '$BackupFolder' matching pattern '$backupPattern'."
		return $false
	}

	[string[]]$fileNames = (Get-ChildItem -Path $BackupFolder -Filter $backupPattern | Select-Object -ExpandProperty Name);
	[string[]]$menuOptions = $fileNames.Clone();
	$exit = "Exit menu"
	$menuOptions += $exit;
	$backupFileName = Invoke-OptionsMenu -Title "Restore Container Volume" -Options $menuOptions -ExitChoice $exit
	if ($backupFileName -eq $exit -or [string]::IsNullOrWhiteSpace($backupFileName)) {
		Write-Host "Restore cancelled by user."
		return $false
	}

	$hostWinFilePath = Join-Path $volumeBackupPath $backupFileName
	$hostWslFilePath = ConvertTo-WSLPath -winPath $hostWinFilePath

	# Command runs inside the container engine's context.
	Write-Host "$EngineType machine ssh ""$EngineType volume import '$VolumeName' '$hostWslFilePath'"""
	& $EngineType machine ssh "$EngineType volume import '$VolumeName' '$hostWslFilePath'"
	$success = $LASTEXITCODE -eq 0
	if ($success) {
		Write-Host "Successfully restored volume '$VolumeName' from '$hostWinFilePath'" -ForegroundColor Green
	}
	else {
		Write-Error "Failed to restore volume '$VolumeName'"
	}
	return $success
}

#==============================================================================
# Function: Copy-MachineToHost
#==============================================================================
<#
.SYNOPSIS
    Copies a file or directory from a container (Docker or Podman) to the host machine.
.DESCRIPTION
    Handles the differences between Docker 'cp' and Podman 'cp' (which requires 'machine ssh'
    and WSL path conversion on Windows). Creates the destination directory if it doesn't exist.
.PARAMETER EnginePath
    The full path to the container engine executable (docker.exe or podman.exe). Mandatory.
.PARAMETER ContainerEngineType
    The type of container engine ('docker' or 'podman'). Mandatory.
.PARAMETER ContainerName
    The name of the container from which to copy. Mandatory.
.PARAMETER ContainerSourcePath
    The path to the file or directory inside the container. Mandatory.
.PARAMETER HostDestinationPath
    The path on the host machine where the file or directory should be copied. Mandatory.
.OUTPUTS
    [bool] $true if the copy operation was successful, $false otherwise.
.EXAMPLE
    Copy-MachineToHost -EnginePath "C:\Program Files\Docker\Docker\resources\bin\docker.exe" `
                       -ContainerEngineType "docker" `
                       -ContainerName "my-app" `
                       -ContainerSourcePath "/app/data.txt" `
                       -HostDestinationPath "C:\Downloads\data.txt"
.EXAMPLE
    Copy-MachineToHost -EnginePath "C:\Program Files\RedHat\Podman\podman.exe" `
                       -ContainerEngineType "podman" `
                       -ContainerName "my-podman-app" `
                       -ContainerSourcePath "/data/config.json" `
                       -HostDestinationPath "C:\Temp\config.json"
.NOTES
    Relies on the ConvertTo-WSLPath function (expected to be available in the same scope)
    when using Podman on Windows.
    Uses Write-Host for status messages for consistency with other functions in this file,
    although Write-Information might be preferred according to .clinerules.
#>
function Copy-MachineToHost {
	[CmdletBinding(SupportsShouldProcess = $true)]
	[OutputType([bool])]
	param(
		[Parameter(Mandatory = $true)]
		[string]$EnginePath,

		[Parameter(Mandatory = $true)]
		[ValidateSet("docker", "podman")]
		[string]$ContainerEngineType,

		[Parameter(Mandatory = $true)]
		[string]$ContainerName,

		[Parameter(Mandatory = $true)]
		[string]$ContainerSourcePath,

		[Parameter(Mandatory = $true)]
		[string]$HostDestinationPath
	)

	$targetDescription = "file '$ContainerSourcePath' from container '$ContainerName' to host '$HostDestinationPath'"
	if (-not $PSCmdlet.ShouldProcess($targetDescription, "Copy")) {
		Write-Host "Skipped copy due to -WhatIf or user cancellation."
		return $false
	}

	# Ensure destination directory exists on host
	$destinationDir = Split-Path -Path $HostDestinationPath -Parent
	if (-not (Test-Path -Path $destinationDir -PathType Container)) {
		Write-Host "Creating destination directory: $destinationDir"
		try {
			New-Item -Path $destinationDir -ItemType Directory -Force -ErrorAction Stop | Out-Null
		}
		catch {
			Write-Error "Failed to create destination directory '$destinationDir'. Error: $_"
			return $false
		}
	}

	Write-Host "Copying '$ContainerSourcePath' from $ContainerEngineType container '$ContainerName' to '$HostDestinationPath'..."
	$copySuccess = $false
	$cpResult = $null
	$LASTEXITCODE = 0 # Reset before command

	try {
		if ($ContainerEngineType -eq "docker") {
			# Docker cp syntax: docker cp <container>:<src_path> <dest_path>
			$cpCommandArgs = @(
				"cp",
				"$($ContainerName):$ContainerSourcePath",
				$HostDestinationPath
			)
			Write-Host "Running cp command: $EnginePath $($cpCommandArgs -join ' ')"
			$cpResult = & $EnginePath @cpCommandArgs 2>&1
		}
		else {
			# Podman requires WSL path for destination and execution via 'machine ssh'
			# Podman cp syntax (inside ssh): podman cp <container>:<src_path> <dest_path_wsl>
			$wslDestinationFilePath = ConvertTo-WSLPath -winPath $HostDestinationPath
			if ($wslDestinationFilePath -eq $HostDestinationPath) {
				# Conversion likely failed, ConvertTo-WSLPath should issue a warning.
				throw "Failed to convert Windows destination path '$HostDestinationPath' to a WSL path. Cannot proceed with Podman copy."
			}
			# Escape single quotes in paths for the inner command string
			$escapedContainerPath = $ContainerSourcePath -replace "'", "'\''"
			$escapedWslDestPath = $wslDestinationFilePath -replace "'", "'\''"
			$innerCpCommand = "podman cp '$ContainerName`:$escapedContainerPath' '$escapedWslDestPath'"
			# Outer quotes handle spaces in EnginePath if needed, inner command is single arg to ssh
			$sshArgs = @(
				"machine",
				"ssh",
				$innerCpCommand # The command string to execute inside the SSH session
			)
			Write-Host "Running cp command via podman machine ssh: $EnginePath $($sshArgs -join ' ')"
			$cpResult = & $EnginePath @sshArgs 2>&1
		}

		Write-Host "Cp result: $cpResult" # Display output regardless of exit code for debugging
		if ($LASTEXITCODE -ne 0) {
			Write-Error "Copy operation failed. Exit Code: $LASTEXITCODE." # Removed Output from here as it's already printed
			throw "Copy command failed with exit code $LASTEXITCODE."
		}

		# Verify the file/dir exists at the destination
		if (Test-Path -Path $HostDestinationPath) {
			Write-Host "Successfully copied to '$HostDestinationPath'." -ForegroundColor Green
			$copySuccess = $true
		}
		else {
			# This case might happen if the source path didn't exist in the container, but 'cp' still returned 0.
			Write-Error "Copy command reported success (Exit Code 0), but the destination '$HostDestinationPath' was not found or is empty. Check source path existence in container or command output: $cpResult"
			$copySuccess = $false
		}
	}
	catch {
		Write-Error "An error occurred during the copy operation: $_"
		$copySuccess = $false
	}

	return $copySuccess
}

#==============================================================================
# Function: Copy-HostToMachine
#==============================================================================
<#
.SYNOPSIS
    Copies a file or directory from the host machine into a container (Docker or Podman).
.DESCRIPTION
    Handles the differences between Docker 'cp' and Podman 'cp' (which requires 'machine ssh'
    and WSL path conversion for the source path on Windows). Checks if the source exists on the host.
.PARAMETER EnginePath
    The full path to the container engine executable (docker.exe or podman.exe). Mandatory.
.PARAMETER ContainerEngineType
    The type of container engine ('docker' or 'podman'). Mandatory.
.PARAMETER ContainerName
    The name of the container into which to copy. Mandatory.
.PARAMETER HostSourcePath
    The path to the file or directory on the host machine to copy from. Mandatory.
.PARAMETER ContainerDestinationPath
    The path inside the container where the file or directory should be copied. Mandatory.
.OUTPUTS
    [bool] $true if the copy operation was successful, $false otherwise.
.EXAMPLE
    Copy-HostToMachine -EnginePath "C:\Program Files\Docker\Docker\resources\bin\docker.exe" `
                       -ContainerEngineType "docker" `
                       -ContainerName "my-app" `
                       -HostSourcePath "C:\Uploads\config.json" `
                       -ContainerDestinationPath "/app/config.json"
.EXAMPLE
    Copy-HostToMachine -EnginePath "C:\Program Files\RedHat\Podman\podman.exe" `
                       -ContainerEngineType "podman" `
                       -ContainerName "my-podman-app" `
                       -HostSourcePath "C:\Temp\data.txt" `
                       -ContainerDestinationPath "/data/data.txt"
.NOTES
    Relies on the ConvertTo-WSLPath function (expected to be available in the same scope)
    when using Podman on Windows to convert the host source path.
    Uses Write-Host for status messages for consistency with other functions in this file.
#>
function Copy-HostToMachine {
	[CmdletBinding(SupportsShouldProcess = $true)]
	[OutputType([bool])]
	param(
		[Parameter(Mandatory = $true)]
		[string]$EnginePath,

		[Parameter(Mandatory = $true)]
		[ValidateSet("docker", "podman")]
		[string]$ContainerEngineType,

		[Parameter(Mandatory = $true)]
		[string]$ContainerName,

		[Parameter(Mandatory = $true)]
		[string]$HostSourcePath,

		[Parameter(Mandatory = $true)]
		[string]$ContainerDestinationPath
	)

	# Check if source exists on host
	if (-not (Test-Path -Path $HostSourcePath)) {
		Write-Error "Host source path '$HostSourcePath' not found."
		return $false
	}

	$targetDescription = "file '$HostSourcePath' from host to container '$($ContainerName):$($ContainerDestinationPath)'"
	if (-not $PSCmdlet.ShouldProcess($targetDescription, "Copy")) {
		Write-Host "Skipped copy due to -WhatIf or user cancellation."
		return $false
	}

	Write-Host "Copying '$HostSourcePath' from host to $ContainerEngineType container '$($ContainerName):$($ContainerDestinationPath)'..."
	$copySuccess = $false
	$cpResult = $null
	$LASTEXITCODE = 0 # Reset before command

	try {
		if ($ContainerEngineType -eq "docker") {
			# Docker cp syntax: docker cp <host_src_path> <container>:<dest_path>
			$cpCommandArgs = @(
				"cp",
				$HostSourcePath,
				"$($ContainerName):$ContainerDestinationPath"
			)
			Write-Host "Running cp command: $EnginePath $($cpCommandArgs -join ' ')"
			$cpResult = & $EnginePath @cpCommandArgs 2>&1
		}
		else {
			# Podman requires WSL path for source and execution via 'machine ssh'
			# Podman cp syntax (inside ssh): podman cp <host_src_path_wsl> <container>:<dest_path>
			$wslSourceFilePath = ConvertTo-WSLPath -winPath $HostSourcePath
			if ($wslSourceFilePath -eq $HostSourcePath) {
				# Conversion likely failed, ConvertTo-WSLPath should issue a warning.
				throw "Failed to convert Windows source path '$HostSourcePath' to a WSL path. Cannot proceed with Podman copy."
			}
			# Escape single quotes in paths for the inner command string
			$escapedWslSourcePath = $wslSourceFilePath -replace "'", "'\''"
			$escapedContainerDestPath = $ContainerDestinationPath -replace "'", "'\''"
			$innerCpCommand = "podman cp '$escapedWslSourcePath' '$ContainerName`:$escapedContainerDestPath'"
			# Outer quotes handle spaces in EnginePath if needed, inner command is single arg to ssh
			$sshArgs = @(
				"machine",
				"ssh",
				$innerCpCommand # The command string to execute inside the SSH session
			)
			Write-Host "Running cp command via podman machine ssh: $EnginePath $($sshArgs -join ' ')"
			$cpResult = & $EnginePath @sshArgs 2>&1
		}

		Write-Host "Cp result: $cpResult" # Display output regardless of exit code for debugging
		if ($LASTEXITCODE -ne 0) {
			Write-Error "Copy operation failed. Exit Code: $LASTEXITCODE."
			throw "Copy command failed with exit code $LASTEXITCODE."
		}

		# Verification inside the container is tricky and often not necessary. Assume success if exit code is 0.
		Write-Host "Successfully initiated copy to '$($ContainerName):$($ContainerDestinationPath)'. Verify inside container if needed." -ForegroundColor Green
		$copySuccess = $true

	}
	catch {
		Write-Error "An error occurred during the copy operation: $_"
		$copySuccess = $false
	}

	return $copySuccess
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
	Relies on the output format of 'engine images --format "{{.Repository}}:{{.Tag}}"''.
	Formerly named Backup-ContainerImages.
#>
function Invoke-ContainerImageBackup {
	[CmdletBinding()]
	[OutputType([bool])]
	param(
		[Parameter(Mandatory = $true)]
		[string]$Engine,

		[string]$BackupFolder = $global:backupFolder
	)

	$imageBackupPath = Join-Path $BackupFolder $global:dockerImagesFolder
	# Ensure image backup folder exists
	if (-not (Test-Path $imageBackupPath)) {
		New-Item -ItemType Directory -Force -Path $imageBackupPath | Out-Null
		Write-Host "Created image backup folder: $imageBackupPath"
	}

	# Use Write-Host for status messages
	Write-Host "Retrieving list of images for $Engine..."
	$images = & $Engine images --format "{{.Repository}}:{{.Tag}}" | Where-Object { $_ -ne "<none>:<none>" }

	if (-not $images) {
		# Use Write-Host for status messages
		Write-Host "No images found for $Engine."
		return $false
	}

	$successCount = 0
	foreach ($image in $images) {
		# Call the singular version
		if (Backup-ContainerImage -Engine $Engine -ImageName $image -BackupFolder $imageBackupPath) {
			$successCount++
		}
	}

	# Use Write-Host for status messages
	Write-Host "Backed up $successCount out of $($images.Count) images."
	return ($successCount -gt 0)
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
	Uses $LASTEXITCODE to check the success of the engine's run command.
#>
function Start-RestoredContainer {
	[CmdletBinding(SupportsShouldProcess = $true)]
	[OutputType([bool])]
	param(
		[Parameter(Mandatory = $true)]
		[string]$Engine,

		[Parameter(Mandatory = $true)]
		[string]$ImageName
	)

	# Generate a container name by replacing ':' and '/' with underscores
	$containerName = ($ImageName -replace "[:/]", "_") + "_container"

	# Use Write-Host for status messages
	Write-Host "Starting container from image '$ImageName' with container name '$containerName'..."

	# Check if the action should be performed
	if ($PSCmdlet.ShouldProcess("container '$containerName' from image '$ImageName'", "Start")) {
		# podman run [options] IMAGE [COMMAND [ARG...]]
		# run         Run a command in a new container.
		# --detach    Run container in background and print container ID.
		# --name      Assign a name to the container.
		& $Engine run --detach --name $containerName $ImageName

		if ($LASTEXITCODE -eq 0) {
			# Use Write-Host for status messages
			Write-Host "Container '$containerName' started successfully."
			return $true
		}
		else {
			Write-Error "Failed to start container from image '$ImageName'."
			return $false
		}
	} # Closing brace for ShouldProcess block
	else {
		# If ShouldProcess returns false (e.g., user chose "No" or used -WhatIf)
		Write-Host "Skipped starting container '$containerName' due to ShouldProcess."
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
h#>
function Invoke-ContainerImageRestore {
	[CmdletBinding()]
	[OutputType([bool])]
	param(
		[Parameter(Mandatory = $true)]
		[string]$Engine,

		[string]$BackupFolder = $global:backupFolder,

		[switch]$RunContainers = $false
	)

	$imageBackupPath = Join-Path $BackupFolder $global:dockerImagesFolder
	if (-not (Test-Path $imageBackupPath)) {
		# Use Write-Host for status messages
		Write-Host "Image backup folder '$imageBackupPath' does not exist. Nothing to restore."
		return $false
	}

	$tarFiles = Get-ChildItem -Path $imageBackupPath -Filter "*-image-*.tar" # Filter specifically for image backups
	if (-not $tarFiles) {
		# Use Write-Host for status messages
		Write-Host "No image backup tar files found in '$imageBackupPath'."
		return $false
	}

	$successCount = 0
	foreach ($file in $tarFiles) {
		# Call the singular version
		if (Restore-ContainerImage -Engine $Engine -BackupFile $file.FullName -RunContainer:$RunContainers) {
			$successCount++
		}
	}

	# Use Write-Host for status messages
	Write-Host "Restored $successCount out of $($tarFiles.Count) images."
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

		[string]$BackupFolder = $global:backupFolder
	)

	$imageBackupPath = Join-Path $BackupFolder $global:dockerImagesFolder
	
	# Look for backup files matching the image pattern
	$safeName = $ImageName -replace "[:/]", "_"
	$backupPattern = "$safeName-image-*.tar"
	$backupFiles = Get-ChildItem -Path $imageBackupPath -Filter $backupPattern | Sort-Object LastWriteTime -Descending

	if (-not $backupFiles) {
		# Use Write-Host for status messages
		Write-Host "No backup files found for image '$ImageName' in folder '$imageBackupPath'."
		return $false
	}

	$backupFile = $backupFiles[0].FullName # Use the most recent backup
	# Use Write-Host for status messages
	Write-Host "Backup file found for image '$ImageName': $backupFile"
	$choice = Read-Host "Do you want to restore the backup for '$ImageName'? (Y/N, default N)"
	if ($choice -and $choice.ToUpper() -eq "Y") {
		# Call the singular version with the correct parameter
		return (Restore-ContainerImage -Engine $Engine -BackupFile $backupFile)
	}
	else {
		# Use Write-Host for status messages
		Write-Host "User opted not to restore backup for image '$ImageName'."
		return $false
	}
}

#==============================================================================
# Function: Get-AvailableImages
#==============================================================================
<#
.SYNOPSIS
	Retrieves a list of all available container images.
.DESCRIPTION
	Queries the specified container engine for a list of all images,
	excluding intermediate or untagged images (<none>:<none>).
.PARAMETER Engine
	Path to the container engine executable (e.g., 'docker' or 'podman'). Mandatory.
.OUTPUTS
	[string[]] An array of image names (e.g., 'nginx:latest', 'ubuntu:20.04').
.EXAMPLE
	$images = Get-AvailableImages -Engine "docker"
	$images | ForEach-Object { Write-Host $_ }
.NOTES
	Uses 'engine images --format' to get a clean list.
#>
function Get-AvailableImages {
	[CmdletBinding()]
	[OutputType([string[]])]
	param(
		[Parameter(Mandatory = $true)]
		[string]$Engine
	)

	Write-Host "Retrieving list of available images..."
	try {
		$images = & $Engine images --format "{{.Repository}}:{{.Tag}}" 2>$null | Where-Object { $_ -ne "<none>:<none>" }
		if ($images) {
			Write-Host "Found $($images.Count) images." -ForegroundColor Green
			return $images
		}
		else {
			Write-Host "No images found." -ForegroundColor Yellow
			return @()
		}
	}
	catch {
		Write-Error "Failed to retrieve images: $_"
		return @()
	}
}

#==============================================================================
# Function: Get-AvailableVolumes
#==============================================================================
<#
.SYNOPSIS
	Retrieves a list of all available container volumes.
.DESCRIPTION
	Queries the specified container engine for a list of all named volumes.
.PARAMETER Engine
	Path to the container engine executable (e.g., 'docker' or 'podman'). Mandatory.
.OUTPUTS
	[string[]] An array of volume names.
.EXAMPLE
	$volumes = Get-AvailableVolumes -Engine "podman"
	$volumes | ForEach-Object { Write-Host $_ }
.NOTES
	Uses 'engine volume ls --format' to get a clean list.
#>
function Get-AvailableVolumes {
	[CmdletBinding()]
	[OutputType([string[]])]
	param(
		[Parameter(Mandatory = $true)]
		[string]$Engine
	)

	Write-Host "Retrieving list of available volumes..."
	try {
		$volumes = & $Engine volume ls --format "{{.Name}}" 2>$null
		if ($volumes) {
			Write-Host "Found $($volumes.Count) volumes." -ForegroundColor Green
			return $volumes
		}
		else {
			Write-Host "No volumes found." -ForegroundColor Yellow
			return @()
		}
	}
	catch {
		Write-Error "Failed to retrieve volumes: $_"
		return @()
	}
}

#==============================================================================
# Function: Invoke-ContainerVolumeBackup
#==============================================================================
<#
.SYNOPSIS
	Backs up all available container volumes using the specified engine.
.DESCRIPTION
	Retrieves a list of all named volumes known to the container engine.
	For each volume found, it calls the Backup-ContainerVolume function to save it to a .tar file
	in the specified backup folder.
.PARAMETER EngineType
	The type of the container engine ('docker' or 'podman'). Mandatory.
.PARAMETER BackupFolder
	The directory where the backup .tar files will be saved. Defaults to '.\Backup'.
.OUTPUTS
	[bool] Returns $true if at least one volume was successfully backed up, $false otherwise.
.EXAMPLE
	Invoke-ContainerVolumeBackup -EngineType "docker" -BackupFolder "D:\DockerBackups"
.NOTES
	Relies on Get-AvailableVolumes.
#>
function Invoke-ContainerVolumeBackup {
	[CmdletBinding()]
	[OutputType([bool])]
	param(
		[Parameter(Mandatory = $true)]
		[ValidateSet("docker", "podman")]
		[string]$EngineType,

		[string]$BackupFolder = $global:backupFolder
	)

	$volumeBackupPath = Join-Path $BackupFolder $global:dockerVolumesFolder
	# Ensure volume backup folder exists
	if (-not (Test-Path $volumeBackupPath)) {
		New-Item -ItemType Directory -Force -Path $volumeBackupPath | Out-Null
		Write-Host "Created volume backup folder: $volumeBackupPath"
	}

	Write-Host "Retrieving list of volumes for $EngineType..."
	$volumes = Get-AvailableVolumes -Engine $EngineType

	if (-not $volumes) {
		Write-Host "No volumes found for $EngineType."
		return $false
	}

	$successCount = 0
	foreach ($volume in $volumes) {
		Write-Host "Backing up volume: $volume"
		if (Backup-ContainerVolume -EngineType $EngineType -VolumeName $volume -BackupFolder $volumeBackupPath) {
			$successCount++
		}
	}

	Write-Host "Backed up $successCount out of $($volumes.Count) volumes."
	return ($successCount -gt 0)
}

#==============================================================================
# Function: Invoke-ContainerVolumeRestore
#==============================================================================
<#
.SYNOPSIS
	Restores all container volumes found in .tar files within a specified backup folder.
.DESCRIPTION
	Searches the specified backup folder for files matching '*-volume-*.tar'. For each file found,
	it attempts to parse the volume name from the filename and then calls the Restore-ContainerVolume function.
.PARAMETER EngineType
	The type of the container engine ('docker' or 'podman'). Mandatory.
.PARAMETER BackupFolder
	The directory containing the .tar backup files. Defaults to '.\Backup'.
.OUTPUTS
	[bool] Returns $true if at least one volume was successfully restored, $false otherwise.
.EXAMPLE
	Invoke-ContainerVolumeRestore -EngineType "docker" -BackupFolder "D:\DockerBackups"
.NOTES
	Assumes backup filenames follow the pattern '{VolumeName}-volume-*.tar'.
#>
function Invoke-ContainerVolumeRestore {
	[CmdletBinding()]
	[OutputType([bool])]
	param(
		[Parameter(Mandatory = $true)]
		[ValidateSet("docker", "podman")]
		[string]$EngineType,

		[string]$BackupFolder = $global:backupFolder
	)

	$volumeBackupPath = Join-Path $BackupFolder $global:dockerVolumesFolder
	if (-not (Test-Path $volumeBackupPath)) {
		Write-Host "Volume backup folder '$volumeBackupPath' does not exist. Nothing to restore."
		return $false
	}

	$tarFiles = Get-ChildItem -Path $volumeBackupPath -Filter "*-volume-*.tar"
	if (-not $tarFiles) {
		Write-Host "No volume backup tar files found in '$volumeBackupPath'."
		return $false
	}

	$successCount = 0
	foreach ($file in $tarFiles) {
		# Attempt to parse volume name from filename (e.g., "myvolume-volume-20231026-1230.tar")
		if ($file.Name -match "^(.+)-volume-\d{8}-\d{4}\.tar$") {
			$volumeName = $matches[1]
			Write-Host "Restoring volume '$volumeName' from file: $($file.Name)"
			if (Restore-ContainerVolume -EngineType $EngineType -VolumeName $volumeName -BackupFolder $volumeBackupPath) {
				$successCount++
			}
		}
		else {
			Write-Warning "Skipping file '$($file.Name)': Does not match expected volume backup filename pattern."
		}
	}

	Write-Host "Restored $successCount out of $($tarFiles.Count) volume backups."
	return ($successCount -gt 0)
}


#==============================================================================
# Function: Get-AvailableImageBackups
#==============================================================================
<#
.SYNOPSIS
	Retrieves a list of all available image backup files.
.DESCRIPTION
	Searches the image backup directory for files matching the pattern '*-image-*.tar'
	and returns their names sorted by modification time (newest first).
.OUTPUTS
	[string[]] An array of backup file names (e.g., 'nginx_latest-image-20231026-1230.tar').
.EXAMPLE
	$backups = Get-AvailableImageBackups
	$backups | ForEach-Object { Write-Host $_ }
.NOTES
	Uses global variables for path construction.
#>
function Get-AvailableImageBackups {
	[CmdletBinding()]
	[OutputType([string[]])]
	param()

	$imageBackupPath = Join-Path $global:backupFolder $global:dockerImagesFolder
	if (-not (Test-Path $imageBackupPath)) {
		Write-Host "Image backup directory does not exist: $imageBackupPath" -ForegroundColor Yellow
		return @()
	}

	$backupFiles = Get-ChildItem -Path $imageBackupPath -Filter "*-image-*.tar" | Sort-Object LastWriteTime -Descending | Select-Object -ExpandProperty Name
	if ($backupFiles) {
		Write-Host "Found $($backupFiles.Count) image backup files." -ForegroundColor Green
		return $backupFiles
	}
	else {
		Write-Host "No image backup files found." -ForegroundColor Yellow
		return @()
	}
}

#==============================================================================
# Function: Get-AvailableVolumeBackups
#==============================================================================
<#
.SYNOPSIS
	Retrieves a list of all available volume backup files.
.DESCRIPTION
	Searches the volume backup directory for files matching the pattern '*-volume-*.tar'
	and returns their names sorted by modification time (newest first).
.OUTPUTS
	[string[]] An array of backup file names (e.g., 'mydata-volume-20231026-1230.tar').
.EXAMPLE
	$backups = Get-AvailableVolumeBackups
	$backups | ForEach-Object { Write-Host $_ }
.NOTES
	Uses global variables for path construction.
#>
function Get-AvailableVolumeBackups {
	[CmdletBinding()]
	[OutputType([string[]])]
	param()

	$volumeBackupPath = Join-Path $global:backupFolder $global:dockerVolumesFolder
	if (-not (Test-Path $volumeBackupPath)) {
		Write-Host "Volume backup directory does not exist: $volumeBackupPath" -ForegroundColor Yellow
		return @()
	}

	$backupFiles = Get-ChildItem -Path $volumeBackupPath -Filter "*-volume-*.tar" | Sort-Object LastWriteTime -Descending | Select-Object -ExpandProperty Name
	if ($backupFiles) {
		Write-Host "Found $($backupFiles.Count) volume backup files." -ForegroundColor Green
		return $backupFiles
	}
	else {
		Write-Host "No volume backup files found." -ForegroundColor Yellow
		return @()
	}
}

#==============================================================================
# Function: Restore-ContainerImageFromFile
#==============================================================================
<#
.SYNOPSIS
	Restores a container image from a specific backup file by name.
.DESCRIPTION
	Takes a backup file name, constructs the full path using global variables,
	and calls the existing Restore-ContainerImage function.
.PARAMETER Engine
	Path to the container engine executable (e.g., 'docker' or 'podman'). Mandatory.
.PARAMETER BackupFileName
	The name of the backup file (e.g., 'nginx_latest-image-20231026-1230.tar'). Mandatory.
.PARAMETER RunContainer
	Switch parameter. If present, attempts to start a container from the restored image. Defaults to $false.
.OUTPUTS
	[bool] Returns $true if the image was restored successfully, $false otherwise.
.EXAMPLE
	Restore-ContainerImageFromFile -Engine "docker" -BackupFileName "nginx_latest-image-20231026-1230.tar"
.NOTES
	Uses global variables for path construction and delegates to Restore-ContainerImage.
#>
function Restore-ContainerImageFromFile {
	[CmdletBinding()]
	[OutputType([bool])]
	param(
		[Parameter(Mandatory = $true)]
		[string]$Engine,
		
		[Parameter(Mandatory = $true)]
		[string]$BackupFileName,
		
		[Parameter(Mandatory = $false)]
		[switch]$RunContainer = $false
	)

	$imageBackupPath = Join-Path $global:backupFolder $global:dockerImagesFolder
	$fullBackupPath = Join-Path $imageBackupPath $BackupFileName
	
	if (-not (Test-Path $fullBackupPath)) {
		Write-Error "Backup file not found: $fullBackupPath"
		return $false
	}

	return (Restore-ContainerImage -Engine $Engine -BackupFile $fullBackupPath -RunContainer:$RunContainer)
}
