################################################################################
# File         : Setup_8_CloudBeaver.ps1
# Description  : Script to set up and run the CloudBeaver Community Edition
#                container using Docker/Podman.
#                Verifies volume presence, pulls the image if necessary,
#                and runs the container with port and volume mappings.
# Usage        : Run as Administrator if using Docker.
################################################################################

using namespace System
using namespace System.IO
using namespace System.Diagnostics.CodeAnalysis

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
$global:imageName = "dbeaver/cloudbeaver:latest" # Community Edition Image
$global:containerName = "cloudbeaver"
$global:volumeNameConf = "cloudbeaver_conf"         # Volume for /opt/cloudbeaver/conf
$global:volumeNameWorkspace = "cloudbeaver_workspace" # Volume for /opt/cloudbeaver/workspace
$global:containerPort = 8978                     # Default CloudBeaver port

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
}
else { # Assumes podman
	$global:pullOptions = @("--tls-verify=false")
}
# Get the engine path after setting specific options
$global:enginePath = Get-EnginePath -EngineName $global:containerEngine

#==============================================================================
# Function: Start-CloudBeaverContainer
#==============================================================================
<#
.SYNOPSIS
	Starts a new CloudBeaver container with specified configuration.
.DESCRIPTION
	Runs a new container using the selected engine with the specified image.
	Configures standard CloudBeaver settings: detached mode, name 'cloudbeaver',
	mounts 'cloudbeaver_conf' to '/opt/cloudbeaver/conf', mounts 'cloudbeaver_workspace'
	to '/opt/cloudbeaver/workspace', and maps host port 8978 to container port 8978.
	Sets the TimeZone environment variable.
	After starting, waits 20 seconds and performs TCP and HTTP connectivity tests.
	Supports -WhatIf.
.PARAMETER Image
	The CloudBeaver container image to use (e.g., 'dbeaver/cloudbeaver:latest'). Mandatory.
.OUTPUTS
	[bool] Returns $true if the container starts successfully and connectivity tests pass.
		   Returns $false if start fails, tests fail, or action is skipped due to -WhatIf.
.EXAMPLE
	Start-CloudBeaverContainer -Image "dbeaver/cloudbeaver:latest"
.NOTES
	Relies on Test-TCPPort and Test-HTTPPort helper functions.
#>
function Start-CloudBeaverContainer {
	[CmdletBinding(SupportsShouldProcess = $true)]
	[OutputType([bool])]
	param(
		[Parameter(Mandatory = $true)]
		[string]$Image
	)

	# Build the run command
	$runOptions = @(
		"--env", "TZ=Europe/London",                          # Linux tzdata TZ
		"--detach", # Run container in background.
		"--publish", "$($global:containerPort):$($global:containerPort)", # Map host port to container port.
		"--volume", "$($global:volumeNameConf):/opt/cloudbeaver/conf", # Mount the conf volume.
		"--volume", "$($global:volumeNameWorkspace):/opt/cloudbeaver/workspace", # Mount the workspace volume.
		"--name", $global:containerName         # Assign a name to the container.
	)

	# Run the container
	if ($PSCmdlet.ShouldProcess($global:containerName, "Start Container with Image '$Image'")) {
		Write-Host "Starting CloudBeaver container with image: $Image"
		Write-Host "& $global:enginePath run $runOptions $Image"
		& $global:enginePath run $runOptions $Image

		if ($LASTEXITCODE -eq 0) {
			Write-Host "Waiting for container startup..."
			Start-Sleep -Seconds 20

			# Test connectivity
			$tcpTest = Test-TCPPort -ComputerName "localhost" -Port $global:containerPort -serviceName $global:containerName
			$httpTest = Test-HTTPPort -Uri "http://localhost:$($global:containerPort)" -serviceName $global:containerName

			if ($tcpTest -and $httpTest) {
				Write-Host "CloudBeaver is now running and accessible at http://localhost:$($global:containerPort)"
				return $true
			}
			else {
				Write-Warning "CloudBeaver container started but connectivity tests failed. Please check the container logs."
				return $false
			}
		}
		else {
			Write-Error "Failed to start CloudBeaver container."
			return $false
		}
	}
	else {
		Write-Warning "Container start skipped due to -WhatIf."
		return $false # Action skipped due to -WhatIf
	}
}

#==============================================================================
# Function: Install-CloudBeaverContainer
#==============================================================================
<#
.SYNOPSIS
	Installs and starts the CloudBeaver container.
.DESCRIPTION
	Ensures the 'cloudbeaver_conf' and 'cloudbeaver_workspace' volumes exist using Confirm-ContainerResource.
	Checks if the CloudBeaver image exists locally; if not, attempts to restore from backup using
	Test-AndRestoreBackup, falling back to pulling the image using Invoke-PullImage.
	Removes any existing 'cloudbeaver' container using Remove-ContainerAndVolume (prompts about one volume).
	Starts the new container using Start-CloudBeaverContainer with the determined image.
.EXAMPLE
	Install-CloudBeaverContainer
.NOTES
	Orchestrates volume creation, image acquisition, cleanup, and container start.
	Relies on Confirm-ContainerResource, Test-AndRestoreBackup, Invoke-PullImage,
	Remove-ContainerAndVolume, and Start-CloudBeaverContainer helper functions.
#>
function Install-CloudBeaverContainer {
	[CmdletBinding(SupportsShouldProcess = $true)] # Added for overall control
	param()

	if (-not $PSCmdlet.ShouldProcess($global:containerName, "Install Container")) {
		return
	}

	# Ensure the volumes exist
	if (-not (Confirm-ContainerResource -Engine $global:enginePath -ResourceType "volume" -ResourceName $global:volumeNameConf)) {
		Write-Error "Failed to ensure volume '$($global:volumeNameConf)' exists. Exiting..."
		return
	}
	if (-not (Confirm-ContainerResource -Engine $global:enginePath -ResourceType "volume" -ResourceName $global:volumeNameWorkspace)) {
		Write-Error "Failed to ensure volume '$($global:volumeNameWorkspace)' exists. Exiting..."
		return
	}
	Write-Host "IMPORTANT: Using volumes '$($global:volumeNameConf)' and '$($global:volumeNameWorkspace)' - existing data will be preserved."

	# Check if the image is already available, restore from backup, or pull new.
	$existingImage = & $global:enginePath images --filter "reference=$($global:imageName)" --format "{{.ID}}"
	if (-not $existingImage) {
		if (-not (Test-AndRestoreBackup -Engine $global:enginePath -ImageName $global:imageName)) {
			Write-Host "No backup restored. Pulling CloudBeaver image '$global:imageName'..."
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
		Write-Host "CloudBeaver image already exists. Skipping pull."
	}

	# Remove any existing container using the shared function
	# Pass container name and one volume name (e.g., workspace) for the prompt.
	# The user will be asked if they want to remove the *workspace* volume. Conf volume is untouched by this prompt.
	Remove-ContainerAndVolume -Engine $global:enginePath -ContainerName $global:containerName -VolumeName $global:volumeNameWorkspace # This function supports ShouldProcess

	# Start the container using the global image name
	Start-CloudBeaverContainer -Image $global:imageName # This function now supports ShouldProcess
}

#==============================================================================
# Function: Update-CloudBeaverContainer
#==============================================================================
<#
.SYNOPSIS
	Updates the CloudBeaver container to the latest image version while preserving data.
.DESCRIPTION
	Orchestrates the update process:
	1. Prompts the user to optionally back up the current container image and both volumes.
	2. Calls the simplified generic Update-Container function (handles update check, removal, pull).
	3. If core update steps succeed, calls Start-CloudBeaverContainer to start the new container.
.EXAMPLE
	Update-CloudBeaverContainer -WhatIf
.NOTES
	Relies on Backup-ContainerImage, Backup-ContainerVolume, Update-Container,
	Start-CloudBeaverContainer helper functions.
	User interaction handled via Read-Host for backup confirmation.
#>
function Update-CloudBeaverContainer {
	[CmdletBinding(SupportsShouldProcess = $true)] # Keep ShouldProcess for overall control
	param()

	# Check ShouldProcess before proceeding
	if (-not $PSCmdlet.ShouldProcess($global:containerName, "Update Container")) {
		return
	}

	Write-Host "Initiating update for CloudBeaver..."

	# Check if container actually exists before prompting for backup
	$existingContainer = & $global:enginePath ps -a --filter "name=$($global:containerName)" --format "{{.ID}}"
	if ($existingContainer) {
		$createBackup = Read-Host "Create backup before updating? (Y/N, default is Y)"
		if ($createBackup -ne "N") {
			Write-Host "Saving '$($global:containerName)' Container Image..."
			Backup-ContainerImage -Engine $global:enginePath -ContainerName $global:containerName
			Write-Host "Exporting '$($global:volumeNameConf)' Volume..."
			$null = Backup-ContainerVolume -EngineType $global:containerEngine -VolumeName $global:volumeNameConf
			Write-Host "Exporting '$($global:volumeNameWorkspace)' Volume..."
			$null = Backup-ContainerVolume -EngineType $global:containerEngine -VolumeName $global:volumeNameWorkspace
		}
	}
	else {
		Write-Warning "Container '$($global:containerName)' not found. Skipping backup prompt."
	}

	# Call simplified Update-Container (handles check, remove, pull)
	# Pass one volume name (e.g., workspace) for the removal step prompt.
	if (Update-Container -Engine $global:enginePath -ContainerName $global:containerName -VolumeName $global:volumeNameWorkspace -ImageName $global:imageName) {
		Write-Host "Core update steps successful. Starting new container..."
		# Start the new container
		if (-not (Start-CloudBeaverContainer -Image $global:imageName)) {
			Write-Error "Failed to start updated CloudBeaver container."
		}
		# Success message is handled within Start-CloudBeaverContainer if successful
	}
	else {
		# Update-Container already wrote a message explaining why it returned false.
	}
}

#==============================================================================
# Function: Restart-CloudBeaverContainer
#==============================================================================
<#
.SYNOPSIS
	Restarts the CloudBeaver container.
.DESCRIPTION
	Removes the existing container using Remove-ContainerAndVolume (prompts about one volume).
	Starts a new container using Start-CloudBeaverContainer with the global image name.
.EXAMPLE
	Restart-CloudBeaverContainer
.NOTES
	Relies on Remove-ContainerAndVolume, Start-CloudBeaverContainer helper functions.
	Supports -WhatIf via helper functions.
#>
function Restart-CloudBeaverContainer {
	[CmdletBinding(SupportsShouldProcess = $true)]
	param()

	if (-not $PSCmdlet.ShouldProcess($global:containerName, "Restart Container")) {
		return
	}

	# Remove the existing container using the shared function (prompts about workspace volume)
	if (-not (Remove-ContainerAndVolume -Engine $global:enginePath -ContainerName $global:containerName -VolumeName $global:volumeNameWorkspace)) {
		Write-Error "Failed to remove existing container or action skipped. Restart aborted."
		return
	}

	# Start a new container with the same image
	if (Start-CloudBeaverContainer -Image $global:imageName) {
		Write-Host "CloudBeaver container restarted successfully!"
	}
	else {
		Write-Error "Failed to restart container or action skipped."
	}
}

################################################################################
# Main Menu Loop using Generic Function
################################################################################

# Define Menu Title and Items
$menuTitle = "CloudBeaver Container & Data Management Menu"
$menuItems = [ordered]@{
	"1" = "Show Info & Test Connection"
	"2" = "Install container"
	"3" = "Uninstall container (prompts about workspace volume)"
	"4" = "Save Image (App)"
	"5" = "Load Image (App)"
	"6" = "Export Volume (Conf Data)"
	"7" = "Import Volume (Conf Data)"
	"8" = "Export Volume (Workspace Data)"
	"9" = "Import Volume (Workspace Data)"
	"A" = "Update System (Image)"
	"B" = "Restart"
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
			-HttpPort $global:containerPort `
			-AdditionalInfo @{ "Conf Volume" = $global:volumeNameConf; "Workspace Volume" = $global:volumeNameWorkspace }
	}
	"2" = { Install-CloudBeaverContainer }
	"3" = { Remove-ContainerAndVolume -Engine $global:enginePath -ContainerName $global:containerName -VolumeName $global:volumeNameWorkspace } # Call shared function directly
	"4" = {
		Write-Host "Saving '$($global:containerName)' Container Image..."
		Backup-ContainerImage -Engine $global:enginePath -ContainerName $global:containerName
	}
	"5" = {
		Write-Host "Loading '$($global:containerName)' Container Image..."
		Restore-ContainerImage -Engine $global:enginePath -ContainerName $global:containerName
	}
	"6" = {
		Write-Host "Exporting '$($global:volumeNameConf)' Volume..."
		$null = Backup-ContainerVolume -EngineType $global:containerEngine -VolumeName $global:volumeNameConf
	}
	"7" = {
		Write-Host "Importing '$($global:volumeNameConf)' Volume ..."
		$null = Restore-ContainerVolume -EngineType $global:containerEngine -VolumeName $global:volumeNameConf
		Restart-CloudBeaverContainer # Restart after import
	}
	"8" = {
		Write-Host "Exporting '$($global:volumeNameWorkspace)' Volume..."
		$null = Backup-ContainerVolume -EngineType $global:containerEngine -VolumeName $global:volumeNameWorkspace
	}
	"9" = {
		Write-Host "Importing '$($global:volumeNameWorkspace)' Volume ..."
		$null = Restore-ContainerVolume -EngineType $global:containerEngine -VolumeName $global:volumeNameWorkspace
		Restart-CloudBeaverContainer # Restart after import
	}
	"A" = { Update-CloudBeaverContainer }
	"B" = { Restart-CloudBeaverContainer }
	# Note: "0" action is handled internally by Invoke-MenuLoop
}

# Invoke the Menu Loop
Invoke-MenuLoop -MenuTitle $menuTitle -MenuItems $menuItems -ActionMap $menuActions -ExitChoice "0"
