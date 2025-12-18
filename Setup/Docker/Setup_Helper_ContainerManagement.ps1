################################################################################
# Description  : Contains container management functions:
#                - Backup-ContainerState: Backup a running container's state.
#                - Restore-ContainerState: Restore a container from state backup.
#                - Check-ImageUpdateAvailable: Check for newer image versions.
#                - Update-Container: Generic container update function.
################################################################################

# Global default folder for offline image tar files (used when registries are blocked).
# Scripts can override per-call by passing -OfflineImagesFolder.
$global:offlineImagesFolder = Join-Path $PSScriptRoot "downloads\images"

#==============================================================================
# Function: Get-AspireManifestPaths
#==============================================================================
<#
.SYNOPSIS
	Resolves Aspire base and overlay manifest paths.
.DESCRIPTION
	Determines the base manifest path and the optional overlay manifest path based on an optional profile name.
	Overlay naming convention: manifest.<profile>.json (same directory as base).
.PARAMETER ManifestPath
	Base Aspire manifest.json path. If not supplied, defaults to 'Files\Aspire\manifest.json' under the repo.
.PARAMETER Profile
	Optional profile name (e.g., 'local', 'prod') used to resolve overlay manifest path.
.OUTPUTS
	[PSCustomObject] with BaseManifestPath and OverlayManifestPath.
#>
function Get-AspireManifestPaths {
	[CmdletBinding()]
	[OutputType([object])]
	param(
		[Parameter(Mandatory = $false)]
		[string]$ManifestPath,

		[Parameter(Mandatory = $false)]
		[string]$Profile
	)

	$base = $ManifestPath
	if ([string]::IsNullOrWhiteSpace($base)) {
		$base = Join-Path $PSScriptRoot "Files\Aspire\manifest.json"
	}

	$overlay = $null
	if (-not [string]::IsNullOrWhiteSpace($Profile)) {
		$dir = Split-Path -Parent $base
		$file = Split-Path -Leaf $base
		$nameNoExt = [IO.Path]::GetFileNameWithoutExtension($file)
		$ext = [IO.Path]::GetExtension($file)
		$candidate = Join-Path $dir ("{0}.{1}{2}" -f $nameNoExt, $Profile, $ext)
		if (Test-Path -LiteralPath $candidate) {
			$overlay = $candidate
		}
	}

	return [PSCustomObject]@{
		BaseManifestPath    = $base
		OverlayManifestPath = $overlay
	}
}

#==============================================================================
# Function: Merge-AspireResourceOverlay
#==============================================================================
<#
.SYNOPSIS
	Merges an overlay resource into a base manifest for a single resource name.
.DESCRIPTION
	Implements a "base + overlay" merge for one resource:
	- If base resource doesn't exist and overlay resource exists: adds it.
	- If both exist: deep-merges properties (scalars override, lists replace, dictionaries merge keys).
.PARAMETER BaseManifest
	Base manifest object produced by ConvertFrom-Json.
.PARAMETER OverlayManifest
	Overlay manifest object produced by ConvertFrom-Json.
.PARAMETER ResourceName
	Resource name under .resources to merge (e.g., 'n8n').
.OUTPUTS
	[PSCustomObject] The updated BaseManifest object (same instance mutated).
#>
function Merge-AspireResourceOverlay {
	[CmdletBinding()]
	[OutputType([object])]
	param(
		[Parameter(Mandatory = $true)]
		[object]$BaseManifest,

		[Parameter(Mandatory = $true)]
		[object]$OverlayManifest,

		[Parameter(Mandatory = $true)]
		[string]$ResourceName
	)

	if (-not $BaseManifest.resources) {
		$BaseManifest | Add-Member -NotePropertyName "resources" -NotePropertyValue ([PSCustomObject]@{}) -Force
	}

	$baseRes = $BaseManifest.resources.$ResourceName
	$overlayRes = $OverlayManifest.resources.$ResourceName

	if ($null -eq $baseRes -and $overlayRes) {
		$BaseManifest.resources.$ResourceName = $overlayRes
		return $BaseManifest
	}

	if (-not ($overlayRes -and $overlayRes.properties)) {
		return $BaseManifest
	}

	$bp = $baseRes.properties
	$op = $overlayRes.properties

	if ($op.image) { $bp.image = $op.image }
	if ($op.restart) { $bp.restart = $op.restart }
	if ($op.platform) { $bp.platform = $op.platform }

	if ($op.bindings) { $bp.bindings = $op.bindings }
	if ($op.volumes) { $bp.volumes = $op.volumes }
	if ($op.dependencies) { $bp.dependencies = $op.dependencies }
	if ($op.networks) { $bp.networks = $op.networks }
	if ($op.command) { $bp.command = $op.command }

	if ($op.environment) {
		if ($null -eq $bp.environment) { $bp.environment = [PSCustomObject]@{} }
		foreach ($p in $op.environment.PSObject.Properties) {
			$bp.environment | Add-Member -NotePropertyName $p.Name -NotePropertyValue $p.Value -Force
		}
	}

	if ($op.additionalHosts) {
		if ($null -eq $bp.additionalHosts) { $bp.additionalHosts = [PSCustomObject]@{} }
		foreach ($p in $op.additionalHosts.PSObject.Properties) {
			$bp.additionalHosts | Add-Member -NotePropertyName $p.Name -NotePropertyValue $p.Value -Force
		}
	}

	if ($op.resources) {
		if ($null -eq $bp.resources) { $bp.resources = $op.resources }
		else {
			if ($op.resources.memory) { $bp.resources.memory = $op.resources.memory }
			if ($op.resources.memorySwap) { $bp.resources.memorySwap = $op.resources.memorySwap }
		}
	}

	return $BaseManifest
}

#==============================================================================
# Function: Get-AspireManifestWithOptionalOverlay
#==============================================================================
<#
.SYNOPSIS
	Loads an Aspire manifest and optionally applies an overlay for a single resource.
.DESCRIPTION
	Loads the base manifest JSON from disk. If a profile overlay exists, merges the specified resource
	from overlay into base and returns the merged manifest object.
.PARAMETER BaseManifestPath
	Path to base manifest.json.
.PARAMETER OverlayManifestPath
	Optional path to overlay manifest.<profile>.json.
.PARAMETER ResourceName
	Resource name to merge when overlay is present.
.OUTPUTS
	[PSCustomObject] The merged manifest object.
#>
function Get-AspireManifestWithOptionalOverlay {
	[CmdletBinding()]
	[OutputType([object])]
	param(
		[Parameter(Mandatory = $true)]
		[string]$BaseManifestPath,

		[Parameter(Mandatory = $false)]
		[string]$OverlayManifestPath,

		[Parameter(Mandatory = $true)]
		[string]$ResourceName
	)

	if (-not (Test-Path -LiteralPath $BaseManifestPath)) {
		throw "Manifest file not found: $BaseManifestPath"
	}

	$baseManifest = Get-Content -Raw $BaseManifestPath | ConvertFrom-Json

	if (-not [string]::IsNullOrWhiteSpace($OverlayManifestPath) -and (Test-Path -LiteralPath $OverlayManifestPath)) {
		$overlayManifest = Get-Content -Raw $OverlayManifestPath | ConvertFrom-Json
		$null = Merge-AspireResourceOverlay -BaseManifest $baseManifest -OverlayManifest $overlayManifest -ResourceName $ResourceName
	}

	return $baseManifest
}

#==============================================================================
# Function: Update-AspireManifestResourceImage
#==============================================================================
<#
.SYNOPSIS
	Updates a resource image reference in Aspire manifest.json and optionally persists it.
.DESCRIPTION
	Updates .resources.<ResourceName>.properties.image with the provided Image value.
	Useful when update flow chooses a pinned semver tag and the manifest should start the same image next time.
.PARAMETER ManifestPath
	Path to manifest.json to update.
.PARAMETER ResourceName
	Resource name under .resources (e.g., 'n8n').
.PARAMETER Image
	New image reference (e.g., 'docker.io/n8nio/n8n:1.123.7').
.PARAMETER PassThru
	If set, returns the updated manifest object.
.OUTPUTS
	If -PassThru: [PSCustomObject] updated manifest object; otherwise [void].
#>
function Update-AspireManifestResourceImage {
	[CmdletBinding(SupportsShouldProcess = $true)]
	[OutputType([object])]
	param(
		[Parameter(Mandatory = $true)]
		[string]$ManifestPath,

		[Parameter(Mandatory = $true)]
		[string]$ResourceName,

		[Parameter(Mandatory = $true)]
		[string]$Image,

		[Parameter(Mandatory = $false)]
		[switch]$PassThru
	)

	if (-not (Test-Path -LiteralPath $ManifestPath)) {
		throw "Manifest file not found: $ManifestPath"
	}

	$root = Get-Content -Raw $ManifestPath | ConvertFrom-Json

	if (-not $root.resources) {
		throw "Manifest '$ManifestPath' has no 'resources' section."
	}

	$res = $root.resources.$ResourceName
	if (-not $res) {
		throw "Resource '$ResourceName' not found in manifest '$ManifestPath'."
	}

	if (-not $res.properties) {
		$res | Add-Member -NotePropertyName "properties" -NotePropertyValue ([PSCustomObject]@{}) -Force
	}

	$res.properties.image = $Image

	if ($PSCmdlet.ShouldProcess($ManifestPath, "Update resource '$ResourceName' image to '$Image'")) {
		$json = $root | ConvertTo-Json -Depth 50
		Set-Content -LiteralPath $ManifestPath -Value $json -Encoding UTF8
	}

	if ($PassThru) {
		return $root
	}
}

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
		[array]$PullOptions = @(),

		[Parameter(Mandatory = $false)]
		[string]$OfflineImagesFolder = $global:offlineImagesFolder
	)

	# If OfflineImagesFolder is provided and contains matching tar(s), offer to load instead of pulling.
	if (-not [string]::IsNullOrWhiteSpace($OfflineImagesFolder) -and (Test-Path -LiteralPath $OfflineImagesFolder)) {

		# Split image into repo + tag
		$repo = $ImageName
		$tag = $null
		if ($ImageName -match '^(?<r>.+):(?<t>[^:]+)$') {
			$repo = $matches['r']
			$tag = $matches['t']
		}

		# 1) Exact match tar (repo+tag) - current behavior
		$safeImageName = $ImageName -replace "[:/]", "_"
		$pattern = "$safeImageName-image-*.tar"
		$tarFiles = @(Get-ChildItem -LiteralPath $OfflineImagesFolder -Filter $pattern -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending)

		# 2) If config points to ':latest' but downloaded tars are versioned (repo_<ver>-image-*.tar),
		# try discovering versioned tars for the same repository.
		$versionedChoices = @()
		if ((-not $tarFiles -or $tarFiles.Count -eq 0) -and ($tag -eq "latest")) {
			$safeRepo = $repo -replace "[:/]", "_"
			$pattern2 = "$safeRepo`_*-image-*.tar"
			$files2 = @(Get-ChildItem -LiteralPath $OfflineImagesFolder -Filter $pattern2 -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending)

			foreach ($f in $files2) {
				$re = '^' + [regex]::Escape($safeRepo) + '_(?<t>.+?)-image-'
				if ($f.Name -match $re) {
					$t = [string]$matches['t']
					$sv = ConvertTo-SemVer -Version $t
					$versionedChoices += [PSCustomObject]@{
						Tag     = $t
						Version = $sv
						File    = $f
					}
				}
			}

			if ($versionedChoices.Count -gt 0) {
				$withVer = @($versionedChoices | Where-Object { $null -ne $_.Version })
				$withoutVer = @($versionedChoices | Where-Object { $null -eq $_.Version })

				$sortedWithVer = @(
					$withVer | Sort-Object `
						@{ Expression = { $_.Version.Major }; Descending = $true }, `
						@{ Expression = { $_.Version.Minor }; Descending = $true }, `
						@{ Expression = { $_.Version.Patch }; Descending = $true }
				)
				$sortedWithoutVer = @($withoutVer | Sort-Object @{ Expression = { $_.File.LastWriteTime }; Descending = $true })
				$versionedChoices = @($sortedWithVer + $sortedWithoutVer)
			}
		}

		if ($tarFiles -and $tarFiles.Count -gt 0) {
			$latest = $tarFiles[0]
			Write-Host ""
			Write-Host "Offline image tar found:" -ForegroundColor Cyan
			Write-Host "  $($latest.FullName)" -ForegroundColor Gray
			Write-Host "  LastWriteTime: $($latest.LastWriteTime)" -ForegroundColor Gray
			Write-Host ""
			Write-Host "Choose image source:" -ForegroundColor White
			Write-Host "1. Load from offline tar (recommended for blocked registries)" -ForegroundColor Cyan
			Write-Host "2. Pull from internet registry" -ForegroundColor Cyan
			Write-Host "0. Cancel" -ForegroundColor Cyan
			$choice = Read-Host "Enter your choice"
			if ([string]::IsNullOrWhiteSpace($choice)) { $choice = "1" }

			if ($choice -eq "0") {
				Write-Warning "Image acquisition cancelled by user."
				return $false
			}

			if ($choice -eq "1") {
				if ($PSCmdlet.ShouldProcess($latest.FullName, "Load image tar for '$ImageName'")) {
					Write-Host "Loading image from tar: $($latest.FullName)" -ForegroundColor Yellow
					& $Engine load --input $latest.FullName
					if ($LASTEXITCODE -eq 0) {
						Write-Host "Image loaded successfully from offline tar." -ForegroundColor Green
						return $true
					}
					Write-Error "Failed to load image from offline tar."
					return $false
				}
				Write-Warning "Image load skipped due to -WhatIf."
				return $false
			}

			# Else fall through to normal pull (choice 2 or unknown)
		}
		elseif ($versionedChoices -and $versionedChoices.Count -gt 0) {
			Write-Host ""
			Write-Host "Versioned offline image tar(s) found for '$repo' while '$ImageName' is configured as ':latest':" -ForegroundColor Cyan
			[int]$i = 1
			foreach ($c in $versionedChoices) {
				if ($null -ne $c.Version) {
					Write-Host ("{0}. {1}  ({2})" -f $i, $c.Tag, $c.File.Name) -ForegroundColor Gray
				}
				else {
					Write-Host ("{0}. {1}" -f $i, $c.File.Name) -ForegroundColor Gray
				}
				$i++
			}
			Write-Host "0. Pull from internet registry" -ForegroundColor Cyan
			$pick = Read-Host "Select tar to load (default 1)"
			if ([string]::IsNullOrWhiteSpace($pick)) { $pick = "1" }
			if ($pick -eq "0") {
				# fall through to pull
			}
			else {
				[int]$idx = 0
				if ([int]::TryParse($pick, [ref]$idx) -and $idx -ge 1 -and $idx -le $versionedChoices.Count) {
					$sel = $versionedChoices[$idx - 1]
					if ($PSCmdlet.ShouldProcess($sel.File.FullName, "Load image tar for '${repo}:$($sel.Tag)'")) {
						Write-Host "Loading image from tar: $($sel.File.FullName)" -ForegroundColor Yellow
						& $Engine load --input $sel.File.FullName
						if ($LASTEXITCODE -eq 0) {

							# If the config requested ':latest' but we loaded a versioned tar, ensure ':latest' points
							# to the loaded image locally (so later 'run ...:latest' does not pull from internet).
							if ($tag -eq "latest" -and -not [string]::IsNullOrWhiteSpace($sel.Tag)) {
								try {
									$loadedRef = "${repo}:$($sel.Tag)"
									$loadedId = $null
									[string[]]$lines = @(& $Engine images --format "{{.Repository}}:{{.Tag}} {{.ID}}" 2>$null)
									foreach ($line in $lines) {
										if ([string]::IsNullOrWhiteSpace($line)) { continue }
										$parts = $line.Trim() -split '\s+', 2
										if ($parts.Count -lt 2) { continue }
										if ($parts[0].Trim() -eq $loadedRef) {
											$loadedId = $parts[1].Trim()
											break
										}
									}

									if (-not [string]::IsNullOrWhiteSpace($loadedId)) {
										& $Engine tag $loadedId "${repo}:latest" 2>$null | Out-Null
									}
								}
								catch {
									# Tagging is best-effort; if it fails, caller may still pull.
								}
							}

							Write-Host "Image loaded successfully from versioned offline tar." -ForegroundColor Green
							return $true
						}
						Write-Error "Failed to load image from offline tar."
						return $false
					}
					Write-Warning "Image load skipped due to -WhatIf."
					return $false
				}
				Write-Warning "Invalid selection. Falling back to pull."
			}
		}
	}

	if ($PSCmdlet.ShouldProcess($ImageName, "Pull Image")) {
		Write-Host "Pulling image '$ImageName'..."
		$pullCmd = @("pull") + $PullOptions + $ImageName
		& $Engine @pullCmd

		if ($LASTEXITCODE -eq 0) {
			Write-Host "Image '$ImageName' pulled successfully."
			return $true
		}

		Write-Warning "Image pull failed (registry may be unreachable)."
		if (-not [string]::IsNullOrWhiteSpace($OfflineImagesFolder) -and (Test-Path -LiteralPath $OfflineImagesFolder)) {
			$safeImageName = $ImageName -replace "[:/]", "_"
			$pattern = "$safeImageName-image-*.tar"
			$tarFiles = @(Get-ChildItem -LiteralPath $OfflineImagesFolder -Filter $pattern -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending)

			if ($tarFiles -and $tarFiles.Count -gt 0) {
				$latest = $tarFiles[0]
				Write-Host ""
				Write-Host "An offline tar is available and can be used instead of pulling:" -ForegroundColor Cyan
				Write-Host "  $($latest.FullName)" -ForegroundColor Gray
				$useOfflineAfterPullFail = Read-Host "Load offline tar now? (Y/N, default is Y)"
				if ($useOfflineAfterPullFail -ne "N") {
					if ($PSCmdlet.ShouldProcess($latest.FullName, "Load image tar for '$ImageName'")) {
						Write-Host "Loading image from tar: $($latest.FullName)" -ForegroundColor Yellow
						& $Engine load --input $latest.FullName
						if ($LASTEXITCODE -eq 0) {
							Write-Host "Image loaded successfully from offline tar." -ForegroundColor Green
							return $true
						}
						Write-Error "Failed to load image from offline tar."
						return $false
					}
					Write-Warning "Image load skipped due to -WhatIf."
					return $false
				}
			}
		}

		Write-Error "Failed to pull image '$ImageName'."
		return $false
	}

	Write-Warning "Image pull skipped due to -WhatIf."
	return $false # Indicate failure if skipped
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

		[Parameter(Mandatory = $false)] # Made optional
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

	# Only proceed with volume check/removal if a VolumeName was provided
	if (-not [string]::IsNullOrWhiteSpace($VolumeName)) {
		# Check if volume exists
		$existingVolume = & $Engine volume ls --filter "name=^$VolumeName$" --format "{{.Name}}"
		if ($existingVolume -eq $VolumeName) { # Ensure exact match
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
				} else {
					Write-Warning "Volume removal skipped due to -WhatIf."
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
	} else {
		# Use Write-Host for status messages
		Write-Host "No volume name provided, skipping volume removal check."
	}


	return $true
}


#==============================================================================
# Function: ConvertTo-SemVer
#==============================================================================
<#
.SYNOPSIS
	Parses a semantic version string (major.minor.patch) into an ordered numeric object.
.DESCRIPTION
	Accepts versions like '1.123.6'. Returns $null if parsing fails.
.OUTPUTS
	[PSCustomObject] with Major/Minor/Patch or $null.
#>
function ConvertTo-SemVer {
	[CmdletBinding()]
	[OutputType([object])]
	param(
		[Parameter(Mandatory = $true)]
		[string]$Version
	)

	$v = $Version.Trim()
	if ($v -match '^(?<maj>\d+)\.(?<min>\d+)\.(?<pat>\d+)$') {
		return [PSCustomObject]@{
			Major = [int]$matches['maj']
			Minor = [int]$matches['min']
			Patch = [int]$matches['pat']
		}
	}

	return $null
}

#==============================================================================
# Function: Compare-SemVer
#==============================================================================
<#
.SYNOPSIS
	Compares two semantic versions (major.minor.patch).
.DESCRIPTION
	Returns 1 if A > B, -1 if A < B, 0 if equal.
#>
function Compare-SemVer {
	[CmdletBinding()]
	[OutputType([int])]
	param(
		[Parameter(Mandatory = $true)]
		[object]$A,

		[Parameter(Mandatory = $true)]
		[object]$B
	)

	if ($A.Major -ne $B.Major) { return [Math]::Sign($A.Major - $B.Major) }
	if ($A.Minor -ne $B.Minor) { return [Math]::Sign($A.Minor - $B.Minor) }
	if ($A.Patch -ne $B.Patch) { return [Math]::Sign($A.Patch - $B.Patch) }
	return 0
}

#==============================================================================
# Function: Get-DockerHubTags
#==============================================================================
<#
.SYNOPSIS
	Fetches tags for a Docker Hub repository.
.DESCRIPTION
	Uses Docker Hub v2 API to list tags. Returns tag names.
.PARAMETER Namespace
	Docker Hub namespace (e.g., 'n8nio').
.PARAMETER Repository
	Docker Hub repository (e.g., 'n8n').
.PARAMETER PageSize
	Page size (max 100 recommended).
.PARAMETER MaxPages
	Safety limit to avoid excessive requests.
#>
function Get-DockerHubTags {
	[CmdletBinding()]
	[OutputType([string[]])]
	param(
		[Parameter(Mandatory = $true)]
		[string]$Namespace,

		[Parameter(Mandatory = $true)]
		[string]$Repository,

		[int]$PageSize = 100,

		[int]$MaxPages = 20
	)

	$tags = New-Object System.Collections.Generic.List[string]
	$url = "https://hub.docker.com/v2/repositories/$Namespace/$Repository/tags?page_size=$PageSize"

	[int]$page = 0
	while (-not [string]::IsNullOrWhiteSpace($url) -and $page -lt $MaxPages) {
		$page++
		try {
			$r = Invoke-RestMethod -Method Get -Uri $url -Headers @{ "Accept" = "application/json" }
		}
		catch {
			Write-Warning "Failed to query Docker Hub tags: $_"
			break
		}

		if ($r -and $r.results) {
			foreach ($t in $r.results) {
				if ($t -and $t.name) { $tags.Add([string]$t.name) }
			}
		}

		$url = $r.next
	}

	return @($tags | Sort-Object -Unique)
}

#==============================================================================
# Function: Get-LatestSemVerTag
#==============================================================================
<#
.SYNOPSIS
	Finds the latest semantic version tag for a given major version from a list of tag strings.
.DESCRIPTION
	Filters tags matching '<major>.<minor>.<patch>' and returns the highest semver.
#>
function Get-LatestSemVerTag {
	[CmdletBinding()]
	[OutputType([string])]
	param(
		[Parameter(Mandatory = $true)]
		[string[]]$Tags,

		[Parameter(Mandatory = $true)]
		[int]$Major
	)

	$best = $null
	$bestSem = $null

	foreach ($tag in $Tags) {
		if ([string]::IsNullOrWhiteSpace($tag)) { continue }
		if ($tag -notmatch "^$Major\.\d+\.\d+$") { continue }

		$sv = ConvertTo-SemVer -Version $tag
		if ($null -eq $sv) { continue }

		if ($null -eq $bestSem -or (Compare-SemVer -A $sv -B $bestSem) -gt 0) {
			$bestSem = $sv
			$best = $tag
		}
	}

	return $best
}

#==============================================================================
# Function: Get-ContainerAppVersion
#==============================================================================
<#
.SYNOPSIS
	Attempts to read an application's version by executing a command inside a running container.
.DESCRIPTION
	Uses 'engine exec' to run a version command and parses stdout.
#>
function Get-ContainerAppVersion {
	[CmdletBinding()]
	[OutputType([string])]
	param(
		[Parameter(Mandatory = $true)]
		[string]$Engine,

		[Parameter(Mandatory = $true)]
		[string]$ContainerName,

		[Parameter(Mandatory = $true)]
		[string[]]$Command
	)

	try {
		$out = & $Engine exec $ContainerName @Command 2>$null
		if ($LASTEXITCODE -ne 0) { return $null }
		$s = ($out | Out-String).Trim()
		if ([string]::IsNullOrWhiteSpace($s)) { return $null }
		return $s
	}
	catch {
		return $null
	}
}

#==============================================================================
# Function: Get-ImageLabelValue
#==============================================================================
<#
.SYNOPSIS
	Reads a label value from a local image (by reference or ID).
.DESCRIPTION
	Uses 'engine inspect' to read Config.Labels.<LabelKey> from an image reference or image ID.
.PARAMETER Engine
	Path to container engine executable (docker/podman).
.PARAMETER ImageRefOrId
	Local image reference (repo:tag) or image ID.
.PARAMETER LabelKey
	Label key to read (e.g., 'org.opencontainers.image.version').
.OUTPUTS
	[string] The label value or $null.
#>
function Get-ImageLabelValue {
	[CmdletBinding()]
	[OutputType([string])]
	param(
		[Parameter(Mandatory = $true)]
		[string]$Engine,

		[Parameter(Mandatory = $true)]
		[string]$ImageRefOrId,

		[Parameter(Mandatory = $true)]
		[string]$LabelKey
	)

	try {
		$img = & $Engine inspect $ImageRefOrId 2>$null | ConvertFrom-Json
		if (-not $img) { return $null }
		$one = if ($img -is [array]) { $img[0] } else { $img }

		$labels = $one.Config.Labels
		if (-not $labels) { return $null }
		if ($labels.PSObject.Properties.Name -notcontains $LabelKey) { return $null }

		$v = [string]$labels.$LabelKey
		if ([string]::IsNullOrWhiteSpace($v)) { return $null }
		return $v.Trim()
	}
	catch {
		return $null
	}
}

#==============================================================================
# Function: Get-LocalOfflineTarVersions
#==============================================================================
<#
.SYNOPSIS
	Discovers offline image tar versions for a repository in the offline images folder.
.DESCRIPTION
	Searches for tar files named like '<safeRepo>_<tag>-image-*.tar' and extracts <tag>.
	Returns a unique list of discovered tags and the latest semver tag (if any).
.PARAMETER OfflineImagesFolder
	Folder path containing offline image tar files.
.PARAMETER Repository
	Repository name without tag (e.g., 'docker.io/n8nio/n8n').
.OUTPUTS
	[PSCustomObject] with Tags ([string[]]) and LatestSemVerTag ([string]) or $null if none found.
#>
function Get-LocalOfflineTarVersions {
	[CmdletBinding()]
	[OutputType([object])]
	param(
		[Parameter(Mandatory = $true)]
		[string]$OfflineImagesFolder,

		[Parameter(Mandatory = $true)]
		[string]$Repository
	)

	if ([string]::IsNullOrWhiteSpace($OfflineImagesFolder) -or -not (Test-Path -LiteralPath $OfflineImagesFolder)) {
		return $null
	}

	$safeRepo = $Repository -replace "[:/]", "_"
	$pattern = "$safeRepo`_*-image-*.tar"
	$files = @(Get-ChildItem -LiteralPath $OfflineImagesFolder -Filter $pattern -ErrorAction SilentlyContinue)
	if (-not $files -or $files.Count -eq 0) { return $null }

	$tags = New-Object System.Collections.Generic.List[string]
	foreach ($f in $files) {
		$re = '^' + [regex]::Escape($safeRepo) + '_(?<t>.+?)-image-'
		if ($f.Name -match $re) {
			$t = [string]$matches['t']
			if (-not [string]::IsNullOrWhiteSpace($t)) { $tags.Add($t.Trim()) }
		}
	}

	$uniq = @($tags | Sort-Object -Unique)
	if (-not $uniq -or $uniq.Count -eq 0) { return $null }

	$sem = @()
	foreach ($t in $uniq) {
		$sv = ConvertTo-SemVer -Version $t
		if ($null -ne $sv) {
			$sem += [PSCustomObject]@{ Tag = $t; Ver = $sv }
		}
	}
	$latest = $null
	if ($sem.Count -gt 0) {
		$sorted = @(
			$sem | Sort-Object `
				@{ Expression = { $_.Ver.Major }; Descending = $true }, `
				@{ Expression = { $_.Ver.Minor }; Descending = $true }, `
				@{ Expression = { $_.Ver.Patch }; Descending = $true }
		)
		$latest = $sorted[0].Tag
	}

	return [PSCustomObject]@{
		Tags           = $uniq
		LatestSemVerTag = $latest
	}
}

#==============================================================================
# Function: Get-DockerHubRepoFromImageRef
#==============================================================================
<#
.SYNOPSIS
	Parses a docker.io image reference and returns Docker Hub namespace/repository.
.DESCRIPTION
	Supports image references like:
	- docker.io/<namespace>/<repo>
	- <namespace>/<repo>
	Does not support library images like 'docker.io/library/redis' specially; it will return namespace 'library'.
.PARAMETER Repository
	Image repository without tag.
.OUTPUTS
	[PSCustomObject] with Namespace and Repository, or $null if not parseable.
#>
function Get-DockerHubRepoFromImageRef {
	[CmdletBinding()]
	[OutputType([object])]
	param(
		[Parameter(Mandatory = $true)]
		[string]$Repository
	)

	$repo = $Repository.Trim()
	if ($repo -match '^docker\.io/(?<ns>[^/]+)/(?<r>[^/]+)$') {
		return [PSCustomObject]@{ Namespace = [string]$matches['ns']; Repository = [string]$matches['r'] }
	}
	if ($repo -match '^(?<ns>[^/]+)/(?<r>[^/]+)$') {
		return [PSCustomObject]@{ Namespace = [string]$matches['ns']; Repository = [string]$matches['r'] }
	}
	return $null
}

#==============================================================================
# Function: Get-RemoteSemVerOptions
#==============================================================================
<#
.SYNOPSIS
	Gets latest semver tags for current and next major lines from Docker Hub for a repository.
.DESCRIPTION
	Determines current major from local image label org.opencontainers.image.version when available,
	otherwise infers from highest semver tag on Docker Hub.
.PARAMETER Engine
	Container engine executable path.
.PARAMETER Repository
	Image repository without tag.
.PARAMETER CurrentTag
	Current tag from the image reference (used to decide whether to do semver flow; typically 'latest').
.OUTPUTS
	[PSCustomObject] with CurrentMajor, CurrentMajorLatestTag, NextMajorLatestTag, RepoName, Namespace, Repository or $null.
#>
function Get-RemoteSemVerOptions {
	[CmdletBinding()]
	[OutputType([object])]
	param(
		[Parameter(Mandatory = $true)]
		[string]$Engine,

		[Parameter(Mandatory = $true)]
		[string]$Repository,

		[Parameter(Mandatory = $false)]
		[string]$CurrentTag
	)

	if ($CurrentTag -ne "latest") { return $null }

	$hub = Get-DockerHubRepoFromImageRef -Repository $Repository
	if (-not $hub) { return $null }

	$hubTags = Get-DockerHubTags -Namespace $hub.Namespace -Repository $hub.Repository
	if (-not $hubTags -or $hubTags.Count -eq 0) { return $null }

	$curMajor = $null

	$labelVer = Get-ImageLabelValue -Engine $Engine -ImageRefOrId "${Repository}:latest" -LabelKey "org.opencontainers.image.version"
	if (-not [string]::IsNullOrWhiteSpace($labelVer)) {
		$sv = ConvertTo-SemVer -Version $labelVer
		if ($null -ne $sv) { $curMajor = [int]$sv.Major }
	}

	if ($null -eq $curMajor) {
		$semTags = @()
		foreach ($t in $hubTags) {
			$sv = ConvertTo-SemVer -Version $t
			if ($null -ne $sv) { $semTags += [PSCustomObject]@{ Tag = $t; Ver = $sv } }
		}
		if ($semTags.Count -gt 0) {
			$sorted = @(
				$semTags | Sort-Object `
					@{ Expression = { $_.Ver.Major }; Descending = $true }, `
					@{ Expression = { $_.Ver.Minor }; Descending = $true }, `
					@{ Expression = { $_.Ver.Patch }; Descending = $true }
			)
			$curMajor = [int]$sorted[0].Ver.Major
		}
	}

	if ($null -eq $curMajor) { return $null }

	$remoteCur = Get-LatestSemVerTag -Tags $hubTags -Major $curMajor
	$remoteNext = Get-LatestSemVerTag -Tags $hubTags -Major ($curMajor + 1)

	if ([string]::IsNullOrWhiteSpace($remoteCur) -and [string]::IsNullOrWhiteSpace($remoteNext)) { return $null }

	return [PSCustomObject]@{
		CurrentMajor          = $curMajor
		CurrentMajorLatestTag = $remoteCur
		NextMajorLatestTag    = $remoteNext
		RepoName              = $hub.Repository
		Namespace             = $hub.Namespace
		Repository            = $hub.Repository
	}
}

#==============================================================================
# Function: Show-ImageDownloadOptions
#==============================================================================
<#
.SYNOPSIS
	Shows remote semver download options for ':latest' images (update vs upgrade).
.DESCRIPTION
	Uses Get-RemoteSemVerOptions and returns a concrete target image ref to download, or $null.
.PARAMETER Engine
	Container engine executable path.
.PARAMETER ImageName
	Image reference (repo:tag).
.OUTPUTS
	[PSCustomObject] with TargetImage, Source ('internet') or $null.
#>
function Show-ImageDownloadOptions {
	[CmdletBinding()]
	[OutputType([object])]
	param(
		[Parameter(Mandatory = $true)]
		[string]$Engine,

		[Parameter(Mandatory = $true)]
		[string]$ImageName
	)

	if ($ImageName -notmatch '^(?<repo>.+):(?<tag>[^:]+)$') { return $null }
	$repo = [string]$matches['repo']
	$tag = [string]$matches['tag']

	# When downloading, "update" should usually mean: latest semver in the most recent "supported major".
	# The label on ':latest' may point to a newer major already (e.g., n8n 2.x), which would hide 1.x updates.
	# So for downloads we infer the current major from available remote tags and then optionally offer previous major.
	$hub = Get-DockerHubRepoFromImageRef -Repository $repo
	if (-not $hub) { return $null }

	$hubTags = Get-DockerHubTags -Namespace $hub.Namespace -Repository $hub.Repository
	if (-not $hubTags -or $hubTags.Count -eq 0) { return $null }

	# Build semver tag list
	$semTags = @()
	foreach ($t in $hubTags) {
		$sv = ConvertTo-SemVer -Version $t
		if ($null -ne $sv) { $semTags += [PSCustomObject]@{ Tag = $t; Ver = $sv } }
	}
	if (-not $semTags -or $semTags.Count -eq 0) { return $null }

	$sorted = @(
		$semTags | Sort-Object `
			@{ Expression = { $_.Ver.Major }; Descending = $true }, `
			@{ Expression = { $_.Ver.Minor }; Descending = $true }, `
			@{ Expression = { $_.Ver.Patch }; Descending = $true }
	)

	$topMajor = [int]$sorted[0].Ver.Major
	$prevMajor = $topMajor - 1

	$topTag = Get-LatestSemVerTag -Tags $hubTags -Major $topMajor
	$prevTag = $null
	if ($prevMajor -ge 1) { $prevTag = Get-LatestSemVerTag -Tags $hubTags -Major $prevMajor }

	$options = @()
	$map = @{}

	# Prefer showing the "update" line (previous major) first when available, then "upgrade" (top major).
	if ($prevTag) {
		$options += "Download from: Remote (internet) $prevMajor.x latest: $prevTag"
		$map[$options.Count] = "$repo`:$prevTag"
	}
	if ($topTag) {
		$options += "Download from: Remote (internet) $topMajor.x latest: $topTag"
		$map[$options.Count] = "$repo`:$topTag"
	}

	if (-not $options -or $options.Count -eq 0) { return $null }

	$pick = Invoke-OptionsMenu -Title "Download Options" -Options $options -ExitChoice "Exit menu"
	if (-not $pick -or $pick -eq "Exit menu") { return $null }

	# Resolve selected option index (Invoke-OptionsMenu returns the option string)
	[int]$idx = 0
	for ($i = 0; $i -lt $options.Count; $i++) {
		if ($options[$i] -eq $pick) { $idx = $i + 1; break }
	}
	if ($idx -lt 1 -or -not $map.ContainsKey($idx)) { return $null }

	return [PSCustomObject]@{
		TargetImage = [string]$map[$idx]
		Source      = "internet"
	}
}

#==============================================================================
# Function: Show-ImageUpdateOptions
#==============================================================================
<#
.SYNOPSIS
	Shows current/local/remote versions and lets the user choose reinstall/update/upgrade and source.
.DESCRIPTION
	- Current: reads running container app version via Get-ContainerAppVersion.
	- Local offline: reads versions from tar file names in OfflineImagesFolder.
	- Remote: reads latest tags from Docker Hub for a major line.
.PARAMETER Engine
	Path to container engine executable (docker/podman).
.PARAMETER ContainerName
	Running container name.
.PARAMETER Repository
	Repository without tag (e.g., 'docker.io/n8nio/n8n').
.PARAMETER OfflineImagesFolder
	Offline image tar folder.
.PARAMETER CurrentMajor
	Major line to consider for "update" (e.g., 1).
.PARAMETER VersionCommand
	Command to run in container to get app version (e.g., @('n8n','--version')).
.OUTPUTS
	[PSCustomObject] with TargetImage ([string]) and Source ([string] 'offline'|'internet') or $null if cancelled.
#>
function Show-ImageUpdateOptions {
	[CmdletBinding()]
	[OutputType([object])]
	param(
		[Parameter(Mandatory = $true)]
		[string]$Engine,

		[Parameter(Mandatory = $true)]
		[string]$ContainerName,

		[Parameter(Mandatory = $true)]
		[string]$Repository,

		[Parameter(Mandatory = $false)]
		[string]$OfflineImagesFolder = $global:offlineImagesFolder,

		[Parameter(Mandatory = $true)]
		[int]$CurrentMajor,

		[Parameter(Mandatory = $true)]
		[string[]]$VersionCommand
	)

	Write-Host ""
	Write-Host "Image Update Options:" -ForegroundColor Cyan
	Write-Host "=====================" -ForegroundColor Cyan

	$currentVerRaw = Get-ContainerAppVersion -Engine $Engine -ContainerName $ContainerName -Command $VersionCommand
	$currentVerRaw = if ($currentVerRaw) { $currentVerRaw.Trim() } else { $null }

	$offline = Get-LocalOfflineTarVersions -OfflineImagesFolder $OfflineImagesFolder -Repository $Repository
	$offlineLatest = if ($offline) { $offline.LatestSemVerTag } else { $null }

	$hub = Get-DockerHubRepoFromImageRef -Repository $Repository
	$tags = if ($hub) { Get-DockerHubTags -Namespace $hub.Namespace -Repository $hub.Repository } else { $null }
	$remoteCurrentMajor = if ($tags) { Get-LatestSemVerTag -Tags $tags -Major $CurrentMajor } else { $null }
	$remoteNextMajor = if ($tags) { Get-LatestSemVerTag -Tags $tags -Major ($CurrentMajor + 1) } else { $null }

	if ($currentVerRaw) {
		Write-Host "Current (running) version: $currentVerRaw" -ForegroundColor Gray
	} else {
		Write-Host "Current (running) version: [unknown - container may be stopped]" -ForegroundColor Yellow
	}

	if ($offlineLatest) {
		Write-Host "Local (offline) latest:    $offlineLatest" -ForegroundColor Gray
	} else {
		Write-Host "Local (offline) latest:    [none found in '$OfflineImagesFolder']" -ForegroundColor DarkGray
	}

	if ($remoteCurrentMajor) {
		Write-Host "Remote (internet) $CurrentMajor.x latest: $remoteCurrentMajor" -ForegroundColor Gray
	} else {
		Write-Host "Remote (internet) $CurrentMajor.x latest: [unavailable]" -ForegroundColor DarkGray
	}

	if ($remoteNextMajor) {
		Write-Host "Remote (internet) $($CurrentMajor + 1).x latest: $remoteNextMajor" -ForegroundColor Gray
	} else {
		Write-Host "Remote (internet) $($CurrentMajor + 1).x latest: [unavailable]" -ForegroundColor DarkGray
	}

	Write-Host ""
	Write-Host "Choose update type:" -ForegroundColor White

	$choices = @()

	# 1) Local (offline) latest
	if ($offlineLatest) {
		$choices += [PSCustomObject]@{ Key = "1"; Label = "Install from: Local (offline) latest:    $offlineLatest"; Tag = $offlineLatest; Source = "offline" }
	}

	# 2) Remote (internet) current major latest
	if ($remoteCurrentMajor) {
		$k = [string]($choices.Count + 1)
		$choices += [PSCustomObject]@{ Key = $k; Label = "Install from: Remote (internet) $CurrentMajor.x latest: $remoteCurrentMajor"; Tag = $remoteCurrentMajor; Source = "internet" }
	}

	# 3) Remote (internet) next major latest
	if ($remoteNextMajor) {
		$k = [string]($choices.Count + 1)
		$choices += [PSCustomObject]@{ Key = $k; Label = "Install from: Remote (internet) $($CurrentMajor + 1).x latest: $remoteNextMajor"; Tag = $remoteNextMajor; Source = "internet" }
	}

	if (-not $choices -or $choices.Count -eq 0) {
		Write-Warning "No install options are available (offline folder empty and remote tags unavailable)."
		return $null
	}

	foreach ($c in $choices) {
		Write-Host ("{0}. {1}" -f $c.Key, $c.Label) -ForegroundColor Cyan
	}
	Write-Host "0. Cancel" -ForegroundColor Cyan

	$pick = Read-Host "Enter your choice"
	if ([string]::IsNullOrWhiteSpace($pick)) { $pick = $choices[0].Key }
	if ($pick -eq "0") { return $null }

	$sel = $choices | Where-Object { $_.Key -eq $pick } | Select-Object -First 1
	if (-not $sel) { Write-Warning "Invalid selection."; return $null }

	$targetTag = [string]$sel.Tag
	$targetImage = "$Repository`:$targetTag"

	# If user picked offline, verify the tar exists for that tag.
	if ($sel.Source -eq "offline") {
		if (-not ($offline -and $offline.Tags -and $offline.Tags -contains $targetTag)) {
			Write-Warning "Offline selection was chosen but the required tar for '$targetTag' was not found."
			return $null
		}
	}

	return [PSCustomObject]@{
		TargetImage = $targetImage
		Source      = [string]$sel.Source
		TargetTag   = $targetTag
	}
}

#==============================================================================
# Function: Test-n8nUpdateAvailableByVersion
#==============================================================================
<#
.SYNOPSIS
	Checks for n8n updates by comparing the running container's n8n version with the latest 1.x tag on Docker Hub.
.DESCRIPTION
	Does proper numeric semver comparison (no string comparison issues like 1.10 vs 1.2).
	Requires the container to be running to read the local n8n version reliably.
#>
function Test-n8nUpdateAvailableByVersion {
	[CmdletBinding()]
	[OutputType([bool])]
	param(
		[Parameter(Mandatory = $true)]
		[string]$Engine,

		[Parameter(Mandatory = $true)]
		[string]$ContainerName,

		[int]$Major = 1
	)

	$localRaw = Get-ContainerAppVersion -Engine $Engine -ContainerName $ContainerName -Command @("n8n", "--version")
	if ([string]::IsNullOrWhiteSpace($localRaw)) {
		Write-Warning "Could not read local n8n version from container '$ContainerName'. Falling back to digest-based check."
		return $true
	}

	$localRaw = $localRaw.Trim()
	$localSv = ConvertTo-SemVer -Version $localRaw
	if ($null -eq $localSv) {
		Write-Warning "Local n8n version '$localRaw' is not in expected form 'major.minor.patch'. Falling back to digest-based check."
		return $true
	}

	$tags = Get-DockerHubTags -Namespace "n8nio" -Repository "n8n"
	if (-not $tags -or $tags.Count -eq 0) {
		Write-Warning "Could not fetch remote tags from Docker Hub. Falling back to digest-based check."
		return $true
	}

	$latestTag = Get-LatestSemVerTag -Tags $tags -Major $Major
	if ([string]::IsNullOrWhiteSpace($latestTag)) {
		Write-Warning "Could not determine latest $Major.x.y tag from Docker Hub tags. Falling back to digest-based check."
		return $true
	}

	$remoteSv = ConvertTo-SemVer -Version $latestTag
	if ($null -eq $remoteSv) {
		Write-Warning "Failed to parse latest remote tag '$latestTag'. Falling back to digest-based check."
		return $true
	}

	Write-Host ""
	Write-Host "n8n Version Comparison:" -ForegroundColor Cyan
	Write-Host "=======================" -ForegroundColor Cyan
	Write-Host "Current running n8n:     $localRaw"
	Write-Host "Latest $Major.x image:    $latestTag"
	Write-Host ""

	$cmp = Compare-SemVer -A $remoteSv -B $localSv
	if ($cmp -gt 0) {
		Write-Host "Update available based on n8n version." -ForegroundColor Green
		return $true
	}

	Write-Host "No update available based on n8n version." -ForegroundColor Green
	return $false
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

	Important behavior:
	- If the remote digest cannot be determined (e.g., no internet / DNS failure / registry blocked),
	  this function will NOT automatically assume an update is needed. Instead, it will prompt the user
	  to choose whether to proceed with an update anyway (forced update) or to skip.
.PARAMETER Engine
	Path to the container engine executable (e.g., 'docker' or 'podman'). Mandatory.
.PARAMETER ImageName
	The full name and tag of the container image to check (e.g., 'ghcr.io/open-webui/open-webui:main'). Mandatory.
.OUTPUTS
	[bool] Returns $true if an update is detected (digests differ), the image is not found locally,
		   or the user chooses to force an update when the remote state is unknown.
		   Returns $false if the local and remote digests match, or the user chooses to skip when remote state is unknown.
.EXAMPLE
	if (Test-ImageUpdateAvailable -Engine "podman" -ImageName "docker.io/library/alpine:latest") { Invoke-PullImage ... }
.NOTES
	Attempts multiple methods to get remote digest for robustness (docker manifest, skopeo, podman pull).
	Does not force updates when the remote digest cannot be determined, because that can lead to failed pulls and downtime.
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

	Write-Host "Checking for updates to $ImageName..."

	# First, check if we have the image locally.
	# IMPORTANT: Do not use 'engine inspect <imageName>' as the only presence check.
	# For Docker, fully-qualified references like 'docker.io/n8nio/n8n:latest' may not resolve via inspect
	# unless that exact name is tagged locally, even when the image exists.
	$localImageId = $null

	# Attempt 1 (Docker-compatible): filter by reference
	try {
		$localImageId = (& $Engine images --filter "reference=$ImageName" --format "{{.ID}}" 2>$null | Select-Object -First 1)
		if (-not [string]::IsNullOrWhiteSpace($localImageId)) {
			$localImageId = $localImageId.Trim()
		}
		else {
			$localImageId = $null
		}
	}
	catch {
		$localImageId = $null
	}

	# Attempt 2 (Engine-agnostic): scan image list for exact repo:tag match
	if ($null -eq $localImageId) {
		try {
			[string[]]$lines = @(& $Engine images --format "{{.Repository}}:{{.Tag}} {{.ID}}" 2>$null)
			foreach ($line in $lines) {
				if ([string]::IsNullOrWhiteSpace($line)) { continue }
				$parts = $line.Trim() -split '\s+', 2
				if ($parts.Count -lt 2) { continue }
				$ref = $parts[0].Trim()
				$id = $parts[1].Trim()
				if ($ref -eq $ImageName -and -not [string]::IsNullOrWhiteSpace($id)) {
					$localImageId = $id
					break
				}
			}
		}
		catch {
			$localImageId = $null
		}
	}

	if ($null -eq $localImageId) {
		Write-Host "Image '$ImageName' not found locally. Update is available."
		return $true
	}

	# Inspect by ID to get consistent details.
	$localImageInfo = & $Engine inspect $localImageId 2>$null | ConvertFrom-Json
	if (-not $localImageInfo) {
		Write-Host "Image '$ImageName' not found locally. Update is available."
		return $true
	}

	# Get local image digest and creation info
	$localDigest = $null
	$localRepoDigests = @()
	$localRepoTags = @()
	$localCreated = ""
	$localSize = ""
	$localLabels = $null
	try {
		$local = if ($localImageInfo -is [array]) { $localImageInfo[0] } else { $localImageInfo }
		$localDigest = $local.Id
		$localRepoDigests = $local.RepoDigests
		$localRepoTags = $local.RepoTags
		$localCreated = $local.Created
		$localSize = $local.Size
		$localLabels = $local.Config.Labels
	}
	catch {
		Write-Warning "Could not determine local image information: $_"
		return $true
	}

	# Determine container engine type (docker or podman)
	$engineType = "docker"
	if ((Get-Item $Engine).Name -like "*podman*") {
		$engineType = "podman"
	}

	Write-Host "Checking remote registry for latest version..."
	$isManifestDigest = $false
	$remoteDigest = $null

	if ($engineType -eq "docker") {
		try {
			$remoteDigest = & $Engine manifest inspect $ImageName --verbose 2>$null | ConvertFrom-Json |
			Select-Object -ExpandProperty Descriptor -ErrorAction SilentlyContinue |
			Select-Object -ExpandProperty digest -ErrorAction SilentlyContinue

			if ($remoteDigest) { $isManifestDigest = $true }
		}
		catch {
			$remoteDigest = $null
			Write-Warning "Error checking remote manifest: $_"
		}

		if (-not $remoteDigest) {
			Write-Warning "Could not determine remote image digest. Using fallback method."
			& $Engine pull $ImageName 2>&1 | Out-Null
			if ($LASTEXITCODE -eq 0) {
				$remoteImageInfo = & $Engine inspect $ImageName 2>$null | ConvertFrom-Json
				if ($remoteImageInfo -is [array]) {
					$remoteDigest = $remoteImageInfo[0].Id
				}
				else {
					$remoteDigest = $remoteImageInfo.Id
				}
			}
			else {
				Write-Warning "Fallback pull failed. Check network connection."
			}
		}
	}
	else {
		$tempTag = "temp-check-update-$(Get-Random):latest"

		$skopeo = Get-Command skopeo -ErrorAction SilentlyContinue
		if ($skopeo) {
			try {
				$skopeoUri = $ImageName
				if (-not $skopeoUri.StartsWith("docker://") -and -not $skopeoUri.StartsWith("podman://")) {
					$skopeoUri = "docker://$skopeoUri"
				}

				$skopeoOutput = & skopeo inspect $skopeoUri --raw 2>$null
				$skopeoJson = $skopeoOutput | ConvertFrom-Json
				$remoteDigest = $skopeoJson.config.digest

				if ($remoteDigest) { $isManifestDigest = $true }
			}
			catch {
				$remoteDigest = $null
				Write-Warning "Skopeo inspection failed: $_"
			}
		}

		if (-not $remoteDigest) {
			& $Engine pull --quiet $ImageName 2>&1 | Out-Null

			if ($LASTEXITCODE -eq 0) {
				& $Engine tag $ImageName $tempTag 2>&1 | Out-Null

				$remoteImageInfo = & $Engine inspect $tempTag 2>$null | ConvertFrom-Json
				if ($remoteImageInfo -is [array]) {
					$remoteDigest = $remoteImageInfo[0].Id
				}
				else {
					$remoteDigest = $remoteImageInfo.Id
				}

				& $Engine rmi $tempTag 2>&1 | Out-Null
			}
			else {
				Write-Warning "Fallback pull failed. Check network connection."
			}
		}
	}

	if (-not $remoteDigest) {
		Write-Warning "Could not determine remote image digest. Remote state is unknown."

		$forceWhenUnknown = Read-Host "Remote check failed (no internet/registry?). Force update anyway? (Y/N, default is N)"
		if ($forceWhenUnknown -eq "Y") {
			Write-Host "Proceeding with forced update (remote state unknown)." -ForegroundColor Yellow
			return $true
		}

		Write-Host "Skipping update because remote state is unknown." -ForegroundColor Yellow
		return $false
	}

	# Try to get remote image creation date for better version display
	$remoteCreated = ""
	try {
		if ($engineType -eq "docker") {
			$manifestInfo = & $Engine manifest inspect $ImageName 2>$null | ConvertFrom-Json
			if ($manifestInfo -and $manifestInfo.history) {
				$latestHistory = $manifestInfo.history[0]
				if ($latestHistory.created) {
					$remoteCreated = $latestHistory.created
				}
			}
		}
		else {
			$tempImageInfo = & $Engine inspect $ImageName 2>$null | ConvertFrom-Json
			if ($tempImageInfo) {
				$remoteCreated = if ($tempImageInfo -is [array]) { $tempImageInfo[0].Created } else { $tempImageInfo.Created }
			}
		}
	}
	catch {
		Write-Warning "Could not get remote image creation info: $_"
	}

	if ($localSize) {
		$sizeMB = [math]::Round($localSize / 1MB, 2)
		Write-Host "Current local image size:     $sizeMB MB"
	}

	if ($localRepoTags) {
		$tags = @($localRepoTags) -join ", "
		if (-not [string]::IsNullOrWhiteSpace($tags)) {
			Write-Host "Current local image tags:     $tags"
		}
	}

	if ($localLabels) {
		$labelKeys = @(
			"org.opencontainers.image.version",
			"org.opencontainers.image.revision",
			"org.opencontainers.image.created",
			"org.opencontainers.image.source"
		)

		foreach ($k in $labelKeys) {
			if ($localLabels.PSObject.Properties.Name -contains $k) {
				$v = $localLabels.$k
				if (-not [string]::IsNullOrWhiteSpace([string]$v)) {
					Write-Host ("Current local image label:    {0}={1}" -f $k, $v)
				}
			}
		}
	}

	Write-Host ""
	Write-Host "Version Comparison:" -ForegroundColor Cyan
	Write-Host "==================" -ForegroundColor Cyan

	if ($localCreated) {
		try {
			$localCreatedDate = [DateTime]::Parse($localCreated).ToString("yyyy-MM-dd HH:mm:ss")
			Write-Host "Current local image created:  $localCreatedDate"
		}
		catch {
			Write-Host "Current local image created:  [Unable to parse date]"
		}
	}
	Write-Host "Current local image digest:   $localDigest"

	if ($remoteCreated) {
		try {
			$remoteCreatedDate = [DateTime]::Parse($remoteCreated).ToString("yyyy-MM-dd HH:mm:ss")
			Write-Host "Latest remote image created:  $remoteCreatedDate"
		}
		catch {
			Write-Host "Latest remote image created:  [Unable to parse date]"
		}
	}
	Write-Host "Latest remote image digest:   $remoteDigest"
	Write-Host ""

	$updateAvailable = $false
	if ($isManifestDigest) {
		if ($localRepoDigests -match $remoteDigest) {
			$updateAvailable = $false
		}
		else {
			$updateAvailable = $true
		}
	}
	else {
		if ($localDigest -ne $remoteDigest) {
			$updateAvailable = $true
		}
	}

	if ($updateAvailable) {
		Write-Host "Update available! Local and remote image digests differ." -ForegroundColor Green
		if ($isManifestDigest) {
			Write-Host "Local digest (ID)  : $localDigest" -ForegroundColor Yellow
			Write-Host "Remote digest (Man): $remoteDigest" -ForegroundColor Yellow
		}
		else {
			Write-Host "Local digest : $localDigest" -ForegroundColor Yellow
			Write-Host "Remote digest: $remoteDigest" -ForegroundColor Yellow
		}

		# NOTE: Do not ask to proceed here.
		# The caller (Update-Container) prompts and also offers "image source" selection via Invoke-PullImage.
		return $true
	}

	Write-Host "No update available. You have the latest version." -ForegroundColor Green
	$forceUpdate = Read-Host "No update detected. Do you want to force update anyway? (Y/N, default is N)"
	if ($forceUpdate -eq "Y") {
		Write-Host "Forcing update as requested by user." -ForegroundColor Yellow
		return $true
	}

	return $false
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
	3. Pulls the latest version of the specified image using Invoke-PullImage (which handles ShouldProcess).
	4. Removes the existing container using Remove-ContainerAndVolume (which handles ShouldProcess and optional volume removal).
	It does NOT handle backup, restore, or starting the new container. These steps should be
	orchestrated by the calling script (e.g., the menu action).
.PARAMETER Engine
	Path to the container engine executable (e.g., 'docker' or 'podman'). Mandatory.
.PARAMETER ContainerName
	The name of the container to update. Mandatory.
.PARAMETER VolumeName
	The name of the volume associated with the container (used for removal step). Optional. If provided, Remove-ContainerAndVolume will check/prompt for its removal.
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
	[OutputType([object])]
	param(
		[Parameter(Mandatory = $true)]
		[string]$Engine,

		[Parameter(Mandatory = $true)]
		[string]$ContainerName,

		[Parameter(Mandatory = $false)] # Made optional here too, as it's passed down
		[string]$VolumeName, # Needed for Remove-ContainerAndVolume

		[Parameter(Mandatory = $true)]
		[string]$ImageName,

		[string]$Platform = "linux/amd64", # Keep platform for pull

		[Parameter(Mandatory = $false)]
		[string]$OfflineImagesFolder = $global:offlineImagesFolder
	)

	Write-Host "Initiating update pre-check for container '$ContainerName'..."

	# Step 1: Check if container exists
	& $Engine inspect $ContainerName 2>$null | Out-Null # Check existence without storing info
	if ($LASTEXITCODE -ne 0) {
		Write-Host "Container '$ContainerName' not found. Nothing to update."
		return $false # Can't update something that doesn't exist
	}

	# Step 2: Determine current/local/remote versions and let the user choose target and source.
	# Special-case n8n: use semantic version compare (1.x update vs 2.x upgrade) and allow reinstall.
	$repo = $ImageName
	if ($ImageName -match '^(?<r>.+):(?<t>[^:]+)$') { $repo = $matches['r'] }

	$selection = $null
	if ($repo -like "*n8nio/n8n*") {
		$selection = Show-ImageUpdateOptions -Engine $Engine -ContainerName $ContainerName -Repository $repo -OfflineImagesFolder $OfflineImagesFolder -CurrentMajor 1 -VersionCommand @("n8n", "--version")
	}
	if (-not $selection) {
		# Fallback to old digest-based behavior for non-n8n images or if user cancelled.
		$updateAvailable = Test-ImageUpdateAvailable -Engine $Engine -ImageName $ImageName
		if (-not $updateAvailable) {
			Write-Host "Update canceled by user or no update available/forced."
			return $false
		}
		$selection = [PSCustomObject]@{ TargetImage = $ImageName; Source = "auto" }
	}

	$targetImage = $selection.TargetImage
	Write-Host "Selected target image: $targetImage" -ForegroundColor Gray

	# Step 3: Acquire the selected image.
	# If the user chose internet, skip offline prompt by passing empty OfflineImagesFolder.
	Write-Host "Acquiring image '$targetImage'..."
	$offlineFolderForAcquire = $OfflineImagesFolder
	if ($selection.Source -eq "internet") { $offlineFolderForAcquire = "" }

	if (-not (Invoke-PullImage -Engine $Engine -ImageName $targetImage -PullOptions @("--platform", $Platform) -OfflineImagesFolder $offlineFolderForAcquire)) {
		Write-Error "Failed to acquire the selected image or action skipped. Update aborted."
		return $false
	}
	Write-Host "Image '$targetImage' acquired successfully."

	# Step 4: Remove the existing container (Remove-ContainerAndVolume handles ShouldProcess and volume prompt)
	Write-Host "Removing existing container '$ContainerName'..."
	if (-not (Remove-ContainerAndVolume -Engine $Engine -ContainerName $ContainerName -VolumeName $VolumeName)) {
		Write-Error "Failed to remove container '$ContainerName' or action skipped. Update aborted."
		return $false
	}
	Write-Host "Existing container removed."

	# Indicate that the core update steps (check, acquire, remove) were successful
	Write-Host "Update pre-check, image acquire, and container removal completed successfully."
	return $targetImage
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
