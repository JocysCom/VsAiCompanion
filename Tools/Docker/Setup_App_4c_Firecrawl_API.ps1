################################################################################
# Description  : Script to set up and run a Firecrawl API container.
#                Requires Redis and Playwright service containers to be running first.
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
$global:playwrightServicePort = 3000
$global:openaiApiKey = 'dummy'
$global:firecrawlApiKey = 'fc-dummy'

# UI and Service URLs
$global:firecrawlBaseUrl = "http://localhost:$global:firecrawlPort"
$global:firecrawlApiUrl = "$global:firecrawlBaseUrl/v1"
$global:firecrawlAdminUrl = "$global:firecrawlBaseUrl/admin/undefined/queues"
$global:firecrawlHealthUrl = "$global:firecrawlBaseUrl/v0/health/liveness"
$global:firecrawlReadinessUrl = "$global:firecrawlBaseUrl/v0/health/readiness"
$global:firecrawlServerHealthUrl = "$global:firecrawlBaseUrl/serverHealthCheck"
$global:redisUrl = "localhost:$global:redisPort"
$global:playwrightUrl = "http://localhost:$global:playwrightPort"

#############################################
# Firecrawl Configuration - Developer Mode (Default)
#############################################
# Set developer mode by default to enable all UI features
$global:firecrawlMode = "developer"
$global:startCommand = "pnpm run start"
$global:nodeEnv = "development"
$global:enableDocs = "true"
$global:enableAdmin = "true"
$global:enableUI = "true"
$global:enableSwagger = "true"
$global:enableBullBoard = "true"

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
	Write-Host "Testing Firecrawl UI and service availability..."

	$endpoints = @{
		"Main Interface"  = $global:firecrawlBaseUrl
		"Health Check"    = $global:firecrawlHealthUrl
		"Readiness Check" = $global:firecrawlReadinessUrl
		"Server Health"   = $global:firecrawlServerHealthUrl
		"Admin Dashboard" = $global:firecrawlAdminUrl
	}

	$results = @{}

	foreach ($endpoint in $endpoints.GetEnumerator()) {
		$name = $endpoint.Key
		$url = $endpoint.Value

		Write-Host "  Testing $name..." -NoNewline

		try {
			# Use a simple HTTP test with short timeout
			$response = Invoke-WebRequest -Uri $url -Method GET -TimeoutSec 5 -UseBasicParsing -ErrorAction Stop
			if ($response.StatusCode -eq 200) {
				$results[$name] = @{ Status = "Available"; Url = $url; Icon = "✅" }
				Write-Host " ✅ Available" -ForegroundColor Green
			}
			else {
				$results[$name] = @{ Status = "Error ($($response.StatusCode))"; Url = $url; Icon = "❌" }
				Write-Host " ❌ Error ($($response.StatusCode))" -ForegroundColor Red
			}
		}
		catch {
			# Check if it's a 404 or other HTTP error
			if ($_.Exception.Response.StatusCode -eq 404) {
				$results[$name] = @{ Status = "Not Found (404)"; Url = $url; Icon = "❌" }
				Write-Host " ❌ Not Found (404)" -ForegroundColor Red
			}
			else {
				$results[$name] = @{ Status = "Unavailable"; Url = $url; Icon = "❌" }
				Write-Host " ❌ Unavailable" -ForegroundColor Red
			}
		}
	}

	# Test API endpoints separately (they require POST, not GET)
	Write-Host "  Testing API Endpoints..." -NoNewline
	try {
		# Test if the API endpoint structure is available by checking a simple endpoint
		$testUrl = "$global:firecrawlBaseUrl/v0/health/liveness"
		$response = Invoke-WebRequest -Uri $testUrl -Method GET -TimeoutSec 5 -UseBasicParsing -ErrorAction Stop
		if ($response.StatusCode -eq 200) {
			$results["API Endpoints"] = @{ Status = "Available (v0 & v1)"; Url = "$global:firecrawlBaseUrl/v1/scrape (POST)"; Icon = "✅" }
			Write-Host " ✅ Available (v0 & v1)" -ForegroundColor Green
		}
		else {
			$results["API Endpoints"] = @{ Status = "Error ($($response.StatusCode))"; Url = "$global:firecrawlBaseUrl/v1/scrape (POST)"; Icon = "❌" }
			Write-Host " ❌ Error ($($response.StatusCode))" -ForegroundColor Red
		}
	}
	catch {
		$results["API Endpoints"] = @{ Status = "Unavailable"; Url = "$global:firecrawlBaseUrl/v1/scrape (POST)"; Icon = "❌" }
		Write-Host " ❌ Unavailable" -ForegroundColor Red
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
	Write-Host "==========================================" -ForegroundColor Cyan
	Write-Host "🔥 Firecrawl Services Available" -ForegroundColor Yellow
	Write-Host "==========================================" -ForegroundColor Cyan

	# Main Firecrawl Services
	Write-Host ""
	Write-Host "Main Services:" -ForegroundColor White
	foreach ($service in $UIStatus.GetEnumerator()) {
		$name = $service.Key
		$info = $service.Value
		$icon = $info.Icon
		$url = $info.Url
		$status = $info.Status

		Write-Host "$icon $($name.PadRight(20)): $url" -ForegroundColor $(if ($icon -eq "✅") { "Green" } else { "Red" })
		if ($icon -eq "❌") {
			Write-Host "   └─ Status: $status" -ForegroundColor Gray
		}
	}

	# Related Services
	Write-Host ""
	Write-Host "Related Services:" -ForegroundColor White

	# Test Redis availability
	$redisAvailable = Test-TCPPort -ComputerName "localhost" -Port $global:redisPort -serviceName "Redis"
	$redisIcon = if ($redisAvailable) { "✅" } else { "❌" }
	Write-Host "$redisIcon Redis Cache        : $global:redisUrl" -ForegroundColor $(if ($redisAvailable) { "Green" } else { "Red" })

	# Test Playwright availability
	$playwrightAvailable = Test-TCPPort -ComputerName "localhost" -Port $global:playwrightPort -serviceName "Playwright"
	$playwrightIcon = if ($playwrightAvailable) { "✅" } else { "❌" }
	Write-Host "$playwrightIcon Playwright Service: $global:playwrightUrl" -ForegroundColor $(if ($playwrightAvailable) { "Green" } else { "Red" })

	# Usage Notes
	Write-Host ""
	Write-Host "Usage Notes:" -ForegroundColor White
	Write-Host "• ✅ = Service is available and responding" -ForegroundColor Gray
	Write-Host "• ❌ = Service is not available or not responding" -ForegroundColor Gray

	# Check if any UI features are missing and provide guidance
	$missingServices = $UIStatus.Values | Where-Object { $_.Icon -eq "❌" }
	if ($missingServices.Count -gt 0) {
		Write-Host ""
		Write-Host "Troubleshooting:" -ForegroundColor Yellow
		Write-Host "• Some UI features may require development mode or additional configuration" -ForegroundColor Gray
		Write-Host "• Admin Dashboard may need specific environment variables" -ForegroundColor Gray
		Write-Host "• Try accessing the main interface first: $global:firecrawlBaseUrl" -ForegroundColor Gray
	}

	Write-Host ""
	Write-Host "==========================================" -ForegroundColor Cyan
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
	# Prompt for OpenAI API Key if not set
	$global:openaiApiKey = Get-EnvironmentVariableWithDefault -EnvVarName 'OPENAI_API_KEY' -DefaultValue $global:openaiApiKey -PromptText 'OpenAI API Key'
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
		"--env", "OPENAI_API_KEY=$($global:openaiApiKey)",
		"--env", "REDIS_URL=redis://$($global:redisNetworkAlias):$($global:redisPort)", # Point to the Redis container using network alias.
		"--env", "REDIS_RATE_LIMIT_URL=redis://$($global:redisNetworkAlias):$($global:redisPort)",
		"--env", "REDIS_HOST=$global:redisNetworkAlias",
		"--env", "REDIS_PORT=$global:redisPort",
		"--env", "PLAYWRIGHT_MICROSERVICE_URL=http://playwright-service:3000/scrape", # Required for web scraping
		"--env", "HOST=0.0.0.0", # Bind to all interfaces
		"--env", "PORT=$global:firecrawlPort", # Set the port
		"--env", "FLY_PROCESS_GROUP=app", # Set process group for API
		"--env", "NODE_ENV=$global:nodeEnv", # Set Node environment (development/production)
		"--env", "ENABLE_DOCS=$global:enableDocs", # Enable/disable documentation
		"--env", "ENABLE_ADMIN=$global:enableAdmin", # Enable/disable admin dashboard
		"--env", "ENABLE_UI=$global:enableUI", # Enable UI features
		"--env", "ENABLE_SWAGGER=$global:enableSwagger", # Enable Swagger documentation
		"--env", "ENABLE_BULL_BOARD=$global:enableBullBoard", # Enable Bull queue dashboard
		"--env", "BULL_BOARD_ENABLED=true", # Alternative Bull board flag
		"--env", "SWAGGER_ENABLED=true", # Alternative Swagger flag
		"--env", "API_DOCS_ENABLED=true", # Alternative API docs flag
		"--env", "ADMIN_ENABLED=true", # Alternative admin flag
		"--env", "DEBUG=*", # Enable debug logging
		"--env", "LOG_LEVEL=debug", # Set log level to debug
		"--env", "SELF_HOSTED=true", # Indicate self-hosted deployment
		"--env", "POSTHOG_API_KEY=" # Disable PostHog analytics.
	)

	Write-Host "Running Firecrawl in $global:firecrawlMode mode with command: $global:startCommand" -ForegroundColor Cyan

	# Execute the command using splatting with the selected start command
	& $global:enginePath run @runOptions $global:imageName $global:startCommand
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
		"--env", "OPENAI_API_KEY=$($global:openaiApiKey)",
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
		& $global:enginePath restart $global:firecrawlName
	}
	"9" = { Test-ImageUpdateAvailable -Engine $global:enginePath -ImageName $global:imageName }
	# Note: "0" action is handled internally by Invoke-MenuLoop
}

# Invoke the Menu Loop
Invoke-MenuLoop -MenuTitle $menuTitle -MenuItems $menuItems -ActionMap $menuActions -ExitChoice "0"
