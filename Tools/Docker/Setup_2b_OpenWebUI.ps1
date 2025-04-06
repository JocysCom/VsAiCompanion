################################################################################
# File         : Setup_2b_OpenWebUI.ps1
# Description  : Script to set up, back up, restore, uninstall, and update the
#                Open WebUI container using Docker/Podman support. The script
#                provides a container menu (with install, backup, restore, uninstall,
#                update, and exit options).
# Usage        : Run as Administrator if using Docker.
################################################################################

using namespace System
using namespace System.IO

# Dot-source the necessary helper function files.
. "$PSScriptRoot\Setup_0_Core.ps1"
. "$PSScriptRoot\Setup_0_Network.ps1"
. "$PSScriptRoot\Setup_0_ContainerEngine.ps1"
. "$PSScriptRoot\Setup_0_BackupRestore.ps1"
. "$PSScriptRoot\Setup_0_ContainerMgmt.ps1"
. "$PSScriptRoot\Setup_0_WSL.ps1" # Needed for Check-WSLStatus

# Ensure the script working directory is set.
Set-ScriptLocation

#############################################
# Pick Container Engine and Set Global Variables
#############################################
$containerEngine = Select-ContainerEngine
if ($containerEngine -eq "docker") {
	Ensure-Elevated
	$enginePath = Get-DockerPath
}
else {
	$enginePath = Get-PodmanPath
}
$imageName = "ghcr.io/open-webui/open-webui:main"
$containerName = "open-webui"
$volumeName = "open-webui" # Assuming volume name matches container name

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
	$containerInfo = & $enginePath inspect $containerName 2>$null | ConvertFrom-Json
	if (-not $containerInfo) {
		Write-Host "Container '$containerName' not found."
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
		$volumeMounts = @("$($volumeName):/app/backend/data")
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
	Write-Host "$action '$containerName'..."

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
		$runOptions += "$($volumeName):/app/backend/data"
	}

	# Add environment variables if provided
	if ($config -and $config.EnvVars) {
		foreach ($env in $config.EnvVars) {
			$runOptions += "--env"
			$runOptions += $env
		}
	}

	# Add host networking for Docker only
	if ($containerEngine -eq "docker") {
		$runOptions += "--add-host"
		$runOptions += "host.docker.internal:host-gateway"
	}

	# Add restart policy
	$runOptions += "--restart"
	$runOptions += "always"

	# Add container name
	$runOptions += "--name"
	$runOptions += $containerName

	# Add image name
	if ($config -and $config.Image) {
		$runOptions += $config.Image
	}
	else {
		$runOptions += $imageName
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
	if ($PSCmdlet.ShouldProcess($containerName, "Run Container with Image '$($config.Image -or $imageName)'")) {
		& $enginePath run @runOptions
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
	if (-not (Confirm-ContainerVolume -Engine $enginePath -VolumeName $volumeName)) {
		Write-Error "Failed to ensure volume '$volumeName' exists. Exiting..."
		return
	}
	Write-Host "IMPORTANT: Using volume '$volumeName' - existing user data will be preserved."

	# Check if image exists locally, restore from backup, or pull new
	$existingImage = & $enginePath images --filter "reference=$imageName" --format "{{.ID}}"
	if (-not $existingImage) {
		if (-not (Test-AndRestoreBackup -Engine $enginePath -ImageName $imageName)) {
			Write-Host "No backup restored. Pulling Open WebUI image '$imageName'..."
			# Use shared pull function
			if (-not (Invoke-PullImage -Engine $enginePath -ImageName $imageName -PullOptions @("--platform", "linux/amd64"))) {
				Write-Error "Pull failed. Check internet connection or image URL."
				return
			}
		}
		else {
			Write-Host "Using restored backup image '$imageName'."
		}
	}
	else {
		Write-Host "Using restored backup image '$imageName'."
	}
	# Remove any existing container.
	$existingContainer = & $enginePath ps -a --filter "name=^$containerName$" --format "{{.ID}}"
	if ($existingContainer) {
		Write-Host "Removing existing container '$containerName'..."
		# Remove container:
		# rm         Remove one or more containers.
		# --force    Force removal of a running container.
		& $enginePath rm --force $containerName
	}
	Start-OpenWebUIContainer -action "Running container" -successMessage "Open WebUI is now running and accessible at http://localhost:3000`nReminder: In Open WebUI settings, set the OpenAI API URL to 'http://host.docker.internal:9099' and API key to '0p3n-w3bu!' if integrating pipelines."
}

#==============================================================================
# Function: Uninstall-OpenWebUIContainer
#==============================================================================
<#
.SYNOPSIS
	Uninstalls the Open WebUI container and optionally removes its data volume.
.DESCRIPTION
	Calls the Remove-ContainerAndVolume helper function, specifying 'open-webui' as both the
	container and volume name. This will stop/remove the container and prompt the user
	about removing the volume. Supports -WhatIf.
.EXAMPLE
	Uninstall-OpenWebUIContainer -Confirm:$false
.NOTES
	Relies on Remove-ContainerAndVolume helper function.
#>
function Uninstall-OpenWebUIContainer {
	Remove-ContainerAndVolume -Engine $enginePath -ContainerName $containerName -VolumeName $volumeName
}

#==============================================================================
# Function: Backup-OpenWebUIContainer
#==============================================================================
<#
.SYNOPSIS
	Backs up the state of the running Open WebUI container.
.DESCRIPTION
	Calls the Backup-ContainerState helper function, specifying 'open-webui' as the container name.
	This commits the container state to an image and saves it as a tar file.
.EXAMPLE
	Backup-OpenWebUIContainer
.NOTES
	Relies on Backup-ContainerState helper function.
#>
function Backup-OpenWebUIContainer {
	Backup-ContainerState -Engine $enginePath -ContainerName $containerName
}

#==============================================================================
# Function: Restore-OpenWebUIContainer
#==============================================================================
<#
.SYNOPSIS
	Restores the Open WebUI container image from a backup tar file.
.DESCRIPTION
	Calls the Restore-ContainerState helper function, specifying 'open-webui' as the container name.
	This loads the image from the backup tar file. Note: This only restores the image,
	it does not automatically start a container from it.
.EXAMPLE
	Restore-OpenWebUIContainer
.NOTES
	Relies on Restore-ContainerState helper function. Does not handle volume restore.
#>
function Restore-OpenWebUIContainer {
	Restore-ContainerState -Engine $enginePath -ContainerName $containerName
}

#==============================================================================
# Function: Update-OpenWebUIContainer
#==============================================================================
<#
.SYNOPSIS
	Updates the Open WebUI container to the latest image version using the generic update workflow.
.DESCRIPTION
	Defines a script block (`$runContainerFunction`) that encapsulates the logic to start the
	Open WebUI container using its current configuration (obtained via Get-OpenWebUIContainerConfig)
	but with the latest image name.
	Calls the generic Update-Container helper function, passing the specific details for the
	Open WebUI container (name, image name) and the defined script block via -RunFunction.
	Supports -WhatIf.
.EXAMPLE
	Update-OpenWebUIContainer -WhatIf
.NOTES
	Relies on Get-OpenWebUIContainerConfig, Start-OpenWebUIContainer, and the generic Update-Container function.
	The script block ensures that existing configuration (ports, volumes, env vars) is preserved during the update.
#>
function Update-OpenWebUIContainer {
	[CmdletBinding(SupportsShouldProcess = $true)]
	param()

	# Define a script block that knows how to run the container with the right options
	$runContainerFunction = {
		$config = Get-OpenWebUIContainerConfig
		if (-not $config) {
			throw "Failed to get container configuration"
		}

		# Update the image in the config to use the latest one
		$config.Image = $imageName

		$result = Start-OpenWebUIContainer -action "Starting updated container" -successMessage "OpenWebUI container has been successfully updated and is running at http://localhost:3000" -config $config
		if (-not $result) {
			throw "Failed to start updated container"
		}
	}

	# Use the shared update function (which supports ShouldProcess)
	Update-Container -Engine $enginePath -ContainerName $containerName -ImageName $imageName -RunFunction $runContainerFunction
}

#==============================================================================
# Function: Update-OpenWebUIUserData
#==============================================================================
<#
.SYNOPSIS
	Placeholder function for updating user data; currently displays information only.
.DESCRIPTION
	Displays informational messages explaining that direct user data update is not implemented
	but that data resides in the 'open-webui' volume and can be backed up or accessed via 'exec'.
	Supports -WhatIf.
.EXAMPLE
	Update-OpenWebUIUserData
.NOTES
	This function needs implementation if specific user data update procedures are required.
	Uses Write-Host for output.
#>
function Update-OpenWebUIUserData {
	[CmdletBinding(SupportsShouldProcess = $true)]
	param()

	if ($PSCmdlet.ShouldProcess("Open WebUI container", "Display user data information")) {
		# Provide some helpful information
		Write-Host "Update User Data functionality is not implemented for OpenWebUI container."
		Write-Host "User data is stored in the 'open-webui' volume at '/app/backend/data' inside the container."
		Write-Host "To back up user data, you can use the 'Backup Live container' option."
		Write-Host "To modify user data directly, you would need to access the container with:"
		Write-Host "  $enginePath exec -it $containerName /bin/bash"
	}
}

# Define Menu Title and Items
$menuTitle = "Open WebUI Container Menu"
$menuItems = [ordered]@{
	"1" = "Show Info & Test Connection"
	"2" = "Install container"
	"3" = "Uninstall container"
	"4" = "Backup Live container"
	"5" = "Restore Live container"
	"6" = "Update System"
	"7" = "Update User Data"
	"8" = "Check for Updates"
	"0" = "Exit"
}

# Define Menu Actions
$menuActions = @{
	"1" = {
		Show-ContainerStatus -ContainerName $containerName `
			-ContainerEngine $global:containerEngine `
			-EnginePath $enginePath `
			-DisplayName "Open WebUI" `
			-TcpPort 3000 `
			-HttpPort 3000 `
			-WsPort 3000 `
			-WsPath "/api/v1/chat/completions"
	}
	"2" = { Install-OpenWebUIContainer }
	"3" = { Uninstall-OpenWebUIContainer }
	"4" = { Backup-OpenWebUIContainer }
	"5" = { Restore-OpenWebUIContainer }
	"6" = { Update-OpenWebUIContainer }
	"7" = { Update-OpenWebUIUserData }
	"8" = { Test-ImageUpdateAvailable -Engine $enginePath -ImageName $imageName }
	# Note: "0" action is handled internally by Invoke-MenuLoop
}

# Invoke the Menu Loop
Invoke-MenuLoop -MenuTitle $menuTitle -MenuItems $menuItems -ActionMap $menuActions -ExitChoice "0"
