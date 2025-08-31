################################################################################
# Description  : Script to set up and run the PostgreSQL container with pgvector.
#                Creates a PostgreSQL database with pgvector extension enabled.
#                This database is required for ZEP container operations.
# Usage        : Run as Administrator if using Docker.
################################################################################

using namespace System
using namespace System.IO
using namespace System.Diagnostics.CodeAnalysis

# Dot-source the necessary helper function files.
. "$PSScriptRoot\Setup_Helper_CoreFunctions.ps1"
. "$PSScriptRoot\Setup_Helper_NetworkTests.ps1"
. "$PSScriptRoot\Setup_Helper_ContainerEngine.ps1"
. "$PSScriptRoot\Setup_Helper_BackupRestore.ps1"
. "$PSScriptRoot\Setup_Helper_ContainerManagement.ps1"

# Ensure the script working directory is set.
Set-ScriptLocation

#############################################
# Global Variables
#############################################
# Note: PSAvoidGlobalVars warnings are ignored here as these are used across menu actions.
$global:imageName = "pgvector/pgvector:pg16"
$global:containerName = "zep-db"
$global:volumeName = "zep-db_data"
$global:containerPort = 5433
$global:volumeMountPath = "/var/lib/postgresql/data"
$global:postgresUser = "postgres"
$global:postgresPassword = "postgres"
$global:postgresDb = "zep"
$global:networkName = "podman"
$global:networkAlias = "zep-db"

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
# Function: Get-PostgresContainerConfig
#==============================================================================
<#
.SYNOPSIS
	Gets the current PostgreSQL container configuration, including environment variables.
.DESCRIPTION
	Inspects the PostgreSQL container using the selected engine. Extracts the image name and relevant
	environment variables. Sets up basic environment variables needed for PostgreSQL operation.
	If the container doesn't exist, uses default settings.
.OUTPUTS
	[PSCustomObject] Returns a custom object containing the extracted/updated configuration details
					 (Image, EnvVars) or $null if inspection fails unexpectedly.
.EXAMPLE
	$currentConfig = Get-PostgresContainerConfig
	if ($currentConfig) { Write-Host "Current Image: $($currentConfig.Image)" }
.NOTES
	Uses 'engine inspect'. Modifies the extracted environment variables list.
#>
function Get-PostgresContainerConfig {
	$envVars = @()
	$imageName = $global:imageName # Default image name

	$containerInfo = & $global:enginePath inspect $global:containerName 2>$null | ConvertFrom-Json
	if ($containerInfo) {
		# Container exists, try to preserve existing vars and image name
		$imageName = $containerInfo.Config.Image
		try {
			$envList = @($containerInfo.Config.Env)
			foreach ($env in $envList) {
				# Preserve existing POSTGRES_ vars
				if ($env -match "^(POSTGRES_)") {
					$envVars += $env
				}
			}
		}
		catch {
			Write-Warning "Could not parse existing environment variables: $_"
		}
	}
	else {
		Write-Host "Container '$global:containerName' not found. Using default settings for environment."
	}

	# Set PostgreSQL environment variables if not already present
	$hasUser = $envVars | Where-Object { $_ -match "^POSTGRES_USER=" }
	$hasPassword = $envVars | Where-Object { $_ -match "^POSTGRES_PASSWORD=" }
	$hasDb = $envVars | Where-Object { $_ -match "^POSTGRES_DB=" }

	if (-not $hasUser) {
		$envVars += "POSTGRES_USER=$global:postgresUser"
	}
	if (-not $hasPassword) {
		$envVars += "POSTGRES_PASSWORD=$global:postgresPassword"
	}
	if (-not $hasDb) {
		$envVars += "POSTGRES_DB=$global:postgresDb"
	}

	# Return a custom object
	return [PSCustomObject]@{
		Image   = $imageName
		EnvVars = $envVars
	}
}

#==============================================================================
# Function: Test-PostgresConnectionAndExtension
#==============================================================================
<#
.SYNOPSIS
    Tests connectivity to the PostgreSQL container and ensures the pgvector extension is enabled.
.DESCRIPTION
    Attempts to connect to the PostgreSQL database inside the container and then
    executes SQL commands to create the 'vector' extension if it doesn't exist.
    This function is crucial for ensuring ZEP's database requirements are met.
.PARAMETER ContainerName
    The name of the PostgreSQL container.
.PARAMETER EnginePath
    The path to the container engine executable.
.PARAMETER PostgresUser
    The PostgreSQL username.
.PARAMETER PostgresDb
    The PostgreSQL database name.
.OUTPUTS
    [bool] Returns $true if connection is successful and pgvector extension is confirmed/created,
           $false otherwise.
.EXAMPLE
    Test-PostgresConnectionAndExtension -ContainerName "postgres" -EnginePath $global:enginePath -PostgresUser "postgres" -PostgresDb "zep"
.NOTES
    Requires the 'psql' client to be available inside the PostgreSQL container.
#>
function Test-PostgresConnectionAndExtension {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory=$true)]
        [string]$ContainerName,

        [Parameter(Mandatory=$true)]
        [string]$EnginePath,

        [Parameter(Mandatory=$true)]
        [string]$PostgresUser,

        [Parameter(Mandatory=$true)]
        [string]$PostgresDb
    )

    Write-Host "Testing PostgreSQL connection and pgvector extension..."

    # Test basic connection
    $testConnectionCmd = @(
        "exec", $ContainerName,
        "psql", "-U", $PostgresUser, "-d", $PostgresDb, "-c", "SELECT version();"
    )

    try {
        $connectionResult = & $EnginePath @testConnectionCmd 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Failed to connect to PostgreSQL: $connectionResult"
            return $false
        }
        Write-Host "PostgreSQL connection successful."
    }
    catch {
        Write-Warning "Error testing PostgreSQL connection: $_"
        return $false
    }

    # Check if pgvector extension exists, if not create it
    $checkExtensionCmd = @(
        "exec", $ContainerName,
        "psql", "-U", $PostgresUser, "-d", $PostgresDb, "-t", "-c",
        "SELECT COUNT(*) FROM pg_extension WHERE extname = 'vector';"
    )

    try {
        $extensionResult = & $EnginePath @checkExtensionCmd 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Failed to check pgvector extension: $extensionResult"
            return $false
        }

        $extensionCount = ($extensionResult -replace '\s+', '').Trim()
        if ($extensionCount -eq "0") {
            Write-Host "pgvector extension not found. Installing..."

            # Create pgvector extension
            $createExtensionCmd = @(
                "exec", $ContainerName,
                "psql", "-U", $PostgresUser, "-d", $PostgresDb, "-c",
                "CREATE EXTENSION IF NOT EXISTS vector;"
            )

            $createResult = & $EnginePath @createExtensionCmd 2>&1
            if ($LASTEXITCODE -ne 0) {
                Write-Warning "Failed to create pgvector extension: $createResult"
                return $false
            }
            Write-Host "pgvector extension installed successfully."
        }
        else {
            Write-Host "pgvector extension is already installed."
        }

        return $true
    }
    catch {
        Write-Warning "Error checking/installing pgvector extension: $_"
        return $false
    }
}

#==============================================================================
# Function: Ensure-NetworkExists
#==============================================================================
<#
.SYNOPSIS
    Ensures that the required network exists for container communication.
.DESCRIPTION
    Checks if the specified network exists and creates it if it doesn't.
    This network allows containers to communicate with each other.
.PARAMETER NetworkName
    The name of the network to create/verify.
.PARAMETER EnginePath
    The path to the container engine executable.
.OUTPUTS
    [bool] Returns $true if network exists or is created successfully, $false otherwise.
.EXAMPLE
    Ensure-NetworkExists -NetworkName "zep_network" -EnginePath $global:enginePath
.NOTES
    Uses docker/podman network commands.
#>
function Ensure-NetworkExists {
    [CmdletBinding(SupportsShouldProcess=$true)]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory=$true)]
        [string]$NetworkName,

        [Parameter(Mandatory=$true)]
        [string]$EnginePath
    )

    # Check if network exists
    $networkExists = & $EnginePath network ls --format "{{.Name}}" | Where-Object { $_ -eq $NetworkName }

    if ($networkExists) {
        Write-Host "Network '$NetworkName' already exists."
        return $true
    }

    if ($PSCmdlet.ShouldProcess($NetworkName, "Create Network")) {
        Write-Host "Creating network '$NetworkName'..."
        & $EnginePath network create $NetworkName

        if ($LASTEXITCODE -eq 0) {
            Write-Host "Network '$NetworkName' created successfully."
            return $true
        }
        else {
            Write-Warning "Failed to create network '$NetworkName'."
            return $false
        }
    }

    return $false
}

#==============================================================================
# Function: Start-PostgresContainer
#==============================================================================
<#
.SYNOPSIS
	Starts a new PostgreSQL container with specified configuration.
.DESCRIPTION
	Runs a new container using the selected engine with the specified image.
	Configures standard PostgreSQL settings: detached mode, volume mount for data persistence,
	port mapping, and network attachment. After starting, waits for the database to be ready
	and tests connectivity and pgvector extension installation.
	Supports -WhatIf.
.PARAMETER Image
	The PostgreSQL container image to use. Mandatory.
.PARAMETER EnvVars
	Optional array of environment variables strings.
.OUTPUTS
	[bool] Returns $true if the container starts successfully and tests pass.
		   Returns $false if start fails, tests fail, or action is skipped due to -WhatIf.
.EXAMPLE
	Start-PostgresContainer -Image "postgres:17" -EnvVars @("POSTGRES_DB=zep")
.NOTES
	Relies on Test-TCPPort, Ensure-NetworkExists, and Test-PostgresConnectionAndExtension functions.
#>
function Start-PostgresContainer {
	[CmdletBinding(SupportsShouldProcess = $true)]
	[OutputType([bool])]
	param(
		[Parameter(Mandatory = $true)]
		[string]$Image,

		[Parameter(Mandatory = $false)]
		[array]$EnvVars = @()
	)

	# Ensure network exists
	if (-not (Ensure-NetworkExists -NetworkName $global:networkName -EnginePath $global:enginePath)) {
		Write-Error "Failed to create/verify network. Cannot start container."
		return $false
	}

	# Build the run command
	$runOptions = @(
		"--detach", # Run container in background
		"--publish", "$($global:containerPort):5432", # Map host port 5433 to container's default PostgreSQL port 5432
		"--volume", "$($global:volumeName):$($global:volumeMountPath)", # Mount the named volume for persistent data
		"--name", $global:containerName, # Assign a name to the container
		"--network", $global:networkName, # Connect to the network
		"--network-alias", $global:networkAlias # Set network alias for other containers to connect
	)

	# Add all environment variables
	foreach ($env in $EnvVars) {
		$runOptions += "--env"
		$runOptions += $env
	}

	# Run the container
	if ($PSCmdlet.ShouldProcess($global:containerName, "Start Container with Image '$Image'")) {
		Write-Host "Starting PostgreSQL container with image: $Image"
		& $global:enginePath run @runOptions $Image

		if ($LASTEXITCODE -eq 0) {
			Write-Host "Waiting for PostgreSQL to be ready..."
			Start-Sleep -Seconds 10

			# Wait for PostgreSQL to accept connections (up to 60 seconds)
			$maxWaitTime = 60
			$waitInterval = 5
			$elapsed = 0
			$isReady = $false

			while ($elapsed -lt $maxWaitTime -and -not $isReady) {
				$tcpTest = Test-TCPPort -ComputerName "localhost" -Port $global:containerPort -serviceName $global:containerName
				if ($tcpTest) {
					# Additional wait to ensure database is fully initialized
					Start-Sleep -Seconds 5
					$isReady = $true
				}
				else {
					Start-Sleep -Seconds $waitInterval
					$elapsed += $waitInterval
					Write-Host "Waiting for PostgreSQL to be ready... ($elapsed/$maxWaitTime seconds)"
				}
			}

			if ($isReady) {
				# Test database connection and pgvector extension
				if (Test-PostgresConnectionAndExtension -ContainerName $global:containerName -EnginePath $global:enginePath -PostgresUser $global:postgresUser -PostgresDb $global:postgresDb) {
					Write-Host "PostgreSQL is now running and accessible at localhost:$($global:containerPort)"
					Write-Host "Database: $global:postgresDb"
					Write-Host "User: $global:postgresUser"
					Write-Host "pgvector extension is enabled and ready for use."
					Write-Host "Connection string for ZEP: postgres://$($global:postgresUser):$($global:postgresPassword)@$($global:networkAlias):5432/$($global:postgresDb)?sslmode=disable"
					return $true
				}
				else {
					Write-Warning "PostgreSQL started but extension tests failed. Please check the container logs."
					& $global:enginePath logs --tail 20 $global:containerName
					return $false
				}
			}
			else {
				Write-Warning "PostgreSQL container started but did not become ready within $maxWaitTime seconds."
				& $global:enginePath logs --tail 20 $global:containerName
				return $false
			}
		}
		else {
			Write-Error "Failed to start PostgreSQL container."
			return $false
		}
	}
	else {
		return $false # Action skipped due to -WhatIf
	}
}

#==============================================================================
# Function: Install-PostgresContainer
#==============================================================================
<#
.SYNOPSIS
	Installs and starts the PostgreSQL container with pgvector extension.
.DESCRIPTION
	Checks if the PostgreSQL image exists locally; if not, attempts to restore from backup using
	Test-AndRestoreBackup, falling back to pulling the image using Invoke-PullImage.
	Removes any existing PostgreSQL container using Remove-ContainerAndVolume.
	Gets the container configuration including environment variables.
	Starts the new container using Start-PostgresContainer with the determined image and environment variables.
.EXAMPLE
	Install-PostgresContainer
.NOTES
	Orchestrates image acquisition, cleanup, environment configuration, and container start.
	Relies on Test-AndRestoreBackup, Invoke-PullImage, Remove-ContainerAndVolume,
	Get-PostgresContainerConfig, and Start-PostgresContainer helper functions.
#>
function Install-PostgresContainer {
	Write-Host "IMPORTANT: Using volume '$global:volumeName' - existing database data will be preserved."

	# Check if the PostgreSQL image is already available, restore from backup, or pull new.
	$existingImage = & $global:enginePath images --filter "reference=$($global:imageName)" --format "{{.ID}}"
	if (-not $existingImage) {
		if (-not (Test-AndRestoreBackup -Engine $global:enginePath -ImageName $global:imageName)) {
			Write-Host "No backup restored. Pulling PostgreSQL image '$global:imageName'..."
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
		Write-Host "PostgreSQL image already exists. Skipping pull."
	}

	# Remove any existing container using the shared function
	# Pass container name and volume name. It will prompt about volume removal.
	Remove-ContainerAndVolume -Engine $global:enginePath -ContainerName $global:containerName -VolumeName $global:volumeName

	# Get the configuration (which includes setting default environment variables)
	$config = Get-PostgresContainerConfig

	# Start the container using the global image name and the retrieved config
	Start-PostgresContainer -Image $global:imageName -EnvVars $config.EnvVars
}

#==============================================================================
# Function: Update-PostgresContainer
#==============================================================================
<#
.SYNOPSIS
	Updates the PostgreSQL container to the latest image version while preserving data.
.DESCRIPTION
	Orchestrates the update process:
	1. Gets the current container configuration.
	2. Prompts the user to optionally back up the current container image.
	3. Calls the simplified generic Update-Container function (handles update check, removal, pull).
	4. If core update steps succeed, calls Start-PostgresContainer to start the new container with preserved config.
.EXAMPLE
	Update-PostgresContainer -WhatIf
.NOTES
	Relies on Get-PostgresContainerConfig, Backup-ContainerImage, Update-Container,
	Start-PostgresContainer helper functions.
	User interaction handled via Read-Host for backup confirmation.
#>
function Update-PostgresContainer {
	[CmdletBinding(SupportsShouldProcess = $true)]
	param()

	# Check ShouldProcess before proceeding
	if (-not $PSCmdlet.ShouldProcess($global:containerName, "Update Container")) {
		return
	}

	Write-Host "Initiating update for PostgreSQL..."
	$config = Get-PostgresContainerConfig # Get config before potential removal
	if (-not $config) {
		Write-Error "Cannot update: Failed to get PostgreSQL configuration."
		return # Exit the function if config cannot be read
	}

	# Check if container actually exists before prompting for backup
	$existingContainer = & $global:enginePath ps -a --filter "name=$($global:containerName)" --format "{{.ID}}"
	if ($existingContainer) {
		$createBackup = Read-Host "Create backup before updating? (Y/N, default is Y)"
		if ($createBackup -ne "N") {
			Write-Host "Saving '$global:containerName' Container Image..."
			Backup-ContainerImage -Engine $global:enginePath -ImageName $global:imageName
			Write-Host "Exporting '$($global:volumeName)' Volume..."
			$null = Backup-ContainerVolume -EngineType $global:containerEngine -VolumeName $global:volumeName
		}
	}
	else {
		Write-Warning "Container '$($global:containerName)' not found. Skipping backup prompt."
	}

	# Call simplified Update-Container (handles check, remove, pull)
	# Pass volume name for removal step
	if (Update-Container -Engine $global:enginePath -ContainerName $global:containerName -VolumeName $global:volumeName -ImageName $global:imageName) {
		Write-Host "Core update steps successful. Starting new container..."
		# Start the new container using the config retrieved earlier
		if (-not (Start-PostgresContainer -Image $global:imageName -EnvVars $config.EnvVars)) {
			Write-Error "Failed to start updated PostgreSQL container."
		}
		# Success message is handled within Start-PostgresContainer if successful
	}
	else {
		# Update-Container already wrote a message explaining why it returned false (e.g., no update available).
		# No need to write an error here.
	}
}

################################################################################
# Main Menu Loop using Generic Function
################################################################################

# Define Menu Title and Items
$menuTitle = "PostgreSQL Container & Data Management Menu"
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
	"T" = "Test pgvector Extension"
	"L" = "Show Container Logs"
	"R" = "Restart Container"
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
			-HttpPort $null
	}
	"2" = { Install-PostgresContainer }
	"3" = { Remove-ContainerAndVolume -Engine $global:enginePath -ContainerName $global:containerName -VolumeName $global:volumeName }
	"4" = { Backup-ContainerImage -Engine $global:enginePath -ImageName $global:imageName }
	"5" = { Test-AndRestoreBackup -Engine $global:enginePath -ImageName $global:imageName }
	"6" = { Update-PostgresContainer }
	"7" = { $null = Backup-ContainerVolume -EngineType $global:containerEngine -VolumeName $global:volumeName }
	"8" = {
		$null = Restore-ContainerVolume -EngineType $global:containerEngine -VolumeName $global:volumeName
		& $global:enginePath restart $global:containerName
	}
	"9" = { Test-ImageUpdateAvailable -Engine $global:enginePath -ImageName $global:imageName }
	"T" = { Test-PostgresConnectionAndExtension -ContainerName $global:containerName -EnginePath $global:enginePath -PostgresUser $global:postgresUser -PostgresDb $global:postgresDb }
	"L" = { & $global:enginePath logs --tail 100 $global:containerName }
	"R" = { & $global:enginePath restart $global:containerName }
	# Note: "0" action is handled internally by Invoke-MenuLoop
}

# Invoke the Menu Loop
Invoke-MenuLoop -MenuTitle $menuTitle -MenuItems $menuItems -ActionMap $menuActions -ExitChoice "0"