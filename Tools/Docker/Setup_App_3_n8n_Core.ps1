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

# Dot-source the necessary helper function files.
. "$PSScriptRoot\Setup_Helper_CoreFunctions.ps1"
. "$PSScriptRoot\Setup_Helper_NetworkTests.ps1"
. "$PSScriptRoot\Setup_Helper_ContainerEngine.ps1"
. "$PSScriptRoot\Setup_Helper_BackupRestore.ps1"
. "$PSScriptRoot\Setup_Helper_ContainerManagement.ps1"

# Ensure the script working directory is set.
Set-ScriptLocation

#############################################
# Global Variables
#############################################
# Note: PSAvoidGlobalVars warnings are ignored here as these are used across menu actions.
$global:imageName = "docker.io/n8nio/n8n:latest" # Use docker.io for both now
#$global:imageName = "docker.io/n8nio/n8n:1.86.1" # Use docker.io for both now - Pinned to specific version
$global:containerName = "n8n"
$global:volumeName = "n8n_data"
$global:containerPort = 5678

# Rule of thumb: heap ≈ 75–80 % of the VM / host RAM, container limit ≈ 110 % of that.
# Host RAM	--max-old-space-size  --memory / --memory-swap
#   2 GB     1024 MB                 1.5 GB                   Leaves ≥25 % for OS & DB.
#   4 GB     3072 MB	             4 GB                     Most users report this is enough for 100k-row workflows n8n Community
#   8 GB     6144 MB                 7 GB                     Lets you process ~500 k rows or large binary files n8n Community
#  16 GB	12288 MB                14 GB                     Heavy AI chains, large spreadsheets.
$global:n8nHeapMiB   = 12288
$global:n8nMemLimitG = 14

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
	Gets the current n8n container configuration, including environment variables, and prompts for external domain.
.DESCRIPTION
	Inspects the n8n container using the selected engine. Extracts the image name and relevant
	environment variables (starting with N8N_ or WEBHOOK_). Ensures community packages and tool usage
	are enabled by adding the respective environment variables if missing. Prompts the user via Read-Host
	to enter an external domain and adds N8N_HOST and WEBHOOK_URL environment variables if provided.
.OUTPUTS
	[PSCustomObject] Returns a custom object containing the extracted/updated configuration details
					 (Image, EnvVars) or $null if the container is not found or inspection fails.
.EXAMPLE
	$currentConfig = Get-n8nContainerConfig
	if ($currentConfig) { Write-Host "Current Image: $($currentConfig.Image)" }
.NOTES
	Uses 'engine inspect'. Modifies the extracted environment variables list.
	Requires user interaction via Read-Host for domain configuration.
#>
function Get-n8nContainerConfig {
	$envVars = @()
	$imageName = $global:imageName # Default image name

	$containerInfo = & $global:enginePath inspect $global:containerName 2>$null | ConvertFrom-Json
	if ($containerInfo) {
		# Container exists, try to preserve existing vars and image name
		$imageName = $containerInfo.Config.Image
		try {
			$envList = @($containerInfo.Config.Env)
			foreach ($env in $envList) {
				# Preserve existing N8N_ or WEBHOOK_ vars, excluding the ones we always add
				if ($env -match "^(N8N_|WEBHOOK_)" `
						-and $env -notmatch "^N8N_COMMUNITY_PACKAGES_ENABLED=" `
						-and $env -notmatch "^N8N_COMMUNITY_PACKAGES_ALLOW_TOOL_USAGE=") {
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

	# Always ensure community packages and tool usage are enabled
	$envVars += "N8N_COMMUNITY_PACKAGES_ENABLED=true"
	$envVars += "N8N_COMMUNITY_PACKAGES_ALLOW_TOOL_USAGE=true"
	$envVars += "N8N_RUNNERS_ENABLED=true"
	$envVars += "N8N_ENFORCE_SETTINGS_FILE_PERMISSIONS=true"
	$envVars += "N8N_TRUST_HOST_HEADERS=true"
	$envVars += "N8N_LOG_LEVEL=debug"
	$envVars += "NODE_OPTIONS=--max-old-space-size=$($global:n8nHeapMiB)"
	#$envVars += "N8N_PUSH_BACKEND=websocket"
	#$envVars += "N8N_PUSH_BACKEND=sse"
	#$envVars += "N8N_PROXY_HOPS=1"
	#$envVars += "N8N_EXPRESS_TRUST_PROXY=true"
	#$envVars += "N8N_PROTOCOL=https"
	#$envVars += "N8N_TRUST_PROXY=127.0.0.1/32,::1/128"

	# Prompt user for external domain configuration.
	$externalDomain = Read-Host "Enter external domain for n8n container (e.g., n8n.example.com) or press Enter to skip"
	if (-not [string]::IsNullOrWhiteSpace($externalDomain)) {
		$envVars += "N8N_PUBLIC_API_BASE_URL=https://$externalDomain/"
		$envVars += "WEBHOOK_URL=https://$externalDomain/"
		$envVars += "N8N_CORS_ALLOW_ORIGIN=https://$externalDomain"
		#$envVars += "N8N_PROTOCOL=https"
		#$envVars += "N8N_HOST=$externalDomain"
		#$envVars += "N8N_PORT=443"
		#$envVars += "N8N_EDITOR_BASE_URL=https://$externalDomain"
	}

	# Return a custom object
	return [PSCustomObject]@{
		Image   = $imageName
		EnvVars = $envVars
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
		[array]$EnvVars = @()
	)

	# Get the host's IP as seen by Podman/WSL2
	$HostIpForContainer = (podman machine ssh "grep nameserver /etc/resolv.conf | cut -d' ' -f2").Trim()
	if (-not [string]::IsNullOrWhiteSpace($HostIpForContainer)) {
		Write-Host "Host IP for container: $HostIpForContainer"
	} else {
		Write-Error "Could not determine host IP for container."
	}
	
	# Build the run command
	$runOptions = @(
		# Workaround: Accept self-signed certificates.
		#"--env", "NODE_TLS_REJECT_UNAUTHORIZED=0",
		#"--dns", "1.1.1.1", "--dns", "8.8.8.8",
		"--add-host", "host.local:$HostIpForContainer",
		"--env", "GENERIC_TIMEZONE=Europe/London",            # n8n’s internal TZ
		"--env", "TZ=Europe/London",                          # Linux tzdata TZ
		"--memory",      "$($global:n8nMemLimitG)g",
		"--memory-swap", "$($global:n8nMemLimitG)g",
		"--detach", # Run container in background.
		"--publish", "5678:5678", # Map host port 5678 to container port 5678.
		"--volume", "$($global:volumeName):/home/node/.n8n", # Mount the named volume for persistent data.
		"--name", $global:containerName         # Assign a name to the container.
		#"--cap-add", "NET_RAW",
		#"--cap-add", "NET_ADMIN",
	)

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
			Start-Sleep -Seconds 30

			# Test connectivity
			$tcpTest = Test-TCPPort -ComputerName "localhost" -Port $global:containerPort -serviceName $global:containerName
			$httpTest = Test-HTTPPort -Uri "http://localhost:5678" -serviceName $global:containerName

			if ($tcpTest -and $httpTest) {
				Write-Host "n8n is now running and accessible at http://localhost:5678"
				Write-Host "If accessing from another container, use 'http://host.docker.internal:5678' as the URL."
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
	#if (-not (Confirm-ContainerVolume -Engine $global:enginePath -VolumeName $global:volumeName)) {
	#	Write-Error "Failed to ensure volume '$global:volumeName' exists. Exiting..."
	#	return
	#}
	Write-Host "IMPORTANT: Using volume '$global:volumeName' - existing user data will be preserved."

	# Check if the n8n image is already available, restore from backup, or pull new.
	$existingImage = & $global:enginePath images --filter "reference=$($global:imageName)" --format "{{.ID}}"
	if (-not $existingImage) {
		if (-not (Test-AndRestoreBackup -Engine $global:enginePath -ImageName $global:imageName)) {
			Write-Host "No backup restored. Pulling n8n image '$global:imageName'..."
			# Use shared pull function
			if (-not (Invoke-PullImage -Engine $global:enginePath -ImageName $global:imageName -PullOptions $global:pullOptions)) {
				Write-Error "Image pull failed. Exiting..."
				return
			}
		}
		else {
			Write-Host "Using restored backup image '$global:imageName'."
		}
	}
	else {
		Write-Host "n8n image already exists. Skipping pull."
	}

	# Remove any existing container using the shared function
	# Pass container name and volume name. It will prompt about volume removal.
	Remove-ContainerAndVolume -Engine $global:enginePath -ContainerName $global:containerName -VolumeName $global:volumeName # This function supports ShouldProcess

	# Get the configuration (which includes prompting for domain and setting defaults)
	$config = Get-n8nContainerConfig

	# Start the container using the global image name and the retrieved config
	Start-n8nContainer -Image $global:imageName -EnvVars $config.EnvVars # This function now supports ShouldProcess
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
	$config = Get-n8nContainerConfig # Get config before potential removal (includes domain prompt)
	if (-not $config) {
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
			Write-Host "Saving '$containerName' Container Image..."
			Backup-ContainerImage -Engine $global:enginePath -ContainerName $global:containerName
			Write-Host "Exporting '$($global:volumeName)' Volume..."
			$null = Backup-ContainerVolume -EngineType $global:containerEngine -VolumeName $global:volumeName
		}
	}
	else {
		Write-Warning "Container '$($global:containerName)' not found. Skipping backup prompt."
	}

	# Call simplified Update-Container (handles check, remove, pull)
	# Pass volume name for removal step
	if (Update-Container -Engine $global:enginePath -ContainerName $global:containerName -VolumeName $global:volumeName -ImageName $global:imageName) {
		Write-Host "Core update steps successful. Starting new container..."
		# Start the new container using the config retrieved earlier
		if (-not (Start-n8nContainer -Image $global:imageName -EnvVars $config.EnvVars)) {
			Write-Error "Failed to start updated n8n container."
		}
		# Success message is handled within Start-n8nContainer if successful
	}
	else {
		# Update-Container already wrote a message explaining why it returned false (e.g., no update available).
		# No need to write an error here.
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
# Function: Restart-n8nContainer
#==============================================================================
<#
.SYNOPSIS
	Restarts the n8n container, applying current configuration (e.g., enabling community packages).
.DESCRIPTION
	Retrieves the current container configuration using Get-n8nContainerConfig (which also ensures
	community packages are enabled in the config object).
	Removes the existing container using Remove-n8nContainer.
	Starts a new container using Start-n8nContainer with the retrieved configuration (image and env vars).
	This effectively restarts the container with potentially updated environment variables from Get-n8nContainerConfig.
.EXAMPLE
	Restart-n8nContainer
.NOTES
	Relies on Get-n8nContainerConfig, Remove-n8nContainer, Start-n8nContainer helper functions.
	Supports -WhatIf via helper functions.
#>
function Restart-n8nContainer {
	[CmdletBinding(SupportsShouldProcess = $true)]
	param()

	# Get current container configuration (includes domain prompt and defaults)
	$config = Get-n8nContainerConfig

	# Remove the existing container using the shared function
	if (-not (Remove-ContainerAndVolume -Engine $global:enginePath -ContainerName $global:containerName -VolumeName $global:volumeName)) {
		Write-Error "Failed to remove existing container or action skipped. Restart aborted."
		return
	}

	# Start a new container with the same image and configuration
	if (Start-n8nContainer -Image $config.Image -EnvVars $config.EnvVars) {
		# This function now supports ShouldProcess
		Write-Host "n8n container restarted successfully with community packages enabled!"
	}
	else {
		Write-Error "Failed to restart container or action skipped."
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

################################################################################
# Main Menu Loop using Generic Function
################################################################################

# Define Menu Title and Items
$menuTitle = "n8n Container & Data Management Menu" # Updated Title
$menuItems = [ordered]@{
	"1" = "Show Info & Test Connection"
	"2" = "Install container"
	"3" = "Uninstall container (preserves user data volume)"
	"4" = "Save Image (App)"
	"5" = "Load Image (App)"
	"6" = "Export Volume (Data)"
	"7" = "Import Volume (Data)"
	"8" = "Update System (App)"
	"A" = "Restart"
	"B" = "Reset Admin Password"
	"0" = "Exit menu"
}

# Define Menu Actions
$menuActions = @{
	"1" = {
		Show-ContainerStatus -ContainerName $global:containerName `
			-ContainerEngine $global:containerEngine `
			-EnginePath $global:enginePath `
			-DisplayName $global:containerName `
			-TcpPort $global:containerPort `
			-HttpPort $global:containerPort
	}
	"2" = { Install-n8nContainer }
	"3" = { Remove-ContainerAndVolume -Engine $global:enginePath -ContainerName $global:containerName -VolumeName $global:volumeName } # Call shared function directly
	"4" = {
		Write-Host "Saving '$containerName' Container Image..."
		Backup-ContainerImage -Engine $global:enginePath -ContainerName $global:containerName
	}
	"5" = {
		Write-Host "Loading '$($global:containerName)' Container Image..."
		Restore-ContainerImage -Engine $global:enginePath -ContainerName $global:containerName
	}
	"6" = {
		Write-Host "Exporting '$($global:volumeName)' Volume..."
		$null = Backup-ContainerVolume -EngineType $global:containerEngine -VolumeName $global:volumeName
	}
	"7" = {
		Write-Host "Importing '$($global:volumeName)' Volume ..."
		$null = Restore-ContainerVolume -EngineType $global:containerEngine -VolumeName $global:volumeName
		Restart-n8nContainer
	}
	"8" = { Update-n8nContainer }
	"A" = { Restart-n8nContainer }
	"B" = { Reset-AdminPassword }
	# Note: "0" action is handled internally by Invoke-MenuLoop
}

# Invoke the Menu Loop
Invoke-MenuLoop -MenuTitle $menuTitle -MenuItems $menuItems -ActionMap $menuActions -ExitChoice "0"
