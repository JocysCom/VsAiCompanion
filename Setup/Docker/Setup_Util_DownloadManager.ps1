################################################################################
# Description  : Utility to manage locally downloaded container images (Docker/Podman).
#                - List Downloaded Images
#                - Remove Downloaded Images (0 = ALL or choose one)
################################################################################

using namespace System

# Dot-source helper scripts
. "$PSScriptRoot\Setup_Helper_CoreFunctions.ps1"
. "$PSScriptRoot\Setup_Helper_ContainerEngine.ps1"
. "$PSScriptRoot\Setup_Helper_ContainerManagement.ps1"

Set-ScriptLocation

#==============================================================================
# Script Variables
#==============================================================================

$script:toolName = "Downloaded Images Manager"
$script:containerEngine = $null
$script:enginePath = $null

$script:manifestPaths = @(
	(Join-Path $PSScriptRoot "Files\Aspire\manifest.json"),
	(Join-Path $PSScriptRoot "Files\Aspire\manifest.zep.json")
)

# Uses $global:offlineImagesFolder from Setup_Helper_ContainerManagement.ps1

#==============================================================================
# Function: Initialize-DownloadManagerContext
#==============================================================================
<#
.SYNOPSIS
	Prompts for container engine selection and initializes engine path variables.
.DESCRIPTION
	Uses the shared Select-ContainerEngine helper to choose Docker or Podman, and resolves
	the executable path with Get-EnginePath. Docker operations require elevation; this
	function enforces admin privilege for Docker.
.OUTPUTS
	[bool] True if initialization succeeds; otherwise False (and may exit).
.EXAMPLE
	PS C:\> Initialize-DownloadManagerContext
#>
function Initialize-DownloadManagerContext {
	[CmdletBinding()]
	[OutputType([bool])]
	param()

	$script:containerEngine = Select-ContainerEngine
	if (-not $script:containerEngine) {
		Write-Warning "No container engine selected. Exiting."
		return $false
	}

	if ($script:containerEngine -eq "docker") {
		Test-AdminPrivilege
	}

	$script:enginePath = Get-EnginePath -EngineName $script:containerEngine
	if (-not $script:enginePath) {
		Write-Error "Failed to resolve engine executable path."
		return $false
	}

	return $true
}

#==============================================================================
# Function: Get-DownloadedImage
#==============================================================================
<#
.SYNOPSIS
	Returns a list of locally available container images.
.DESCRIPTION
	Queries the selected container engine for locally available images and returns an array
	of "repository:tag" strings, excluding dangling images (<none>:<none>).
.PARAMETER EnginePath
	Path to the container engine executable.
.OUTPUTS
	[object[]] Array of image names (repository:tag).
.EXAMPLE
	PS C:\> Get-DownloadedImage -EnginePath $script:enginePath
#>
function Get-DownloadedImage {
	[CmdletBinding()]
	[OutputType([object[]])]
	param(
		[Parameter(Mandatory = $true)]
		[string]$EnginePath
	)

	# Output "repository:tag" per line. Works for both docker and podman.
	[string[]]$lines = @(& $EnginePath images --format "{{.Repository}}:{{.Tag}}" 2>$null)
	if (-not $lines -or $lines.Count -eq 0) {
		return @()
	}

	$images = @()
	foreach ($line in $lines) {
		if ([string]::IsNullOrWhiteSpace($line)) { continue }
		$val = $line.Trim()
		if ($val -eq "<none>:<none>") { continue }
		$images += $val
	}

	$images = @($images | Sort-Object -Unique)
	[string[]]$images = $images
	return $images
}

#==============================================================================
# Function: Show-DownloadedImage
#==============================================================================
<#
.SYNOPSIS
	Prints downloaded images to the console.
.DESCRIPTION
	Lists locally downloaded container images. If none exist, prints a friendly message.
.EXAMPLE
	PS C:\> Show-DownloadedImage
#>
function Show-DownloadedImage {
	[CmdletBinding()]
	param()

	$images = Get-DownloadedImage -EnginePath $script:enginePath
	Write-Host ""
	Write-Host "Engine : $script:containerEngine ($script:enginePath)" -ForegroundColor Cyan
	Write-Host "-------------------------------------------" -ForegroundColor Yellow

	if (-not $images -or $images.Count -eq 0) {
		Write-Host "No downloaded images found." -ForegroundColor Yellow
		return
	}

	Write-Host "Downloaded Images:" -ForegroundColor White
	[int]$i = 1
	foreach ($img in $images) {
		Write-Host ("{0}. {1}" -f $i, $img) -ForegroundColor Gray
		$i++
	}
}

#==============================================================================
# Function: Remove-DownloadedImage
#==============================================================================
<#
.SYNOPSIS
	Removes downloaded images.
.DESCRIPTION
	Prompts the user to remove ALL images (choice 0) or to pick a single image from a menu.
	Removal is executed using the selected container engine's rmi command.
.EXAMPLE
	PS C:\> Remove-DownloadedImage
#>
function Remove-DownloadedImage {
	[CmdletBinding(SupportsShouldProcess = $true)]
	param()

	$images = Get-DownloadedImage -EnginePath $script:enginePath
	if (-not $images -or $images.Count -eq 0) {
		Write-Host "No downloaded images found to remove." -ForegroundColor Yellow
		return
	}

	Write-Host ""
	Write-Host "Remove Downloaded Images" -ForegroundColor White
	Write-Host "-------------------------------------------" -ForegroundColor Yellow
	Write-Host "0. ALL"
	Write-Host "1. Pick one" -ForegroundColor Cyan

	$mode = Read-Host "Enter your choice"
	if ([string]::IsNullOrWhiteSpace($mode)) { $mode = "1" }

	if ($mode -eq "0") {
		$confirmAll = Read-Host "Remove ALL downloaded images listed? (Y/N, default N)"
		if ($confirmAll -ne "Y") {
			Write-Host "Cancelled." -ForegroundColor Yellow
			return
		}

		foreach ($img in $images) {
			if ($PSCmdlet.ShouldProcess($img, "Remove image")) {
				Write-Host "Removing image: $img" -ForegroundColor Yellow
				& $script:enginePath rmi $img 2>$null | Out-Null
				if ($LASTEXITCODE -ne 0) {
					Write-Warning "Failed to remove image: $img (it may be in use)."
				}
			}
		}
		Write-Host "Done." -ForegroundColor Green
		return
	}

	$selection = Invoke-OptionsMenu -Title "Select Image to Remove" -Options $images -ExitChoice "Exit menu"
	if (-not $selection -or $selection -eq "Exit menu") {
		return
	}

	$confirmOne = Read-Host "Remove image '$selection'? (Y/N, default N)"
	if ($confirmOne -ne "Y") {
		Write-Host "Cancelled." -ForegroundColor Yellow
		return
	}

	if ($PSCmdlet.ShouldProcess($selection, "Remove image")) {
		Write-Host "Removing image: $selection" -ForegroundColor Yellow
		& $script:enginePath rmi $selection
		if ($LASTEXITCODE -eq 0) {
			Write-Host "Removed: $selection" -ForegroundColor Green
		}
		else {
			Write-Error "Failed to remove image: $selection"
		}
	}
}

#==============================================================================
# Function: Get-ManifestImage
#==============================================================================
<#
.SYNOPSIS
	Reads Aspire manifest.json files and returns unique container image strings.
.DESCRIPTION
	Parses one or more Aspire manifest JSON files and extracts the .resources.*.properties.image values.
	Returns a unique, sorted list of images.
.PARAMETER ManifestPaths
	Array of manifest file paths to parse.
.OUTPUTS
	[string[]] List of image references found in the manifests.
.EXAMPLE
	PS C:\> Get-ManifestImage -ManifestPaths $script:manifestPaths
#>
function Get-ManifestImage {
	[CmdletBinding()]
	[OutputType([string[]])]
	param(
		[Parameter(Mandatory = $true)]
		[string[]]$ManifestPaths
	)

	$images = New-Object System.Collections.Generic.List[string]

	foreach ($path in $ManifestPaths) {
		if (-not (Test-Path -LiteralPath $path)) {
			continue
		}

		try {
			$manifest = Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
		}
		catch {
			Write-Warning "Failed to parse manifest: $path"
			continue
		}

		if (-not $manifest.resources) { continue }

		foreach ($resProp in $manifest.resources.PSObject.Properties) {
			$resource = $resProp.Value
			$image = $resource.properties.image
			if (-not [string]::IsNullOrWhiteSpace($image)) {
				$images.Add($image.Trim())
			}
		}
	}

	[string[]]$result = @($images | Sort-Object -Unique)
	return $result
}

#==============================================================================
# Function: Get-LocalImageId
#==============================================================================
<#
.SYNOPSIS
	Gets local image ID for an image reference (if present).
.DESCRIPTION
	Uses the container engine to query the local image store for the given image reference.
.PARAMETER EnginePath
	Path to docker.exe/podman.exe.
.PARAMETER ImageName
	Image reference (repo:tag).
.OUTPUTS
	[string] Image ID or $null if not found.
.EXAMPLE
	PS C:\> Get-LocalImageId -EnginePath $script:enginePath -ImageName "redis:alpine"
#>
function Get-LocalImageId {
	[CmdletBinding()]
	[OutputType([string])]
	param(
		[Parameter(Mandatory = $true)]
		[string]$EnginePath,

		[Parameter(Mandatory = $true)]
		[string]$ImageName
	)

	$id = & $EnginePath images --filter "reference=$ImageName" --format "{{.ID}}" 2>$null
	if ([string]::IsNullOrWhiteSpace($id)) {
		return $null
	}
	return ($id | Select-Object -First 1).Trim()
}

#==============================================================================
# Function: Get-DownloadedImageTar
#==============================================================================
<#
.SYNOPSIS
	Finds downloaded image tar backups for a given image name in downloads\images.
.DESCRIPTION
	Searches the downloads\images folder for files matching the naming scheme
	<safe-image>-image-*.tar and returns them sorted newest-first.
.PARAMETER ImageName
	Image reference (repo:tag).
.OUTPUTS
	[System.IO.FileInfo[]] Matching tar files.
.EXAMPLE
	PS C:\> Get-DownloadedImageTar -ImageName "redis:alpine"
#>
function Get-DownloadedImageTar {
	[CmdletBinding()]
	[OutputType([object[]])]
	param(
		[Parameter(Mandatory = $true)]
		[string]$ImageName
	)

	if (-not (Test-Path -LiteralPath $global:offlineImagesFolder)) {
		return @()
	}

	$safeImageName = $ImageName -replace "[:/]", "_"
	$pattern = "$safeImageName-image-*.tar"
	[System.IO.FileInfo[]]$files = @(Get-ChildItem -LiteralPath $global:offlineImagesFolder -Filter $pattern -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending)
	return $files
}

#==============================================================================
# Function: Save-DownloadedImageTar
#==============================================================================
<#
.SYNOPSIS
	Pulls an image (if needed) and saves it as a tar in downloads\images.
.DESCRIPTION
	Ensures the downloads\images folder exists. Checks if the image exists locally; if not,
	pulls it using Invoke-PullImage. Then saves the image to a timestamped tar file using
	'engine save --output ...'.
.PARAMETER ImageName
	Image reference (repo:tag).
.EXAMPLE
	PS C:\> Save-DownloadedImageTar -ImageName "docker.io/n8nio/n8n:latest"
#>
function Save-DownloadedImageTar {
	[CmdletBinding(SupportsShouldProcess = $true)]
	param(
		[Parameter(Mandatory = $true)]
		[string]$ImageName
	)

	if (-not (Test-Path -LiteralPath $global:offlineImagesFolder)) {
		New-Item -ItemType Directory -Path $global:offlineImagesFolder -Force | Out-Null
	}

	$localId = Get-LocalImageId -EnginePath $script:enginePath -ImageName $ImageName
	if ($null -eq $localId) {
		if (-not (Invoke-PullImage -Engine $script:enginePath -ImageName $ImageName)) {
			Write-Error "Failed to pull image '$ImageName'."
			return
		}
	}

	$timestamp = Get-Date -Format "yyyyMMdd-HHmm"
	$safeImageName = $ImageName -replace "[:/]", "_"
	$tarName = "$safeImageName-image-$timestamp.tar"
	$tarPath = Join-Path $global:offlineImagesFolder $tarName

	if ($PSCmdlet.ShouldProcess($ImageName, "Save image tar to '$tarPath'")) {
		Write-Host "Saving image to: $tarPath" -ForegroundColor Yellow
		& $script:enginePath save --output $tarPath $ImageName 2>$null | Out-Null
		if ($LASTEXITCODE -eq 0) {
			Write-Host "Saved: $tarPath" -ForegroundColor Green
		}
		else {
			Write-Error "Failed to save image '$ImageName' to '$tarPath'."
		}
	}
}

#==============================================================================
# Function: Show-ManifestImageStatus
#==============================================================================
<#
.SYNOPSIS
	Displays local image status and downloaded tar availability for a selected manifest image.
.DESCRIPTION
	Shows whether the image exists locally (and its ID) and lists any downloaded tar files
	in downloads\images for offline transfer.
.PARAMETER ImageName
	Image reference.
.EXAMPLE
	PS C:\> Show-ManifestImageStatus -ImageName "docker.io/n8nio/n8n:latest"
#>
function Show-ManifestImageStatus {
	[CmdletBinding()]
	param(
		[Parameter(Mandatory = $true)]
		[string]$ImageName
	)

	Write-Host ""
	Write-Host "Image: $ImageName" -ForegroundColor White
	Write-Host "-------------------------------------------" -ForegroundColor Yellow

	$localId = Get-LocalImageId -EnginePath $script:enginePath -ImageName $ImageName
	if ($null -eq $localId) {
		Write-Host "Local image: (none)" -ForegroundColor Yellow
	}
	else {
		Write-Host "Local image: $localId" -ForegroundColor Cyan
	}

	$tarFiles = Get-DownloadedImageTar -ImageName $ImageName
	if (-not $tarFiles -or $tarFiles.Count -eq 0) {
		Write-Host "Downloaded tar: (none) in '$global:offlineImagesFolder'" -ForegroundColor Yellow
	}
	else {
		$latest = $tarFiles[0]
		Write-Host "Downloaded tar (latest): $($latest.Name)  [$([DateTime]$latest.LastWriteTime)]" -ForegroundColor Green
	}
}

#==============================================================================
# Function: Select-ManifestImageAndManageDownload
#==============================================================================
<#
.SYNOPSIS
	Selects an image from manifests and offers actions to download (pull+save) offline tar.
.DESCRIPTION
	Shows the selected image status, then offers to create a downloaded tar in downloads\images.
.EXAMPLE
	PS C:\> Select-ManifestImageAndManageDownload
#>
function Select-ManifestImageAndManageDownload {
	[CmdletBinding()]
	param()

	$images = Get-ManifestImage -ManifestPaths $script:manifestPaths
	if (-not $images -or $images.Count -eq 0) {
		Write-Host "No images found in manifest files." -ForegroundColor Yellow
		return
	}

	$selection = Invoke-OptionsMenu -Title "Select Image from Manifests" -Options $images -ExitChoice "Exit menu"
	if (-not $selection -or $selection -eq "Exit menu") {
		return
	}

	Show-ManifestImageStatus -ImageName $selection

	Write-Host ""
	Write-Host "Actions:" -ForegroundColor White
	Write-Host "1. Download image tar now (pull if needed, then save to downloads\\images)" -ForegroundColor Cyan
	Write-Host "0. Back" -ForegroundColor Cyan
	$action = Read-Host "Enter your choice"
	if ($action -eq "1") {
		Save-DownloadedImageTar -ImageName $selection -Confirm:$false
		Show-ManifestImageStatus -ImageName $selection
	}
}

#==============================================================================
# Main Script Execution
#==============================================================================

if (-not (Initialize-DownloadManagerContext)) {
	exit 1
}

$menuTitle = "$script:toolName Menu"
$menuItems = [ordered]@{
	"1" = "List Downloaded Images"
	"2" = "Remove Downloaded Images"
	"3" = "Images from Aspire Manifests (download tar to downloads\\images)"
	"0" = "Exit menu"
}

$menuActions = @{
	"1" = {
		Show-DownloadedImage
		Read-Host "`nPress Enter to continue"
	}
	"2" = {
		Remove-DownloadedImage -Confirm:$false
		Read-Host "`nPress Enter to continue"
	}
	"3" = {
		Select-ManifestImageAndManageDownload
		Read-Host "`nPress Enter to continue"
	}
}

Invoke-MenuLoop -MenuTitle $menuTitle -MenuItems $menuItems -ActionMap $menuActions -ExitChoice "0" -DefaultChoice "1"