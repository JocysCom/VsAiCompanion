################################################################################
# Description  : Script to set up and run a Firecrawl API container.
#                Requires PostgreSQL, Redis and Playwright service containers to be running first.
#                This container provides the API endpoints and includes UI testing
#                and comprehensive service availability reporting.
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

# Ensure the script is running as Administrator and set the working directory.
# Note: This script currently only supports Docker due to network alias usage.
#Test-AdminPrivilege # Docker requires elevation
Set-ScriptLocation

#############################################
# Global Configuration
#############################################
$global:containerName = "firecrawl-api"
$global:redisContainerName = "firecrawl-redis"
$global:playwrightContainerName = "playwright-service"

Write-Host "Firecrawl configured in Developer Mode (all UI features enabled)" -ForegroundColor Green

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
$manifestConfig = $manifest.resources.'firecrawl-api'

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
	hostPort = $manifestConfig.properties.bindings[0].hostPort
	containerPort = $manifestConfig.properties.bindings[0].containerPort
}

# Create derived URLs from configuration
$global:firecrawlBaseUrl = "http://localhost:$($config.hostPort)"
$global:firecrawlApiUrl = "$global:firecrawlBaseUrl/v1"
$global:firecrawlAdminUrl = "$global:firecrawlBaseUrl/admin/undefined/queues"
$global:firecrawlHealthUrl = "$global:firecrawlBaseUrl/v0/health/liveness"
$global:firecrawlReadinessUrl = "$global:firecrawlBaseUrl/v0/health/readiness"
$global:firecrawlServerHealthUrl = "$global:firecrawlBaseUrl/v0/health/liveness"
$global:redisUrl = "localhost:$($config.environment.REDIS_PORT)"
$global:playwrightUrl = "http://localhost:3010"

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
# Function: Test-FirecrawlAPIDependency
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
    if (-not (Test-FirecrawlAPIDependency)) { exit 1 }
.NOTES
	This function should be called before attempting to install Firecrawl API.
#>
function Test-FirecrawlAPIDependency {
	Write-Host "Checking Firecrawl API dependencies..."

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
				"firecrawl-postgres" {
					Write-Host "Please run 'Setup_App_4a_Firecrawl_Postgres.ps1' first to install and start the PostgreSQL container."
				}
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

	Write-Host "All dependencies are satisfied. Redis and Playwright service are ready for Firecrawl API."
	return $true
}

#==============================================================================
# Function: Test-FirecrawlUIAvailability
#==============================================================================
<#
.SYNOPSIS
	Tests the availability of Firecrawl UI and documentation endpoints.
.DESCRIPTION
	Tests various Firecrawl endpoints to determine which UI features are available:
	1. Main interface (root URL)
	2. API documentation (/docs)
	3. Admin dashboard (/admin/queues)
	4. Health check (/health)
	5. API endpoint (/v1/scrape)
	Returns a hashtable with the status of each endpoint.
.OUTPUTS
	[hashtable] Returns a hashtable with endpoint URLs as keys and availability status as values.
.EXAMPLE
	$uiStatus = Test-FirecrawlUIAvailability
.NOTES
	Uses Test-HTTPPort function to check endpoint availability.
	Designed to be called after the container is running.
#>
function Test-FirecrawlUIAvailability {
	Write-Host ""
	Write-Host "Main Services:" -ForegroundColor White

	$endpoints = [ordered]@{
		"Liveness Check"  = @{ Url = $global:firecrawlHealthUrl }
		"Readiness Check" = @{ Url = $global:firecrawlReadinessUrl }
		"Main Interface"  = @{ Url = $global:firecrawlBaseUrl }
		"Admin Dashboard" = @{ Url = $global:firecrawlAdminUrl }
		"API Endpoints"   = @{ Url = "$global:firecrawlBaseUrl/v1/scrape"; Method = "POST"; Body = '{"url":"https://example.com"}' }
	}

	$results = @{}

	foreach ($endpoint in $endpoints.GetEnumerator()) {
		$name = $endpoint.Key
		$cfg = $endpoint.Value
		$testParams = @{ Uri = $cfg.Url; serviceName = $name; Timeout = 10 }
		if ($cfg.Method) { $testParams["Method"] = $cfg.Method }
		if ($cfg.Body) { $testParams["Body"] = $cfg.Body }
		$testResult = Test-HTTPPort @testParams
		if ($testResult) {
			$results[$name] = @{ Status = "Available"; Url = $cfg.Url; Icon = "✅" }
		}
		else {
			$results[$name] = @{ Status = "Unavailable"; Url = $cfg.Url; Icon = "❌" }
		}
	}

	return $results
}

#==============================================================================
# Function: Show-FirecrawlServicesSummary
#==============================================================================
<#
.SYNOPSIS
	Displays a comprehensive summary of all Firecrawl services and their URLs.
.DESCRIPTION
	Shows a formatted list of all available Firecrawl services, their URLs, and availability status.
	Includes main services, related services, and helpful notes for users.
.PARAMETER UIStatus
	Hashtable containing the UI availability test results from Test-FirecrawlUIAvailability.
.EXAMPLE
	Show-FirecrawlServicesSummary -UIStatus $uiTestResults
.NOTES
	Uses Write-Host for formatted output with colors.
	Designed to be called at the end of installation process.
#>
function Show-FirecrawlServicesSummary {
	param(
		[hashtable]$UIStatus
	)

	Write-Host ""
	Write-Host "Related Services:" -ForegroundColor White

	$redisPort = [int]$config.environment.REDIS_PORT
	$null = Test-TCPPort -ComputerName "localhost" -Port $redisPort -serviceName "Redis Cache ($global:redisUrl)"

	$playwrightUrl = $config.environment.PLAYWRIGHT_MICROSERVICE_URL
	$playwrightPort = if ($playwrightUrl -match ":(\d+)/") { [int]$Matches[1] } else { 3000 }
	$null = Test-TCPPort -ComputerName "localhost" -Port $playwrightPort -serviceName "Playwright Service ($global:playwrightUrl)"
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
	if (-not (Test-FirecrawlAPIDependency)) {
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
			# Use shared pull function
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
		Write-Host "Using restored backup image '$($config.imageName)'."
	}

	#############################################
	# Step 4: Remove Existing Firecrawl Container (if any)
	#############################################
	$existingFirecrawl = & $global:enginePath ps --all --filter "name=^$global:containerName$" --format "{{.ID}}"
	if ($existingFirecrawl) {
		Write-Host "Removing existing Firecrawl container '$global:containerName'..."
		& $global:enginePath rm --force $global:containerName
	}

	#############################################
	# Step 5: Run the Firecrawl Container with Overridden Redis Settings
	#############################################
	Write-Host "Starting Firecrawl container '$global:containerName'..."
	# Prompt for OpenAI API Key if not set
	$openaiApiKey = Get-EnvironmentVariableWithDefault -EnvVarName 'OPENAI_API_KEY' -DefaultValue $config.environment.OPENAI_API_KEY -PromptText 'OpenAI API Key'
	# Ensure the volume exists
	if (-not (Confirm-ContainerResource -Engine $global:enginePath -ResourceType "volume" -ResourceName $config.volumeName)) {
		Write-Error "Failed to ensure volume '$($config.volumeName)' exists. Exiting..."
		exit 1
	}
	Write-Host "IMPORTANT: Using volume '$($config.volumeName)' - existing user data will be preserved."

	# Define run options as an array
	$runOptions = @(
		"--detach", # Run container in background.
		"--publish", "$($config.hostPort):$($config.containerPort)", # Map host port to container port.
		"--restart", $config.restartPolicy, # Restart policy from configuration.
		"--network", $config.networkName, # Attach the container to the specified Docker network.
		"--name", $global:containerName, # Assign the container the specified name.
		"--volume", "$($config.volumeName):$($config.dataPath)", # Mount the named volume for persistent data.
		"--env", "OPENAI_API_KEY=$openaiApiKey"
	)

	# Add all environment variables from config
	foreach ($envVar in $config.environment.PSObject.Properties) {
		$runOptions += "--env"
		$runOptions += "$($envVar.Name)=$($envVar.Value)"
	}

	Write-Host "Running Firecrawl container with Aspire configuration..." -ForegroundColor Cyan

	# Execute the command using splatting with the configured command
	if ($config.command -and $config.command.Count -gt 0) {
		& $global:enginePath run @runOptions $config.imageName $config.command
	} else {
		& $global:enginePath run @runOptions $config.imageName
	}
	if ($LASTEXITCODE -ne 0) {
		Write-Error "Failed to run Firecrawl container '$global:containerName'."
		exit 1
	}

	#############################################
	# Step 6: Wait and Test Connectivity
	#############################################
	Write-Host "Waiting for container startup..."

	Write-Host "Testing Firecrawl API connectivity on port $($config.hostPort)..."
	Test-TCPPort -ComputerName "localhost" -Port $config.hostPort -serviceName "Firecrawl API"
	Test-HTTPPort -Uri "http://localhost:$($config.hostPort)" -serviceName "Firecrawl API"

	Write-Host "Testing Redis container connectivity on port $([int]$config.environment.REDIS_PORT)..."
	Test-TCPPort -ComputerName "localhost" -Port ([int]$config.environment.REDIS_PORT) -serviceName "Firecrawl Redis"

	#############################################
	# Step 7: Test UI Availability and Show Summary
	#############################################
	Write-Host ""
	$uiTestResults = Test-FirecrawlUIAvailability
	Show-FirecrawlServicesSummary -UIStatus $uiTestResults

	Write-Host ""
	Write-Host "Firecrawl installation completed successfully!" -ForegroundColor Green
	Write-Host "Main API is accessible at: $global:firecrawlBaseUrl" -ForegroundColor Cyan
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

	# Define run options using configuration from manifest
	$runOptions = @(
		"--detach",
		"--publish", "$($config.hostPort):$($config.containerPort)",
		"--restart", $config.restartPolicy,
		"--network", $config.networkName,
		"--name", $ContainerName,
		"--volume", "$($VolumeName):$($config.dataPath)"
	)

	# Add all environment variables from config
	foreach ($envVar in $config.environment.PSObject.Properties) {
		$runOptions += "--env"
		$runOptions += "$($envVar.Name)=$($envVar.Value)"
	}

	# Execute the command
	if ($config.command -and $config.command.Count -gt 0) {
		& $EnginePath run @runOptions $ImageName $config.command
	} else {
		& $EnginePath run @runOptions $ImageName
	}
	if ($LASTEXITCODE -ne 0) {
		throw "Failed to run updated Firecrawl container '$ContainerName'."
	}

	# Wait and Test Connectivity (same as in Install-FirecrawlContainer)
	Write-Host "Waiting for container startup..."
	Write-Host "Testing Firecrawl API connectivity on port $($config.hostPort)..."
	$null = Test-TCPPort -ComputerName "localhost" -Port $config.hostPort -serviceName "Firecrawl API"
	$null = Test-HTTPPort -Uri "http://localhost:$($config.hostPort)" -serviceName "Firecrawl API"
	Write-Host "Testing Redis container connectivity on port $([int]$config.environment.REDIS_PORT)..."
	$null = Test-TCPPort -ComputerName "localhost" -Port ([int]$config.environment.REDIS_PORT) -serviceName "Firecrawl Redis"
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
	if (-not $PSCmdlet.ShouldProcess($global:containerName, "Update Container")) {
		return
	}

	Write-Host "Initiating update for Firecrawl..."
	$backupMade = $false
	# Check if container exists before prompting for backup
	$existingContainer = & $global:enginePath ps -a --filter "name=$global:containerName" --format "{{.ID}}"
	if ($existingContainer) {
		$createBackup = Read-Host "Create backup before updating? (Y/N, default is Y)"
		if ($createBackup -ne "N") {
			Write-Host "Saving '$global:containerName' Container Image..."
			Backup-ContainerImage -Engine $global:enginePath -ImageName $config.imageName
			Write-Host "Exporting '$($config.volumeName)' Volume..."
			$null = Backup-ContainerVolume -EngineType $global:containerEngine -VolumeName $config.volumeName
			$backupMade = $true
		}
	}
	else {
		Write-Warning "Container '$global:containerName' not found. Skipping backup prompt."
	}

	# Call Update-Container (handles check, acquire image, and remove)
	$updateResult = Update-Container -Engine $global:enginePath -ContainerName $global:containerName -VolumeName $config.volumeName -ImageName $config.imageName

	if ($updateResult -eq $false) {
		Write-Host "No update available, update canceled, or container '$($global:containerName)' not found." -ForegroundColor Yellow
		return
	}

	$targetImage = $updateResult
	Write-Host "Core update steps successful. Starting new container..."
	# Start the new container using the dedicated start function
	try {
		# Invoke-StartFirecrawlForUpdate expects these params, pass configuration values
		Invoke-StartFirecrawlForUpdate -EnginePath $global:enginePath -ContainerEngineType $global:containerEngine -ContainerName $global:containerName -VolumeName $config.volumeName -ImageName $targetImage
		# Success message is handled within Invoke-StartFirecrawlForUpdate
	}
	catch {
		Write-Error "Failed to start updated Firecrawl container: $($_.Exception.Message)"
		if ($backupMade) {
			$restore = Read-Host "Would you like to restore from backup? (Y/N, default is Y)"
			if ($restore -ne "N") {
				Write-Host "Loading '$global:containerName' Container Image..."
				Test-AndRestoreBackup -Engine $global:enginePath -ImageName $config.imageName
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
$menuTitle = "Firecrawl Container Menu"
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
			-DisplayName "Firecrawl API" `
			-DelaySeconds 0

		$uiTestResults = Test-FirecrawlUIAvailability
		Show-FirecrawlServicesSummary -UIStatus $uiTestResults
	}
	"2" = { Install-FirecrawlContainer }
	"3" = {
		# Uninstall only the Firecrawl container (Redis remains for other uses)
		Remove-ContainerAndVolume -Engine $global:enginePath -ContainerName $global:containerName -VolumeName $config.volumeName
		Write-Host "Note: Redis container '$global:redisContainerName' was not removed."
		Write-Host "Use 'Setup_4a_Firecrawl_Redis.ps1' to manage the Redis container separately."
	}
	"4" = { Backup-ContainerImage -Engine $global:enginePath -ImageName $config.imageName } # Call shared function directly
	"5" = { Test-AndRestoreBackup -Engine $global:enginePath -ImageName $config.imageName } # Call shared function directly
	"6" = { Update-FirecrawlContainer } # Calls the dedicated update function
	"7" = { $null = Backup-ContainerVolume -EngineType $global:containerEngine -VolumeName $config.volumeName } # Call shared function directly
	"8" = {
		$null = Restore-ContainerVolume -EngineType $global:containerEngine -VolumeName $config.volumeName
		& $global:enginePath restart $global:containerName
	}
	"9" = { Test-ImageUpdateAvailable -Engine $global:enginePath -ImageName $config.imageName }
	# Note: "0" action is handled internally by Invoke-MenuLoop
}

# Invoke the Menu Loop
Invoke-MenuLoop -MenuTitle $menuTitle -MenuItems $menuItems -ActionMap $menuActions -ExitChoice "0"
