################################################################################
# Description  : Script to set up and run a dedicated Redis container for Firecrawl.
#                Creates a Docker network and launches Redis with proper configuration
#                for Firecrawl integration.
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
$global:containerName = "firecrawl-redis"

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
$manifestConfig = $manifest.resources.'firecrawl-redis'

# Create consolidated configuration object
$config = [PSCustomObject]@{
	imageName = $manifestConfig.properties.image
	networkName = $manifestConfig.properties.networks[0].name
	volumeName = $manifestConfig.properties.volumes[0].name
	containerPort = $manifestConfig.properties.bindings[0].containerPort
	hostPort = $manifestConfig.properties.bindings[0].hostPort
	networkAlias = $manifestConfig.properties.networks[0].alias
	dataPath = $manifestConfig.properties.volumes[0].containerPath
	restartPolicy = $manifestConfig.properties.restart
	environment = $manifestConfig.properties.environment
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
# Function: Install-FirecrawlRedisContainer
#==============================================================================
<#
.SYNOPSIS
	Installs the dedicated Redis container for Firecrawl.
.DESCRIPTION
	Performs the following steps:
	1. Ensures the 'firecrawl-net' network exists.
	2. Pulls the Redis Alpine image if not available locally.
	3. Removes any existing 'firecrawl-redis' container.
	4. Runs the Redis container with proper network configuration.
	5. Tests connectivity to ensure Redis is working properly.
.EXAMPLE
	Install-FirecrawlRedisContainer
.NOTES
	This function sets up Redis specifically for Firecrawl integration.
	Uses Write-Host for status messages.
#>
function Install-FirecrawlRedisContainer {
	#############################################
	# Step 1: Ensure Network Exists
	#############################################
	if (-not (Confirm-ContainerResource -Engine $global:enginePath -ResourceType "network" -ResourceName $config.networkName)) {
		Write-Error "Failed to ensure network '$($config.networkName)' exists. Exiting..."
		exit 1
	}

	#############################################
	# Step 2: Pull Redis Image (or Restore)
	#############################################
	$existingImage = & $global:enginePath images --filter "reference=$($config.imageName)" --format "{{.ID}}"
	if (-not $existingImage) {
		if (-not (Test-AndRestoreBackup -Engine $global:enginePath -ImageName $config.imageName)) {
			Write-Host "No backup restored. Pulling Redis image '$($config.imageName)'..."
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
		Write-Host "Redis image already exists. Skipping pull."
	}

	#############################################
	# Step 3: Remove Existing Container (if any)
	#############################################
	$existingContainer = & $global:enginePath ps --all --filter "name=^$global:containerName$" --format "{{.ID}}"
	if ($existingContainer) {
		Write-Host "Removing existing Redis container '$global:containerName'..."
		& $global:enginePath rm --force $global:containerName
	}

	#############################################
	# Step 4: Ensure Volume Exists and Run Redis Container
	#############################################
	# Ensure the volume exists
	if (-not (Confirm-ContainerResource -Engine $global:enginePath -ResourceType "volume" -ResourceName $config.volumeName)) {
		Write-Error "Failed to ensure volume '$($config.volumeName)' exists. Exiting..."
		exit 1
	}
	Write-Host "IMPORTANT: Using volume '$($config.volumeName)' - existing Redis data will be preserved."

	Write-Host "Starting Redis container '$global:containerName' on network '$($config.networkName)'..."

	# Define run options as an array
	$runOptions = @(
		"--detach", # Run container in background.
		"--name", $global:containerName, # Assign a name to the container.
		"--network", $config.networkName, # Connect container to the specified network.
		"--network-alias", $config.networkAlias, # Assign a unique alias for use within the network.
		"--publish", "$($config.hostPort):$($config.containerPort)", # Map host port to container port.
		"--volume", "$($config.volumeName):$($config.dataPath)", # Mount the named volume for persistent Redis data.
		"--restart", $config.restartPolicy # Restart policy from manifest
	)

	# Add environment variables from manifest
	if ($config.environment) {
		foreach ($key in $config.environment.PSObject.Properties.Name) {
			$runOptions += "--env"
			$runOptions += "$key=$($config.environment.$key)"
		}
	}

	# Execute the command using splatting
	& $global:enginePath run @runOptions $config.imageName
	if ($LASTEXITCODE -ne 0) {
		Write-Error "Failed to start Redis container '$global:containerName'."
		exit 1
	}

	#############################################
	# Step 5: Wait and Test Connectivity
	#############################################
	Write-Host "Waiting 10 seconds for Redis container to initialize..."
	Start-Sleep -Seconds 10

	Write-Host "Testing Redis container connectivity on port $($config.containerPort)..."
	if (Test-TCPPort -ComputerName "localhost" -Port $config.hostPort -serviceName "Firecrawl Redis") {
		Write-Host "Redis is now running and accessible at localhost:$($config.hostPort)"
		Write-Host "Network alias '$($config.networkAlias)' is available for other containers on the '$($config.networkName)' network."
	}
	else {
		Write-Error "Redis connectivity test failed. Please check the container logs."
		exit 1
	}
}

#==============================================================================
# Function: Update-FirecrawlRedisContainer
#==============================================================================
<#
.SYNOPSIS
	Updates the Redis container to the latest image version.
.DESCRIPTION
	Orchestrates the update process:
	1. Prompts the user to optionally back up the current container image.
	2. Calls the generic Update-Container function (handles update check, removal, pull).
	3. If core update steps succeed, starts the new container.
	4. Offers to restore from backup if the start fails (and a backup was made).
.EXAMPLE
	Update-FirecrawlRedisContainer -WhatIf
.NOTES
	Uses the generic update workflow for consistency.
	User interaction handled via Read-Host for backup confirmation.
#>
function Update-FirecrawlRedisContainer {
	[CmdletBinding(SupportsShouldProcess = $true)]
	param()

	# Check ShouldProcess before proceeding
	if (-not $PSCmdlet.ShouldProcess($global:containerName, "Update Container")) {
		return
	}


	Write-Host "Initiating update for Firecrawl Redis..."
	$backupMade = $false

	# Check if container exists before prompting for backup
	$existingContainer = & $global:enginePath ps -a --filter "name=$($global:containerName)" --format "{{.ID}}"
	if ($existingContainer) {
		$createBackup = Read-Host "Create backup before updating? (Y/N, default is Y)"
		if ($createBackup -ne "N") {
			Write-Host "Saving '$($global:containerName)' Container Image..."
			Backup-ContainerImage -Engine $global:enginePath -ImageName $global:imageName
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
			Install-FirecrawlRedisContainer
		}
		catch {
			Write-Error "Failed to start updated Redis container: $($_.Exception.Message)"
			if ($backupMade) {
				$restore = Read-Host "Would you like to restore from backup? (Y/N, default is Y)"
				if ($restore -ne "N") {
					Write-Host "Loading '$($global:containerName)' Container Image..."
					Test-AndRestoreBackup -Engine $global:enginePath -ImageName $global:imageName
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
				Test-AndRestoreBackup -Engine $global:enginePath -ImageName $global:imageName
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
$menuTitle = "Firecrawl Redis Container Menu"
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
			-DisplayName "Firecrawl Redis" `
			-TcpPort $config.hostPort `
			-DelaySeconds 3
	}
	"2" = { Install-FirecrawlRedisContainer }
	"3" = {
		Remove-ContainerAndVolume -Engine $global:enginePath -ContainerName $global:containerName -VolumeName $config.volumeName
	}
	"4" = { Backup-ContainerImage -Engine $global:enginePath -ImageName $global:imageName }
	"5" = { Test-AndRestoreBackup -Engine $global:enginePath -ImageName $global:imageName }
	"6" = { Update-FirecrawlRedisContainer }
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
