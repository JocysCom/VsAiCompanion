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
# Function: Confirm-ContainerResource
#==============================================================================
<#
.SYNOPSIS
	Checks if a container resource (network or volume) exists and creates it if it doesn't.
.DESCRIPTION
	Uses the provided container engine to check if a resource (network or volume) with the
	specified name exists. If it doesn't exist, it attempts to create the resource. Supports -WhatIf.
.PARAMETER Engine
	Path to the container engine executable (e.g., 'docker' or 'podman'). Mandatory.
.PARAMETER ResourceType
	The type of resource to check/create ('network' or 'volume'). Mandatory.
.PARAMETER ResourceName
	The name of the container resource to check or create. Mandatory.
.OUTPUTS
	[bool] Returns $true if the resource exists or was successfully created.
		   Returns $false if creation failed or was skipped due to -WhatIf.
.EXAMPLE
	Confirm-ContainerResource -Engine "podman" -ResourceType "network" -ResourceName "my-app-network"
.EXAMPLE
	Confirm-ContainerResource -Engine "docker" -ResourceType "volume" -ResourceName "my-db-data"
.NOTES
	Relies on 'engine [network|volume] ls' and 'engine [network|volume] create'.
#>
function Confirm-ContainerResource {
	[CmdletBinding(SupportsShouldProcess = $true)]
	[OutputType([bool])]
	param(
		[Parameter(Mandatory = $true)]
		[string]$Engine,

		[Parameter(Mandatory = $true)]
		[ValidateSet("network", "volume")]
		[string]$ResourceType,

		[Parameter(Mandatory = $true)]
		[string]$ResourceName
	)

	# Check if resource exists
	$listArgs = @($ResourceType, "ls", "--filter", "name=^$ResourceName$", "--format", "{{.Name}}")
	$existingResource = & $Engine @listArgs

	# Note: Network ls returns the name if found, Volume ls returns the name if found.
	# If not found, network ls returns empty string, volume ls returns empty string.
	# So, check if the returned name matches the requested name.
	if ($existingResource -ne $ResourceName) {
		if ($PSCmdlet.ShouldProcess($ResourceName, "Create $ResourceType")) {
			Write-Host "Creating container $ResourceType '$ResourceName'..."
			& $Engine $ResourceType create $ResourceName
			if ($LASTEXITCODE -eq 0) {
				Write-Host "$ResourceType '$ResourceName' created successfully."
				return $true
			}
			else {
				Write-Error "Failed to create $ResourceType '$ResourceName'."
				return $false
			}
		}
		else {
			Write-Warning "$ResourceType creation skipped due to -WhatIf."
			return $false # Indicate resource doesn't exist if creation skipped
		}
	}
	else {
		Write-Host "$ResourceType '$ResourceName' already exists. Skipping creation."
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
	Performs the core steps of a container update: check for updates, remove old container, pull new image.
.DESCRIPTION
	This simplified function focuses on the non-interactive parts of an update:
	1. Checks if the container exists.
	2. Checks if a remote image update is available using Test-ImageUpdateAvailable (prompts to force if not).
	3. Removes the existing container using Remove-ContainerAndVolume (which handles ShouldProcess).
	4. Pulls the latest version of the specified image using Invoke-PullImage (which handles ShouldProcess).
	It does NOT handle backup, restore, or starting the new container. These steps should be
	orchestrated by the calling script (e.g., the menu action).
.PARAMETER Engine
	Path to the container engine executable (e.g., 'docker' or 'podman'). Mandatory.
.PARAMETER ContainerName
	The name of the container to update. Mandatory.
.PARAMETER VolumeName
	The name of the volume associated with the container (used for removal step). Mandatory.
.PARAMETER ImageName
	The full name and tag of the container image to update to (e.g., 'nginx:latest'). Mandatory.
.PARAMETER Platform
	The target platform for the image pull (e.g., 'linux/amd64'). Defaults to 'linux/amd64'.
.OUTPUTS
	[bool] Returns $true if the update check, removal, and pull steps complete successfully (or are skipped via -WhatIf).
		   Returns $false if any critical step fails or the update is canceled by the user during prompts.
.EXAMPLE
	# Called from a menu action:
	# if (Update-Container -Engine $eng -ContainerName $cn -VolumeName $vn -ImageName $img) {
	#     Start-SpecificContainer ...
	# }
.NOTES
	Relies on Test-ImageUpdateAvailable, Remove-ContainerAndVolume, Invoke-PullImage.
	User interaction for forcing update is handled within Test-ImageUpdateAvailable.
	User interaction for volume removal is handled within Remove-ContainerAndVolume.
	Backup/restore and starting the new container must be handled by the caller.
#>
function Update-Container {
	[CmdletBinding(SupportsShouldProcess = $true)] # Keep ShouldProcess for overall control if needed, though sub-functions handle it
	[OutputType([bool])]
	param(
		[Parameter(Mandatory = $true)]
		[string]$Engine,

		[Parameter(Mandatory = $true)]
		[string]$ContainerName,

		[Parameter(Mandatory = $true)]
		[string]$VolumeName, # Needed for Remove-ContainerAndVolume

		[Parameter(Mandatory = $true)]
		[string]$ImageName,

		[string]$Platform = "linux/amd64" # Keep platform for pull
	)

	Write-Host "Initiating update pre-check for container '$ContainerName'..."

	# Step 1: Check if container exists
	& $Engine inspect $ContainerName 2>$null | Out-Null # Check existence without storing info
	if ($LASTEXITCODE -ne 0) {
		Write-Host "Container '$ContainerName' not found. Nothing to update."
		return $false # Can't update something that doesn't exist
	}

	# Step 2: Check if an update is available (includes force prompt)
	$updateAvailable = Test-ImageUpdateAvailable -Engine $Engine -ImageName $ImageName
	if (-not $updateAvailable) {
		# Test-ImageUpdateAvailable handles the force prompt. If it returns false, user chose not to force.
		Write-Host "Update canceled by user or no update available/forced."
		return $false
	}
	Write-Host "Update available or forced. Proceeding..."

	# Step 3: Remove the existing container (Remove-ContainerAndVolume handles ShouldProcess and volume prompt)
	Write-Host "Removing existing container '$ContainerName'..."
	if (-not (Remove-ContainerAndVolume -Engine $Engine -ContainerName $ContainerName -VolumeName $VolumeName)) {
		Write-Error "Failed to remove container '$ContainerName' or action skipped. Update aborted."
		return $false
	}
	Write-Host "Existing container removed."

	# Step 4: Pull the latest image (Invoke-PullImage handles ShouldProcess)
	Write-Host "Pulling latest image '$ImageName'..."
	if (-not (Invoke-PullImage -Engine $Engine -ImageName $ImageName -PullOptions @("--platform", $Platform))) {
		Write-Error "Failed to pull the latest image or action skipped. Update aborted."
		# NOTE: Backup/Restore logic is now responsibility of the CALLER script's menu action.
		return $false
	}
	Write-Host "Image '$ImageName' pulled successfully."

	# Indicate that the core update steps (check, remove, pull) were successful
	Write-Host "Update pre-check, removal, and image pull completed successfully."
	return $true
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
