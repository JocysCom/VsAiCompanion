################################################################################
# File         : Setup_1c_Portainer.ps1
# Description  : Script to set up and run Portainer container using Docker/Podman.
#                Provides installation, uninstallation, backup, restore, and
#                update functionality for Portainer - a lightweight web UI for
#                managing Docker and Podman environments.
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

# Ensure the script working directory is set.
Set-ScriptLocation

#############################################
# Global Variables
#############################################
# Note: PSAvoidGlobalVars warnings are ignored here as these are used across menu actions.
$global:containerEngine = Select-ContainerEngine
if ($global:containerEngine -eq "docker") {
	Test-AdminPrivilege
	$global:enginePath = Get-DockerPath
	$global:pullOptions = @()  # No extra options needed for Docker.
	$global:imageName = "portainer/portainer-ce:latest"
}
else {
	$global:enginePath = Get-PodmanPath
	$global:pullOptions = @("--tls-verify=false")
	$global:imageName = "portainer/portainer-ce:latest"
}

# Default ports for Portainer
$global:httpPort = 9000
$global:httpsPort = 9443

#==============================================================================
# Function: Get-PortainerContainerConfig
#==============================================================================
<#
.SYNOPSIS
	Gets the current Portainer container configuration.
.DESCRIPTION
	Retrieves container information including the image name and Portainer-specific
	environment variables by inspecting the 'portainer' container using the selected engine.
.OUTPUTS
	[PSCustomObject] Returns a custom object with 'Image' and 'EnvVars' properties,
					 or $null if the container is not found or inspection fails.
.EXAMPLE
	$config = Get-PortainerContainerConfig
	if ($config) { Write-Host "Image: $($config.Image)" }
.NOTES
	Uses 'engine inspect'. Filters environment variables to keep only those starting with 'PORTAINER_'.
#>
function Get-PortainerContainerConfig {
	$containerInfo = & $global:enginePath inspect portainer 2>$null | ConvertFrom-Json
	if (-not $containerInfo) {
		Write-Host "Container 'portainer' not found."
		return $null
	}

	# Extract environment variables
	$envVars = @()
	try {
		# Handle potential single vs multiple env vars
		$envList = @($containerInfo.Config.Env)
		foreach ($env in $envList) {
			# Only preserve Portainer-specific environment variables
			if ($env -match "^(PORTAINER_)") {
				$envVars += $env
			}
		}
	}
	catch {
		Write-Warning "Could not parse existing environment variables: $_"
	}

	# Return a custom object with the container information
	return [PSCustomObject]@{
		Image   = $containerInfo.Config.Image
		EnvVars = $envVars
	}
}

#==============================================================================
# Function: Start-PortainerContainer
#==============================================================================
<#
.SYNOPSIS
	Starts a new Portainer container with specified or default configuration.
.DESCRIPTION
	Runs a new container using the selected engine with the specified image.
	Configures standard Portainer settings: detached mode, name 'portainer',
	mounts 'portainer_data' volume, maps HTTP/HTTPS ports, and mounts the
	appropriate engine socket (/var/run/docker.sock or /run/podman/podman.sock).
	Applies any additional environment variables provided.
	After starting, waits 10 seconds and performs TCP and HTTP connectivity tests.
	Supports -WhatIf.
.PARAMETER Image
	The Portainer container image to use (e.g., 'portainer/portainer-ce:latest'). Mandatory.
.PARAMETER EnvVars
	Optional array of environment variables strings (e.g., @("MY_VAR=value")).
.PARAMETER HttpPort
	Optional. The host port to map to container port 9000. Defaults to global $httpPort (9000).
.PARAMETER HttpsPort
	Optional. The host port to map to container port 9443. Defaults to global $httpsPort (9443).
.OUTPUTS
	[bool] Returns $true if the container starts successfully and connectivity tests pass.
		   Returns $false if start fails, tests fail, or action is skipped due to -WhatIf.
.EXAMPLE
	Start-PortainerContainer -Image "portainer/portainer-ce:latest"
.EXAMPLE
	Start-PortainerContainer -Image "portainer/portainer-ce:latest" -HttpPort 8080 -HttpsPort 8443
.NOTES
	Relies on Test-TCPPort and Test-HTTPPort helper functions.
	Uses Write-Host for status messages.
#>
function Start-PortainerContainer {
	[CmdletBinding(SupportsShouldProcess = $true)]
	param(
		[Parameter(Mandatory = $true)]
		[string]$Image,

		[Parameter(Mandatory = $false)]
		[array]$EnvVars = @(),

		[Parameter(Mandatory = $false)]
		[int]$HttpPort = $global:httpPort,

		[Parameter(Mandatory = $false)]
		[int]$HttpsPort = $global:httpsPort
	)

	# Build the run command
	$runOptions = @(
		"--detach", # Run container in background.
		"--publish", "${HttpPort}:9000", # Map host HTTP port to container port 9000.
		"--publish", "${HttpsPort}:9443", # Map host HTTPS port to container port 9443.
		"--volume", "portainer_data:/data"      # Mount the named volume for persistent data.
	)

	# Add socket volume based on container engine
	if ($global:containerEngine -eq "docker") {
		# Mount the Docker socket for container management.
		$runOptions += "--volume"
		$runOptions += "/var/run/docker.sock:/var/run/docker.sock"
	}
	else {
		# Mount the Podman socket for container management (read-only).
		$runOptions += "--volume"
		$runOptions += "/run/podman/podman.sock:/var/run/docker.sock:ro"
	}

	# Assign a name to the container.
	$runOptions += "--name"
	$runOptions += "portainer"

	# Add all environment variables (if any).
	foreach ($env in $EnvVars) {
		$runOptions += "--env"
		$runOptions += $env
	}

	# Run the container
	if ($PSCmdlet.ShouldProcess("portainer", "Start Container with Image '$Image'")) {
		Write-Host "Starting Portainer container with image: $Image"
		& $global:enginePath run $runOptions $Image

		if ($LASTEXITCODE -eq 0) {
			Write-Host "Waiting for container startup..."
			Start-Sleep -Seconds 10

			# Test connectivity
			$tcpTest = Test-TCPPort -ComputerName "localhost" -Port $HttpPort -serviceName "Portainer"
			$httpTest = Test-HTTPPort -Uri "http://localhost:$HttpPort" -serviceName "Portainer"

			if ($tcpTest -and $httpTest) {
				Write-Host "Portainer is now running and accessible at:"
				Write-Host "  HTTP:  http://localhost:$HttpPort"
				Write-Host "  HTTPS: https://localhost:$HttpsPort"
				Write-Host "On first connection, you'll need to create an admin account."
				return $true
			}
			else {
				Write-Warning "Portainer container started but connectivity tests failed. Please check the container logs."
				return $false
			}
		}
		else {
			Write-Error "Failed to start Portainer container."
			return $false
		}
	}
	else {
		return $false # Action skipped due to -WhatIf
	}
}

#==============================================================================
# Function: Install-PortainerContainer
#==============================================================================
<#
.SYNOPSIS
	Installs and starts the Portainer container.
.DESCRIPTION
	Ensures the 'portainer_data' volume exists using Confirm-ContainerVolume.
	Checks if the Portainer image exists locally; if not, attempts to restore from backup using
	Test-AndRestoreBackup, falling back to pulling the image using Invoke-PullImage.
	Removes any existing 'portainer' container using Remove-PortainerContainer.
	Starts the new container using Start-PortainerContainer.
.EXAMPLE
	Install-PortainerContainer
.NOTES
	Orchestrates volume creation, image acquisition, cleanup, and container start.
	Relies on Confirm-ContainerVolume, Test-AndRestoreBackup, Invoke-PullImage,
	Remove-PortainerContainer, and Start-PortainerContainer helper functions.
	Uses Write-Host for status messages.
#>
function Install-PortainerContainer {
	# Ensure the volume exists
	if (-not (Confirm-ContainerVolume -Engine $global:enginePath -VolumeName "portainer_data")) {
		Write-Error "Failed to ensure volume 'portainer_data' exists. Exiting..."
		return
	}
	Write-Host "IMPORTANT: Using volume 'portainer_data' - existing user data will be preserved."

	# Check if the Portainer image is already available.
	$existingImage = & $global:enginePath images --filter "reference=$($global:imageName)" --format "{{.ID}}"
	if (-not $existingImage) {
		if (-not (Test-AndRestoreBackup -Engine $global:enginePath -ImageName $global:imageName)) {
			Write-Host "No backup restored. Pulling Portainer image '$global:imageName'..."
			# Use the shared Invoke-PullImage function
			if (-not (Invoke-PullImage -Engine $global:enginePath -ImageName $global:imageName -PullOptions $global:pullOptions)) {
				Write-Error "Image pull failed. Exiting..."
				return
			}
		}
	}

	# Remove existing container before starting new one
	Remove-PortainerContainer # This now supports ShouldProcess

	# Start the container
	Start-PortainerContainer -Image $global:imageName # This now supports ShouldProcess
}

# Note: Uninstall-PortainerContainer, Backup-PortainerContainer, Restore-PortainerContainer functions removed. Shared functions called directly from menu.

#==============================================================================
# Function: Update-PortainerContainer
#==============================================================================
<#
.SYNOPSIS
	Updates the Portainer container to the latest image version while preserving data.
.DESCRIPTION
	Orchestrates the update process:
	1. Gets the current container configuration.
	2. Prompts the user to optionally back up the current container state.
	3. Calls the simplified generic Update-Container function (handles update check, removal, pull).
	4. If core update steps succeed, calls Start-PortainerContainer to start the new container with preserved config.
	5. Offers to restore from backup if the start fails (and a backup was made).
.EXAMPLE
	Update-PortainerContainer -WhatIf
.NOTES
	Relies on Get-PortainerContainerConfig, Backup-PortainerContainer, Update-Container,
	Start-PortainerContainer, Restore-PortainerContainer helper functions.
	User interaction handled via Read-Host for backup confirmation.
#>
function Update-PortainerContainer {
	[CmdletBinding(SupportsShouldProcess = $true)] # Keep ShouldProcess for overall control
	param()

	# Check ShouldProcess before proceeding
	if (-not $PSCmdlet.ShouldProcess("portainer", "Update Container")) {
		return
	}

	Write-Host "Initiating update for Portainer..."
	$backupMade = $false
	$config = Get-PortainerContainerConfig # Get config before potential removal
	if (-not $config) {
		Write-Error "Cannot update: Portainer container not found or config could not be read."
		return # Exit the function if config cannot be read
	}

	# Check if container actually exists before prompting for backup
	$existingContainer = & $global:enginePath ps -a --filter "name=portainer" --format "{{.ID}}"
	if ($existingContainer) {
		$createBackup = Read-Host "Create backup before updating? (Y/N, default is Y)"
		if ($createBackup -ne "N") {
			if (Backup-PortainerContainer) { # Calls Backup-ContainerState
				$backupMade = $true
			}
		}
	}
	else {
		Write-Warning "Container 'portainer' not found. Skipping backup prompt."
	}

	# Call simplified Update-Container (handles check, remove, pull)
	# Pass volume name for removal step
	if (Update-Container -Engine $global:enginePath -ContainerName "portainer" -VolumeName "portainer_data" -ImageName $global:imageName) {
		Write-Host "Core update steps successful. Starting new container..."
		# Start the new container using the original config (image name is implicitly latest from pull)
		if (-not (Start-PortainerContainer -Image $global:imageName -EnvVars $config.EnvVars)) {
			Write-Error "Failed to start updated Portainer container."
			if ($backupMade) {
				$restore = Read-Host "Would you like to restore from backup? (Y/N, default is Y)"
				if ($restore -ne "N") {
					Restore-PortainerContainer # Calls Restore-ContainerState
				}
			}
		}
		# Success message is handled within Start-PortainerContainer if successful
	}
	else {
		Write-Error "Update process failed during check, removal, or pull."
		if ($backupMade) {
			$restore = Read-Host "Would you like to restore from backup? (Y/N, default is Y)"
			if ($restore -ne "N") {
				Restore-PortainerContainer # Calls Restore-ContainerState
			}
		}
	}
}

################################################################################
# Main Menu Loop using Generic Function
################################################################################

# Define Menu Title and Items
$menuTitle = "Portainer Container Menu"
$menuItems = [ordered]@{
	"1" = "Show Info & Test Connection"
	"2" = "Install container"
	"3" = "Uninstall container (preserves user data)"
	"4" = "Backup Live container"
	"5" = "Restore Live container"
	"6" = "Update container"
	"0" = "Exit menu"
}

# Define Menu Actions
$menuActions = @{
	"1" = {
		# Pass the global variable directly to the restored -ContainerEngine parameter
		Show-ContainerStatus -ContainerName "portainer" `
			-ContainerEngine $global:containerEngine `
			-EnginePath $global:enginePath `
			-DisplayName "Portainer" `
			-TcpPort $global:httpPort `
			-HttpPort $global:httpPort
	}
	"2" = { Install-PortainerContainer }
	"3" = { Remove-ContainerAndVolume -Engine $global:enginePath -ContainerName "portainer" -VolumeName "portainer_data" } # Call shared function directly
	"4" = { Backup-ContainerState -Engine $global:enginePath -ContainerName "portainer" } # Call shared function directly
	"5" = { Restore-ContainerState -Engine $global:enginePath -ContainerName "portainer" } # Call shared function directly
	"6" = { Update-PortainerContainer } # Calls the dedicated update function
	# Note: "0" action is handled internally by Invoke-MenuLoop
}

# Invoke the Menu Loop
Invoke-MenuLoop -MenuTitle $menuTitle -MenuItems $menuItems -ActionMap $menuActions -ExitChoice "0"
