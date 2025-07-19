################################################################################
# Description  : Script to set up, back up, restore, uninstall, and update the
#                Open WebUI container using Docker/Podman support. The script
#                provides a container menu (with install, backup, restore, uninstall,
#                update, and exit options).
# Usage        : Run as Administrator if using Docker.
################################################################################

using namespace System
using namespace System.IO

# Dot-source the necessary helper function files.
. "$PSScriptRoot\Setup_Helper_CoreFunctions.ps1"
. "$PSScriptRoot\Setup_Helper_NetworkTests.ps1"
. "$PSScriptRoot\Setup_Helper_ContainerEngine.ps1"
. "$PSScriptRoot\Setup_Helper_BackupRestore.ps1"
. "$PSScriptRoot\Setup_Helper_ContainerManagement.ps1"
. "$PSScriptRoot\Setup_Helper_WSLFunctions.ps1" # Needed for Check-WSLStatus

# Ensure the script working directory is set.
Set-ScriptLocation

#############################################
# Pick Container Engine and Set Global Variables
#############################################
$global:imageName = "ghcr.io/open-webui/open-webui:main"
$global:containerName = "open-webui"
$global:volumeName = $global:containerName # Default: same as container name.

# --- Engine Selection ---
$global:containerEngine = Select-ContainerEngine # Renamed variable for clarity
# Exit if no engine was selected
if (-not $global:containerEngine) {
	Write-Warning "No container engine selected. Exiting script."
	exit 1
}
# Set engine-specific options (only admin check for Docker)
if ($global:containerEngine -eq "docker") {
	Test-AdminPrivilege
}
# Get the engine path after setting specific options
$global:enginePath = Get-EnginePath -EngineName $global:containerEngine # Renamed variable

#==============================================================================
# Function: Get-OpenWebUIContainerConfig
#==============================================================================
<#
.SYNOPSIS
	Gets the current Open WebUI container configuration by inspecting the running container.
.DESCRIPTION
	Inspects the container specified by the global $containerName variable using the selected engine.
	Extracts the image name, relevant environment variables (starting with OPENWEBUI_ or API_),
	volume mounts, and port mappings.
.OUTPUTS
	[PSCustomObject] Returns a custom object containing the extracted configuration details
					 (Image, EnvVars, VolumeMounts, Ports, Platform) or $null if the container
					 is not found or inspection fails.
.EXAMPLE
	$currentConfig = Get-OpenWebUIContainerConfig
	if ($currentConfig) { Write-Host "Current Image: $($currentConfig.Image)" }
.NOTES
	Uses 'engine inspect'. Includes error handling for parsing environment variables,
	volume mounts, and port mappings, providing defaults if parsing fails.
#>
function Get-OpenWebUIContainerConfig {
	$containerInfo = & $global:enginePath inspect $global:containerName 2>$null | ConvertFrom-Json # Use global vars
	if (-not $containerInfo) {
		Write-Host "Container '$($global:containerName)' not found." # Use global var
		return $null
	}

	# Extract environment variables
	$envVars = @()
	try {
		foreach ($env in $containerInfo.Config.Env) {
			# Only preserve specific environment variables if needed
			if ($env -match "^(OPENWEBUI_|API_)") {
				$envVars += $env
			}
		}
	}
	catch {
		Write-Warning "Could not parse existing environment variables: $_"
	}

	# Extract volume mounts
	$volumeMounts = @()
	try {
		foreach ($mount in $containerInfo.Mounts) {
			if ($mount.Type -eq "volume") {
				$volumeMounts += "$($mount.Name):$($mount.Destination)"
			}
		}
	}
	catch {
		Write-Warning "Could not parse existing volume mounts: $_"
		# Default volume mount if parsing fails
		$volumeMounts = @("$($global:volumeName):/app/backend/data") # Use global var
	}

	# Extract port mappings
	$ports = @()
	try {
		foreach ($portMapping in $containerInfo.NetworkSettings.Ports.PSObject.Properties) {
			foreach ($binding in $portMapping.Value) {
				$ports += "$($binding.HostPort):$($portMapping.Name.Split('/')[0])"
			}
		}
	}
	catch {
		Write-Warning "Could not parse existing port mappings: $_"
		# Default port mapping if parsing fails
		$ports = @("3000:8080")
	}

	# Return a custom object with the container information
	return [PSCustomObject]@{
		Image        = $containerInfo.Config.Image
		EnvVars      = $envVars
		VolumeMounts = $volumeMounts
		Ports        = $ports
		Platform     = "linux/amd64"  # Assuming this is the platform used
	}
}

#==============================================================================
# Function: Start-OpenWebUIContainer
#==============================================================================
<#
.SYNOPSIS
	Starts the Open WebUI container with specified or default configuration.
.DESCRIPTION
	Runs a new container using the selected engine. Builds the 'run' command arguments based on
	either a provided configuration object or default values (image, ports, volumes, name, restart policy).
	Includes engine-specific arguments like '--add-host' for Docker.
	After starting the container, waits 20 seconds and performs HTTP, TCP, and WebSocket connectivity tests.
	Attempts to create a firewall rule for port 3000. Supports -WhatIf.
.PARAMETER action
	A string describing the action being performed (e.g., "Running container", "Starting updated container"), used in status messages. Mandatory.
.PARAMETER successMessage
	The message to display upon successful startup and connectivity tests. Mandatory.
.PARAMETER config
	Optional. A PSCustomObject (typically from Get-OpenWebUIContainerConfig) containing specific
	configuration (Image, EnvVars, VolumeMounts, Ports, Platform) to use instead of defaults.
.OUTPUTS
	[bool] Returns $true if the container starts successfully and connectivity tests pass.
		   Returns $false if start fails, tests fail, or action is skipped due to -WhatIf.
.EXAMPLE
	Start-OpenWebUIContainer -action "Running initial container" -successMessage "Container started!"
.EXAMPLE
	$cfg = Get-OpenWebUIContainerConfig; Start-OpenWebUIContainer -action "Restarting container" -successMessage "Restarted!" -config $cfg
.NOTES
	Relies on Test-HTTPPort, Test-TCPPort, Test-WebSocketPort helper functions.
	Uses Write-Host for status messages. Firewall rule creation uses New-NetFirewallRule.
#>
function Start-OpenWebUIContainer {
	[CmdletBinding(SupportsShouldProcess = $true)]
	[OutputType([bool])]
	param (
		[string]$action,
		[string]$successMessage,
		[PSCustomObject]$config = $null
	)
	Write-Host "$action '$($global:containerName)'..." # Use global var

	# Build the run command with either provided config or defaults
	$runOptions = @("--platform")

	if ($config) {
		$runOptions += $config.Platform
	}
	else {
		$runOptions += "linux/amd64"
	}

	$runOptions += @("--detach")

	# Add port mappings
	if ($config -and $config.Ports) {
		foreach ($port in $config.Ports) {
			$runOptions += "--publish"
			$runOptions += $port
		}
	}
	else {
		$runOptions += "--publish"
		$runOptions += "3000:8080"
	}

	# Add volume mounts
	if ($config -and $config.VolumeMounts) {
		foreach ($volume in $config.VolumeMounts) {
			$runOptions += "--volume"
			$runOptions += $volume
		}
	}
	else {
		$runOptions += "--volume"
		$runOptions += "$($global:volumeName):/app/backend/data" # Use global var
	}

	# Add environment variables if provided
	if ($config -and $config.EnvVars) {
		foreach ($env in $config.EnvVars) {
			$runOptions += "--env"
			$runOptions += $env
		}
	}

	# Add host networking for Docker only
	if ($global:containerEngine -eq "docker") {
		# Use global var
		$runOptions += "--add-host"
		$runOptions += "host.docker.internal:host-gateway"
	}

	# Add restart policy
	$runOptions += "--restart"
	$runOptions += "always"

	# Add container name
	$runOptions += "--name"
	$runOptions += $global:containerName # Use global var

	# Add image name
	if ($config -and $config.Image) {
		$runOptions += $config.Image
	}
	else {
		$runOptions += $global:imageName # Use global var
	}

	# Command: run
	#   --platform: Specify platform (linux/amd64).
	#   --detach: Run container in background.
	#   --publish: Map host port 3000 to container port 8080.
	#   --volume: Mount the named volume for persistent data.
	#   --add-host: (Docker only) Map host.docker.internal to host gateway IP.
	#   --restart always: Always restart the container unless explicitly stopped.
	#   --name: Assign a name to the container.
	# Run the container with all options
	if ($PSCmdlet.ShouldProcess($global:containerName, "Run Container with Image '$($config.Image -or $global:imageName)'")) {
		# Use global vars
		& $global:enginePath run @runOptions # Use global var
	}
	else {
		Write-Warning "Skipping container run due to -WhatIf."
		return $false # Indicate failure if skipped
	}

	if ($LASTEXITCODE -ne 0) {
		Write-Error "Failed to run container."
		return $false
	}

	Write-Host "Waiting 20 seconds for container startup..."
	Start-Sleep -Seconds 20

	# Test connectivity
	Test-HTTPPort -Uri "http://localhost:3000" -serviceName "OpenWebUI"
	Test-TCPPort -ComputerName "localhost" -Port 3000 -serviceName "OpenWebUI"
	Test-WebSocketPort -Uri "ws://localhost:3000/api/v1/chat/completions" -serviceName "OpenWebUI WebSockets"

	# Create firewall rule if needed
	if ($PSCmdlet.ShouldProcess("Port 3000", "Create Firewall Rule 'Allow WebSockets'")) {
		try {
			New-NetFirewallRule -DisplayName "Allow WebSockets" -Direction Inbound -LocalPort 3000 -Protocol TCP -Action Allow -ErrorAction SilentlyContinue
		}
		catch {
			Write-Warning "Could not create firewall rule. You may need to manually allow port 3000."
		}
	}
	else {
		Write-Warning "Skipping firewall rule creation due to -WhatIf."
	}

	Write-Host $successMessage
	return $true
}

#==============================================================================
# Function: Install-OpenWebUIContainer
#==============================================================================
<#
.SYNOPSIS
	Installs and starts the Open WebUI container.
.DESCRIPTION
	Ensures the required volume exists using Confirm-ContainerVolume.
	Checks if the image exists locally. If not, attempts to restore from backup using
	Test-AndRestoreBackup, falling back to pulling the image using Invoke-PullImage.
	Removes any existing container with the same name.
	Starts the new container using Start-OpenWebUIContainer with default settings.
.EXAMPLE
	Install-OpenWebUIContainer
.NOTES
	Orchestrates volume creation, image acquisition, cleanup, and container start.
	Relies on Confirm-ContainerVolume, Test-AndRestoreBackup, Invoke-PullImage,
	Start-OpenWebUIContainer helper functions.
	Uses Write-Host for status messages.
#>
function Install-OpenWebUIContainer {
	# Ensure the volume exists
	if (-not (Confirm-ContainerResource -Engine $global:enginePath -ResourceType "volume" -ResourceName $global:volumeName)) {
		# Use global vars
		Write-Error "Failed to ensure volume '$($global:volumeName)' exists. Exiting..." # Use global var
		return
	}
	Write-Host "IMPORTANT: Using volume '$($global:volumeName)' - existing user data will be preserved." # Use global var

	# Check if image exists locally, restore from backup, or pull new
	$existingImage = & $global:enginePath images --filter "reference=$($global:imageName)" --format "{{.ID}}" # Use global vars
	if (-not $existingImage) {
		if (-not (Test-AndRestoreBackup -Engine $global:enginePath -ImageName $global:imageName)) {
			# Use global vars
			Write-Host "No backup restored. Pulling Open WebUI image '$($global:imageName)'..." # Use global var
			# Use shared pull function
			if (-not (Invoke-PullImage -Engine $global:enginePath -ImageName $global:imageName -PullOptions @("--platform", "linux/amd64"))) {
				# Use global vars
				Write-Error "Pull failed. Check internet connection or image URL."
				return
			}
		}
		else {
			Write-Host "Using restored backup image '$($global:imageName)'." # Use global var
		}
	}
	else {
		Write-Host "Using restored backup image '$($global:imageName)'." # Use global var
	}
	# Remove any existing container.
	$existingContainer = & $global:enginePath ps -a --filter "name=^$($global:containerName)$" --format "{{.ID}}" # Use global vars
	if ($existingContainer) {
		Write-Host "Removing existing container '$($global:containerName)'..." # Use global var
		# Remove container:
		# rm         Remove one or more containers.
		# --force    Force removal of a running container.
		& $global:enginePath rm --force $global:containerName # Use global vars
	}
	Start-OpenWebUIContainer -action "Running container" -successMessage "Open WebUI is now running and accessible at http://localhost:3000`nReminder: In Open WebUI settings, set the OpenAI API URL to 'http://host.docker.internal:9099' and API key to '0p3n-w3bu!' if integrating pipelines."
}

# Note: Uninstall-OpenWebUIContainer, Backup-OpenWebUIContainer, Restore-OpenWebUIContainer functions removed. Shared functions called directly from menu.

#==============================================================================
# Function: Update-OpenWebUIContainer
#==============================================================================
<#
.SYNOPSIS
	Updates the Open WebUI container to the latest image version while preserving data.
.DESCRIPTION
	Orchestrates the update process:
	1. Gets the current container configuration.
	2. Prompts the user to optionally back up the current container state.
	3. Calls the simplified generic Update-Container function (handles update check, removal, pull).
	4. If core update steps succeed, calls Start-OpenWebUIContainer to start the new container with preserved config.
	5. Offers to restore from backup if the start fails (and a backup was made).
.EXAMPLE
	Update-OpenWebUIContainer -WhatIf
.NOTES
	Relies on Get-OpenWebUIContainerConfig, Backup-OpenWebUIContainer, Update-Container,
	Start-OpenWebUIContainer, Restore-OpenWebUIContainer helper functions.
	User interaction handled via Read-Host for backup confirmation.
#>
function Update-OpenWebUIContainer {
	[CmdletBinding(SupportsShouldProcess = $true)] # Keep ShouldProcess for overall control
	param()

	# Check ShouldProcess before proceeding
	if (-not $PSCmdlet.ShouldProcess($global:containerName, "Update Container")) {
		# Use global var
		return
	}

	Write-Host "Initiating update for Open WebUI..."
	$backupMade = $false
	$config = Get-OpenWebUIContainerConfig # Get config before potential removal
	if (-not $config) {
		Write-Error "Cannot update: Open WebUI container not found or config could not be read."
		return # Exit the function if config cannot be read
	}

	# Check if container actually exists before prompting for backup
	$existingContainer = & $global:enginePath ps -a --filter "name=$($global:containerName)" --format "{{.ID}}" # Use global vars
	if ($existingContainer) {
		$createBackup = Read-Host "Create backup before updating? (Y/N, default is Y)"
		if ($createBackup -ne "N") {
			Write-Host "Saving '$($global:containerName)' Container Image..."
			Backup-ContainerImage -Engine $global:enginePath -ContainerName $global:containerName
			Write-Host "Exporting '$($global:volumeName)' Volume..."
			$null = Backup-ContainerVolume -EngineType $global:containerEngine -VolumeName $global:volumeName
			$backupMade = $true
		}
	}
	else {
		Write-Warning "Container '$($global:containerName)' not found. Skipping backup prompt." # Use global var
	}


	# Call simplified Update-Container (handles check, remove, pull)
	# Pass volume name for removal step
	if (Update-Container -Engine $global:enginePath -ContainerName $global:containerName -VolumeName $global:volumeName -ImageName $global:imageName) {
		# Use global vars
		Write-Host "Core update steps successful. Starting new container..."
		# Start the new container using the original config (image name is implicitly latest from pull)
		# Update the image name in the retrieved config before starting
		$config.Image = $global:imageName # Use global var
		if (-not (Start-OpenWebUIContainer -action "Starting updated container" -successMessage "Open WebUI container updated successfully!" -config $config)) {
			Write-Error "Failed to start updated Open WebUI container."
			if ($backupMade) {
				$restore = Read-Host "Would you like to restore from backup? (Y/N, default is Y)"
				if ($restore -ne "N") {
					Write-Host "Loading '$($global:containerName)' Container Image..."
					Restore-ContainerImage -Engine $global:enginePath -ContainerName $global:containerName
					Write-Host "Importing '$($global:volumeName)' Volume..."
					$null = Restore-ContainerVolume -EngineType $global:containerEngine -VolumeName $global:volumeName
				}
			}
		}
		# Success message is handled within Start-OpenWebUIContainer if successful
	}
	else {
		Write-Error "Update process failed during check, removal, or pull."
		if ($backupMade) {
			$restore = Read-Host "Would you like to restore from backup? (Y/N, default is Y)"
			if ($restore -ne "N") {
				Write-Host "Loading '$($global:containerName)' Container Image..."
				Restore-ContainerImage -Engine $global:enginePath -ContainerName $global:containerName
				Write-Host "Importing '$($global:volumeName)' Volume..."
				$null = Restore-ContainerVolume -EngineType $global:containerEngine -VolumeName $global:volumeName
			}
		}
	}
}

# Define Menu Title and Items
$menuTitle = "Open WebUI Container Menu"
$menuItems = [ordered]@{
	"1" = "Show Info & Test Connection"
	"2" = "Install container"
	"3" = "Uninstall container"
	"4" = "Save Image (App)"
	"5" = "Load Image (App)"
	"6" = "Export Volume (Data)"
	"7" = "Import Volume (Data)"
	"8" = "Update Image (App)"
	"9" = "Check for Updates"
	"0" = "Exit menu"
}

# Define Menu Actions
$menuActions = @{
	"1" = {
		Show-ContainerStatus -ContainerName $global:containerName ` # Use global var
		-ContainerEngine $global:containerEngine `
			-EnginePath $global:enginePath ` # Use global var
		-DisplayName "Open WebUI" `
			-TcpPort 3000 `
			-HttpPort 3000 `
			-WsPort 3000 `
			-WsPath "/api/v1/chat/completions"
	}
	"2" = { Install-OpenWebUIContainer }
	"3" = { Remove-ContainerAndVolume -Engine $global:enginePath -ContainerName $global:containerName -VolumeName $global:volumeName } # Call shared function directly, use global vars
	"4" = { Backup-ContainerImage -Engine $global:enginePath -ContainerName $global:containerName } # Call shared function directly, use global vars
	"5" = { Restore-ContainerImage -Engine $global:enginePath -ContainerName $global:containerName } # Call shared function directly, use global vars
	"6" = { Backup-ContainerVolume -EngineType $global:containerEngine -VolumeName $global:volumeName } # Call shared function directly
	"7" = {
		Restore-ContainerVolume -EngineType $global:containerEngine -VolumeName $global:volumeName
		Write-Host "Restarting container '$($global:containerName)' to apply imported volume data..."
		& $global:enginePath restart $global:containerName
	}
	"8" = { Update-OpenWebUIContainer } # Calls the dedicated update function
	"9" = { Test-ImageUpdateAvailable -Engine $global:enginePath -ImageName $global:imageName } # Use global vars
	# Note: "0" action is handled internally by Invoke-MenuLoop
}

# Invoke the Menu Loop
Invoke-MenuLoop -MenuTitle $menuTitle -MenuItems $menuItems -ActionMap $menuActions -ExitChoice "0"
