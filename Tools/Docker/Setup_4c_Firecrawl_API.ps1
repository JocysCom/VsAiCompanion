################################################################################
# File         : Setup_4b_Firecrawl_API.ps1
# Description  : Script to set up and run a Firecrawl API container.
#                Requires Redis and Playwright service containers to be running first.
#                This container provides the API endpoints and Bull queue dashboard UI.
# Usage        : Run as Administrator if using Docker.
#                Prerequisites: Redis (Setup_4a_Firecrawl_Redis.ps1) and
#                              Playwright (Setup_10_Playwright_Service.ps1) containers.
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
#Test-AdminPrivilege # Docker requires elevation
Set-ScriptLocation

#############################################
# Global Variables
#############################################
$global:imageName = "ghcr.io/mendableai/firecrawl"
$global:firecrawlName = "firecrawl-api" # Use this for container name
$global:volumeName = $global:firecrawlName # Default: same as container name.
$global:redisImage = "redis:alpine"
$global:redisContainerName = "firecrawl-redis"
$global:playwrightContainerName = "playwright-service"
$global:networkName = "firecrawl-net"
$global:firecrawlPort = 3002
$global:firecrawlDataPath = "/app/data"
$global:redisNetworkAlias = "firecrawl-redis"
$global:redisPort = 6379
$global:playwrightPort = 3010

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
else {
 # Assumes podman
	$global:pullOptions = @("--tls-verify=false")
}
# Get the engine path after setting specific options
$global:enginePath = Get-EnginePath -EngineName $global:containerEngine

#==============================================================================
# Function: Test-FirecrawlAPIDependencies
#==============================================================================
<#
.SYNOPSIS
	Tests if the required dependencies are available for Firecrawl API.
.DESCRIPTION
	Checks for the following prerequisites:
	1. The 'firecrawl-net' network exists.
	2. The 'firecrawl-redis' container exists and is running.
	3. The 'playwright-service' container exists and is running.
	4. TCP connectivity to Redis on port 6379 is working.
	5. TCP connectivity to Playwright service on port 3000 is working.
	Provides helpful error messages if any dependency is missing.
.OUTPUTS
	[bool] Returns $true if all dependencies are met, $false otherwise.
.EXAMPLE
	if (-not (Test-FirecrawlAPIDependencies)) { exit 1 }
.NOTES
	This function should be called before attempting to install Firecrawl API.
#>
function Test-FirecrawlAPIDependencies {
	Write-Host "Checking Firecrawl API dependencies..."

	# Check if network exists
	$networkExists = & $global:enginePath network ls --filter "name=^$global:networkName$" --format "{{.Name}}"
	if ($networkExists -ne $global:networkName) {
		Write-Error "Network '$global:networkName' not found."
		Write-Host "Please run 'Setup_4a_Firecrawl_Redis.ps1' first to create the Redis container and network."
		return $false
	}

	# Check if Redis container exists and is running
	$redisContainer = & $global:enginePath ps --filter "name=^$global:redisContainerName$" --format "{{.Names}}"
	if ($redisContainer -ne $global:redisContainerName) {
		Write-Error "Redis container '$global:redisContainerName' is not running."
		Write-Host "Please run 'Setup_4a_Firecrawl_Redis.ps1' first to install and start the Redis container."
		return $false
	}

	# Check if Playwright service container exists and is running
	$playwrightContainer = & $global:enginePath ps --filter "name=^$global:playwrightContainerName$" --format "{{.Names}}"
	if ($playwrightContainer -ne $global:playwrightContainerName) {
		Write-Error "Playwright service container '$global:playwrightContainerName' is not running."
		Write-Host "Please run 'Setup_10_Playwright_Service.ps1' first to install and start the Playwright service container."
		return $false
	}

	# Test Redis connectivity
	Write-Host "Testing Redis connectivity..."
	if (-not (Test-TCPPort -ComputerName "localhost" -Port $global:redisPort -serviceName "Firecrawl Redis")) {
		Write-Error "Cannot connect to Redis on port $global:redisPort."
		Write-Host "Please ensure the Redis container is running properly using 'Setup_4a_Firecrawl_Redis.ps1'."
		return $false
	}

	# Test Playwright service connectivity
	Write-Host "Testing Playwright service connectivity..."
	if (-not (Test-TCPPort -ComputerName "localhost" -Port $global:playwrightPort -serviceName "Playwright Service")) {
		Write-Error "Cannot connect to Playwright service on port $global:playwrightPort."
		Write-Host "Please ensure the Playwright service container is running properly using 'Setup_10_Playwright_Service.ps1'."
		return $false
	}

	Write-Host "All dependencies are satisfied. Redis and Playwright service are ready for Firecrawl API."
	return $true
}

#==============================================================================
# Function: Install-FirecrawlContainer
#==============================================================================
<#
.SYNOPSIS
	Installs the Firecrawl container.
.DESCRIPTION
	Performs the following steps:
	1. Checks that Redis dependencies are available (network, container, connectivity).
	2. Ensures the 'firecrawl' volume exists.
	3. Checks if the Firecrawl image exists locally, restores from backup, or pulls it.
	4. Removes any existing 'firecrawl' container.
	5. Runs the Firecrawl container, connecting it to the network, mounting the volume, and setting environment variables to use the existing Redis.
	6. Waits and tests TCP/HTTP connectivity to the Firecrawl API.
.EXAMPLE
	Install-FirecrawlContainer
.NOTES
	This function requires that Redis is already running (use Setup_4a_Firecrawl_Redis.ps1 first).
	Uses Write-Host for status messages.
#>
function Install-FirecrawlContainer {
	#############################################
	# Step 1: Check All Dependencies
	#############################################
	if (-not (Test-FirecrawlAPIDependencies)) {
		Write-Error "Dependencies not met. Exiting..."
		exit 1
	}

	#############################################
	# Step 2: Pull the Firecrawl Docker Image (or Restore)
	#############################################
	$existingImage = & $global:enginePath images --filter "reference=$($global:imageName)" --format "{{.ID}}"
	if (-not $existingImage) {
		if (-not (Test-AndRestoreBackup -Engine $global:enginePath -ImageName $global:imageName)) {
			Write-Host "No backup restored. Pulling Firecrawl Docker image '$global:imageName'..."
			# Use shared pull function
			if (-not (Invoke-PullImage -Engine $global:enginePath -ImageName $global:imageName -PullOptions $global:pullOptions)) {
				Write-Error "Image pull failed for '$global:imageName'."
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
	if (-not (Confirm-ContainerResource -Engine $global:enginePath -ResourceType "volume" -ResourceName $global:volumeName)) {
		Write-Error "Failed to ensure volume '$($global:volumeName)' exists. Exiting..."
		exit 1
	}
	Write-Host "IMPORTANT: Using volume '$($global:volumeName)' - existing user data will be preserved."

	# Define run options as an array
	$runOptions = @(
		"--detach", # Run container in background.
		"--publish", "$($global:firecrawlPort):$($global:firecrawlPort)", # Map host port to container port.
		"--restart", "always", # Always restart the container unless explicitly stopped.
		"--network", $global:networkName, # Attach the container to the specified Docker network.
		"--name", $global:firecrawlName, # Assign the container the name 'firecrawl'.
		"--volume", "$($global:volumeName):$global:firecrawlDataPath", # Mount the named volume for persistent data.
		"--env", "OPENAI_API_KEY=dummy", # Set dummy OpenAI key.
		"--env", "REDIS_URL=redis://$($global:redisNetworkAlias):$($global:redisPort)", # Point to the Redis container using network alias.
		"--env", "REDIS_RATE_LIMIT_URL=redis://$($global:redisNetworkAlias):$($global:redisPort)",
		"--env", "REDIS_HOST=$global:redisNetworkAlias",
		"--env", "REDIS_PORT=$global:redisPort",
		"--env", "PLAYWRIGHT_MICROSERVICE_URL=http://playwright-service:3000/scrape", # Required for web scraping
		"--env", "HOST=0.0.0.0", # Bind to all interfaces
		"--env", "PORT=$global:firecrawlPort", # Set the port
		"--env", "FLY_PROCESS_GROUP=app", # Set process group for API
		"--env", "POSTHOG_API_KEY=" # Disable PostHog analytics.
	)

	# Execute the command using splatting with the API command
	& $global:enginePath run @runOptions $global:imageName pnpm run start:production
	if ($LASTEXITCODE -ne 0) {
		Write-Error "Failed to run Firecrawl container '$global:firecrawlName'."
		exit 1
	}

	#############################################
	# Step 6: Wait and Test Connectivity
	#############################################
	Write-Host "Waiting 20 seconds for containers to fully start..."
	Start-Sleep -Seconds 20

	Write-Host "Testing Firecrawl API connectivity on port $global:firecrawlPort..."
	Test-TCPPort -ComputerName "localhost" -Port $global:firecrawlPort -serviceName "Firecrawl API"
	Test-HTTPPort -Uri "http://localhost:$global:firecrawlPort" -serviceName "Firecrawl API"

	Write-Host "Testing Redis container connectivity on port $global:redisPort..."
	Test-TCPPort -ComputerName "localhost" -Port $global:redisPort -serviceName "Firecrawl Redis"

	Write-Host "Firecrawl is now running and accessible at http://localhost:$global:firecrawlPort"
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
	if (-not (Confirm-ContainerResource -Engine $EnginePath -ResourceType "volume" -ResourceName $VolumeName)) {
		throw "Failed to ensure volume '$VolumeName' exists during update."
	}

	Write-Host "Starting updated Firecrawl container '$ContainerName'..."

	# Define run options (same as in Install-FirecrawlContainer)
	# Note: Uses $global:networkName, assuming it's accessible or should be passed if not.
	# For now, relying on the global scope as the original script block did.
	$runOptions = @(
		"--detach",
		"--publish", "$($global:firecrawlPort):$($global:firecrawlPort)",
		"--restart", "always",
		"--network", $global:networkName, # Use global network name
		"--name", $ContainerName,
		"--volume", "$($VolumeName):$global:firecrawlDataPath",
		"--env", "OPENAI_API_KEY=dummy",
		"--env", "REDIS_URL=redis://$($global:redisNetworkAlias):$($global:redisPort)",
		"--env", "REDIS_RATE_LIMIT_URL=redis://$($global:redisNetworkAlias):$($global:redisPort)",
		"--env", "REDIS_HOST=$global:redisNetworkAlias",
		"--env", "REDIS_PORT=$global:redisPort",
		"--env", "PLAYWRIGHT_MICROSERVICE_URL=http://playwright-service:3000/scrape", # Required for web scraping
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
	Write-Host "Testing Firecrawl API connectivity on port $global:firecrawlPort..."
	Test-TCPPort -ComputerName "localhost" -Port $global:firecrawlPort -serviceName "Firecrawl API"
	Test-HTTPPort -Uri "http://localhost:$global:firecrawlPort" -serviceName "Firecrawl API"
	Write-Host "Testing Redis container connectivity on port $global:redisPort..."
	Test-TCPPort -ComputerName "localhost" -Port $global:redisPort -serviceName "Firecrawl Redis"
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
			Write-Host "Saving '$($global:firecrawlName)' Container Image..."
			Backup-ContainerImage -Engine $global:enginePath -ContainerName $global:firecrawlName
			Write-Host "Exporting '$($global:volumeName)' Volume..."
			$null = Backup-ContainerVolume -EngineType $global:containerEngine -VolumeName $global:volumeName
			$backupMade = $true
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
					Write-Host "Loading '$($global:firecrawlName)' Container Image..."
					Restore-ContainerImage -Engine $global:enginePath -ContainerName $global:firecrawlName
					Write-Host "Importing '$($global:volumeName)' Volume..."
					$null = Restore-ContainerVolume -EngineType $global:containerEngine -VolumeName $global:volumeName
				}
			}
		}
	}
	else {
		Write-Error "Update process failed during check, removal, or pull."
		if ($backupMade) {
			$restore = Read-Host "Would you like to restore from backup? (Y/N, default is Y)"
			if ($restore -ne "N") {
				Write-Host "Loading '$($global:firecrawlName)' Container Image..."
				Restore-ContainerImage -Engine $global:enginePath -ContainerName $global:firecrawlName
				Write-Host "Importing '$($global:volumeName)' Volume..."
				$null = Restore-ContainerVolume -EngineType $global:containerEngine -VolumeName $global:volumeName
			}
		}
	}
}

################################################################################
# Main Menu Loop using Generic Function
################################################################################

# Define Menu Title and Items
$menuTitle = "Firecrawl Container Menu"
$menuItems = [ordered]@{
	"1" = "Show Info & Test Connection"
	"2" = "Install container (requires Redis)"
	"3" = "Uninstall container"
	"4" = "Save Image (App)"
	"5" = "Load Image (App)"
	"6" = "Update Image (App)"
	"7" = "Export Volume (Data)"
	"8" = "Import Volume (Data)"
	"0" = "Exit menu"
}

# Define Menu Actions
$menuActions = @{
	"1" = {
		# Show status for Firecrawl itself
		Show-ContainerStatus -ContainerName $global:firecrawlName `
			-ContainerEngine $global:containerEngine `
			-EnginePath $global:enginePath `
			-DisplayName "Firecrawl API" `
			-TcpPort $global:firecrawlPort `
			-HttpPort $global:firecrawlPort `
			-DelaySeconds 0

		# Show status for the associated Redis container
		Show-ContainerStatus -ContainerName $global:redisContainerName `
			-ContainerEngine $global:containerEngine `
			-EnginePath $global:enginePath `
			-DisplayName "Firecrawl Redis" `
			-TcpPort $global:redisPort `
			-DelaySeconds 3
	}
	"2" = { Install-FirecrawlContainer }
	"3" = {
		# Uninstall only the Firecrawl container (Redis remains for other uses)
		Remove-ContainerAndVolume -Engine $global:enginePath -ContainerName $global:firecrawlName -VolumeName $global:volumeName
		Write-Host "Note: Redis container '$global:redisContainerName' was not removed."
		Write-Host "Use 'Setup_4a_Firecrawl_Redis.ps1' to manage the Redis container separately."
	}
	"4" = { Backup-ContainerImage -Engine $global:enginePath -ContainerName $global:firecrawlName } # Call shared function directly
	"5" = { Restore-ContainerImage -Engine $global:enginePath -ContainerName $global:firecrawlName } # Call shared function directly
	"6" = { Update-FirecrawlContainer } # Calls the dedicated update function
	"7" = { Backup-ContainerVolume -EngineType $global:containerEngine -VolumeName $global:volumeName } # Call shared function directly
	"8" = {
		Restore-ContainerVolume -EngineType $global:containerEngine -VolumeName $global:volumeName
		Write-Host "Restarting container '$($global:firecrawlName)' to apply imported volume data..."
		& $global:enginePath restart $global:firecrawlName
	}
	# Note: "0" action is handled internally by Invoke-MenuLoop
}

# Invoke the Menu Loop
Invoke-MenuLoop -MenuTitle $menuTitle -MenuItems $menuItems -ActionMap $menuActions -ExitChoice "0"
