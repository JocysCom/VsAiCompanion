################################################################################
# Description  : Script to set up and run a Firecrawl Worker container.
#                Requires Redis and Playwright service containers to be running first.
#                This container runs background workers for processing jobs.
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

# Ensure the script working directory is set.
Set-ScriptLocation

#############################################
# Global Configuration
#############################################
$global:containerName = "firecrawl-worker"

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

# Load configuration from Aspire manifest
$aspireManifestPath = Join-Path $PSScriptRoot "Files\Aspire\manifest.json"
$manifest = Get-Content -Raw $aspireManifestPath | ConvertFrom-Json
$manifestConfig = $manifest.resources.'firecrawl-worker'

# Create consolidated configuration object
$config = [PSCustomObject]@{
	imageName = $manifestConfig.properties.image
	networkName = $manifestConfig.properties.networks[0].name
	volumeName = $manifestConfig.properties.volumes[0].name
	networkAlias = $manifestConfig.properties.networks[0].alias
	dataPath = $manifestConfig.properties.volumes[0].containerPath
	restartPolicy = $manifestConfig.properties.restart
	environment = $manifestConfig.properties.environment
	command = $manifestConfig.properties.command
	dependencies = $manifestConfig.properties.dependencies
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

#==============================================================================
# Function: Test-FirecrawlWorkerDependencies
#==============================================================================
<#
.SYNOPSIS
	Tests if the required dependencies are available for Firecrawl Worker.
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
	if (-not (Test-FirecrawlWorkerDependencies)) { exit 1 }
.NOTES
	This function should be called before attempting to install Firecrawl Worker.
#>
function Test-FirecrawlWorkerDependencies {
	Write-Host "Checking Firecrawl Worker dependencies..."

	# Check if network exists
	$networkExists = & $global:enginePath network ls --filter "name=^$($config.networkName)$" --format "{{.Name}}"
	if ($networkExists -ne $config.networkName) {
		Write-Error "Network '$($config.networkName)' not found."
		Write-Host "Please run 'Setup_4a_Firecrawl_Redis.ps1' first to create the Redis container and network."
		return $false
	}

	# Check dependencies from manifest
	foreach ($dependency in $config.dependencies) {
		$depContainer = & $global:enginePath ps --filter "name=^$dependency$" --format "{{.Names}}"
		if ($depContainer -ne $dependency) {
			Write-Error "Dependency container '$dependency' is not running."
			switch ($dependency) {
				"firecrawl-redis" {
					Write-Host "Please run 'Setup_4a_Firecrawl_Redis.ps1' first to install and start the Redis container."
				}
				"playwright-service" {
					Write-Host "Please run 'Setup_9_Playwright_Service.ps1' first to install and start the Playwright service container."
				}
				default {
					Write-Host "Please ensure the '$dependency' container is running."
				}
			}
			return $false
		}
	}

	# Test Redis connectivity (port from environment config)
	$redisPort = [int]$config.environment.REDIS_PORT
	Write-Host "Testing Redis connectivity..."
	if (-not (Test-TCPPort -ComputerName "localhost" -Port $redisPort -serviceName "Firecrawl Redis")) {
		Write-Error "Cannot connect to Redis on port $redisPort."
		Write-Host "Please ensure the Redis container is running properly using 'Setup_4a_Firecrawl_Redis.ps1'."
		return $false
	}

	# Test Playwright service connectivity (extract port from URL)
	$playwrightUrl = $config.environment.PLAYWRIGHT_MICROSERVICE_URL
	$playwrightPort = if ($playwrightUrl -match ":(\d+)/") { [int]$Matches[1] } else { 3000 }
	Write-Host "Testing Playwright service connectivity..."
	if (-not (Test-TCPPort -ComputerName "localhost" -Port $playwrightPort -serviceName "Playwright Service")) {
		Write-Error "Cannot connect to Playwright service on port $playwrightPort."
		Write-Host "Please ensure the Playwright service container is running properly using 'Setup_9_Playwright_Service.ps1'."
		return $false
	}

	Write-Host "All dependencies are satisfied. Redis and Playwright service are ready for Firecrawl Worker."
	return $true
}

#==============================================================================
# Function: Install-FirecrawlWorkerContainer
#==============================================================================
<#
.SYNOPSIS
	Installs the Firecrawl Worker container.
.DESCRIPTION
	Performs the following steps:
	1. Checks that all dependencies are available (network, Redis, Playwright).
	2. Ensures the 'firecrawl-worker' volume exists.
	3. Checks if the Firecrawl image exists locally, restores from backup, or pulls it.
	4. Removes any existing 'firecrawl-worker' container.
	5. Runs the Firecrawl Worker container with proper environment variables.
	6. Waits and verifies the container is running.
.EXAMPLE
	Install-FirecrawlWorkerContainer
.NOTES
	This function requires that Redis and Playwright are already running.
	Uses Write-Host for status messages.
#>
function Install-FirecrawlWorkerContainer {
	#############################################
	# Step 1: Check All Dependencies
	#############################################
	if (-not (Test-FirecrawlWorkerDependencies)) {
		Write-Error "Dependencies not met. Exiting..."
		exit 1
	}

	#############################################
	# Step 2: Pull the Firecrawl Docker Image (or Restore)
	#############################################
	$existingImage = & $global:enginePath images --filter "reference=$($config.imageName)" --format "{{.ID}}"
	if (-not $existingImage) {
		if (-not (Test-AndRestoreBackup -Engine $global:enginePath -ImageName $config.imageName)) {
			Write-Host "No backup restored. Pulling Firecrawl Docker image '$($config.imageName)'..."
			if (-not (Invoke-PullImage -Engine $global:enginePath -ImageName $config.imageName -PullOptions $global:pullOptions)) {
				Write-Error "Image pull failed for '$($config.imageName)'."
				exit 1
			}
		}
		else {
			Write-Host "Using restored backup image '$($config.imageName)'."
		}
	}
	else {
		Write-Host "Firecrawl image already exists. Skipping pull."
	}

	#############################################
	# Step 3: Remove Existing Firecrawl Worker Container (if any)
	#############################################
	$existingWorker = & $global:enginePath ps --all --filter "name=^$global:containerName$" --format "{{.ID}}"
	if ($existingWorker) {
		Write-Host "Removing existing Firecrawl Worker container '$global:containerName'..."
		& $global:enginePath rm --force $global:containerName
	}

	#############################################
	# Step 4: Ensure Volume Exists and Run Firecrawl Worker Container
	#############################################
	# Ensure the volume exists
	if (-not (Confirm-ContainerResource -Engine $global:enginePath -ResourceType "volume" -ResourceName $config.volumeName)) {
		Write-Error "Failed to ensure volume '$($config.volumeName)' exists. Exiting..."
		exit 1
	}
	Write-Host "IMPORTANT: Using volume '$($config.volumeName)' - existing worker data will be preserved."

	Write-Host "Starting Firecrawl Worker container '$global:containerName' on network '$($config.networkName)'..."

	# Define run options as an array
	$runOptions = @(
		"--detach", # Run container in background.
		"--name", $global:containerName, # Assign a name to the container.
		"--network", $config.networkName, # Connect container to the specified network.
		"--volume", "$($config.volumeName):$($config.dataPath)", # Mount the named volume for persistent data.
		"--restart", $config.restartPolicy # Restart policy from manifest
	)

	# Add environment variables from manifest
	if ($config.environment) {
		foreach ($key in $config.environment.PSObject.Properties.Name) {
			$runOptions += "--env"
			$runOptions += "$key=$($config.environment.$key)"
		}
	}

	# Execute the command using splatting with the Worker command from manifest
	& $global:enginePath run @runOptions $config.imageName @($config.command)
	if ($LASTEXITCODE -ne 0) {
		Write-Error "Failed to start Firecrawl Worker container '$global:containerName'."
		exit 1
	}

	#############################################
	# Step 5: Wait and Verify Container Status
	#############################################
	Write-Host "Waiting 15 seconds for Firecrawl Worker container to initialize..."
	Start-Sleep -Seconds 15

	# Check if container is running
	$runningContainer = & $global:enginePath ps --filter "name=^$global:containerName$" --format "{{.Names}}"
	if ($runningContainer -eq $global:containerName) {
		Write-Host "Firecrawl Worker container is now running successfully."
		Write-Host "Worker is processing jobs from the Redis queue on the '$($config.networkName)' network."
	}
	else {
		Write-Error "Firecrawl Worker container failed to start properly. Please check the container logs."
		exit 1
	}
}

#==============================================================================
# Function: Update-FirecrawlWorkerContainer
#==============================================================================
<#
.SYNOPSIS
	Updates the Firecrawl Worker container to the latest image version.
.DESCRIPTION
	Orchestrates the update process:
	1. Prompts the user to optionally back up the current container image.
	2. Calls the generic Update-Container function (handles update check, removal, pull).
	3. If core update steps succeed, starts the new container.
	4. Offers to restore from backup if the start fails (and a backup was made).
.EXAMPLE
	Update-FirecrawlWorkerContainer -WhatIf
.NOTES
	Uses the generic update workflow for consistency.
	User interaction handled via Read-Host for backup confirmation.
#>
function Update-FirecrawlWorkerContainer {
	[CmdletBinding(SupportsShouldProcess = $true)]
	param()

	# Check ShouldProcess before proceeding
	if (-not $PSCmdlet.ShouldProcess($global:containerName, "Update Container")) {
		return
	}

	Write-Host "Initiating update for Firecrawl Worker..."
	$backupMade = $false

	# Check if container exists before prompting for backup
	$existingContainer = & $global:enginePath ps -a --filter "name=$($global:containerName)" --format "{{.ID}}"
	if ($existingContainer) {
		$createBackup = Read-Host "Create backup before updating? (Y/N, default is Y)"
		if ($createBackup -ne "N") {
			Write-Host "Saving '$($global:containerName)' Container Image..."
			Backup-ContainerImage -Engine $global:enginePath -ContainerName $global:containerName
			Write-Host "Exporting '$($config.volumeName)' Volume..."
			$null = Backup-ContainerVolume -EngineType $global:containerEngine -VolumeName $config.volumeName
			$backupMade = $true
		}
	}
	else {
		Write-Warning "Container '$($global:containerName)' not found. Skipping backup prompt."
	}

	# Call simplified Update-Container (handles check, remove, pull)
	if (Update-Container -Engine $global:enginePath -ContainerName $global:containerName -ImageName $config.imageName) {
		Write-Host "Core update steps successful. Starting new container..."
		# Start the new container
		try {
			Install-FirecrawlWorkerContainer
		}
		catch {
			Write-Error "Failed to start updated Firecrawl Worker container: $($_.Exception.Message)"
			if ($backupMade) {
				$restore = Read-Host "Would you like to restore from backup? (Y/N, default is Y)"
				if ($restore -ne "N") {
					Write-Host "Loading '$($global:containerName)' Container Image..."
					Restore-ContainerImage -Engine $global:enginePath -ContainerName $global:containerName
					Write-Host "Importing '$($config.volumeName)' Volume..."
					$null = Restore-ContainerVolume -EngineType $global:containerEngine -VolumeName $config.volumeName
				}
			}
		}
	}
	else {
		Write-Error "Update process failed during check, removal, or pull."
		if ($backupMade) {
			$restore = Read-Host "Would you like to restore from backup? (Y/N, default is Y)"
			if ($restore -ne "N") {
				Write-Host "Loading '$($global:containerName)' Container Image..."
				Restore-ContainerImage -Engine $global:enginePath -ContainerName $global:containerName
				Write-Host "Importing '$($config.volumeName)' Volume..."
				$null = Restore-ContainerVolume -EngineType $global:containerEngine -VolumeName $config.volumeName
			}
		}
	}
}

################################################################################
# Main Menu Loop using Generic Function
################################################################################

# Define Menu Title and Items
$menuTitle = "Firecrawl Worker Container Menu"
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
	"0" = "Exit menu"
}

# Define Menu Actions
$menuActions = @{
	"1" = {
		Show-ContainerStatus -ContainerName $global:containerName `
			-ContainerEngine $global:containerEngine `
			-EnginePath $global:enginePath `
			-DisplayName "Firecrawl Worker" `
			-DelaySeconds 3
	}
	"2" = { Install-FirecrawlWorkerContainer }
	"3" = {
		Remove-ContainerAndVolume -Engine $global:enginePath -ContainerName $global:containerName -VolumeName $config.volumeName
	}
	"4" = { Backup-ContainerImage -Engine $global:enginePath -ContainerName $global:containerName }
	"5" = { Restore-ContainerImage -Engine $global:enginePath -ContainerName $global:containerName }
	"6" = { Update-FirecrawlWorkerContainer }
	"7" = {
		$null = Backup-ContainerVolume -EngineType $global:containerEngine -VolumeName $config.volumeName
	}
	"8" = {
		$null = Restore-ContainerVolume -EngineType $global:containerEngine -VolumeName $config.volumeName
		& $global:enginePath restart $global:containerName
	}
	"9" = {
		Test-ImageUpdateAvailable -Engine $global:enginePath -ImageName $config.imageName
	}
	# Note: "0" action is handled internally by Invoke-MenuLoop
}

# Invoke the Menu Loop
Invoke-MenuLoop -MenuTitle $menuTitle -MenuItems $menuItems -ActionMap $menuActions -ExitChoice "0"
