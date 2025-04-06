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
Ensure-Elevated
Set-ScriptLocation

#############################################
# Global Variables
#############################################
$global:imageName = "obeoneorg/firecrawl"
$global:redisImage = "redis:alpine"
$global:firecrawlName = "firecrawl"
$global:redisContainerName = "firecrawl-redis"
$global:networkName = "firecrawl-net"
$global:enginePath = Get-DockerPath # Explicitly use Docker
$global:volumeName = "firecrawl_data" # Define a volume name

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
	Uses Write-Information for status messages. Assumes Docker engine.
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
		Write-Information "Removing existing Redis container '$global:redisContainerName'..."
		& $global:enginePath rm --force $global:redisContainerName
	}
	Write-Information "Starting Redis container '$global:redisContainerName' on network '$global:networkName' with alias 'redis'..."
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

	Write-Information "Waiting 10 seconds for Redis container to initialize..."
	Start-Sleep -Seconds 10

	Write-Information "Testing Redis container connectivity on port 6379 before installing Firecrawl..."
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
			Write-Information "No backup restored. Pulling Firecrawl Docker image '$global:imageName'..."
			# Use shared pull function
			if (-not (Invoke-PullImage -Engine $global:enginePath -ImageName $global:imageName)) {
				# No specific pull options needed
				Write-Error "Docker pull failed for image '$global:imageName'."
				exit 1
			}
		}
		else {
			Write-Information "Using restored backup image '$global:imageName'."
		}
	}
	else {
		Write-Information "Using restored backup image '$global:imageName'."
	}

	#############################################
	# Step 4: Remove Existing Firecrawl Container (if any)
	#############################################
	$existingFirecrawl = & $global:enginePath ps --all --filter "name=^$global:firecrawlName$" --format "{{.ID}}"
	if ($existingFirecrawl) {
		Write-Information "Removing existing Firecrawl container '$global:firecrawlName'..."
		& $global:enginePath rm --force $global:firecrawlName
	}

	#############################################
	# Step 5: Run the Firecrawl Container with Overridden Redis Settings
	#############################################
	Write-Information "Starting Firecrawl container '$global:firecrawlName'..."
	# Ensure the volume exists
	if (-not (Confirm-ContainerVolume -Engine $global:enginePath -VolumeName $global:volumeName)) {
		Write-Error "Failed to ensure volume '$($global:volumeName)' exists. Exiting..."
		exit 1
	}
	Write-Information "IMPORTANT: Using volume '$($global:volumeName)' - existing user data will be preserved."

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
	Write-Information "Waiting 20 seconds for containers to fully start..."
	Start-Sleep -Seconds 20

	Write-Information "Testing Firecrawl API connectivity on port 3002..."
	Test-TCPPort -ComputerName "localhost" -Port 3002 -serviceName "Firecrawl API"
	Test-HTTPPort -Uri "http://localhost:3002" -serviceName "Firecrawl API"

	Write-Information "Testing Redis container connectivity on port 6379..."
	Test-TCPPort -ComputerName "localhost" -Port 6379 -serviceName "Firecrawl Redis"

	Write-Information "Firecrawl is now running and accessible at http://localhost:3002"
}

#==============================================================================
# Function: Uninstall-FirecrawlContainer
#==============================================================================
<#
.SYNOPSIS
	Uninstalls the Firecrawl container, its data volume (optional), and the associated Redis container.
.DESCRIPTION
	Calls Remove-ContainerAndVolume for the Firecrawl container and volume ('firecrawl', 'firecrawl_data').
	Stops and removes the dedicated Redis container ('firecrawl-redis'). Supports -WhatIf via Remove-ContainerAndVolume.
.EXAMPLE
	Uninstall-FirecrawlContainer -Confirm:$false
.NOTES
	Relies on Remove-ContainerAndVolume helper function.
	Uses 'docker rm --force' for the Redis container.
#>
function Uninstall-FirecrawlContainer {
	# Remove Firecrawl container and potentially its volume
	Remove-ContainerAndVolume -Engine $global:enginePath -ContainerName $global:firecrawlName -VolumeName $global:volumeName

	# Remove the dedicated Redis container
	$existingRedis = & $global:enginePath ps -a --filter "name=^$global:redisContainerName$" --format "{{.ID}}"
	if ($existingRedis) {
		Write-Information "Removing Redis container '$global:redisContainerName'..."
		& $global:enginePath rm --force $global:redisContainerName
	}
}

#==============================================================================
# Function: Backup-FirecrawlContainer
#==============================================================================
<#
.SYNOPSIS
	Backs up the state of the running Firecrawl container.
.DESCRIPTION
	Calls the Backup-ContainerState helper function, specifying 'firecrawl' as the container name.
	This commits the container state to an image and saves it as a tar file.
.EXAMPLE
	Backup-FirecrawlContainer
.NOTES
	Relies on Backup-ContainerState helper function. Does not back up the Redis container state.
#>
function Backup-FirecrawlContainer {
	Backup-ContainerState -Engine $global:enginePath -ContainerName $global:firecrawlName
}

#==============================================================================
# Function: Restore-FirecrawlContainer
#==============================================================================
<#
.SYNOPSIS
	Restores the Firecrawl container image from a backup tar file.
.DESCRIPTION
	Calls the Restore-ContainerState helper function, specifying 'firecrawl' as the container name.
	This loads the image from the backup tar file. Note: This only restores the Firecrawl image,
	it does not automatically start the container or restore/start the Redis container.
.EXAMPLE
	Restore-FirecrawlContainer
.NOTES
	Relies on Restore-ContainerState helper function. Does not handle Redis restore or container start.
#>
function Restore-FirecrawlContainer {
	Restore-ContainerState -Engine $global:enginePath -ContainerName $global:firecrawlName
	# Consider adding logic to restart Redis if needed after restore
}

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
	Uses Write-Information for status messages. Assumes Docker engine and relies on global $networkName.
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

	Write-Information "Starting updated Firecrawl container '$ContainerName'..."

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
	Write-Information "Waiting 20 seconds for container to fully start..."
	Start-Sleep -Seconds 20
	Write-Information "Testing Firecrawl API connectivity on port 3002..."
	Test-TCPPort -ComputerName "localhost" -Port 3002 -serviceName "Firecrawl API"
	Test-HTTPPort -Uri "http://localhost:3002" -serviceName "Firecrawl API"
	Write-Information "Testing Redis container connectivity on port 6379..."
	Test-TCPPort -ComputerName "localhost" -Port 6379 -serviceName "Firecrawl Redis"
	Write-Information "Firecrawl container updated successfully."
}

#==============================================================================
# Function: Update-FirecrawlContainer
#==============================================================================
<#
.SYNOPSIS
	Updates the Firecrawl container to the latest image version using the generic update workflow.
.DESCRIPTION
	Calls the generic Update-Container helper function, providing the specific details for the
	Firecrawl container (name, image name) and passing a reference to the
	Invoke-StartFirecrawlForUpdate function via the -RunFunction parameter. This ensures the
	container is started correctly after the image is pulled and the old container is removed.
	The associated Redis container is assumed to be running and is not affected by this update.
	Supports -WhatIf.
.EXAMPLE
	Update-FirecrawlContainer -WhatIf
.NOTES
	Relies on the Update-Container helper function and Invoke-StartFirecrawlForUpdate. Assumes Docker engine.
#>
function Update-FirecrawlContainer {
	[CmdletBinding(SupportsShouldProcess = $true)]
	param()

	# Check ShouldProcess before proceeding with the delegated update
	if (-not $PSCmdlet.ShouldProcess($global:firecrawlName, "Update Container")) {
		return
	}

	# Previously, a script block was defined here and passed using .GetNewClosure().
	# .GetNewClosure() creates a copy of the script block that captures the current
	# state of variables in its scope, ensuring the generic Update-Container function
	# executes it with the correct context from this script.
	# We now use a dedicated function (Invoke-StartFirecrawlForUpdate) instead for better structure.

	# Call the generic Update-Container function
	Update-Container -Engine $global:enginePath `
		-ContainerName $global:firecrawlName `
		-ImageName $global:imageName `
		-RunFunction ${function:Invoke-StartFirecrawlForUpdate} # Pass function reference
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
		Write-Information "Update User Data functionality is not implemented for Firecrawl container."
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
	"3" = { Uninstall-FirecrawlContainer }
	"4" = { Backup-FirecrawlContainer }
	"5" = { Restore-FirecrawlContainer }
	"6" = { Update-FirecrawlContainer }
	"7" = { Update-FirecrawlUserData }
	# Note: "0" action is handled internally by Invoke-MenuLoop
}

# Invoke the Menu Loop
Invoke-MenuLoop -MenuTitle $menuTitle -MenuItems $menuItems -ActionMap $menuActions -ExitChoice "0"
