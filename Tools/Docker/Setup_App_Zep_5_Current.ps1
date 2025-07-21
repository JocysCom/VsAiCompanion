################################################################################
# Description  : Script to set up and run the ZEP container using Docker/Podman.
#                Verifies volume presence, pulls the ZEP image if necessary,
#                and runs the container with port and volume mappings.
#                ZEP is a temporal knowledge graph-based memory layer for AI agents.
# Usage        : Run as Administrator if using Docker.
################################################################################

using namespace System
using namespace System.IO
using namespace System.Diagnostics.CodeAnalysis

# Set Information Preference (commented out as Write-Host is used now)
# $InformationPreference = 'Continue'

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
$global:imageName = "zepai/zep:latest"
$global:containerName = "zep"
$global:volumeName = "zep_data"
$global:containerPort = 8002
$global:volumeMountPath = "/app/data"
$global:zepOpenAiApiKey = "dummy_zep_openai_api_key"
$global:zepApiSecret = "dummy_zep_api_secret"
$global:networkName = "podman"
$global:postgresContainerName = "zep-db"
$global:postgresUser = "postgres"
$global:postgresPassword = "postgres"
$global:postgresDb = "zep"
$global:postgresPort = 5433
$global:postgresInternalPort = 5432

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
# Function: Test-PostgresRequirement
#==============================================================================
<#
.SYNOPSIS
    Tests if PostgreSQL container is available and accessible with required pgvector extension.
.DESCRIPTION
    Checks if the PostgreSQL container is running, accessible on the expected port,
    and has the pgvector extension installed. This is a prerequisite for ZEP operation.
.PARAMETER ContainerName
    The name of the PostgreSQL container to test.
.PARAMETER EnginePath
    The path to the container engine executable.
.PARAMETER PostgresUser
    The PostgreSQL username.
.PARAMETER PostgresDb
    The PostgreSQL database name.
.PARAMETER Port
    The PostgreSQL port number.
.OUTPUTS
    [bool] Returns $true if PostgreSQL is available and ready, $false otherwise.
.EXAMPLE
    Test-PostgresRequirement -ContainerName "postgres" -EnginePath $global:enginePath -PostgresUser "postgres" -PostgresDb "zep" -Port 5432
.NOTES
    This function is essential to ensure ZEP has a working PostgreSQL backend before starting.
#>
function Test-PostgresRequirement {
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
        [string]$PostgresDb,
        
        [Parameter(Mandatory=$true)]
        [int]$Port
    )

    Write-Host "Checking PostgreSQL requirement for ZEP..."
    
    # Check if PostgreSQL container is running
    $containerStatus = & $EnginePath ps --filter "name=$ContainerName" --filter "status=running" --format "{{.Names}}"
    if (-not $containerStatus -or $containerStatus -ne $ContainerName) {
        Write-Warning "PostgreSQL container '$ContainerName' is not running."
        Write-Host "Please run Setup_App_PostgreSQL.ps1 first to install and start PostgreSQL."
        return $false
    }
    
    # Test TCP connectivity
    if (-not (Test-TCPPort -ComputerName "localhost" -Port $Port -serviceName "PostgreSQL")) {
        Write-Warning "PostgreSQL is not accessible on port $Port."
        return $false
    }
    
    # Test database connection and pgvector extension
    try {
        $testConnectionCmd = @(
            "exec", $ContainerName,
            "psql", "-U", $PostgresUser, "-d", $PostgresDb, "-t", "-c",
            "SELECT COUNT(*) FROM pg_extension WHERE extname = 'vector';"
        )
        
        $extensionResult = & $EnginePath @testConnectionCmd 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Failed to check pgvector extension: $extensionResult"
            return $false
        }
        
        $extensionCount = ($extensionResult -replace '\s+', '').Trim()
        if ($extensionCount -eq "0") {
            Write-Warning "pgvector extension is not installed in PostgreSQL database."
            Write-Host "Please run Setup_App_PostgreSQL.ps1 to ensure pgvector extension is installed."
            return $false
        }
        
        Write-Host "PostgreSQL requirement satisfied: Container running, accessible, and pgvector extension installed."
        return $true
    }
    catch {
        Write-Warning "Error checking PostgreSQL requirements: $_"
        return $false
    }
}

#==============================================================================
# Function: Get-ZepContainerConfig
#==============================================================================
<#
.SYNOPSIS
	Gets the current ZEP container configuration, including environment variables.
.DESCRIPTION
	Inspects the ZEP container using the selected engine. Extracts the image name and relevant
	environment variables. Sets up basic environment variables needed for ZEP operation.
	If the container doesn't exist, uses default settings.
.OUTPUTS
	[PSCustomObject] Returns a custom object containing the extracted/updated configuration details
					 (Image, EnvVars) or $null if inspection fails unexpectedly.
.EXAMPLE
	$currentConfig = Get-ZepContainerConfig
	if ($currentConfig) { Write-Host "Current Image: $($currentConfig.Image)" }
.NOTES
	Uses 'engine inspect'. Modifies the extracted environment variables list.
#>
function Get-ZepContainerConfig {
	$envVars = @()
	$imageName = $global:imageName # Default image name

	$containerInfo = & $global:enginePath inspect $global:containerName 2>$null | ConvertFrom-Json
	if ($containerInfo) {
		# Container exists, try to preserve existing vars and image name
		$imageName = $containerInfo.Config.Image
		try {
			$envList = @($containerInfo.Config.Env)
			foreach ($env in $envList) {
				# Preserve existing ZEP_ vars and common environment variables
				if ($env -match "^(ZEP_|POSTGRES_|DATABASE_|API_)" `
						-and $env -notmatch "^ZEP_DEVELOPMENT=") {
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

	# Set basic ZEP environment variables
	$envVars += "ZEP_DEVELOPMENT=false"
	$envVars += "ZEP_LOG_LEVEL=debug"
	
	# Note: PostgreSQL configuration is handled via zep.yaml config file
	# No need to set ZEP_STORE_TYPE, ZEP_DATABASE__URL, or ZEP_STORE_POSTGRES_DSN environment variables
	
	# Use consistent ZEP API key from global variable (required by ZEP)
	$apiKeyExists = $false
	foreach ($env in $envVars) {
		if ($env -match "^(ZEP_OPENAI_API_KEY)=") {
			$apiKeyExists = $true
			break
		}
	}
	if (-not $apiKeyExists) {
		$envVars += "ZEP_OPENAI_API_KEY=$global:zepOpenAiApiKey"
	}

	# Set ZEP authentication variables (required for Zep to start)
	$authRequiredExists = $false
	$authSecretExists = $false
	foreach ($env in $envVars) {
		if ($env -match "^ZEP_AUTH_REQUIRED=") {
			$authRequiredExists = $true
		}
		if ($env -match "^ZEP_AUTH_SECRET=") {
			$authSecretExists = $true
		}
	}
	if (-not $authRequiredExists) {
		$envVars += "ZEP_AUTH_REQUIRED=true"
	}
	if (-not $authSecretExists) {
		$envVars += "ZEP_AUTH_SECRET=$global:zepApiSecret"
	}

	# Set ZEP_CONFIG_FILE to point to the mounted config file
	$configFileExists = $false
	foreach ($env in $envVars) {
		if ($env -match "^ZEP_CONFIG_FILE=") {
			$configFileExists = $true
			break
		}
	}
	if (-not $configFileExists) {
		$envVars += "ZEP_CONFIG_FILE=/app/config.yaml"
	}

	# Return a custom object
	return [PSCustomObject]@{
		Image   = $imageName
		EnvVars = $envVars
	}
}

#==============================================================================
# Function: Start-ZepContainer
#==============================================================================
<#
.SYNOPSIS
	Starts a new ZEP container with specified configuration.
.DESCRIPTION
	Runs a new container using the selected engine with the specified image.
	Configures standard ZEP settings: detached mode, name 'zep', mounts 'zep_data' volume
	to '/app/data', and maps host port 8002 to container port 8002.
	Applies any additional environment variables provided via the EnvVars parameter.
	After starting, waits 30 seconds and performs TCP and HTTP connectivity tests.
	Supports -WhatIf.
.PARAMETER Image
	The ZEP container image to use (e.g., 'zepai/zep:latest'). Mandatory.
.PARAMETER EnvVars
	Optional array of environment variables strings (e.g., @("ZEP_LOG_LEVEL=debug")).
.OUTPUTS
	[bool] Returns $true if the container starts successfully and connectivity tests pass.
		   Returns $false if start fails, tests fail, or action is skipped due to -WhatIf.
.EXAMPLE
	Start-ZepContainer -Image "zepai/zep:latest" -EnvVars @("ZEP_LOG_LEVEL=debug")
.NOTES
	Relies on Test-TCPPort and Test-HTTPPort helper functions.
#>
function Start-ZepContainer {
	[CmdletBinding(SupportsShouldProcess = $true)]
	[OutputType([bool])]
	param(
		[Parameter(Mandatory = $true)]
		[string]$Image,

		[Parameter(Mandatory = $false)]
		[array]$EnvVars = @()
	)

	# Get the host's IP as seen by Podman/WSL2
	$HostIpForContainer = (podman machine ssh "grep nameserver /etc/resolv.conf | cut -d' ' -f2").Trim()
	if (-not [string]::IsNullOrWhiteSpace($HostIpForContainer)) {
		Write-Host "Host IP for container: $HostIpForContainer"
	} else {
		Write-Error "Could not determine host IP for container."
	}
	
	# Get the PostgreSQL container's IP address dynamically
	$PostgresIp = (podman machine ssh "sudo podman inspect $($global:postgresContainerName) --format '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}'").Trim()
	if (-not [string]::IsNullOrWhiteSpace($PostgresIp)) {
		Write-Host "PostgreSQL container IP: $PostgresIp"
	} else {
		Write-Warning "Could not determine PostgreSQL container IP. Ensure $($global:postgresContainerName) is running."
	}
	
	# Create zep.yaml inside Podman VM line by line with LF endings
	podman machine ssh "sudo echo 'api_secret: $($global:zepApiSecret)' > /root/zep.yaml"
	podman machine ssh "sudo echo 'store:' >> /root/zep.yaml"
	podman machine ssh "sudo echo '  type: postgres' >> /root/zep.yaml"
	podman machine ssh "sudo echo '  postgres:' >> /root/zep.yaml"
	podman machine ssh "sudo echo '    user: $($global:postgresUser)' >> /root/zep.yaml"
	podman machine ssh "sudo echo '    password: $($global:postgresPassword)' >> /root/zep.yaml"
	podman machine ssh "sudo echo '    host: $($global:postgresContainerName)' >> /root/zep.yaml"
	podman machine ssh "sudo echo '    port: $($global:postgresInternalPort)' >> /root/zep.yaml"
	podman machine ssh "sudo echo '    database: $($global:postgresDb)' >> /root/zep.yaml"
	podman machine ssh "sudo echo '    sslmode: disable' >> /root/zep.yaml"
	# Display the created zep.yaml for verification	
	Write-Host "-------------------------------------------"
	Write-Host "/root/zep.yaml:/app/config.yaml"
	Write-Host "-------------------------------------------"
	podman machine ssh "sudo cat /root/zep.yaml"
	Write-Host "-------------------------------------------"

	# Build the run command
	$runOptions = @(
		"--add-host", "host.local:$HostIpForContainer",
		"--env", "TZ=Europe/London",
		"--detach", # Run container in background.
		"--publish", "$($global:containerPort):8000", # Map host port 8002 to container port 8000.
		"--volume", "$($global:volumeName):$($global:volumeMountPath)", # Mount the named volume for persistent data.
		"--volume", "/root/zep.yaml:/app/config.yaml", # Mount zep.yaml from VM path
		"--name", $global:containerName, # Assign a name to the container.
		"--network", $global:networkName # Connect to the same network as PostgreSQL
	)

	# Add PostgreSQL hostname mapping if IP was retrieved successfully
	if (-not [string]::IsNullOrWhiteSpace($PostgresIp)) {
		$runOptions += "--add-host"
		$runOptions += "zep-db:$PostgresIp"
		Write-Host "Adding host mapping: zep-db -> $PostgresIp"
	}

	# Add all environment variables
	foreach ($env in $EnvVars) {
		$runOptions += "--env"
		$runOptions += $env
	}

	# Run the container
	if ($PSCmdlet.ShouldProcess($global:containerName, "Start Container with Image '$Image'")) {
		Write-Host "Starting ZEP container with image: $Image"
		Write-Host "& $global:enginePath machine ssh sudo $global:containerEngine run $runOptions $Image"
		& $global:enginePath machine ssh sudo $global:containerEngine run $runOptions $Image

		if ($LASTEXITCODE -eq 0) {
			Write-Host "Waiting for container startup..."
			Start-Sleep -Seconds 30

			# Test connectivity
			$tcpTest = Test-TCPPort -ComputerName "localhost" -Port $global:containerPort -serviceName $global:containerName
			$httpTest = Test-HTTPPort -Uri "http://localhost:$($global:containerPort)" -serviceName $global:containerName

			if ($tcpTest -and $httpTest) {
				Write-Host "ZEP is now running and accessible at http://localhost:$($global:containerPort)"
				Write-Host "If accessing from another container, use 'http://host.docker.internal:$($global:containerPort)' as the URL."
				return $true
			}
			else {
				Write-Warning "ZEP container started but connectivity tests failed. Please check the container logs."
				& $global:enginePath logs --tail $Lines 100
				return $false
			}
		}
		else {
			Write-Error "Failed to start ZEP container."
			return $false
		}
	}
	else {
		return $false # Action skipped due to -WhatIf
	}
}

#==============================================================================
# Function: Install-ZepContainer
#==============================================================================
<#
.SYNOPSIS
	Installs and starts the ZEP container.
.DESCRIPTION
	Checks if the ZEP image exists locally; if not, attempts to restore from backup using
	Test-AndRestoreBackup, falling back to pulling the image using Invoke-PullImage.
	Removes any existing 'zep' container using Remove-ContainerAndVolume.
	Gets the container configuration including environment variables.
	Starts the new container using Start-ZepContainer with the determined image and environment variables.
.EXAMPLE
	Install-ZepContainer
.NOTES
	Orchestrates image acquisition, cleanup, environment configuration, and container start.
	Relies on Test-AndRestoreBackup, Invoke-PullImage, Remove-ContainerAndVolume, 
	Get-ZepContainerConfig, and Start-ZepContainer helper functions.
#>
function Install-ZepContainer {
	Write-Host "IMPORTANT: Using volume '$global:volumeName' - existing user data will be preserved."

	# Test PostgreSQL requirement before proceeding
	if (-not (Test-PostgresRequirement -ContainerName $global:postgresContainerName -EnginePath $global:enginePath -PostgresUser $global:postgresUser -PostgresDb $global:postgresDb -Port $global:postgresPort)) {
		Write-Error "PostgreSQL requirement not met. ZEP installation cannot proceed."
		Write-Host "Please run Setup_App_PostgreSQL.ps1 first to install and configure PostgreSQL with pgvector extension."
		return
	}

	# Prompt for OpenAI API Key if not set
	$global:zepOpenAiApiKey = Get-EnvironmentVariableWithDefault -EnvVarName 'ZEP_OPENAI_API_KEY' -DefaultValue $global:zepOpenAiApiKey -PromptText 'OpenAI API Key for ZEP'

	# Check if the ZEP image is already available, restore from backup, or pull new.
	$existingImage = & $global:enginePath images --filter "reference=$($global:imageName)" --format "{{.ID}}"
	if (-not $existingImage) {
		if (-not (Test-AndRestoreBackup -Engine $global:enginePath -ImageName $global:imageName)) {
			Write-Host "No backup restored. Pulling ZEP image '$global:imageName'..."
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
		Write-Host "ZEP image already exists. Skipping pull."
	}

	# Remove any existing container using the shared function
	# Pass container name and volume name. It will prompt about volume removal.
	Remove-ContainerAndVolume -Engine $global:enginePath -ContainerName $global:containerName -VolumeName $global:volumeName

	# Get the configuration (which includes setting default environment variables)
	$config = Get-ZepContainerConfig

	# Start the container using the global image name and the retrieved config
	Start-ZepContainer -Image $global:imageName -EnvVars $config.EnvVars
}

#==============================================================================
# Function: Update-ZepContainer
#==============================================================================
<#
.SYNOPSIS
	Updates the ZEP container to the latest image version while preserving data.
.DESCRIPTION
	Orchestrates the update process:
	1. Gets the current container configuration.
	2. Prompts the user to optionally back up the current container image.
	3. Calls the simplified generic Update-Container function (handles update check, removal, pull).
	4. If core update steps succeed, calls Start-ZepContainer to start the new container with preserved config.
.EXAMPLE
	Update-ZepContainer -WhatIf
.NOTES
	Relies on Get-ZepContainerConfig, Backup-ContainerImage, Update-Container,
	Start-ZepContainer helper functions.
	User interaction handled via Read-Host for backup confirmation.
#>
function Update-ZepContainer {
	[CmdletBinding(SupportsShouldProcess = $true)]
	param()

	# Check ShouldProcess before proceeding
	if (-not $PSCmdlet.ShouldProcess($global:containerName, "Update Container")) {
		return
	}

	Write-Host "Initiating update for ZEP..."
	$config = Get-ZepContainerConfig # Get config before potential removal
	if (-not $config) {
		Write-Error "Cannot update: Failed to get ZEP configuration."
		return # Exit the function if config cannot be read
	}

	# Check if container actually exists before prompting for backup
	$existingContainer = & $global:enginePath ps -a --filter "name=$($global:containerName)" --format "{{.ID}}"
	if ($existingContainer) {
		$createBackup = Read-Host "Create backup before updating? (Y/N, default is Y)"
		if ($createBackup -ne "N") {
			Write-Host "Saving '$global:containerName' Container Image..."
			Backup-ContainerImage -Engine $global:enginePath -ContainerName $global:containerName
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
		if (-not (Start-ZepContainer -Image $global:imageName -EnvVars $config.EnvVars)) {
			Write-Error "Failed to start updated ZEP container."
		}
		# Success message is handled within Start-ZepContainer if successful
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
$menuTitle = "ZEP Container & Data Management Menu"
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
	"P" = "Test PostgreSQL Requirement"
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
			-HttpPort $global:containerPort
	}
	"2" = { Install-ZepContainer }
	"3" = { Remove-ContainerAndVolume -Engine $global:enginePath -ContainerName $global:containerName -VolumeName $global:volumeName }
	"4" = { Backup-ContainerImage -Engine $global:enginePath -ContainerName $global:containerName }
	"5" = { Restore-ContainerImage -Engine $global:enginePath -ContainerName $global:containerName }
	"6" = { Update-ZepContainer }
	"7" = { $null = Backup-ContainerVolume -EngineType $global:containerEngine -VolumeName $global:volumeName }
	"8" = {
		$null = Restore-ContainerVolume -EngineType $global:containerEngine -VolumeName $global:volumeName
		& $global:enginePath restart $global:containerName
	}
	"9" = { Test-ImageUpdateAvailable -Engine $global:enginePath -ImageName $global:imageName }
	"P" = { Test-PostgresRequirement -ContainerName $global:postgresContainerName -EnginePath $global:enginePath -PostgresUser $global:postgresUser -PostgresDb $global:postgresDb -Port $global:postgresPort }
	"L" = { & $global:enginePath logs --tail 100 $global:containerName }
	"R" = { & $global:enginePath restart $global:containerName }
	# Note: "0" action is handled internally by Invoke-MenuLoop
}

# Invoke the Menu Loop
Invoke-MenuLoop -MenuTitle $menuTitle -MenuItems $menuItems -ActionMap $menuActions -ExitChoice "0"