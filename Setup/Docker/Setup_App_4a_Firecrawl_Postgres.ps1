################################################################################
# Description  : Script to set up and run a dedicated PostgreSQL database container for Firecrawl.
#                Creates a Docker network and launches PostgreSQL with proper configuration
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
$global:containerName = "firecrawl-postgres"

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

# PostgreSQL configuration for Firecrawl
$config = [PSCustomObject]@{
	imageName = "postgres:16-alpine"
	networkName = "firecrawl-net"
	volumeName = "firecrawl-postgres-data"
	containerPort = 5432
	networkAlias = "firecrawl-postgres"
	dataPath = "/var/lib/postgresql/data"
	restartPolicy = "always"
	databaseName = "firecrawl"
	databaseUser = "firecrawl"
	databasePassword = "change-me"
	memoryLimit = "512m"
}

Write-Host "PostgreSQL configuration for Firecrawl:"
foreach ($property in $config.PSObject.Properties) {
	$name = $property.Name
	$value = $property.Value
	if ($null -eq $value -or ($value -is [string] -and [string]::IsNullOrEmpty($value))) {
		Write-Error "Configuration property '$name' is missing or empty."
		exit 1
	}
	Write-Host "  $($name): $value"
}

#==============================================================================
# Function: Install-FirecrawlPostgresContainer
#==============================================================================
<#
.SYNOPSIS
	Installs the dedicated PostgreSQL database container for Firecrawl.
.DESCRIPTION
	Performs the following steps:
	1. Ensures the 'firecrawl-net' network exists.
	2. Pulls the PostgreSQL Alpine image if not available locally.
	3. Removes any existing 'firecrawl-postgres' container.
	4. Runs the PostgreSQL container with proper network configuration.
	5. Tests connectivity to ensure PostgreSQL is working properly.
.EXAMPLE
	Install-FirecrawlPostgresContainer
.NOTES
	This function sets up PostgreSQL specifically for Firecrawl integration.
	Uses Write-Host for status messages.
#>
function Install-FirecrawlPostgresContainer {
	#############################################
	# Step 1: Ensure Network Exists
	#############################################
	if (-not (Confirm-ContainerResource -Engine $global:enginePath -ResourceType "network" -ResourceName $config.networkName)) {
		Write-Error "Failed to ensure network '$($config.networkName)' exists. Exiting..."
		exit 1
	}

	#############################################
	# Step 2: Pull PostgreSQL Image (or Restore)
	#############################################
	$existingImage = & $global:enginePath images --filter "reference=$($config.imageName)" --format "{{.ID}}"
	if (-not $existingImage) {
		if (-not (Test-AndRestoreBackup -Engine $global:enginePath -ImageName $config.imageName)) {
			Write-Host "No backup restored. Pulling PostgreSQL image '$($config.imageName)'..."
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
		Write-Host "PostgreSQL image already exists. Skipping pull."
	}

	#############################################
	# Step 3: Remove Existing Container (if any)
	#############################################
	$existingContainer = & $global:enginePath ps --all --filter "name=^$global:containerName$" --format "{{.ID}}"
	if ($existingContainer) {
		Write-Host "Removing existing PostgreSQL container '$global:containerName'..."
		& $global:enginePath rm --force $global:containerName
	}

	#############################################
	# Step 4: Ensure Volume Exists and Run PostgreSQL Container
	#############################################
	# Ensure the volume exists
	if (-not (Confirm-ContainerResource -Engine $global:enginePath -ResourceType "volume" -ResourceName $config.volumeName)) {
		Write-Error "Failed to ensure volume '$($config.volumeName)' exists. Exiting..."
		exit 1
	}
	Write-Host "IMPORTANT: Using volume '$($config.volumeName)' - existing PostgreSQL data will be preserved."

	Write-Host "Starting PostgreSQL container '$global:containerName' on network '$($config.networkName)'..."

	# Define run options as an array
	$runOptions = @(
		"--detach", # Run container in background.
		"--name", $global:containerName, # Assign a name to the container.
		"--network", $config.networkName, # Connect container to the specified network.
		"--network-alias", $config.networkAlias, # Assign a unique alias for use within the network.
		"--volume", "$($config.volumeName):$($config.dataPath)", # Mount the named volume for persistent PostgreSQL data.
		"--restart", $config.restartPolicy, # Restart policy
		"--memory", $config.memoryLimit, # Memory limit
		"--env", "POSTGRES_DB=$($config.databaseName)", # Database name
		"--env", "POSTGRES_USER=$($config.databaseUser)", # Database user
		"--env", "POSTGRES_PASSWORD=$($config.databasePassword)", # Database password
		"--env", "PGDATA=$($config.dataPath)" # PostgreSQL data directory
	)

	# Execute the command using splatting
	& $global:enginePath run @runOptions $config.imageName
	if ($LASTEXITCODE -ne 0) {
		Write-Error "Failed to start PostgreSQL container '$global:containerName'."
		exit 1
	}

	#############################################
	# Step 5: Wait and Test Connectivity
	#############################################
	Write-Host "Waiting for PostgreSQL container to initialize (this may take up to 60 seconds)..."

	# PostgreSQL needs more time to initialize than Redis
	$maxWait = 60
	$waitInterval = 5
	$elapsed = 0
	$isReady = $false

	while ($elapsed -lt $maxWait -and -not $isReady) {
		Start-Sleep -Seconds $waitInterval
		$elapsed += $waitInterval

		# Test if PostgreSQL is ready using pg_isready
		$null = & $global:enginePath exec $global:containerName pg_isready -U $config.databaseUser -d $config.databaseName 2>$null
		if ($LASTEXITCODE -eq 0) {
			$isReady = $true
			Write-Host ""
			Write-Host "PostgreSQL is ready and accepting connections!"
		}
		else {
			Write-Host "." -NoNewline
		}
	}

	$connectionString = "postgresql://$($config.databaseUser):$($config.databasePassword)@$($config.networkAlias):$($config.containerPort)/$($config.databaseName)"

	if (-not $isReady) {
		Write-Host ""
		Write-Warning "PostgreSQL may still be initializing. Check container logs if needed:"
		Write-Host "$global:enginePath logs $global:containerName"
	}else{

		$sqlScriptName = "$(Split-Path -Leaf $PSCommandPath).sql"

		Write-Host "Copy $sqlScriptName into the running $($global:containerName) container"
		& $global:enginePath cp "$sqlScriptName" "$($global:containerName):/tmp/$sqlScriptName"


		Write-Host "Execute $sqlScriptName inside the container."
		$null = & $global:enginePath exec -e PGPASSWORD="$($config.databasePassword)" -i "$($global:containerName)" `
			psql -U "$($config.databaseUser)" -d "$($config.databaseName)" -v ON_ERROR_STOP=1 -f "/tmp/$sqlScriptName"

		Write-Host "Verify NuQ"
		& $global:enginePath exec "$($global:containerName)" `
			psql -U "$($config.databaseUser)" -d "$($config.databaseName)" -c "\dt nuq.*"


		# Uses stdin with -f -  (psql treats "-" as "read from STDIN")
		#$null = & $global:enginePath exec -i firecrawl-postgres `
		#  psql $connectionString `
		#  -v ON_ERROR_STOP=1 -f - < $sqlScriptName

	}

	Write-Host "PostgreSQL database '$($config.databaseName)' is accessible via network alias '$($config.networkAlias)' on port $($config.containerPort)."
	Write-Host "Connection string: $connectionString"
}

#==============================================================================
# Function: Update-FirecrawlPostgresContainer
#==============================================================================
<#
.SYNOPSIS
	Updates the PostgreSQL container to the latest image version.
.DESCRIPTION
	Orchestrates the update process:
	1. Prompts the user to optionally back up the current container image.
	2. Calls the generic Update-Container function (handles update check, removal, pull).
	3. If core update steps succeed, starts the new container.
	4. Offers to restore from backup if the start fails (and a backup was made).
.EXAMPLE
	Update-FirecrawlPostgresContainer -WhatIf
.NOTES
	Uses the generic update workflow for consistency.
	User interaction handled via Read-Host for backup confirmation.
#>
function Update-FirecrawlPostgresContainer {
	[CmdletBinding(SupportsShouldProcess = $true)]
	param()

	# Check ShouldProcess before proceeding
	if (-not $PSCmdlet.ShouldProcess($global:containerName, "Update Container")) {
		return
	}

	Write-Host "Initiating update for Firecrawl PostgreSQL..."
	$backupMade = $false

	# Check if container exists before prompting for backup
	$existingContainer = & $global:enginePath ps -a --filter "name=$($global:containerName)" --format "{{.ID}}"
	if ($existingContainer) {
		$createBackup = Read-Host "Create backup before updating? (Y/N, default is Y)"
		if ($createBackup -ne "N") {
			Write-Host "Saving '$($global:containerName)' Container Image..."
			Backup-ContainerImage -Engine $global:enginePath -ImageName $config.imageName
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
			Install-FirecrawlPostgresContainer
		}
		catch {
			Write-Error "Failed to start updated PostgreSQL container: $($_.Exception.Message)"
			if ($backupMade) {
				$restore = Read-Host "Would you like to restore from backup? (Y/N, default is Y)"
				if ($restore -ne "N") {
					Write-Host "Loading '$($global:containerName)' Container Image..."
					Test-AndRestoreBackup -Engine $global:enginePath -ImageName $config.imageName
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
				Test-AndRestoreBackup -Engine $global:enginePath -ImageName $config.imageName
				Write-Host "Importing '$($config.volumeName)' Volume..."
				$null = Restore-ContainerVolume -EngineType $global:containerEngine -VolumeName $config.volumeName
			}
		}
	}
}

#==============================================================================
# Function: Test-FirecrawlPostgresConnection
#==============================================================================
<#
.SYNOPSIS
	Tests the connection to the Firecrawl PostgreSQL database.
.DESCRIPTION
	Verifies that the PostgreSQL database is running and accessible by executing
	a simple connection test using pg_isready within the container.
.EXAMPLE
	Test-FirecrawlPostgresConnection
.NOTES
	This function provides detailed connection status for troubleshooting.
#>
function Test-FirecrawlPostgresConnection {
	Write-Host "Testing Firecrawl PostgreSQL connection..." -ForegroundColor Yellow

	# Check if container is running
	$containerRunning = & $global:enginePath ps --filter "name=^$global:containerName$" --format "{{.Names}}"
	if ($containerRunning -ne $global:containerName) {
		Write-Host "❌ Container '$global:containerName' is not running" -ForegroundColor Red
		return $false
	}

	# Test PostgreSQL readiness
	Write-Host "Testing database readiness..." -NoNewline
	$null = & $global:enginePath exec $global:containerName pg_isready -U $config.databaseUser -d $config.databaseName 2>$null
	if ($LASTEXITCODE -eq 0) {
		Write-Host " ✅ PostgreSQL is ready" -ForegroundColor Green

		# Test actual connection
		Write-Host "Testing database connection..." -NoNewline
		$null = & $global:enginePath exec $global:containerName psql -U $config.databaseUser -d $config.databaseName -c "SELECT 1;" 2>$null
		if ($LASTEXITCODE -eq 0) {
			Write-Host " ✅ Connection successful" -ForegroundColor Green
			Write-Host ""
			Write-Host "📊 Database Information:" -ForegroundColor Cyan
			Write-Host "  Database: $($config.databaseName)"
			Write-Host "  User: $($config.databaseUser)"
			Write-Host "  Network Alias: $($config.networkAlias)"
			Write-Host "  Port: $($config.containerPort)"
			Write-Host "  Connection String: postgresql://$($config.databaseUser):$($config.databasePassword)@$($config.networkAlias):$($config.containerPort)/$($config.databaseName)"
			return $true
		}
		else {
			Write-Host " ❌ Connection failed" -ForegroundColor Red
			return $false
		}
	}
	else {
		Write-Host " ❌ PostgreSQL not ready" -ForegroundColor Red
		return $false
	}
}

################################################################################
# Main Menu Loop using Generic Function
################################################################################

# Define Menu Title and Items
$menuTitle = "Firecrawl PostgreSQL Database Menu"
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
			-DisplayName "Firecrawl PostgreSQL" `
			-DelaySeconds 3
		Write-Host ""
		Test-FirecrawlPostgresConnection
	}
	"2" = { Install-FirecrawlPostgresContainer }
	"3" = {
		Remove-ContainerAndVolume -Engine $global:enginePath -ContainerName $global:containerName -VolumeName $config.volumeName
	}
	"4" = { Backup-ContainerImage -Engine $global:enginePath -ImageName $config.imageName }
	"5" = { Test-AndRestoreBackup -Engine $global:enginePath -ImageName $config.imageName }
	"6" = { Update-FirecrawlPostgresContainer }
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