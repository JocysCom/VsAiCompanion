################################################################################
# Description  : Script to set up and run the n8n container using Docker/Podman.
#                Verifies volume presence, pulls the n8n image if necessary,
#                and runs the container with port and volume mappings.
#                Additionally, prompts for an external domain to set N8N_HOST
#                and WEBHOOK_URL if needed.
# Usage        : Run as Administrator if using Docker.
################################################################################

using namespace System
using namespace System.IO
using namespace System.Diagnostics.CodeAnalysis

# Set Information Preference (commented out as Write-Host is used now)
# $InformationPreference = 'Continue'

param(
    [Parameter(Mandatory=$false)]
    [string]$ManifestPath,

    [Parameter(Mandatory=$false)]
    [ValidateSet("local","prod")]
    [string]$Profile
)

# Dot-source the necessary helper function files.
. "$PSScriptRoot\Setup_Helper_CoreFunctions.ps1"
. "$PSScriptRoot\Setup_Helper_NetworkTests.ps1"
. "$PSScriptRoot\Setup_Helper_ContainerEngine.ps1"
. "$PSScriptRoot\Setup_Helper_BackupRestore.ps1"
. "$PSScriptRoot\Setup_Helper_ContainerManagement.ps1"

# Ensure the script working directory is set.
Set-ScriptLocation

#############################################
# Global Configuration
#############################################
$global:containerName = "n8n"

# Rule of thumb: heap ≈ 75–80 % of the VM / host RAM, container limit ≈ 110 % of that.
# Host RAM	--max-old-space-size  --memory / --memory-swap
#   2 GB     1024 MB                 1.5 GB                   Leaves ≥25 % for OS & DB.
#   4 GB     3072 MB	             4 GB                     Most users report this is enough for 100k-row workflows n8n Community
#   8 GB     6144 MB                 7 GB                     Lets you process ~500 k rows or large binary files n8n Community
#  16 GB	12288 MB                14 GB                     Heavy AI chains, large spreadsheets.
# Current configuration: 12288 MB heap, 14 GB container limit (configured in manifest.json)

# Load configuration from Aspire manifest with optional overlay merge (supports -ManifestPath and -Profile)
$paths = Get-AspireManifestPaths -ManifestPath $ManifestPath -Profile $Profile
$baseManifestPath = $paths.BaseManifestPath
$overlayManifestPath = $paths.OverlayManifestPath

if ($Profile -and -not [string]::IsNullOrWhiteSpace($Profile) -and [string]::IsNullOrWhiteSpace($overlayManifestPath)) {
	$manifestDir = Split-Path -Parent $baseManifestPath
	$manifestFile = Split-Path -Leaf $baseManifestPath
	$manifestNameNoExt = [IO.Path]::GetFileNameWithoutExtension($manifestFile)
	$manifestExt = [IO.Path]::GetExtension($manifestFile)
	$candidate = Join-Path $manifestDir ("{0}.{1}{2}" -f $manifestNameNoExt, $Profile, $manifestExt)
	Write-Warning "Profile manifest not found: $candidate. Using base manifest: $baseManifestPath"
}

$manifest = Get-AspireManifestWithOptionalOverlay -BaseManifestPath $baseManifestPath -OverlayManifestPath $overlayManifestPath -ResourceName "n8n"
$manifestConfig = $manifest.resources.'n8n'

# Create consolidated configuration object
$config = [PSCustomObject]@{
    imageName     = $manifestConfig.properties.image
    volumeName    = $manifestConfig.properties.volumes[0].name
    containerPort = $manifestConfig.properties.bindings[0].containerPort
    hostPort      = $manifestConfig.properties.bindings[0].hostPort
    dataPath      = $manifestConfig.properties.volumes[0].containerPath
    restartPolicy = $manifestConfig.properties.restart
    environment   = $manifestConfig.properties.environment
    memoryLimit   = $manifestConfig.properties.resources.memory
    memorySwap    = $manifestConfig.properties.resources.memorySwap
}

Write-Host "Configuration loaded from Aspire manifest:"
foreach ($property in $config.PSObject.Properties) {
	$name = $property.Name
	$value = $property.Value
	if ($null -eq $value -or ($value -is [string] -and [string]::IsNullOrEmpty($value))) {
		Write-Error "Configuration property '$name' is missing or empty in manifest."
		exit 1
	}
	Write-Host "  $($name): $value"
}

# --- Engine Selection ---
$global:containerEngine = Select-ContainerEngine
# Exit if no engine was selected
if (-not $global:containerEngine) {
	Write-Warning "No container engine selected. Exiting script."
	exit 1
}
# Set engine-specific options
if ($global:containerEngine -eq "docker") {
	Test-AdminPrivilege
	$global:pullOptions = @()
	# $global:imageName = "docker.n8n.io/n8nio/n8n:latest" # Original Docker-specific, now using common one
}
else { # Assumes podman
	$global:pullOptions = @("--tls-verify=false")
}
# Get the engine path after setting specific options
$global:enginePath = Get-EnginePath -EngineName $global:containerEngine

#==============================================================================
# Function: Get-n8nContainerConfig
#==============================================================================
<#
.SYNOPSIS
	Gets the current n8n container configuration, including environment variables, and prompts for external domain, TLS, and DNS settings.
.DESCRIPTION
	Inspects the n8n container using the selected engine. Extracts the image name and relevant
	environment variables (starting with N8N_ or WEBHOOK_). Ensures community packages and tool usage
	are enabled by adding the respective environment variables if missing. Prompts the user via Read-Host
	to enter an external domain and adds N8N_HOST and WEBHOOK_URL environment variables if provided.
	Also prompts for TLS certificate acceptance and DNS server preferences, with settings persistence.
.OUTPUTS
	[PSCustomObject] Returns a custom object containing the extracted/updated configuration details
					 (Image, EnvVars, AcceptSelfSigned, UseDNS) or $null if the container is not found or inspection fails.
.EXAMPLE
	$currentConfig = Get-n8nContainerConfig
	if ($currentConfig) { Write-Host "Current Image: $($currentConfig.Image)" }
.NOTES
	Uses 'engine inspect'. Modifies the extracted environment variables list.
	Requires user interaction via Read-Host for domain, TLS, and DNS configuration.
	Loads and saves settings using Load-ScriptSettings and Save-ScriptSettings functions.
#>
function Get-n8nContainerConfig {
	$envVars = @()
	$imageName = $config.imageName # Default image name from manifest

	# Load existing settings
	$existingSettings = Load-ScriptSettings

	# Initialize default values
	$defaultAcceptSelfSigned = $false
	$defaultUseDNS = $false
	$defaultExternalDomain = ""

	# Use existing settings if available
	if ($existingSettings) {
		$defaultAcceptSelfSigned = if ($null -ne $existingSettings.AcceptSelfSigned) { $existingSettings.AcceptSelfSigned } else { $false }
		$defaultUseDNS = if ($null -ne $existingSettings.UseDNS) { $existingSettings.UseDNS } else { $false }
		$defaultExternalDomain = if ($existingSettings.ExternalDomain) { $existingSettings.ExternalDomain } else { "" }
	}

	$containerInfo = & $global:enginePath inspect $global:containerName 2>$null | ConvertFrom-Json
	if ($containerInfo) {
		# Container exists, try to preserve existing vars and image name
		$imageName = $containerInfo.Config.Image
		try {
			$envList = @($containerInfo.Config.Env)
			foreach ($env in $envList) {
				# Preserve existing N8N_ or WEBHOOK_ vars, excluding the ones we always add from manifest
				if ($env -match "^(N8N_|WEBHOOK_)" `
						-and $env -notmatch "^N8N_COMMUNITY_PACKAGES_ENABLED=" `
						-and $env -notmatch "^N8N_COMMUNITY_PACKAGES_ALLOW_TOOL_USAGE=" `
						-and $env -notmatch "^N8N_RUNNERS_ENABLED=" `
						-and $env -notmatch "^N8N_ENFORCE_SETTINGS_FILE_PERMISSIONS=" `
						-and $env -notmatch "^N8N_TRUST_HOST_HEADERS=" `
						-and $env -notmatch "^N8N_LOG_LEVEL=" `
						-and $env -notmatch "^NODE_OPTIONS=") {
					$envVars += $env
				}
			}
		}
		catch {
			Write-Warning "Could not parse existing environment variables: $_"
		}
	}
	else {
		Write-Host "Container '$global:containerName' not found. Using default settings for environment."
	}

	# Add environment variables from manifest configuration
	foreach ($key in $config.environment.PSObject.Properties.Name) {
		$envVars += "$key=$($config.environment.$key)"
	}

	# Prompt user for external domain configuration
	$defaultPrompt = if ($defaultExternalDomain) { " [current: $defaultExternalDomain]" } else { "" }
	$externalDomain = Read-Host "Enter external domain for n8n container (e.g., n8n.example.com) or press Enter to skip$defaultPrompt"
	if ([string]::IsNullOrWhiteSpace($externalDomain) -and $defaultExternalDomain) {
		$externalDomain = $defaultExternalDomain
	}

	if (-not [string]::IsNullOrWhiteSpace($externalDomain)) {
		$envVars += "N8N_PUBLIC_API_BASE_URL=https://$externalDomain/"
		$envVars += "WEBHOOK_URL=https://$externalDomain/"
		$envVars += "N8N_CORS_ALLOW_ORIGIN=https://$externalDomain"
		#$envVars += "N8N_PROTOCOL=https"
		#$envVars += "N8N_HOST=$externalDomain"
		#$envVars += "N8N_PORT=443"
		#$envVars += "N8N_EDITOR_BASE_URL=https://$externalDomain"
	}

	# Prompt for self-signed certificate acceptance
	$defaultSelfSignedText = if ($defaultAcceptSelfSigned) { "Y" } else { "N" }
	$acceptSelfSignedInput = Read-Host "Accept self-signed certificates? (Y/N) [default: $defaultSelfSignedText]"
	if ([string]::IsNullOrWhiteSpace($acceptSelfSignedInput)) {
		$acceptSelfSigned = $defaultAcceptSelfSigned
	} else {
		$acceptSelfSigned = $acceptSelfSignedInput -eq "Y"
	}

	# Prompt for DNS server usage
	$defaultDNSText = if ($defaultUseDNS) { "Y" } else { "N" }
	$useDNSInput = Read-Host "Use Cloudflare (1.1.1.1) and Google (8.8.8.8) public DNS servers? (Y/N) [default: $defaultDNSText]"
	if ([string]::IsNullOrWhiteSpace($useDNSInput)) {
		$useDNS = $defaultUseDNS
	} else {
		$useDNS = $useDNSInput -eq "Y"
	}

	# Save settings for next time
	$newSettings = [PSCustomObject]@{
		AcceptSelfSigned = $acceptSelfSigned
		UseDNS = $useDNS
		ExternalDomain = $externalDomain
	}
	Save-ScriptSettings -Settings $newSettings

	# Return a custom object
	return [PSCustomObject]@{
		Image = $imageName
		EnvVars = $envVars
		AcceptSelfSigned = $acceptSelfSigned
		UseDNS = $useDNS
	}
}

#==============================================================================
# Function: Start-n8nContainer
#==============================================================================
<#
.SYNOPSIS
	Starts a new n8n container with specified configuration.
.DESCRIPTION
	Runs a new container using the selected engine with the specified image.
	Configures standard n8n settings: detached mode, name 'n8n', mounts 'n8n_data' volume
	to '/home/node/.n8n', and maps host port 5678 to container port 5678.
	Applies any additional environment variables provided via the EnvVars parameter.
	After starting, waits 30 seconds and performs TCP and HTTP connectivity tests.
	Supports -WhatIf.
.PARAMETER Image
	The n8n container image to use (e.g., 'docker.n8n.io/n8nio/n8n:latest'). Mandatory.
.PARAMETER EnvVars
	Optional array of environment variables strings (e.g., @("N8N_HOST=n8n.example.com")).
.OUTPUTS
	[bool] Returns $true if the container starts successfully and connectivity tests pass.
		   Returns $false if start fails, tests fail, or action is skipped due to -WhatIf.
.EXAMPLE
	Start-n8nContainer -Image "docker.io/n8nio/n8n:latest" -EnvVars @("N8N_ENCRYPTION_KEY=secret")
.NOTES
	Relies on Test-TCPPort and Test-HTTPPort helper functions.
#>
function Start-n8nContainer {
	[CmdletBinding(SupportsShouldProcess = $true)]
	[OutputType([bool])]
	param(
		[Parameter(Mandatory = $true)]
		[string]$Image,

		[Parameter(Mandatory = $false)]
		[array]$EnvVars = @(),

		[Parameter(Mandatory = $false)]
		[bool]$AcceptSelfSigned = $false,

		[Parameter(Mandatory = $false)]
		[bool]$UseDNS = $false
	)

	# Determine host IP for container networking (optional)
	$HostIpForContainer = $null
	$addHost = $false
	try {
		$hostIpRaw = & $global:enginePath machine ssh "grep nameserver /etc/resolv.conf | cut -d' ' -f2"
		if (-not [string]::IsNullOrWhiteSpace($hostIpRaw)) {
			$HostIpForContainer = $hostIpRaw.Trim()
			Write-Host "Host IP for container: $HostIpForContainer"
			$addHost = $true
		} else {
			Write-Warning "Could not determine host IP for container. Skipping custom --add-host mapping."
		}
	}
	catch {
		Write-Warning "Failed to query host IP using '$global:enginePath machine ssh'. Skipping custom --add-host mapping."
	}

	# Build the run command
	$runOptions = @(
		"--memory",      $config.memoryLimit,
		"--memory-swap", $config.memorySwap,
		"--detach", # Run container in background.
		"--publish", "$($config.hostPort):$($config.containerPort)", # Map host port to container port.
		"--volume", "$($config.volumeName):$($config.dataPath)", # Mount the named volume for persistent data.
		"--name", $global:containerName,         # Assign a name to the container.
		"--restart", $config.restartPolicy
		#"--cap-add", "NET_RAW",
		#"--cap-add", "NET_ADMIN",
	)
	if ($addHost) {
		$runOptions = @("--add-host", "host.local:$HostIpForContainer") + $runOptions
	}

	# Add self-signed certificate acceptance if requested
	if ($AcceptSelfSigned) {
		Write-Host "Adding NODE_TLS_REJECT_UNAUTHORIZED=0 for self-signed certificate acceptance"
		$runOptions += "--env"
		$runOptions += "NODE_TLS_REJECT_UNAUTHORIZED=0"
	}

	# Add DNS servers if requested
	if ($UseDNS) {
		Write-Host "Adding Cloudflare (1.1.1.1) and Google (8.8.8.8) DNS servers"
		$runOptions += "--dns"
		$runOptions += "1.1.1.1"
		$runOptions += "--dns"
		$runOptions += "8.8.8.8"
	}

	# Add all environment variables
	foreach ($env in $EnvVars) {
		$runOptions += "--env"
		$runOptions += $env
	}

	# Run the container
	if ($PSCmdlet.ShouldProcess($global:containerName, "Start Container with Image '$Image'")) {
		Write-Host "Starting n8n container with image: $Image"
		Write-Host "& $global:enginePath machine ssh sudo $global:containerEngine run $runOptions $Image"
		& $global:enginePath machine ssh sudo $global:containerEngine run $runOptions $Image

		if ($LASTEXITCODE -eq 0) {
			Write-Host "Waiting for container startup..."

			# Test connectivity (returns as soon as port is reachable)
			$tcpTest = Test-TCPPort -ComputerName "localhost" -Port $config.hostPort -serviceName $global:containerName
			$httpTest = Test-HTTPPort -Uri "http://localhost:$($config.hostPort)" -serviceName $global:containerName

			if ($tcpTest -and $httpTest) {
				Write-Host "n8n is now running and accessible at http://localhost:$($config.hostPort)"
				Write-Host "If accessing from another container, use 'http://host.docker.internal:$($config.hostPort)' as the URL."

				# Install additional packages automatically
				Write-Host "Installing additional packages required for n8n workflows..."
				$packageInstallResult = Install-n8nPackage
				if ($packageInstallResult) {
					Write-Host "Additional packages installed successfully."
				} else {
					Write-Warning "Package installation failed, but container is running. Packages can be installed manually if needed."
				}

				return $true
			}
			else {
				Write-Warning "n8n container started but connectivity tests failed. Please check the container logs."
				return $false
			}
		}
		else {
			Write-Error "Failed to start n8n container."
			return $false
		}
	}
	else {
		return $false # Action skipped due to -WhatIf
	}
}

#==============================================================================
# Function: Install-n8nContainer
#==============================================================================
<#
.SYNOPSIS
	Installs and starts the n8n container.
.DESCRIPTION
	Ensures the 'n8n_data' volume exists using Confirm-ContainerVolume.
	Checks if the n8n image exists locally; if not, attempts to restore from backup using
	Test-AndRestoreBackup, falling back to pulling the image using Invoke-PullImage.
	Removes any existing 'n8n' container using Remove-n8nContainer.
	Defines default environment variables (enabling community packages/tools).
	Prompts the user for an optional external domain to set N8N_HOST and WEBHOOK_URL.
	Starts the new container using Start-n8nContainer with the determined image and environment variables.
.EXAMPLE
	Install-n8nContainer
.NOTES
	Orchestrates volume creation, image acquisition, cleanup, environment configuration, and container start.
	Relies on Confirm-ContainerVolume, Test-AndRestoreBackup, Invoke-PullImage,
	Remove-n8nContainer, and Start-n8nContainer helper functions.
	Requires user interaction via Read-Host for domain configuration.
#>
function Install-n8nContainer {
	# Ensure the volume exists
	#if (-not (Confirm-ContainerVolume -Engine $global:enginePath -VolumeName $config.volumeName)) {
	#	Write-Error "Failed to ensure volume '$($config.volumeName)' exists. Exiting..."
	#	return
	#}
	Write-Host "IMPORTANT: Using volume '$($config.volumeName)' - existing user data will be preserved."

	# Check if the n8n image is already available, restore from backup, or pull new.
	$existingImage = & $global:enginePath images --filter "reference=$($config.imageName)" --format "{{.ID}}"
	if (-not $existingImage) {
		if (-not (Test-AndRestoreBackup -Engine $global:enginePath -ImageName $config.imageName)) {
			Write-Host "No backup restored. Pulling n8n image '$($config.imageName)'..."
			# Use shared pull function
			if (-not (Invoke-PullImage -Engine $global:enginePath -ImageName $config.imageName -PullOptions $global:pullOptions)) {
				Write-Error "Image pull failed. Exiting..."
				return
			}
		}
		else {
			Write-Host "Using restored backup image '$($config.imageName)'."
		}
	}
	else {
		Write-Host "n8n image already exists. Skipping pull."
	}

	# Remove any existing container using the shared function
	# Pass container name and volume name. It will prompt about volume removal.
	Remove-ContainerAndVolume -Engine $global:enginePath -ContainerName $global:containerName -VolumeName $config.volumeName # This function supports ShouldProcess

	# Get the configuration (which includes prompting for domain and setting defaults)
	$containerConfig = Get-n8nContainerConfig

	# Start the container using the config image name and the retrieved config
	Start-n8nContainer -Image $config.imageName -EnvVars $containerConfig.EnvVars -AcceptSelfSigned $containerConfig.AcceptSelfSigned -UseDNS $containerConfig.UseDNS # This function now supports ShouldProcess
}

# Note: Uninstall-n8nContainer function removed. Shared function called directly from menu.

#==============================================================================
# Function: Update-n8nContainer
#==============================================================================
<#
.SYNOPSIS
	Updates the n8n container to the latest image version while preserving data.
.DESCRIPTION
	Orchestrates the update process:
	1. Gets the current container configuration (including domain prompt).
	2. Prompts the user to optionally back up the current container image.
	3. Calls the simplified generic Update-Container function (handles update check, removal, pull).
	4. If core update steps succeed, calls Start-n8nContainer to start the new container with preserved config.
	5. Offers to restore from backup if the start fails (and a backup was made).
.EXAMPLE
	Update-n8nContainer -WhatIf
.NOTES
	Relies on Get-n8nContainerConfig, Backup-ContainerImage, Update-Container,
	Start-n8nContainer, Restore-ContainerImage helper functions.
	User interaction handled via Read-Host for backup confirmation.
#>
function Update-n8nContainer {
	[CmdletBinding(SupportsShouldProcess = $true)] # Keep ShouldProcess for overall control
	param()

	# Check ShouldProcess before proceeding
	if (-not $PSCmdlet.ShouldProcess($global:containerName, "Update Container")) {
		return
	}

	Write-Host "Initiating update for n8n..."
	$containerConfig = Get-n8nContainerConfig # Get config before potential removal (includes domain prompt)
	if (-not $containerConfig) {
		# Get-n8nContainerConfig handles the case where container doesn't exist,
		# but we still need to check if it returned null unexpectedly.
		Write-Error "Cannot update: Failed to get n8n configuration."
		return # Exit the function if config cannot be read
	}

	# Check if container actually exists before prompting for backup
	$existingContainer = & $global:enginePath ps -a --filter "name=$($global:containerName)" --format "{{.ID}}"
	if ($existingContainer) {
		$createBackup = Read-Host "Create backup before updating? (Y/N, default is Y)"
		if ($createBackup -ne "N") {
			Write-Host "Saving '$global:containerName' Container Image..."
			Backup-ContainerImage -Engine $global:enginePath -ImageName $config.imageName
			Write-Host "Exporting '$($config.volumeName)' Volume..."
			$null = Backup-ContainerVolume -EngineType $global:containerEngine -VolumeName $config.volumeName
		}
	}
	else {
		Write-Warning "Container '$($global:containerName)' not found. Skipping backup prompt."
	}

	# Call Update-Container (now returns the selected target image reference on success)
	# Pass volume name for removal step
	$targetImage = Update-Container -Engine $global:enginePath -ContainerName $global:containerName -VolumeName $config.volumeName -ImageName $config.imageName
	if (-not [string]::IsNullOrWhiteSpace([string]$targetImage)) {

		Update-AspireManifestResourceImage -ManifestPath $baseManifestPath -ResourceName "n8n" -Image ([string]$targetImage)

		$manifest = Get-AspireManifestWithOptionalOverlay -BaseManifestPath $baseManifestPath -OverlayManifestPath $overlayManifestPath -ResourceName "n8n"
		$manifestConfig = $manifest.resources.'n8n'

		$config.imageName = $manifestConfig.properties.image

		Write-Host "Core update steps successful. Starting new container..."
		if (-not (Start-n8nContainer -Image ([string]$targetImage) -EnvVars $containerConfig.EnvVars -AcceptSelfSigned $containerConfig.AcceptSelfSigned -UseDNS $containerConfig.UseDNS)) {
			Write-Error "Failed to start updated n8n container."
		}
	}
	else {
		# Update-Container already wrote a message explaining why it returned false (e.g., cancelled).
	}
}

#==============================================================================
# Function: Update-n8nUserData
#==============================================================================
<#
.SYNOPSIS
	Placeholder function for updating user data in the n8n container.
.DESCRIPTION
	Currently, this function only displays a message indicating that the functionality
	is not implemented. Supports -WhatIf.
.EXAMPLE
	Update-n8nUserData
.NOTES
	This function needs implementation if specific user data update procedures are required.
#>
function Update-n8nUserData {
	[CmdletBinding(SupportsShouldProcess = $true)]
	param()

	if ($PSCmdlet.ShouldProcess("n8n container user data", "Update")) {
		# Placeholder for future implementation
		Write-Host "Update User Data functionality is not implemented for n8n container."
	}
}

#==============================================================================
# Function: Reset-AdminPassword
#==============================================================================
<#
.SYNOPSIS
	Resets Admin Password and Restarts the n8n container.
#>
function Reset-AdminPassword {
	[CmdletBinding(SupportsShouldProcess = $true)]
	param()

	if ($PSCmdlet.ShouldProcess($global:containerName, "Reset Admin Password and Restart Container")) {
		& $global:enginePath exec -it $global:containerName $global:containerName user-management:reset
		& $global:enginePath restart $global:containerName
	}
}

#==============================================================================
# Function: Install-n8nApkToolsIfMissing
#==============================================================================
<#
.SYNOPSIS
	Bootstraps apk-tools inside hardened Alpine containers that ship without the apk binary.
.DESCRIPTION
	Some images are Alpine-based but intentionally exclude the apk executable. This function downloads
	a static apk client (apk.static) inside the container and uses it to install apk-tools.

	This modifies the running container filesystem. The change is lost when the container is removed.
.PARAMETER AlpineMajorMinor
	Alpine major.minor version used for repository URL (e.g., "3.22").
.PARAMETER ApkStaticVersion
	Version of apk.static to download from Alpine GitLab packages.
.PARAMETER ApkStaticArch
	Architecture segment for apk.static download (e.g., "x86_64").
.OUTPUTS
	[bool] $true if apk is available (already or successfully bootstrapped), otherwise $false.
#>
function Install-n8nApkToolsIfMissing {
	[CmdletBinding(SupportsShouldProcess = $true)]
	[OutputType([bool])]
	param(
		[Parameter(Mandatory = $false)]
		[string]$AlpineMajorMinor = "3.22",

		[Parameter(Mandatory = $false)]
		[string]$ApkStaticVersion = "v2.14.4",

		[Parameter(Mandatory = $false)]
		[string]$ApkStaticArch = "x86_64"
	)

	if (-not $PSCmdlet.ShouldProcess($global:containerName, "Bootstrap apk-tools using apk.static")) {
		return $false
	}

	& $global:enginePath machine ssh "sudo $global:containerEngine exec --user root $global:containerName sh -c 'command -v apk >/dev/null 2>&1'"
	if ($LASTEXITCODE -eq 0) {
		return $true
	}

	Write-Host "apk not found. Bootstrapping apk-tools using apk.static..."

	$apkUrl = "https://gitlab.alpinelinux.org/api/v4/projects/5/packages/generic/$ApkStaticVersion/$ApkStaticArch/apk.static"
	$repoUrl = "http://dl-cdn.alpinelinux.org/alpine/v$AlpineMajorMinor/main"

	& $global:enginePath machine ssh "sudo $global:containerEngine exec --user root $global:containerName sh -c 'wget $apkUrl'"
	if ($LASTEXITCODE -ne 0) {
		Write-Error "Failed to download apk.static."
		return $false
	}

	& $global:enginePath machine ssh "sudo $global:containerEngine exec --user root $global:containerName sh -c 'chmod +x apk.static'"
	if ($LASTEXITCODE -ne 0) {
		Write-Error "Failed to chmod apk.static."
		return $false
	}

	& $global:enginePath machine ssh "sudo $global:containerEngine exec --user root $global:containerName sh -c './apk.static -X $repoUrl -U --allow-untrusted --initdb add apk-tools'"
	if ($LASTEXITCODE -ne 0) {
		Write-Error "Failed to bootstrap apk-tools in container."
		return $false
	}

	& $global:enginePath machine ssh "sudo $global:containerEngine exec --user root $global:containerName sh -c 'command -v apk >/dev/null 2>&1'"
	if ($LASTEXITCODE -ne 0) {
		Write-Error "Failed to bootstrap apk-tools in container."
		return $false
	}

	& $global:enginePath machine ssh "sudo $global:containerEngine exec --user root $global:containerName sh -c 'apk update'"
	if ($LASTEXITCODE -ne 0) {
		Write-Error "Failed to update Alpine package index."
		return $false
	}

	Write-Host "apk-tools bootstrapped successfully."
	return $true
}

#==============================================================================
# Function: Install-n8nPackage
#==============================================================================
<#
.SYNOPSIS
	Installs additional OS packages required by common n8n workflows.
.DESCRIPTION
	Executes package installation commands inside the running n8n container.

	Supports Debian-based images (apt-get) and Alpine-based images (apk). For hardened Alpine images
	missing apk, attempts to bootstrap apk-tools via apk.static and then install packages.

	This modifies the running container filesystem. The change is lost when the container is removed.
.EXAMPLE
	Install-n8nPackage
.EXAMPLE
	Install-n8nPackage -WhatIf
.OUTPUTS
	[bool] Returns $true if packages were installed; $false if skipped or failed.
.NOTES
	Requires the n8n container to be running before execution.
	Uses global variables $global:enginePath, $global:containerEngine and $global:containerName.
#>
function Install-n8nPackage {
	[CmdletBinding(SupportsShouldProcess = $true)]
	[OutputType([bool])]
	param()

	if (-not $PSCmdlet.ShouldProcess($global:containerName, "Install additional packages (ffmpeg, zip)")) {
		Write-Warning "Package installation skipped due to -WhatIf."
		return $false
	}

	Write-Host "Installing additional packages in n8n container..."

	try {
		if (-not (Install-n8nApkToolsIfMissing)) {
			return $false
		}

		Write-Host "Installing ffmpeg and zip..."
		& $global:enginePath machine ssh "sudo $global:containerEngine exec --user root $global:containerName sh -c 'apk add --no-cache ffmpeg zip'"
		if ($LASTEXITCODE -ne 0) {
			Write-Error "Failed to install packages using apk."
			return $false
		}

		Write-Host "Additional packages (ffmpeg, zip) installed successfully."
		return $true
	}
	catch {
		Write-Error "Error during package installation: $_"
		return $false
	}
}

################################################################################
# Main Menu Loop using Generic Function
################################################################################

# Define Menu Title and Items
$menuTitle = "n8n Container & Data Management Menu" # Updated Title
$menuItems = [ordered]@{
	"1" = "Show Info & Test Connection"
	"2" = "Install container"
	"3" = "Uninstall container"
	"4" = "Save Image (App)"
	"5" = "Load Image (App)"
	"6" = "Update Image (App)"
	"7" = "Export Volume (Data)"
	"8" = "Import Volume (Data)"
	"9" = "Check for Updates"
	"R" = "Restart Container"
	"P" = "Reset Admin Password"
	"0" = "Exit menu"
}

# Define Menu Actions
$menuActions = @{
	"1" = {
		$hostPort = $config.hostPort
		Show-ContainerStatus -ContainerName $global:containerName `
			-ContainerEngine $global:containerEngine `
			-EnginePath $global:enginePath `
			-DisplayName $global:containerName `
			-TcpPort $hostPort `
			-HttpPort $hostPort
	}
	"2" = { Install-n8nContainer }
	"3" = {
		$volumeName = $config.volumeName
		Remove-ContainerAndVolume -Engine $global:enginePath -ContainerName $global:containerName -VolumeName $volumeName
	}
	"4" = {
		$imageName = $config.imageName
		Backup-ContainerImage -Engine $global:enginePath -ImageName $imageName
	}
	"5" = {
		$imageName = $config.imageName
		Test-AndRestoreBackup -Engine $global:enginePath -ImageName $imageName
	}
	"6" = { Update-n8nContainer }
	"7" = {
		$volumeName = $config.volumeName
		$null = Backup-ContainerVolume -EngineType $global:containerEngine -VolumeName $volumeName
	}
	"8" = {
		$volumeName = $config.volumeName
		$null = Restore-ContainerVolume -EngineType $global:containerEngine -VolumeName $volumeName
		& $global:enginePath restart $global:containerName
	}
	"9" = {
		# Prefer a real version check for n8n (1.x semver), without changing the manifest tag.
		# Requires the container to be running to query 'n8n --version'.
		$hasContainer = & $global:enginePath ps --filter "name=^$($global:containerName)$" --format "{{.ID}}"
		if (-not [string]::IsNullOrWhiteSpace($hasContainer)) {
			$updateByVer = Test-n8nUpdateAvailableByVersion -Engine $global:enginePath -ContainerName $global:containerName -Major 1
			if (-not $updateByVer) { return }
		}

		# Fallback / secondary: digest-based check against the configured image reference.
		$imageName = $config.imageName
		Test-ImageUpdateAvailable -Engine $global:enginePath -ImageName $imageName
	}
	"R" = { & $global:enginePath restart $global:containerName }
	"P" = { Reset-AdminPassword }
	# Note: "0" action is handled internally by Invoke-MenuLoop
}

# Invoke the Menu Loop
Invoke-MenuLoop -MenuTitle $menuTitle -MenuItems $menuItems -ActionMap $menuActions -ExitChoice "0"
