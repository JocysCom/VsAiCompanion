################################################################################
# File         : Setup_App_5a_Qdrant_Core_Windows.ps1
# Description  : Installs and runs Qdrant on Windows without containers.
#                Downloads the official Qdrant Windows release binary to
#                C:\ProgramData\Qdrant and stores data under the same folder.
# Usage        : Run in PowerShell. Choose ports during setup. Then open the shown URL.
################################################################################

using namespace System
using namespace System.IO

# Ensure script runs from its own directory
Set-Location -Path $PSScriptRoot

#==============================================================================
# Global Configuration
#==============================================================================

$global:appName = "Qdrant"
$global:settingsVersion = 1

$global:defaultHttpPort = 6333
$global:defaultGrpcPort = 6334

$global:programDataRoot = [Environment]::GetFolderPath([Environment+SpecialFolder]::CommonApplicationData)
$global:installRoot = Join-Path $global:programDataRoot $global:appName
$global:storageRoot = Join-Path $global:installRoot "storage"
$global:downloadsRoot = Join-Path $global:installRoot "downloads"
$global:staticRoot = Join-Path $global:installRoot "static"
$global:settingsPath = Join-Path $global:installRoot "settings.json"
$global:qdrantExePath = Join-Path $global:installRoot "qdrant.exe"

$global:githubRepoOwner = "qdrant"
$global:githubRepoName = "qdrant"
$global:githubLatestReleaseApiUrl = "https://api.github.com/repos/$($global:githubRepoOwner)/$($global:githubRepoName)/releases/latest"
$global:githubUserAgent = "VsAiCompanion-Qdrant-Windows-Setup"

$global:qdrantWebUiRepoOwner = "qdrant"
$global:qdrantWebUiRepoName = "qdrant-web-ui"
$global:qdrantWebUiLatestReleaseApiUrl = "https://api.github.com/repos/$($global:qdrantWebUiRepoOwner)/$($global:qdrantWebUiRepoName)/releases/latest"
$global:qdrantWebUiZipAssetName = "dist-qdrant.zip"
$global:qdrantWebUiVersionFile = Join-Path $global:installRoot ".qdrant-web-ui-version"

$global:dashboardPath = "/dashboard"

#==============================================================================
# Function: New-Directory
#==============================================================================
<#
.SYNOPSIS
	Creates a directory if it does not exist.
.DESCRIPTION
	Ensures the specified directory exists, creating it (and parents) if needed.
.PARAMETER Path
	Directory path to ensure exists.
.OUTPUTS
	[void]
#>
function New-Directory {
	[CmdletBinding(SupportsShouldProcess = $true)]
	param(
		[Parameter(Mandatory = $true)]
		[string]$Path
	)

	if (-not (Test-Path -LiteralPath $Path)) {
		if ($PSCmdlet.ShouldProcess($Path, "Create directory")) {
			New-Item -ItemType Directory -Path $Path -Force | Out-Null
			Write-Host "Created directory: $Path" -ForegroundColor DarkGray
		}
	}
}

#==============================================================================
# Function: Test-TcpPortAvailable
#==============================================================================
<#
.SYNOPSIS
	Tests if a local TCP port is available.
.DESCRIPTION
	Attempts to bind a TcpListener to localhost on the specified port.
	Returns $true if the bind succeeds; otherwise $false.
.PARAMETER Port
	TCP port to test.
.OUTPUTS
	[bool]
#>
function Test-TcpPortAvailable {
	[CmdletBinding()]
	[OutputType([bool])]
	param(
		[Parameter(Mandatory = $true)]
		[int]$Port
	)

	try {
		$listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, $Port)
		$listener.Start()
		$listener.Stop()
		return $true
	}
	catch {
		return $false
	}
}

#==============================================================================
# Function: Read-ValidatedPort
#==============================================================================
<#
.SYNOPSIS
	Prompts the user for a TCP port number.
.DESCRIPTION
	Prompts for a port with a default value. Validates range (1..65535) and availability.
.PARAMETER Prompt
	Prompt text.
.PARAMETER DefaultPort
	Default port to use when the user presses Enter.
.OUTPUTS
	[int]
#>
function Read-ValidatedPort {
	[CmdletBinding()]
	[OutputType([int])]
	param(
		[Parameter(Mandatory = $true)]
		[string]$Prompt,

		[Parameter(Mandatory = $true)]
		[int]$DefaultPort
	)

	while ($true) {
		$raw = Read-Host "$Prompt [default: $DefaultPort]"
		if ([string]::IsNullOrWhiteSpace($raw)) {
			$port = $DefaultPort
		}
		else {
			$parsed = 0
			if (-not [int]::TryParse($raw, [ref]$parsed)) {
			Write-Warning "Invalid port '$raw'. Please enter a number between 1 and 65535."
			continue
		}
			$port = $parsed
		}

		if ($port -lt 1 -or $port -gt 65535) {
			Write-Warning "Port must be between 1 and 65535."
			continue
		}

		if (-not (Test-TcpPortAvailable -Port $port)) {
			Write-Warning "Port $port is already in use on this machine. Choose another port."
			continue
		}

		return $port
	}
}

#==============================================================================
# Function: Get-QdrantSetting
#==============================================================================
<#
.SYNOPSIS
	Loads persisted Qdrant Windows settings.
.DESCRIPTION
	Reads settings from $global:settingsPath if present. Returns defaults when missing/invalid.
.OUTPUTS
	[pscustomobject]
#>
function Get-QdrantSetting {
	[CmdletBinding()]
	[OutputType([pscustomobject])]
	param()

	if (Test-Path -LiteralPath $global:settingsPath) {
		try {
			$content = Get-Content -LiteralPath $global:settingsPath -Raw -Encoding UTF8
			$settings = $content | ConvertFrom-Json
			if ($null -ne $settings -and $null -ne $settings.HttpPort -and $null -ne $settings.GrpcPort) {
				return [PSCustomObject]@{
					Version  = $settings.Version
					HttpPort  = [int]$settings.HttpPort
					GrpcPort  = [int]$settings.GrpcPort
				}
			}
		}
		catch {
			Write-Warning "Failed to load settings from '$($global:settingsPath)'. Using defaults. Details: $_"
		}
	}

	return [PSCustomObject]@{
		Version  = $global:settingsVersion
		HttpPort = $global:defaultHttpPort
		GrpcPort = $global:defaultGrpcPort
	}
}

#==============================================================================
# Function: Set-QdrantSetting
#==============================================================================
<#
.SYNOPSIS
	Saves persisted Qdrant Windows settings.
.DESCRIPTION
	Writes a small JSON file to $global:settingsPath.
.PARAMETER HttpPort
	HTTP port used by Qdrant.
.PARAMETER GrpcPort
	gRPC port used by Qdrant.
.OUTPUTS
	[void]
#>
function Set-QdrantSetting {
	[CmdletBinding(SupportsShouldProcess = $true)]
	param(
		[Parameter(Mandatory = $true)]
		[int]$HttpPort,

		[Parameter(Mandatory = $true)]
		[int]$GrpcPort
	)

	New-Directory -Path $global:installRoot
	New-Directory -Path $global:storageRoot
	New-Directory -Path $global:downloadsRoot

	$settings = [PSCustomObject]@{
		Version     = $global:settingsVersion
		UpdatedUtc  = (Get-Date).ToUniversalTime().ToString("o")
		InstallRoot = $global:installRoot
		Storage     = $global:storageRoot
		HttpPort    = $HttpPort
		GrpcPort    = $GrpcPort
	}

	if ($PSCmdlet.ShouldProcess($global:settingsPath, "Save settings")) {
		$settings | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $global:settingsPath -Encoding UTF8
		Write-Host "Saved settings to: $($global:settingsPath)" -ForegroundColor DarkGray
	}
}

#==============================================================================
# Function: Get-QdrantLatestRelease
#==============================================================================
<#
.SYNOPSIS
	Fetches the latest Qdrant release metadata from GitHub.
.DESCRIPTION
	Uses the GitHub REST API to retrieve the latest release and its assets.
.OUTPUTS
	[object]
#>
function Get-QdrantLatestRelease {
	[CmdletBinding()]
	[OutputType([object])]
	param()

	try {
		$headers = @{
			"User-Agent" = $global:githubUserAgent
			"Accept"     = "application/vnd.github+json"
		}
		return Invoke-RestMethod -Uri $global:githubLatestReleaseApiUrl -Headers $headers -Method Get -ErrorAction Stop
	}
	catch {
		throw "Failed to query GitHub latest release API: $_"
	}
}

#==============================================================================
# Function: Get-QdrantWebUiLatestRelease
#==============================================================================
<#
.SYNOPSIS
	Fetches the latest Qdrant Web UI release metadata from GitHub.
.DESCRIPTION
	Uses the GitHub REST API to retrieve the latest release and its assets.
.OUTPUTS
	[object]
#>
function Get-QdrantWebUiLatestRelease {
	[CmdletBinding()]
	[OutputType([object])]
	param()

	try {
		$headers = @{
			"User-Agent" = $global:githubUserAgent
			"Accept"     = "application/vnd.github+json"
		}
		return Invoke-RestMethod -Uri $global:qdrantWebUiLatestReleaseApiUrl -Headers $headers -Method Get -ErrorAction Stop
	}
	catch {
		throw "Failed to query GitHub Qdrant Web UI latest release API: $_"
	}
}

#==============================================================================
# Function: Select-QdrantWindowsZipAsset
#==============================================================================
<#
.SYNOPSIS
	Selects the best matching Windows zip asset from a GitHub release.
.DESCRIPTION
	Attempts to find a Windows x64 zip asset. Falls back to any Windows zip.
.PARAMETER Release
	Release object returned by the GitHub API.
.OUTPUTS
	[object]
#>
function Select-QdrantWindowsZipAsset {
	[CmdletBinding()]
	[OutputType([object])]
	param(
		[Parameter(Mandatory = $true)]
		[object]$Release
	)

	$assets = @($Release.assets)
	if (-not $assets -or $assets.Count -eq 0) {
		throw "GitHub release has no assets."
	}

	$zip64 = @(
		$assets | Where-Object {
			$_.name -match "(?i)windows" -and
			$_.name -match "(?i)(x86_64|amd64|x64)" -and
			$_.name -match "(?i)\.zip$"
		}
	)
	if ($zip64 -and $zip64.Count -gt 0) { return $zip64[0] }

	$zipAny = @(
		$assets | Where-Object {
			$_.name -match "(?i)windows" -and
			$_.name -match "(?i)\.zip$"
		}
	)
	if ($zipAny -and $zipAny.Count -gt 0) { return $zipAny[0] }

	$names = ($assets | Select-Object -ExpandProperty name) -join ", "
	throw "No Windows .zip asset found in latest release. Assets: $names"
}

#==============================================================================
# Function: Select-QdrantWebUiZipAsset
#==============================================================================
<#
.SYNOPSIS
	Selects the Qdrant Web UI distribution zip from a GitHub release.
.DESCRIPTION
	Finds the 'dist-qdrant.zip' asset in the latest qdrant-web-ui release.
.PARAMETER Release
	Release object returned by the GitHub API.
.OUTPUTS
	[object]
#>
function Select-QdrantWebUiZipAsset {
	[CmdletBinding()]
	[OutputType([object])]
	param(
		[Parameter(Mandatory = $true)]
		[object]$Release
	)

	$assets = @($Release.assets)
	if (-not $assets -or $assets.Count -eq 0) {
		throw "GitHub Qdrant Web UI release has no assets."
	}

	$asset = $assets | Where-Object { $_.name -eq $global:qdrantWebUiZipAssetName } | Select-Object -First 1
	if ($null -ne $asset) {
		return $asset
	}

	$names = ($assets | Select-Object -ExpandProperty name) -join ", "
	throw "Asset '$($global:qdrantWebUiZipAssetName)' was not found in latest Qdrant Web UI release. Assets: $names"
}

#==============================================================================
# Function: Invoke-DownloadFile
#==============================================================================
<#
.SYNOPSIS
	Downloads a file from a URL.
.DESCRIPTION
	Downloads a file using Invoke-WebRequest with a User-Agent header.
.PARAMETER SourceUrl
	Source URL.
.PARAMETER DestinationPath
	Local destination file path.
.PARAMETER ForceDownload
	If specified, downloads even when the destination already exists.
.OUTPUTS
	[void]
#>
function Invoke-DownloadFile {
	[CmdletBinding()]
	param(
		[Parameter(Mandatory = $true)]
		[string]$SourceUrl,

		[Parameter(Mandatory = $true)]
		[string]$DestinationPath,

		[switch]$ForceDownload
	)

	if ((Test-Path -LiteralPath $DestinationPath) -and (-not $ForceDownload)) {
		Write-Host "File already exists: $DestinationPath" -ForegroundColor DarkGray
		return
	}

	New-Directory -Path (Split-Path -Parent $DestinationPath)

	try {
		$headers = @{ "User-Agent" = $global:githubUserAgent }
		$ProgressPreference = 'SilentlyContinue'
		Write-Host "Downloading: $SourceUrl" -ForegroundColor Yellow
		$invokeParams = @{
			Uri         = $SourceUrl
			Headers     = $headers
			OutFile     = $DestinationPath
			ErrorAction = 'Stop'
		}
		$cmd = Get-Command Invoke-WebRequest -ErrorAction SilentlyContinue
		if ($cmd -and $cmd.Parameters.ContainsKey('UseBasicParsing')) {
			$invokeParams['UseBasicParsing'] = $true
		}
		Invoke-WebRequest @invokeParams
		$ProgressPreference = 'Continue'
		Write-Host "Downloaded to: $DestinationPath" -ForegroundColor Green
	}
	catch {
		$ProgressPreference = 'Continue'
		throw "Failed to download '$SourceUrl' to '$DestinationPath': $_"
	}
}

#==============================================================================
# Function: Install-QdrantWebUiIfMissing
#==============================================================================
<#
.SYNOPSIS
	Installs Qdrant Web UI static files (dashboard) if missing.
.DESCRIPTION
	Downloads the qdrant-web-ui distribution zip and extracts it into .\static under
	$global:installRoot, which Qdrant serves at /dashboard.
.OUTPUTS
	[void]
#>
function Install-QdrantWebUiIfMissing {
	[CmdletBinding()]
	param()

	New-Directory -Path $global:installRoot
	New-Directory -Path $global:downloadsRoot

	$indexPath = Join-Path $global:staticRoot "index.html"
	if (Test-Path -LiteralPath $indexPath) {
		Write-Host "Qdrant Web UI is already installed: $($global:staticRoot)" -ForegroundColor Green
		return
	}

	Write-Host "Qdrant Web UI not found. Downloading latest Web UI..." -ForegroundColor Yellow
	$release = Get-QdrantWebUiLatestRelease
	$asset = Select-QdrantWebUiZipAsset -Release $release

	$zipPath = Join-Path $global:downloadsRoot $asset.name
	Invoke-DownloadFile -SourceUrl $asset.browser_download_url -DestinationPath $zipPath

	$tempExtract = Join-Path $global:downloadsRoot "webui-extract"
	if (Test-Path -LiteralPath $tempExtract) {
		Remove-Item -LiteralPath $tempExtract -Recurse -Force -ErrorAction SilentlyContinue
	}
	New-Directory -Path $tempExtract

	Write-Host "Extracting Web UI: $zipPath" -ForegroundColor Yellow
	Expand-Archive -Path $zipPath -DestinationPath $tempExtract -Force

	$index = Get-ChildItem -LiteralPath $tempExtract -Recurse -Filter "index.html" -File -ErrorAction SilentlyContinue | Select-Object -First 1
	if (-not $index) {
		throw "Qdrant Web UI package did not contain an index.html."
	}

	$uiSourceRoot = Split-Path -Parent $index.FullName
	if (Test-Path -LiteralPath $global:staticRoot) {
		Remove-Item -LiteralPath $global:staticRoot -Recurse -Force -ErrorAction SilentlyContinue
	}
	New-Directory -Path $global:staticRoot
	Copy-Item -Path (Join-Path $uiSourceRoot '*') -Destination $global:staticRoot -Recurse -Force

	Remove-Item -LiteralPath $tempExtract -Recurse -Force -ErrorAction SilentlyContinue

	Set-Content -LiteralPath $global:qdrantWebUiVersionFile -Value $release.tag_name -Encoding UTF8

	if (-not (Test-Path -LiteralPath $indexPath)) {
		throw "Qdrant Web UI installation failed: '$indexPath' not found after extraction."
	}

	Write-Host "Installed Qdrant Web UI: $($global:staticRoot)  (version: $($release.tag_name))" -ForegroundColor Green
}

#==============================================================================
# Function: Install-QdrantIfMissing
#==============================================================================
<#
.SYNOPSIS
	Installs Qdrant into ProgramData if missing.
.DESCRIPTION
	Downloads the latest Windows .zip release from GitHub and extracts it to $global:installRoot.
.OUTPUTS
	[void]
#>
function Install-QdrantIfMissing {
	[CmdletBinding()]
	param()

	New-Directory -Path $global:installRoot
	New-Directory -Path $global:storageRoot
	New-Directory -Path $global:downloadsRoot

	if (Test-Path -LiteralPath $global:qdrantExePath) {
		Write-Host "Qdrant is already installed: $($global:qdrantExePath)" -ForegroundColor Green
		return
	}

	Write-Host "Qdrant not found. Downloading latest release..." -ForegroundColor Yellow
	$release = Get-QdrantLatestRelease
	$asset = Select-QdrantWindowsZipAsset -Release $release

	$zipPath = Join-Path $global:downloadsRoot $asset.name
	Invoke-DownloadFile -SourceUrl $asset.browser_download_url -DestinationPath $zipPath

	$tempExtract = Join-Path $global:downloadsRoot "extract"
	if (Test-Path -LiteralPath $tempExtract) {
		Remove-Item -LiteralPath $tempExtract -Recurse -Force -ErrorAction SilentlyContinue
	}
	New-Directory -Path $tempExtract

	Write-Host "Extracting: $zipPath" -ForegroundColor Yellow
	Expand-Archive -Path $zipPath -DestinationPath $tempExtract -Force

	$exe = Get-ChildItem -LiteralPath $tempExtract -Recurse -Filter "qdrant.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
	if (-not $exe) {
		throw "Extracted archive did not contain qdrant.exe."
	}

	$distRoot = Split-Path -Parent $exe.FullName
	Write-Host "Installing files from: $distRoot" -ForegroundColor DarkGray

	# Copy distribution contents to install root (keeps storage/settings under install root as separate folders/files)
	Copy-Item -Path (Join-Path $distRoot '*') -Destination $global:installRoot -Recurse -Force

	# Cleanup extraction folder
	Remove-Item -LiteralPath $tempExtract -Recurse -Force -ErrorAction SilentlyContinue

	if (-not (Test-Path -LiteralPath $global:qdrantExePath)) {
		throw "Install failed: qdrant.exe not found at '$($global:qdrantExePath)' after extraction."
	}

	Write-Host "Installed: $($global:qdrantExePath)" -ForegroundColor Green
}

#==============================================================================
# Function: Start-Qdrant
#==============================================================================
<#
.SYNOPSIS
	Starts Qdrant on Windows.
.DESCRIPTION
	Prompts for HTTP/gRPC ports, saves settings, sets environment variables and launches Qdrant
	in the foreground.
.OUTPUTS
	[void]
#>
function Start-Qdrant {
	[CmdletBinding(SupportsShouldProcess = $true)]
	param()

	if (-not $PSCmdlet.ShouldProcess("Qdrant", "Install/Start")) {
		return
	}

	Install-QdrantIfMissing
	Install-QdrantWebUiIfMissing

	$settings = Get-QdrantSetting

	Write-Host ""
	Write-Host "Default Qdrant ports:" -ForegroundColor White
	Write-Host "  HTTP API : $($global:defaultHttpPort)" -ForegroundColor Cyan
	Write-Host "  gRPC     : $($global:defaultGrpcPort)" -ForegroundColor Cyan
	Write-Host ""
	Write-Host "If you are already running a container instance on the default ports, choose a different pair for Windows." -ForegroundColor DarkGray
	Write-Host "Example (Windows): HTTP 6335, gRPC 6336" -ForegroundColor DarkGray
	Write-Host ""

	$httpPort = Read-ValidatedPort -Prompt "Enter Qdrant HTTP port" -DefaultPort $settings.HttpPort
	$grpcPort = Read-ValidatedPort -Prompt "Enter Qdrant gRPC port" -DefaultPort $settings.GrpcPort

	if ($httpPort -eq $grpcPort) {
		Write-Warning "HTTP and gRPC ports cannot be the same."
		return
	}

	Set-QdrantSetting -HttpPort $httpPort -GrpcPort $grpcPort

	New-Directory -Path $global:storageRoot

	$env:QDRANT__SERVICE__HTTP_PORT = $httpPort.ToString()
	$env:QDRANT__SERVICE__GRPC_PORT = $grpcPort.ToString()
	$env:QDRANT__STORAGE__STORAGE_PATH = $global:storageRoot

	Write-Host ""
	Write-Host "Qdrant installation / data locations:" -ForegroundColor White
	Write-Host "  Install root : $global:installRoot" -ForegroundColor Cyan
	Write-Host "  Binary       : $global:qdrantExePath" -ForegroundColor Cyan
	Write-Host "  Web UI       : $global:staticRoot" -ForegroundColor Cyan
	Write-Host "  Storage      : $global:storageRoot" -ForegroundColor Cyan
	Write-Host "  Settings     : $global:settingsPath" -ForegroundColor Cyan

	Write-Host ""
	Write-Host "Starting Qdrant in the foreground (Ctrl+C to stop)..." -ForegroundColor Yellow
	Write-Host "HTTP API   : http://localhost:$httpPort" -ForegroundColor Green
	Write-Host "Dashboard  : http://localhost:$httpPort$($global:dashboardPath)" -ForegroundColor Green
	Write-Host "gRPC       : localhost:$grpcPort" -ForegroundColor Green

	Push-Location -LiteralPath $global:installRoot
	try {
		& $global:qdrantExePath
	}
	finally {
		Pop-Location
	}
}

#==============================================================================
# Function: Uninstall-Qdrant
#==============================================================================
<#
.SYNOPSIS
	Uninstalls Qdrant from ProgramData.
.DESCRIPTION
	Prompts the user to remove Qdrant binaries/config and optionally delete storage.
.OUTPUTS
	[void]
#>
function Uninstall-Qdrant {
	[CmdletBinding()]
	param()

	Write-Host ""
	Write-Host "Qdrant uninstall" -ForegroundColor White
	Write-Host "-------------------------------------------" -ForegroundColor Yellow
	Write-Host "Install root : $global:installRoot" -ForegroundColor Cyan
	Write-Host "Storage      : $global:storageRoot" -ForegroundColor Cyan
	Write-Host "Settings     : $global:settingsPath" -ForegroundColor Cyan

	if (-not (Test-Path -LiteralPath $global:installRoot)) {
		Write-Host "Nothing to uninstall (folder not found)." -ForegroundColor DarkGray
		return
	}

	$deleteData = Read-Host "Delete Qdrant data folder '$($global:storageRoot)'? (Y/N, default N)"
	$deleteAll = Read-Host "Delete ALL Qdrant files under '$($global:installRoot)'? (Y/N, default Y)"
	if ([string]::IsNullOrWhiteSpace($deleteAll)) { $deleteAll = "Y" }

	try {
		if ($deleteAll -eq "Y") {
			if ($deleteData -eq "Y") {
				Remove-Item -LiteralPath $global:installRoot -Recurse -Force -ErrorAction Stop
				Write-Host "Removed: $($global:installRoot)" -ForegroundColor Green
				return
			}

			# Delete everything except storage
			$items = Get-ChildItem -LiteralPath $global:installRoot -Force -ErrorAction SilentlyContinue
			foreach ($item in $items) {
				if ($item.FullName -ieq $global:storageRoot) { continue }
				Remove-Item -LiteralPath $item.FullName -Recurse -Force -ErrorAction SilentlyContinue
			}
			Write-Host "Removed Qdrant files (kept storage)." -ForegroundColor Green
		}
		elseif ($deleteData -eq "Y") {
			Remove-Item -LiteralPath $global:storageRoot -Recurse -Force -ErrorAction SilentlyContinue
			Write-Host "Removed storage folder." -ForegroundColor Green
		}
		else {
			Write-Host "No changes made." -ForegroundColor DarkGray
		}
	}
	catch {
		Write-Warning "Uninstall encountered an error: $_"
	}
}

#==============================================================================
# Function: Show-QdrantMenu
#==============================================================================
<#
.SYNOPSIS
	Shows the Qdrant Windows menu.
.DESCRIPTION
	Prints a small menu for installing/starting and uninstalling.
.OUTPUTS
	[void]
#>
function Show-QdrantMenu {
	[CmdletBinding()]
	param()

	Write-Host "===========================================" -ForegroundColor Yellow
	Write-Host "Qdrant (Windows)" -ForegroundColor White
	Write-Host "===========================================" -ForegroundColor Yellow
	Write-Host "1. Install / Start" -ForegroundColor Cyan
	Write-Host "2. Uninstall" -ForegroundColor Cyan
	Write-Host "0. Exit" -ForegroundColor Cyan
	Write-Host "-------------------------------------------" -ForegroundColor Yellow
}

#==============================================================================
# Main
#==============================================================================

New-Directory -Path $global:installRoot
New-Directory -Path $global:storageRoot
New-Directory -Path $global:downloadsRoot
New-Directory -Path $global:staticRoot

$choice = ""
do {
	Show-QdrantMenu
	$choice = Read-Host "Enter your choice"
	if ([string]::IsNullOrWhiteSpace($choice)) { $choice = "1" }

	switch ($choice) {
		"1" {
			Start-Qdrant
		}
		"2" {
			Uninstall-Qdrant
			Read-Host "`nPress Enter to continue"
		}
		"0" { return }
		default {
			Write-Warning "Invalid selection."
		}
	}
} while ($choice -ne "0")
