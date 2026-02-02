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
$global:githubLatestReleaseWebUrl = "https://github.com/$($global:githubRepoOwner)/$($global:githubRepoName)/releases/latest"
$global:githubUserAgent = "VsAiCompanion-Qdrant-Windows-Setup"

# Optional: GitHub token to avoid API rate limiting.
# If missing, the script will prompt for it (for this run only) when needed.
$global:githubTokenEnvVarName = "GITHUB_TOKEN"

$global:qdrantWebUiRepoOwner = "qdrant"
$global:qdrantWebUiRepoName = "qdrant-web-ui"
$global:qdrantWebUiLatestReleaseApiUrl = "https://api.github.com/repos/$($global:qdrantWebUiRepoOwner)/$($global:qdrantWebUiRepoName)/releases/latest"
$global:qdrantWebUiZipAssetName = "dist-qdrant.zip"
$global:qdrantWebUiVersionFile = Join-Path $global:installRoot ".qdrant-web-ui-version"

$global:dashboardPath = "/dashboard"

$global:qdrantServiceTaskName = "VsAiCompanion-Qdrant"
$global:qdrantServiceWrapperPath = Join-Path $global:installRoot "service-wrapper.ps1"
$global:qdrantServiceLogPath = Join-Path $global:installRoot "service.log"
$global:qdrantServicePidPath = Join-Path $global:installRoot "service.pid"
$global:qdrantServicePidPath = Join-Path $global:installRoot "service.pid"

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
# Function: Read-GitHubTokenForThisRun
#==============================================================================
<#
.SYNOPSIS
	Prompts for a GitHub token and stores it in the process environment.
.DESCRIPTION
	Asks the user for a token only when needed. The token is stored in $env:GITHUB_TOKEN
	for this PowerShell process only (not persisted).
.OUTPUTS
	[void]
#>
function Read-GitHubTokenForThisRun {
	[CmdletBinding()]
	param()

	$token = [Environment]::GetEnvironmentVariable($global:githubTokenEnvVarName, "Process")
	if ([string]::IsNullOrWhiteSpace($token)) {
		$token = [Environment]::GetEnvironmentVariable($global:githubTokenEnvVarName, "User")
	}

	if (-not [string]::IsNullOrWhiteSpace($token)) {
		return
	}

	Write-Host ""
	Write-Host "GitHub API rate limits can block downloads." -ForegroundColor Yellow
	Write-Host "Provide a GitHub Personal Access Token (classic or fine-grained) with read access to public repos." -ForegroundColor DarkGray
	Write-Host "Leave blank to continue unauthenticated (may fail if rate limited)." -ForegroundColor DarkGray
	$token = Read-Host "Enter GitHub token (will be used for this run only)"

	if (-not [string]::IsNullOrWhiteSpace($token)) {
		[Environment]::SetEnvironmentVariable($global:githubTokenEnvVarName, $token, "Process")
	}
}

#==============================================================================
# Function: Get-GitHubApiHeaders
#==============================================================================
<#
.SYNOPSIS
	Builds GitHub API headers.
.DESCRIPTION
	Creates a User-Agent header and, if present, adds an Authorization header using
	the token from $env:GITHUB_TOKEN to increase rate limits.
.OUTPUTS
	[hashtable]
#>
function Get-GitHubApiHeaders {
	[CmdletBinding()]
	[OutputType([hashtable])]
	param()

	$headers = @{
		"User-Agent" = $global:githubUserAgent
		"Accept"     = "application/vnd.github+json"
	}

	$token = [Environment]::GetEnvironmentVariable($global:githubTokenEnvVarName, "Process")
	if ([string]::IsNullOrWhiteSpace($token)) {
		$token = [Environment]::GetEnvironmentVariable($global:githubTokenEnvVarName, "User")
	}

	if (-not [string]::IsNullOrWhiteSpace($token)) {
		$headers["Authorization"] = "Bearer $token"
	}

	return $headers
}

#==============================================================================
# Function: Get-QdrantLatestRelease
#==============================================================================
<#
.SYNOPSIS
	Fetches the latest Qdrant release metadata from GitHub.
.DESCRIPTION
	Uses the GitHub REST API to retrieve the latest release and its assets.
	Falls back to parsing the GitHub Releases page when the API rate limit is exceeded.
.OUTPUTS
	[object]
#>
function Get-QdrantLatestRelease {
	[CmdletBinding()]
	[OutputType([object])]
	param()

	try {
		$headers = Get-GitHubApiHeaders
		return Invoke-RestMethod -Uri $global:githubLatestReleaseApiUrl -Headers $headers -Method Get -ErrorAction Stop
	}
	catch {
		$errText = "$_"
		if ($errText -match "API rate limit exceeded") {
			Read-GitHubTokenForThisRun
			try {
				$headers = Get-GitHubApiHeaders
				return Invoke-RestMethod -Uri $global:githubLatestReleaseApiUrl -Headers $headers -Method Get -ErrorAction Stop
			}
			catch {
				$errText2 = "$_"
				if ($errText2 -match "API rate limit exceeded") {
					Write-Warning "GitHub API rate limit exceeded. Falling back to parsing $($global:githubLatestReleaseWebUrl)."
					$tag = Get-GitHubLatestReleaseTagFromWeb -LatestReleaseUrl $global:githubLatestReleaseWebUrl
					return Get-GitHubReleaseByTag -Owner $global:githubRepoOwner -Repo $global:githubRepoName -Tag $tag
				}
				throw
			}
		}

		throw "Failed to query GitHub latest release API: $_"
	}
}

#==============================================================================
# Function: Get-GitHubLatestReleaseTagFromWeb
#==============================================================================
<#
.SYNOPSIS
	Gets the latest GitHub release tag from the /releases/latest redirect.
.DESCRIPTION
	Uses an HTTP request to the GitHub web UI endpoint (not the API), then extracts
	the tag from the final redirected URL.
.PARAMETER LatestReleaseUrl
	The GitHub releases/latest URL.
.OUTPUTS
	[string]
#>
function Get-GitHubLatestReleaseTagFromWeb {
	[CmdletBinding()]
	[OutputType([string])]
	param(
		[Parameter(Mandatory = $true)]
		[string]$LatestReleaseUrl
	)

	try {
		$headers = @{ "User-Agent" = $global:githubUserAgent }
		$response = Invoke-WebRequest -Uri $LatestReleaseUrl -Headers $headers -MaximumRedirection 0 -ErrorAction SilentlyContinue

		# If MaximumRedirection=0, GitHub should respond with 302 and a Location header.
		$location = $null
		if ($response -and $response.Headers) {
			$location = $response.Headers["Location"]
		}

		# Some environments follow redirects anyway; fall back to the final ResponseUri.
		if ([string]::IsNullOrWhiteSpace($location) -and $response -and $response.BaseResponse -and $response.BaseResponse.ResponseUri) {
			$location = [string]$response.BaseResponse.ResponseUri.AbsoluteUri
		}

		if ([string]::IsNullOrWhiteSpace($location)) {
			throw "No redirect location returned."
		}

		if ($location -match "/tag/(?<tag>[^/?#]+)") {
			return $Matches["tag"]
		}

		throw "Could not extract tag from redirect URL '$location'."
	}
	catch {
		throw "Failed to determine latest release tag from '$LatestReleaseUrl': $_"
	}
}

#==============================================================================
# Function: Get-GitHubReleaseByTag
#==============================================================================
<#
.SYNOPSIS
	Fetches a GitHub release by tag.
.DESCRIPTION
	Calls the GitHub REST API endpoint /releases/tags/{tag}.
.PARAMETER Owner
	GitHub organization/user.
.PARAMETER Repo
	Repository name.
.PARAMETER Tag
	Release tag.
.OUTPUTS
	[object]
#>
function Get-GitHubReleaseByTag {
	[CmdletBinding()]
	[OutputType([object])]
	param(
		[Parameter(Mandatory = $true)]
		[string]$Owner,

		[Parameter(Mandatory = $true)]
		[string]$Repo,

		[Parameter(Mandatory = $true)]
		[string]$Tag
	)

	$uri = "https://api.github.com/repos/$Owner/$Repo/releases/tags/$Tag"
	try {
		$headers = Get-GitHubApiHeaders
		return Invoke-RestMethod -Uri $uri -Headers $headers -Method Get -ErrorAction Stop
	}
	catch {
		throw "Failed to query GitHub release by tag API ($uri): $_"
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
	Falls back to HTML scraping of the GitHub Releases page when the API rate limit is exceeded.
.OUTPUTS
	[object]
#>
function Get-QdrantWebUiLatestRelease {
	[CmdletBinding()]
	[OutputType([object])]
	param()

	try {
		$headers = Get-GitHubApiHeaders
		return Invoke-RestMethod -Uri $global:qdrantWebUiLatestReleaseApiUrl -Headers $headers -Method Get -ErrorAction Stop
	}
	catch {
		$errText = "$_"
		if ($errText -match "API rate limit exceeded") {
			Read-GitHubTokenForThisRun
			try {
				$headers = Get-GitHubApiHeaders
				return Invoke-RestMethod -Uri $global:qdrantWebUiLatestReleaseApiUrl -Headers $headers -Method Get -ErrorAction Stop
			}
			catch {
				$errText2 = "$_"
				if ($errText2 -match "API rate limit exceeded") {
					$webLatest = "https://github.com/$($global:qdrantWebUiRepoOwner)/$($global:qdrantWebUiRepoName)/releases/latest"
					Write-Warning "GitHub API rate limit exceeded. Falling back to parsing $webLatest."
					$tag = Get-GitHubLatestReleaseTagFromWeb -LatestReleaseUrl $webLatest
					return Get-GitHubReleaseByTag -Owner $global:qdrantWebUiRepoOwner -Repo $global:qdrantWebUiRepoName -Tag $tag
				}
				throw
			}
		}

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
# Function: Install-Qdrant
#==============================================================================
<#
.SYNOPSIS
	Installs Qdrant prerequisites and persists configuration.
.DESCRIPTION
	Prompts for HTTP and gRPC ports (saved under ProgramData), ensures required folders exist,
	and downloads Qdrant + Web UI if missing.
.OUTPUTS
	[void]
#>
function Install-Qdrant {
	[CmdletBinding()]
	param()

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
}

#==============================================================================
# Function: Start-QdrantConsole
#==============================================================================
<#
.SYNOPSIS
	Starts Qdrant in the current console (foreground).
.DESCRIPTION
	Loads saved settings, sets environment variables and launches Qdrant in the foreground.
.OUTPUTS
	[void]
#>
function Start-QdrantConsole {
	[CmdletBinding(SupportsShouldProcess = $true)]
	param()

	$settings = Get-QdrantSetting
	$httpPort = [int]$settings.HttpPort
	$grpcPort = [int]$settings.GrpcPort

	if (-not $PSCmdlet.ShouldProcess("Qdrant", "Start Console")) {
		return
	}

	if (-not (Test-Path -LiteralPath $global:qdrantExePath)) {
		Write-Warning "Qdrant is not installed. Run 'Install' first."
		return
	}

	$env:QDRANT__SERVICE__HTTP_PORT = $httpPort.ToString()
	$env:QDRANT__SERVICE__GRPC_PORT = $grpcPort.ToString()
	$env:QDRANT__STORAGE__STORAGE_PATH = $global:storageRoot

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
# Function: Write-QdrantServiceWrapper
#==============================================================================
<#
.SYNOPSIS
	Writes the Scheduled Task wrapper script for Qdrant.
.DESCRIPTION
	Creates a PowerShell script under ProgramData which sets environment variables from saved settings
	and starts Qdrant with stdout/stderr appended to a log file.
.OUTPUTS
	[void]
#>
function Write-QdrantServiceWrapper {
	[CmdletBinding(SupportsShouldProcess = $true)]
	param()

	New-Directory -Path $global:installRoot

	$settings = Get-QdrantSetting
	$httpPort = [int]$settings.HttpPort
	$grpcPort = [int]$settings.GrpcPort

	$wrapper = @"
`$ErrorActionPreference = 'Stop'

`$installRoot = '$($global:installRoot)'
`$exePath = '$($global:qdrantExePath)'
`$storageRoot = '$($global:storageRoot)'
`$logPath = '$($global:qdrantServiceLogPath)'
`$pidPath = '$($global:qdrantServicePidPath)'

`$env:QDRANT__SERVICE__HTTP_PORT = '$httpPort'
`$env:QDRANT__SERVICE__GRPC_PORT = '$grpcPort'
`$env:QDRANT__STORAGE__STORAGE_PATH = `"`$storageRoot`"

New-Item -ItemType Directory -Path `"`$installRoot`" -Force | Out-Null
New-Item -ItemType Directory -Path `"`$storageRoot`" -Force | Out-Null

`"[`$(Get-Date -Format o)] Starting Qdrant...`" | Out-File -FilePath `"`$logPath`" -Append -Encoding UTF8

if (Test-Path -LiteralPath `"`$pidPath`" ) {
	try {
		`$oldPid = [int](Get-Content -LiteralPath `"`$pidPath`" -ErrorAction SilentlyContinue | Select-Object -First 1)
		if (`$oldPid -gt 0) {
			`$pOld = Get-Process -Id `$oldPid -ErrorAction SilentlyContinue
			if (`$pOld) {
				`"[`$(Get-Date -Format o)] Existing PID found (`$oldPid). Stopping it...`" | Out-File -FilePath `"`$logPath`" -Append -Encoding UTF8
				Stop-Process -Id `$oldPid -Force -ErrorAction SilentlyContinue
			}
		}
	}
	catch { }
}

Push-Location -LiteralPath `"`$installRoot`"
try {
	`$stdoutPath = `"`$logPath`"
	`$stderrPath = `"`$logPath`" + ".err"
	`$p = Start-Process -FilePath `"`$exePath`" -WindowStyle Hidden -RedirectStandardOutput `"`$stdoutPath`" -RedirectStandardError `"`$stderrPath`" -PassThru
	Set-Content -LiteralPath `"`$pidPath`" -Value `$p.Id -Encoding UTF8
	`"[`$(Get-Date -Format o)] Qdrant started. PID=`$(`$p.Id)`" | Out-File -FilePath `"`$logPath`" -Append -Encoding UTF8
}
finally {
	Pop-Location
}
"@

	if ($PSCmdlet.ShouldProcess($global:qdrantServiceWrapperPath, "Write Qdrant service wrapper script")) {
		Set-Content -LiteralPath $global:qdrantServiceWrapperPath -Value $wrapper -Encoding UTF8
	}
}

#==============================================================================
# Function: Install-QdrantService
#==============================================================================
<#
.SYNOPSIS
	Installs a Scheduled Task as a per-user "service" for Qdrant.
.DESCRIPTION
	Creates/updates a Scheduled Task that runs a wrapper PowerShell script as the current user.
	Optionally enables auto-start at user logon.
.PARAMETER AutoStart
	If set, registers the task with an AtLogOn trigger; otherwise registers without trigger.
.OUTPUTS
	[void]
#>
function Install-QdrantService {
	[CmdletBinding(SupportsShouldProcess = $true)]
	param(
		[Parameter(Mandatory = $false)]
		[switch]$AutoStart
	)

	Install-Qdrant
	Write-QdrantServiceWrapper

	if (-not (Test-Path -LiteralPath $global:qdrantServiceWrapperPath)) {
		throw "Wrapper script was not created: $($global:qdrantServiceWrapperPath)"
	}

	$taskName = $global:qdrantServiceTaskName
	$action = New-ScheduledTaskAction -Execute "PowerShell.exe" -Argument "-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File `"$($global:qdrantServiceWrapperPath)`""
	$principal = New-ScheduledTaskPrincipal -UserId "$env:UserName" -LogonType Interactive -RunLevel Limited
	$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable -MultipleInstances IgnoreNew

	$trigger = $null
	if ($AutoStart) {
		$trigger = New-ScheduledTaskTrigger -AtLogOn -User $env:UserName
	}

	$task = if ($trigger) { New-ScheduledTask -Action $action -Trigger $trigger -Principal $principal -Settings $settings } else { New-ScheduledTask -Action $action -Principal $principal -Settings $settings }

	if ($PSCmdlet.ShouldProcess($taskName, "Register Scheduled Task")) {
		Register-ScheduledTask -TaskName $taskName -InputObject $task -Force | Out-Null
	}
}

#==============================================================================
# Function: Uninstall-QdrantService
#==============================================================================
<#
.SYNOPSIS
	Uninstalls the Scheduled Task "service" for Qdrant.
.DESCRIPTION
	Stops and unregisters the task, and optionally removes the wrapper script.
.OUTPUTS
	[void]
#>
function Uninstall-QdrantService {
	[CmdletBinding(SupportsShouldProcess = $true)]
	param()

	$taskName = $global:qdrantServiceTaskName

	try { Stop-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue | Out-Null } catch { Write-Verbose "Failed to stop scheduled task '$taskName' (may not exist or already stopped)." }

	if ($PSCmdlet.ShouldProcess($taskName, "Unregister Scheduled Task")) {
		Unregister-ScheduledTask -TaskName $taskName -Confirm:$false -ErrorAction SilentlyContinue | Out-Null
	}

	if (Test-Path -LiteralPath $global:qdrantServiceWrapperPath) {
		if ($PSCmdlet.ShouldProcess($global:qdrantServiceWrapperPath, "Remove wrapper script")) {
			Remove-Item -LiteralPath $global:qdrantServiceWrapperPath -Force -ErrorAction SilentlyContinue
		}
	}
}

#==============================================================================
# Function: Start-QdrantService
#==============================================================================
<#
.SYNOPSIS
	Starts the Qdrant Scheduled Task "service".
.DESCRIPTION
	Starts the scheduled task by name.
.OUTPUTS
	[void]
#>
function Start-QdrantService {
	[CmdletBinding(SupportsShouldProcess = $true)]
	param()

	$taskName = $global:qdrantServiceTaskName
	if ($PSCmdlet.ShouldProcess($taskName, "Start Scheduled Task")) {
		Start-ScheduledTask -TaskName $taskName
	}
}

#==============================================================================
# Function: Stop-QdrantService
#==============================================================================
<#
.SYNOPSIS
	Stops the Qdrant Scheduled Task "service".
.DESCRIPTION
	Stops the scheduled task by name.
.OUTPUTS
	[void]
#>
function Stop-QdrantService {
	[CmdletBinding(SupportsShouldProcess = $true)]
	param()

	$taskName = $global:qdrantServiceTaskName
	if ($PSCmdlet.ShouldProcess($taskName, "Stop Scheduled Task")) {
		Stop-ScheduledTask -TaskName $taskName
	}

	$killed = $false

	if (Test-Path -LiteralPath $global:qdrantServicePidPath) {
		try {
			$pidRaw = Get-Content -LiteralPath $global:qdrantServicePidPath -ErrorAction SilentlyContinue | Select-Object -First 1
			[int]$procId = 0
			if ([int]::TryParse([string]$pidRaw, [ref]$procId) -and $procId -gt 0) {
				$proc = Get-Process -Id $procId -ErrorAction SilentlyContinue
				if ($proc -and $proc.Path -and $proc.Path -like "*qdrant.exe") {
					if ($PSCmdlet.ShouldProcess("PID $procId", "Stop Qdrant process")) {
						Stop-Process -Id $procId -Force -ErrorAction SilentlyContinue
						$killed = $true
					}
				}
				elseif ($proc) {
					Write-Warning "PID file points to '$($proc.ProcessName)' (PID $procId), not qdrant. Ignoring PID file."
				}
			}
		}
		catch {
			Write-Verbose "Failed to stop Qdrant process by PID."
		}
	}

	if (-not $killed) {
		$procs = @(Get-Process qdrant -ErrorAction SilentlyContinue)
		if ($procs.Count -gt 0) {
			foreach ($p in $procs) {
				if ($p.Path -and $p.Path -like "*\\ProgramData\\Qdrant\\qdrant.exe") {
					if ($PSCmdlet.ShouldProcess("PID $($p.Id)", "Stop Qdrant process")) {
						Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
					}
				}
			}
		}
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
	Provides options for install, start (console), start service, stop service, and uninstall.
.OUTPUTS
	[void]
#>
function Show-QdrantMenu {
	[CmdletBinding()]
	param()

	$taskName = $global:qdrantServiceTaskName
	$state = "Not Installed"
	$lastResult = ""
	try {
		$t = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
		if ($t) {
			$info = Get-ScheduledTaskInfo -TaskName $taskName -ErrorAction SilentlyContinue
			if ($info) {
				if ($info.State) { $state = [string]$info.State } else { $state = "Installed" }
				if ($null -ne $info.LastTaskResult) { $lastResult = [string]$info.LastTaskResult }
			}
			else {
				$state = "Installed"
			}
		}
	}
	catch {
		$state = "Unknown"
	}

	Write-Host "===========================================" -ForegroundColor Yellow
	Write-Host "Qdrant (Windows)" -ForegroundColor White
	Write-Host "===========================================" -ForegroundColor Yellow
	Write-Host "Service Task: $taskName" -ForegroundColor DarkGray
	Write-Host "Service State: $state" -ForegroundColor DarkGray
	if (-not [string]::IsNullOrWhiteSpace($lastResult)) {
		Write-Host "Last Task Result: $lastResult" -ForegroundColor DarkGray
	}
	Write-Host "-------------------------------------------" -ForegroundColor Yellow
	Write-Host "1. Install App" -ForegroundColor Cyan
	Write-Host "2. Start Console" -ForegroundColor Cyan
	Write-Host "3. Install Service (current user)" -ForegroundColor Cyan
	Write-Host "4. Install Service (current user, autostart)" -ForegroundColor Cyan
	Write-Host "5. Start Service" -ForegroundColor Cyan
	Write-Host "6. Stop Service" -ForegroundColor Cyan
	Write-Host "7. Uninstall Service" -ForegroundColor Cyan
	Write-Host "8. Uninstall App" -ForegroundColor Cyan
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
	if ([string]::IsNullOrWhiteSpace($choice)) { continue }

	switch ($choice) {
		"1" {
			Install-Qdrant
		}
		"2" {
			Start-QdrantConsole
		}
		"3" {
			Install-QdrantService
		}
		"4" {
			Install-QdrantService -AutoStart
		}
		"5" {
			if (-not (Test-Path -LiteralPath $global:settingsPath)) {
				Write-Warning "Qdrant service requires saved ports. Run 'Install App' first."
				break
			}
			Start-QdrantService
		}
		"6" {
			Stop-QdrantService
		}
		"7" {
			Uninstall-QdrantService
		}
		"8" {
			Uninstall-Qdrant
		}
		"0" { return }
		default {
			Write-Warning "Invalid selection."
		}
	}
} while ($choice -ne "0")
