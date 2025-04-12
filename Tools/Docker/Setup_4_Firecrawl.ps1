################################################################################
# File         : Setup_4_Firecrawl.ps1
# Description  : Script to set up and run a Firecrawl container with a dedicated Redis container.
#                Creates a Docker network, launches Redis, pulls Firecrawl image, and runs Firecrawl
#                with environment variable overrides directing it to use the dedicated Redis.
# Usage        : Run as Administrator.
################################################################################

using namespace System
using namespace System.IO

# Dot-source the necessary helper function files.
. "$PSScriptRoot\Setup_0_Core.ps1"
. "$PSScriptRoot\Setup_0_Network.ps1"
. "$PSScriptRoot\Setup_0_ContainerEngine.ps1"
. "$PSScriptRoot\Setup_0_BackupRestore.ps1"
. "$PSScriptRoot\Setup_0_ContainerMgmt.ps1"

# Ensure the script is running as Administrator and set the working directory.
# Note: This script currently only supports Docker due to network alias usage.
Test-AdminPrivilege # Docker requires elevation
Set-ScriptLocation

#############################################
# Global Variables
#############################################
$global:imageName = "obeoneorg/firecrawl"
$global:redisImage = "redis:alpine"
$global:firecrawlName = "firecrawl"
$global:redisContainerName = "firecrawl-redis"
$global:networkName = "firecrawl-net"
$global:containerEngine = "docker" # Hardcode to docker
$global:enginePath = Get-EnginePath -EngineName $global:containerEngine # Use generic function
$global:volumeName = $global:firecrawlName # Default: same as container name.

#==============================================================================
# Function: Install-FirecrawlContainer
#==============================================================================
<#
.SYNOPSIS
	Installs the Firecrawl container and its dedicated Redis dependency.
.DESCRIPTION
	Performs the following steps:
	1. Ensures the Docker network 'firecrawl-net' exists using Confirm-ContainerNetwork.
	2. Removes any existing 'firecrawl-redis' container and starts a new one using the 'redis:alpine' image on the network with alias 'redis'.
	3. Waits and tests TCP connectivity to the Redis container.
	4. Ensures the 'firecrawl_data' volume exists using Confirm-ContainerVolume.
	5. Checks if the Firecrawl image exists locally, restores from backup, or pulls it using Invoke-PullImage.
	6. Removes any existing 'firecrawl' container.
	7. Runs the Firecrawl container, connecting it to the network, mounting the volume, and setting environment variables to use the dedicated Redis via its network alias.
	8. Waits and tests TCP/HTTP connectivity to the Firecrawl API and TCP connectivity to Redis again.
.EXAMPLE
	Install-FirecrawlContainer
.NOTES
	This function orchestrates the entire setup for Firecrawl and its Redis dependency.
	Relies on Confirm-ContainerNetwork, Confirm-ContainerVolume, Test-AndRestoreBackup, Invoke-PullImage, Test-TCPPort, Test-HTTPPort helper functions.
	Uses Write-Host for status messages. Assumes Docker engine.
#>
function Install-FirecrawlContainer {
	#############################################
	# Step 1: Ensure Docker Network Exists
	#############################################
	if (-not (Confirm-ContainerNetwork -Engine $global:enginePath -NetworkName $global:networkName)) {
		Write-Error "Failed to ensure network '$($global:networkName)' exists. Exiting..."
		exit 1
	}

	#############################################
	# Step 2: Run the Redis Container with a Network Alias
	#############################################
	$existingRedis = & $global:enginePath ps --all --filter "name=^$global:redisContainerName$" --format "{{.ID}}"
	if ($existingRedis) {
		Write-Host "Removing existing Redis container '$global:redisContainerName'..."
		& $global:enginePath rm --force $global:redisContainerName
	}
	Write-Host "Starting Redis container '$global:redisContainerName' on network '$global:networkName' with alias 'redis'..."
	# Command: run (for Redis)
	#   --detach: Run container in background.
	#   --name: Assign a name to the container.
	#   --network: Connect container to the specified network.
	#   --network-alias: Assign an alias ('redis') for use within the network.
	#   --publish: Map host port 6379 to container port 6379.
	& $global:enginePath run --detach --name $global:redisContainerName --network $global:networkName --network-alias redis --publish 6379:6379 $global:redisImage
	if ($LASTEXITCODE -ne 0) {
		Write-Error "Failed to start Redis container."
		exit 1
	}

	Write-Host "Waiting 10 seconds for Redis container to initialize..."
	Start-Sleep -Seconds 10

	Write-Host "Testing Redis container connectivity on port 6379 before installing Firecrawl..."
	if (-not (Test-TCPPort -ComputerName "localhost" -Port 6379 -serviceName "Firecrawl Redis")) {
		Write-Error "Redis connectivity test failed. Aborting Firecrawl installation."
		exit 1
	}

	#############################################
	# Step 3: Pull the Firecrawl Docker Image (or Restore)
	#############################################
	$existingImage = & $global:enginePath images --filter "reference=$($global:imageName)" --format "{{.ID}}"
	if (-not $existingImage) {
		if (-not (Test-AndRestoreBackup -Engine $global:enginePath -ImageName $global:imageName)) {
			Write-Host "No backup restored. Pulling Firecrawl Docker image '$global:imageName'..."
			# Use shared pull function
			if (-not (Invoke-PullImage -Engine $global:enginePath -ImageName $global:imageName)) {
				# No specific pull options needed
				Write-Error "Docker pull failed for image '$global:imageName'."
				exit 1
			}
		}
		else {
			Write-Host "Using restored backup image '$global:imageName'."
		}
	}
	else {
		Write-Host "Using restored backup image '$global:imageName'."
	}

	#############################################
	# Step 4: Remove Existing Firecrawl Container (if any)
	#############################################
	$existingFirecrawl = & $global:enginePath ps --all --filter "name=^$global:firecrawlName$" --format "{{.ID}}"
	if ($existingFirecrawl) {
		Write-Host "Removing existing Firecrawl container '$global:firecrawlName'..."
		& $global:enginePath rm --force $global:firecrawlName
	}

	#############################################
	# Step 5: Run the Firecrawl Container with Overridden Redis Settings
	#############################################
	Write-Host "Starting Firecrawl container '$global:firecrawlName'..."
	# Ensure the volume exists
	if (-not (Confirm-ContainerVolume -Engine $global:enginePath -VolumeName $global:volumeName)) {
		Write-Error "Failed to ensure volume '$($global:volumeName)' exists. Exiting..."
		exit 1
	}
	Write-Host "IMPORTANT: Using volume '$($global:volumeName)' - existing user data will be preserved."

	# Define run options as an array
	$runOptions = @(
		"--detach", # Run container in background.
		"--publish", "3002:3002", # Map host port 3002 to container port 3002.
		"--restart", "always", # Always restart the container unless explicitly stopped.
		"--network", $global:networkName, # Attach the container to the specified Docker network.
		"--name", $global:firecrawlName, # Assign the container the name 'firecrawl'.
		"--volume", "$($global:volumeName):/app/data", # Mount the named volume for persistent data.
		"--env", "OPENAI_API_KEY=dummy", # Set dummy OpenAI key.
		"--env", "REDIS_URL=redis://redis:6379", # Point to the Redis container using network alias.
		"--env", "REDIS_RATE_LIMIT_URL=redis://redis:6379",
		"--env", "REDIS_HOST=redis",
		"--env", "REDIS_PORT=6379",
		"--env", "POSTHOG_API_KEY="             # Disable PostHog analytics.
	)

	# Execute the command using splatting
	& $global:enginePath run @runOptions $global:imageName
	if ($LASTEXITCODE -ne 0) {
		Write-Error "Failed to run Firecrawl container '$global:firecrawlName'."
		exit 1
	}

	#############################################
	# Step 6: Wait and Test Connectivity
	#############################################
	Write-Host "Waiting 20 seconds for containers to fully start..."
	Start-Sleep -Seconds 20

	Write-Host "Testing Firecrawl API connectivity on port 3002..."
	Test-TCPPort -ComputerName "localhost" -Port 3002 -serviceName "Firecrawl API"
	Test-HTTPPort -Uri "http://localhost:3002" -serviceName "Firecrawl API"

	Write-Host "Testing Redis container connectivity on port 6379..."
	Test-TCPPort -ComputerName "localhost" -Port 6379 -serviceName "Firecrawl Redis"

	Write-Host "Firecrawl is now running and accessible at http://localhost:3002"
}

# Note: Uninstall-FirecrawlContainer, Backup-FirecrawlContainer, Restore-FirecrawlContainer functions removed. Shared functions called directly from menu.

#==============================================================================
# Function: Invoke-StartFirecrawlForUpdate
#==============================================================================
<#
.SYNOPSIS
	Helper function called by Update-Container to start the Firecrawl container after an update.
.DESCRIPTION
	This function encapsulates the specific logic required to start the Firecrawl container after an update.
	It assumes the network and Redis container are already running. It ensures the volume exists,
	sets the necessary environment variables (pointing to the existing Redis), runs the container
	with the updated image name, waits, and performs connectivity tests.
	It adheres to the parameter signature expected by the -RunFunction parameter of Update-Container.
.PARAMETER EnginePath
	Path to the container engine executable (Docker) (passed by Update-Container).
.PARAMETER ContainerEngineType
	Type of the container engine ('docker'). (Passed by Update-Container, not directly used).
.PARAMETER ContainerName
	Name of the container being updated (e.g., 'firecrawl') (passed by Update-Container).
.PARAMETER VolumeName
	Name of the volume associated with the container (e.g., 'firecrawl_data') (passed by Update-Container).
.PARAMETER ImageName
	The new image name/tag to use for the updated container (passed by Update-Container).
.OUTPUTS
	Throws an error if the container fails to start, which signals failure back to Update-Container.
.EXAMPLE
	# This function is intended to be called internally by Update-Container via -RunFunction
	# Update-Container -RunFunction ${function:Invoke-StartFirecrawlForUpdate}
.NOTES
	Relies on Confirm-ContainerVolume, Test-TCPPort, Test-HTTPPort helper functions.
	Uses Write-Host for status messages. Assumes Docker engine and relies on global $networkName.
#>
function Invoke-StartFirecrawlForUpdate {
	param(
		[string]$EnginePath,
		[string]$ContainerEngineType, # Not used directly, assumes Docker based on script context
		[string]$ContainerName, # Should be $global:firecrawlName
		[string]$VolumeName, # Should be $global:volumeName
		[string]$ImageName            # The updated image name ($global:imageName)
	)

	# Assume Network and Redis are already running from the initial install

	# Ensure the volume exists (important if it was removed manually)
	if (-not (Confirm-ContainerVolume -Engine $EnginePath -VolumeName $VolumeName)) {
		throw "Failed to ensure volume '$VolumeName' exists during update."
	}

	Write-Host "Starting updated Firecrawl container '$ContainerName'..."

	# Define run options (same as in Install-FirecrawlContainer)
	# Note: Uses $global:networkName, assuming it's accessible or should be passed if not.
	# For now, relying on the global scope as the original script block did.
	$runOptions = @(
		"--detach",
		"--publish", "3002:3002",
		"--restart", "always",
		"--network", $global:networkName, # Use global network name
		"--name", $ContainerName,
		"--volume", "$($VolumeName):/app/data",
		"--env", "OPENAI_API_KEY=dummy",
		"--env", "REDIS_URL=redis://redis:6379",
		"--env", "REDIS_RATE_LIMIT_URL=redis://redis:6379",
		"--env", "REDIS_HOST=redis",
		"--env", "REDIS_PORT=6379",
		"--env", "POSTHOG_API_KEY="
	)

	# Execute the command
	& $EnginePath run @runOptions $ImageName
	if ($LASTEXITCODE -ne 0) {
		throw "Failed to run updated Firecrawl container '$ContainerName'."
	}

	# Wait and Test Connectivity (same as in Install-FirecrawlContainer)
	Write-Host "Waiting 20 seconds for container to fully start..."
	Start-Sleep -Seconds 20
	Write-Host "Testing Firecrawl API connectivity on port 3002..."
	Test-TCPPort -ComputerName "localhost" -Port 3002 -serviceName "Firecrawl API"
	Test-HTTPPort -Uri "http://localhost:3002" -serviceName "Firecrawl API"
	Write-Host "Testing Redis container connectivity on port 6379..."
	Test-TCPPort -ComputerName "localhost" -Port 6379 -serviceName "Firecrawl Redis"
	Write-Host "Firecrawl container updated successfully."
}

#==============================================================================
# Function: Update-FirecrawlContainer
#==============================================================================
<#
.SYNOPSIS
	Updates the Firecrawl container to the latest image version using the generic update workflow.
.DESCRIPTION
	Orchestrates the update process:
	1. Prompts the user to optionally back up the current container state.
	2. Calls the simplified generic Update-Container function (handles update check, removal, pull).
	3. If core update steps succeed, calls Invoke-StartFirecrawlForUpdate to start the new container.
	4. Offers to restore from backup if the start fails (and a backup was made).
.EXAMPLE
	Update-FirecrawlContainer -WhatIf
.NOTES
	Relies on Backup-FirecrawlContainer, Update-Container, Invoke-StartFirecrawlForUpdate,
	Restore-FirecrawlContainer helper functions. Assumes Docker engine.
	User interaction handled via Read-Host for backup confirmation.
#>
function Update-FirecrawlContainer {
	[CmdletBinding(SupportsShouldProcess = $true)] # Keep ShouldProcess for overall control
	param()

	# Check ShouldProcess before proceeding
	if (-not $PSCmdlet.ShouldProcess($global:firecrawlName, "Update Container")) {
		return
	}

	Write-Host "Initiating update for Firecrawl..."
	$backupMade = $false
	# Check if container exists before prompting for backup
	$existingContainer = & $global:enginePath ps -a --filter "name=$($global:firecrawlName)" --format "{{.ID}}"
	if ($existingContainer) {
		$createBackup = Read-Host "Create backup before updating? (Y/N, default is Y)"
		if ($createBackup -ne "N") {
			if (Backup-FirecrawlContainer) { # Calls Backup-ContainerState
				$backupMade = $true
			}
		}
	}
	else {
		Write-Warning "Container '$($global:firecrawlName)' not found. Skipping backup prompt."
	}

	# Call simplified Update-Container (handles check, remove, pull)
	# Pass volume name for removal step
	if (Update-Container -Engine $global:enginePath -ContainerName $global:firecrawlName -VolumeName $global:volumeName -ImageName $global:imageName) {
		Write-Host "Core update steps successful. Starting new container..."
		# Start the new container using the dedicated start function
		try {
			# Invoke-StartFirecrawlForUpdate expects these params, pass globals/literals
			Invoke-StartFirecrawlForUpdate -EnginePath $global:enginePath `
				-ContainerEngineType "docker" ` # Hardcoded as this script only supports Docker
				-ContainerName $global:firecrawlName `
				-VolumeName $global:volumeName `
				-ImageName $global:imageName
			# Success message is handled within Invoke-StartFirecrawlForUpdate
		}
		catch {
			Write-Error "Failed to start updated Firecrawl container: $_"
			if ($backupMade) {
				$restore = Read-Host "Would you like to restore from backup? (Y/N, default is Y)"
				if ($restore -ne "N") {
					Restore-FirecrawlContainer # Calls Restore-ContainerState
				}
			}
		}
	}
	else {
		Write-Error "Update process failed during check, removal, or pull."
		if ($backupMade) {
			$restore = Read-Host "Would you like to restore from backup? (Y/N, default is Y)"
			if ($restore -ne "N") {
				Restore-FirecrawlContainer # Calls Restore-ContainerState
			}
		}
	}
}

#==============================================================================
# Function: Update-FirecrawlUserData
#==============================================================================
<#
.SYNOPSIS
	Placeholder function for updating user data in the Firecrawl container.
.DESCRIPTION
	Currently, this function only displays a message indicating that the functionality
	is not implemented. Supports -WhatIf.
.EXAMPLE
	Update-FirecrawlUserData
.NOTES
	This function needs implementation if specific user data update procedures are required.
#>
function Update-FirecrawlUserData {
	[CmdletBinding(SupportsShouldProcess = $true)]
	param()

	if ($PSCmdlet.ShouldProcess("Firecrawl User Data", "Update")) {
		# No actions implemented yet
		Write-Host "Update User Data functionality is not implemented for Firecrawl container."
	}
}

################################################################################
# Main Menu Loop using Generic Function
################################################################################

# Define Menu Title and Items
$menuTitle = "Firecrawl Container Menu"
$menuItems = [ordered]@{
	"1" = "Show Info & Test Connection"
	"2" = "Install container (includes Redis)"
	"3" = "Uninstall container (includes Redis)"
	"4" = "Backup Live container"
	"5" = "Restore Live container"
	"6" = "Update System"
	"7" = "Update User Data"
	"0" = "Exit menu"
}

# Define Menu Actions
$menuActions = @{
	"1" = {
		# Pass the global variable directly to the restored -ContainerEngine parameter
		# Show status for Firecrawl itself
		Show-ContainerStatus -ContainerName $global:firecrawlName `
			-ContainerEngine "docker" ` # This script hardcodes docker
		-EnginePath $global:enginePath `
			-DisplayName "Firecrawl API" `
			-TcpPort 3002 `
			-HttpPort 3002 `
			-DelaySeconds 0

		# Show status for the associated Redis container
		Show-ContainerStatus -ContainerName $global:redisContainerName `
			-ContainerEngine "docker" ` # This script hardcodes docker
		-EnginePath $global:enginePath `
			-DisplayName "Firecrawl Redis" `
			-TcpPort 6379 `
			-DelaySeconds 3
	}
	"2" = { Install-FirecrawlContainer }
	"3" = {
		# Uninstall both Firecrawl and its Redis container
		Remove-ContainerAndVolume -Engine $global:enginePath -ContainerName $global:firecrawlName -VolumeName $global:volumeName
		$existingRedis = & $global:enginePath ps -a --filter "name=^$global:redisContainerName$" --format "{{.ID}}"
		if ($existingRedis) {
			Write-Host "Removing Redis container '$global:redisContainerName'..."
			& $global:enginePath rm --force $global:redisContainerName
		}
	}
	"4" = { Backup-ContainerState -Engine $global:enginePath -ContainerName $global:firecrawlName } # Call shared function directly
	"5" = { Restore-ContainerState -Engine $global:enginePath -ContainerName $global:firecrawlName } # Call shared function directly
	"6" = { Update-FirecrawlContainer } # Calls the dedicated update function
	"7" = { Update-FirecrawlUserData }
	# Note: "0" action is handled internally by Invoke-MenuLoop
}

# Invoke the Menu Loop
Invoke-MenuLoop -MenuTitle $menuTitle -MenuItems $menuItems -ActionMap $menuActions -ExitChoice "0"
