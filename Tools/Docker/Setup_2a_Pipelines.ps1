################################################################################
# File         : Setup_2a_Pipelines.ps1
# Description  : Script to set up, update, backup, restore, uninstall the Pipelines
#                container using Docker or Podman. This version installs Pipelines
#                from scratch by cloning the repository (without converting LF to CRLF)
#                and running the container with the default configuration.
# Usage        : Run as Administrator if using Docker.
################################################################################

using namespace System
using namespace System.IO

# Dot-source the necessary helper function files.
. "$PSScriptRoot\Setup_0_Core.ps1"
. "$PSScriptRoot\Setup_0_Network.ps1"
. "$PSScriptRoot\Setup_0_ContainerEngine.ps1"
. "$PSScriptRoot\Setup_0_BackupRestore.ps1"
. "$PSScriptRoot\Setup_0_ContainerMgmt.ps1"

# Ensure the working directory is set.
Set-ScriptLocation

# Global variables used across functions.
# Note: PSAvoidGlobalVars warnings are ignored here as these are used across menu actions.
$global:containerName = "pipelines"
$global:volumeName = "pipelines" # Assuming volume name matches container name
$global:pipelinesFolder = ".\pipelines"
$global:downloadFolder = ".\downloads"
$global:enginePath = $null
$global:containerEngine = $null

# Ensure the engine is selected.
$global:containerEngine = Select-ContainerEngine
if ($global:containerEngine -eq "docker") {
	$global:enginePath = Get-DockerPath
}
elseif ($global:containerEngine -eq "podman") {
	$global:enginePath = Get-PodmanPath
}
else {
	Write-Error "No container engine (Docker or Podman) found. Please install one before running this script."
	exit 1
}

# Validate that we have a valid engine path
if (-not $global:enginePath) {
	Write-Error "Failed to get path to container engine executable. Exiting."
	exit 1
}

#==============================================================================
# Function: ConvertTo-WSLPath
#==============================================================================
<#
.SYNOPSIS
   Converts a Windows path into a WSL (Linux) path.
.DESCRIPTION
   This function takes an absolute Windows path and converts it to the corresponding WSL
   path by replacing the drive letter and backslashes with the Linux mount point format
   (e.g., C:\Users\Me becomes /mnt/c/Users/Me).
   IMPORTANT: This workaround is CRUCIAL for successfully copying a file from the local
   machine to Podman using 'podman machine ssh "podman cp ..."'.
.PARAMETER winPath
   The Windows path to convert. Mandatory.
.OUTPUTS
   [string] The converted WSL path, or the original path if conversion fails.
.EXAMPLE
   $wslPath = ConvertTo-WSLPath -winPath "C:\MyFolder\MyFile.txt"
   # $wslPath will be "/mnt/c/MyFolder/MyFile.txt"
.NOTES
   Uses Resolve-Path to get the absolute path first.
   Uses regex matching and replacement.
#>
function ConvertTo-WSLPath {
	param(
		[Parameter(Mandatory = $true)]
		[string]$winPath
	)
	$absPath = (Resolve-Path $winPath).Path
	if ($absPath -match '^([A-Z]):\\(.*)$') {
		$drive = $matches[1].ToLower()
		$pathWithoutDrive = $matches[2]
		$unixPath = $pathWithoutDrive -replace '\\', '/'
		return "/mnt/$drive/$unixPath"
	}
	else {
		Write-Warning "Path '$winPath' does not match the expected Windows absolute path format."
		return $absPath
	}
}

#==============================================================================
# Function: Install-PipelinesContainer
#==============================================================================
<#
.SYNOPSIS
	Installs (or reinstalls) the Pipelines container using the official pre-built image.
.DESCRIPTION
	Ensures the required volume exists using Confirm-ContainerVolume.
	Removes any existing container named 'pipelines'.
	Pulls and runs the 'ghcr.io/open-webui/pipelines:main' image.
	Configures port mapping (9099:9099), volume mount, restart policy, and container name.
	Adds '--add-host host.docker.internal:host-gateway' specifically for Docker.
	Waits 20 seconds after starting and performs TCP/HTTP connectivity tests.
.EXAMPLE
	Install-PipelinesContainer
.NOTES
	Relies on Confirm-ContainerVolume, Test-TCPPort, Test-HTTPPort helper functions.
	Uses Write-Information for status messages.
#>
function Install-PipelinesContainer {
	Write-Information "Installing Pipelines using pre-built image from ghcr.io/open-webui/pipelines:main"

	# Ensure the volume exists
	if (-not (Confirm-ContainerVolume -Engine $global:enginePath -VolumeName $global:volumeName)) {
		Write-Error "Failed to ensure volume '$($global:volumeName)' exists. Exiting..."
		return
	}
	Write-Information "IMPORTANT: Using volume '$($global:volumeName)' - existing user data will be preserved."

	# Set the custom image tag to the official pre-built image
	$customPipelineImageTag = "ghcr.io/open-webui/pipelines:main"

	# (Optional) Remove any existing container with the same name
	$existingContainer = & $global:enginePath ps -a --filter "name=$($global:containerName)" --format "{{.ID}}"
	if ($existingContainer) {
		Write-Information "Pipelines container already exists. Removing it..."
		& $global:enginePath rm --force $global:containerName
	}

	Write-Information "Running Pipelines container..."

	# Conditionally set the --add-host parameter if using Docker
	if ($global:containerEngine -eq "docker") {
		$addHostParams = @('--add-host', 'host.docker.internal:host-gateway')
	}
	else {
		# For Podman, skip the --add-host parameter (or add an alternative if required)
		$addHostParams = @()
	}

	# Build the run arguments array
	$runArgs = @(
		'--detach', # run in background
		'--publish', '9099:9099', # port mapping
		'--volume', "$($global:volumeName):/app/pipelines", # volume mapping for persistent data
		'--restart', 'always', # restart policy
		'--name', $global:containerName, # container name
		$customPipelineImageTag                          # pre-built image tag
	) + $addHostParams # Add conditional params at the end

	# Command: run
	#   --detach: Run container in background.
	#   --publish: Map host port 9099 to container port 9099.
	#   --add-host: (Docker only) Map host.docker.internal to host gateway IP.
	#   --volume: Mount the named volume for persistent pipeline data.
	#   --restart always: Always restart the container unless explicitly stopped.
	#   --name: Assign a name to the container.
	& $global:enginePath run @runArgs
	if ($LASTEXITCODE -ne 0) {
		Write-Error "Failed to run the Pipelines container."
		return
	}
	Write-Information "Pipelines container is now running."

	# Wait for the container to initialize, then test connectivity
	Start-Sleep -Seconds 20
	Test-TCPPort -ComputerName "localhost" -Port 9099 -serviceName $global:containerName
	Test-HTTPPort -Uri "http://localhost:9099" -serviceName $global:containerName
}

#==============================================================================
# Function: Backup-PipelinesContainer
#==============================================================================
<#
.SYNOPSIS
	Backs up the state of the running Pipelines container.
.DESCRIPTION
	Checks if the global engine path is set. If set, calls the Backup-ContainerState
	helper function, specifying 'pipelines' as the container name.
.EXAMPLE
	Backup-PipelinesContainer
.NOTES
	Relies on Backup-ContainerState helper function.
#>
function Backup-PipelinesContainer {
	if (-not $global:enginePath) {
		Write-Error "Engine path not set. Please install the Pipelines container first."
		return
	}
	Backup-ContainerState -Engine $global:enginePath -ContainerName $global:containerName # This function supports ShouldProcess
}

#==============================================================================
# Function: Restore-PipelinesContainer
#==============================================================================
<#
.SYNOPSIS
	Restores the Pipelines container image from a backup tar file.
.DESCRIPTION
	Checks if the global engine path is set. If set, calls the Restore-ContainerState
	helper function, specifying 'pipelines' as the container name.
	Note: This only restores the image, it does not automatically start a container from it.
.EXAMPLE
	Restore-PipelinesContainer
.NOTES
	Relies on Restore-ContainerState helper function.
#>
function Restore-PipelinesContainer {
	if (-not $global:enginePath) {
		Write-Error "Engine path not set. Please install the Pipelines container first."
		return
	}
	Restore-ContainerState -Engine $global:enginePath -ContainerName $global:containerName # This function supports ShouldProcess
}

#==============================================================================
# Function: Uninstall-PipelinesContainer
#==============================================================================
<#
.SYNOPSIS
	Uninstalls the Pipelines container and optionally removes its data volume.
.DESCRIPTION
	Calls the Remove-ContainerAndVolume helper function, specifying 'pipelines' as both the
	container and volume name. This will stop/remove the container and prompt the user
	about removing the volume. Supports -WhatIf.
.EXAMPLE
	Uninstall-PipelinesContainer -Confirm:$false
.NOTES
	Relies on Remove-ContainerAndVolume helper function.
#>
function Uninstall-PipelinesContainer {
	Remove-ContainerAndVolume -Engine $global:enginePath -ContainerName $global:containerName -VolumeName $global:volumeName # This function supports ShouldProcess
}

#==============================================================================
# Function: Add-PipelineToContainer
#==============================================================================
<#
.SYNOPSIS
	Downloads a specified pipeline file and copies it into the running Pipelines container.
.DESCRIPTION
	Downloads the pipeline file from the specified URL to a temporary location.
	If using Podman, converts the temporary file's Windows path to a WSL path using ConvertTo-WSLPath.
	Copies the file into the specified destination directory within the container using 'podman machine ssh "podman cp ..."'.
	Restarts the container to load the new pipeline.
	Removes the temporary downloaded file.
.PARAMETER PipelineUrl
	The raw URL of the pipeline Python file to download. Defaults to the Azure OpenAI example.
.PARAMETER DestinationDir
	The directory path inside the container where the pipeline file should be copied. Defaults to '/app/pipelines'.
.PARAMETER ContainerName
	The name of the target Pipelines container. Defaults to the global $containerName ('pipelines').
.EXAMPLE
	Add-PipelineToContainer
.EXAMPLE
	Add-PipelineToContainer -PipelineUrl "https://example.com/my_custom_pipeline.py"
.NOTES
	Relies on Invoke-DownloadFile and ConvertTo-WSLPath helper functions.
	Uses 'podman machine ssh "podman cp ..."' for copying, which requires the Podman machine to be running.
	Uses Write-Information for status messages.
#>
function Add-PipelineToContainer {
	param(
		# URL of the pipeline file (use the raw URL)
		[string]$PipelineUrl = "https://raw.githubusercontent.com/open-webui/pipelines/main/examples/pipelines/providers/azure_openai_pipeline.py",
		# Destination directory inside the container
		[string]$DestinationDir = "/app/pipelines",
		# Container name (defaults to the global container name)
		[string]$ContainerName = $global:containerName
	)

	$fileName = "azure_openai_pipeline.py"
	# Create a temporary file path for the download (assume $global:downloadFolder is a Windows path)
	$tempFile = Join-Path $global:downloadFolder $fileName
	Write-Information "Downloading pipeline from $PipelineUrl to $tempFile..."
	# Use shared download function
	Invoke-DownloadFile -SourceUrl $PipelineUrl -DestinationPath $tempFile -ForceDownload:$true # Force download as it's temporary

	# If using Podman, convert the Windows path to WSL path
	if ($global:containerEngine -eq "podman") {
		$hostPath = ConvertTo-WSLPath -winPath $tempFile
	}
	else {
		$hostPath = $tempFile
	}

	Write-Information "Host Path: $hostPath"

	#Write-Information "Removing any existing copy of $fileName in container '$ContainerName'..."
	#& $global:enginePath exec $ContainerName rm -f "$DestinationDir/$fileName"

	Write-Information "Copying downloaded pipeline into container '$ContainerName' at '$DestinationDir'..."
	& $global:enginePath machine ssh "podman cp '$hostPath' '$($ContainerName):$DestinationDir'"

	Write-Information "Restarting container '$ContainerName' to load the new pipeline..."
	& $global:enginePath restart $ContainerName

	# Clean up the temporary file
	Remove-Item $tempFile -Force
	Write-Information "Pipeline added successfully."

	Write-Information "Reminder: In Open WebUI settings, set the OpenAI API URL to 'http://host.docker.internal:9099' and API key to '0p3n-w3bu!' if integrating pipelines."
}

#==============================================================================
# Function: Invoke-StartPipelinesForUpdate
#==============================================================================
<#
.SYNOPSIS
	Helper function called by Update-Container to start the Pipelines container after an update.
.DESCRIPTION
	This function encapsulates the specific logic required to start the Pipelines container,
	including ensuring the volume exists, setting engine-specific parameters (like --add-host for Docker),
	and running the container with the updated image name. It performs connectivity tests after starting.
	It adheres to the parameter signature expected by the -RunFunction parameter of Update-Container.
.PARAMETER EnginePath
	Path to the container engine executable (passed by Update-Container).
.PARAMETER ContainerEngineType
	Type of the container engine ('docker' or 'podman') (passed by Update-Container).
.PARAMETER ContainerName
	Name of the container being updated (passed by Update-Container).
.PARAMETER VolumeName
	Name of the volume associated with the container (passed by Update-Container).
.PARAMETER ImageName
	The new image name/tag to use for the updated container (passed by Update-Container).
.OUTPUTS
	Throws an error if the container fails to start, which signals failure back to Update-Container.
.EXAMPLE
	# This function is intended to be called internally by Update-Container via -RunFunction
	# Update-Container -RunFunction ${function:Invoke-StartPipelinesForUpdate}
.NOTES
	Relies on Confirm-ContainerVolume, Test-TCPPort, Test-HTTPPort helper functions.
	Uses Write-Information for status messages.
#>
function Invoke-StartPipelinesForUpdate {
	param(
		[string]$EnginePath,
		[string]$ContainerEngineType,
		[string]$ContainerName,
		[string]$VolumeName,
		[string]$ImageName # The updated image name passed by Update-Container
	)

	# Ensure the volume exists (important if it was removed manually)
	if (-not (Confirm-ContainerVolume -Engine $EnginePath -VolumeName $VolumeName)) {
		throw "Failed to ensure volume '$VolumeName' exists during update."
	}

	# Conditionally set the --add-host parameter if using Docker
	if ($ContainerEngineType -eq "docker") {
		$addHostParams = @('--add-host', 'host.docker.internal:host-gateway')
	}
	else {
		$addHostParams = @() # Podman doesn't need this
	}

	# Build the run arguments array
	$runArgs = @(
		'--detach', # run in background
		'--publish', '9099:9099', # port mapping
		'--volume', "$($VolumeName):/app/pipelines", # volume mapping
		'--restart', 'always', # restart policy
		'--name', $ContainerName, # container name
		$ImageName                                       # Use the image name passed to the script block
	) + $addHostParams

	Write-Information "Running updated Pipelines container with image '$ImageName'..."
	& $EnginePath run @runArgs
	if ($LASTEXITCODE -ne 0) {
		Write-Error "Failed to run the updated Pipelines container."
		# Throw an error to signal failure to Update-Container
		throw "Failed to run the updated Pipelines container."
	}
	Write-Information "Pipelines container started."

	# Wait for the container to initialize, then test connectivity
	Write-Information "Waiting for container startup..."
	Start-Sleep -Seconds 20
	Test-TCPPort -ComputerName "localhost" -Port 9099 -serviceName $ContainerName
	Test-HTTPPort -Uri "http://localhost:9099" -serviceName $ContainerName
}

#==============================================================================
# Function: Update-PipelinesContainer
#==============================================================================
<#
.SYNOPSIS
	Updates the Pipelines container to the latest official image using the generic update workflow.
.DESCRIPTION
	Calls the generic Update-Container helper function, providing the specific details for the
	Pipelines container (name, image name) and passing a reference to the
	Invoke-StartPipelinesForUpdate function via the -RunFunction parameter. This ensures the
	container is started correctly after the image is pulled and the old container is removed.
	Supports -WhatIf.
.EXAMPLE
	Update-PipelinesContainer -WhatIf
.NOTES
	Relies on the Update-Container helper function and Invoke-StartPipelinesForUpdate.
#>
function Update-PipelinesContainer {
	[CmdletBinding(SupportsShouldProcess = $true)]
	param()

	# Check ShouldProcess before proceeding with the delegated update
	if (-not $PSCmdlet.ShouldProcess($global:containerName, "Update Container")) {
		return
	}

	# Previously, a script block was defined here and passed using .GetNewClosure().
	# .GetNewClosure() creates a copy of the script block that captures the current
	# state of variables in its scope, ensuring the generic Update-Container function
	# executes it with the correct context from this script.
	# We now use a dedicated function (Invoke-StartPipelinesForUpdate) instead for better structure.

	# Call the generic Update-Container function
	Update-Container -Engine $global:enginePath `
		-ContainerName $global:containerName `
		-ImageName "ghcr.io/open-webui/pipelines:main" `
		-RunFunction ${function:Invoke-StartPipelinesForUpdate} # Pass function reference
}

#==============================================================================
# Function: Update-PipelinesUserData
#==============================================================================
<#
.SYNOPSIS
	Placeholder function for updating user data in the Pipelines container.
.DESCRIPTION
	Currently, this function only displays a message indicating that the functionality
	is not implemented. Supports -WhatIf.
.EXAMPLE
	Update-PipelinesUserData
.NOTES
	This function needs implementation if specific user data update procedures are required.
#>
function Update-PipelinesUserData {
	[CmdletBinding(SupportsShouldProcess = $true)]
	param()

	if ($PSCmdlet.ShouldProcess("Pipelines container user data", "Update")) {
		Write-Information "Update User Data functionality is not implemented for Pipelines container."
	}
}

#==============================================================================
# Function: Show-ContainerMenu
#==============================================================================
<#
.SYNOPSIS
	Displays the main menu options for Pipelines container management.
.DESCRIPTION
	Writes the available menu options (Show Info, Install, Uninstall, Backup, Restore, Add Azure Pipeline,
	Update System, Update User Data, Exit) to the console using Write-Output.
.EXAMPLE
	Show-ContainerMenu
.NOTES
	Uses Write-Output for direct console display.
#>
function Show-ContainerMenu {
	Write-Output "==========================================="
	Write-Output "Pipelines Container Menu"
	Write-Output "==========================================="
	Write-Output "1. Show Info & Test Connection"
	Write-Output "2. Install container"
	Write-Output "3. Uninstall container"
	Write-Output "4. Backup Live container"
	Write-Output "5. Restore Live container"
	Write-Output "A. Add Azure Pipeline to Container"
	Write-Output "B. Update System"
	Write-Output "C. Update User Data"
	Write-Output "0. Exit menu"
}

################################################################################
# Main Menu Loop using Generic Function
################################################################################
$menuActions = @{
	"1" = {
		Show-ContainerStatus -ContainerName $global:containerName `
			-ContainerEngine $global:containerEngine `
			-EnginePath $global:enginePath `
			-DisplayName "Pipelines" `
			-TcpPort 9099 `
			-HttpPort 9099
	}
	"2" = { Install-PipelinesContainer }
	"3" = { Uninstall-PipelinesContainer }
	"4" = { Backup-PipelinesContainer }
	"5" = { Restore-PipelinesContainer }
	"A" = { Add-PipelineToContainer }
	"B" = { Update-PipelinesContainer }
	"C" = { Update-PipelinesUserData }
}

Invoke-MenuLoop -ShowMenuScriptBlock ${function:Show-ContainerMenu} -ActionMap $menuActions -ExitChoice "0"
