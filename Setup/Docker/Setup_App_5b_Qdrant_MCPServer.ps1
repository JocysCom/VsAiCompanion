################################################################################
# Description  : Script to build, run, update, backup, restore, and uninstall the
#                Qdrant MCP Server container using Docker/Podman.
#                Clones the source code, builds the image, and runs the container.
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
# Global Variables
#############################################
# Note: PSAvoidGlobalVars warnings are ignored here as these are used across menu actions.
$global:imageName = "qdrant-mcp-server:latest" # Standardized variable name
$global:containerName = "qdrant-mcp-server"
$global:volumeName = $global:containerName # Default: same as container name (though likely unused by this app).
$global:srcDir = Join-Path $PSScriptRoot "downloads\mcp-server-qdrant"
$global:repoUrl = "https://github.com/qdrant/mcp-server-qdrant.git"
$global:qdrantUrl = "http://localhost:6333"
$global:collectionName = "mcp-default-collection"
$global:qdrantUrlPromptDefault = "http://host.containers.internal:6333"

# --- Engine Selection ---
$global:containerEngine = Select-ContainerEngine
# Exit if no engine was selected
if (-not $global:containerEngine) {
	Write-Warning "No container engine selected. Exiting script."
	exit 1
}
# Set engine-specific options (only admin check for Docker)
if ($global:containerEngine -eq "docker") {
	Test-AdminPrivilege
}
# Get the engine path after setting specific options
$global:enginePath = Get-EnginePath -EngineName $global:containerEngine

#==============================================================================
# Function: Confirm-SourceCode
#==============================================================================
<#
.SYNOPSIS
	Clones or updates the Qdrant MCP Server source code repository.
.DESCRIPTION
	Checks if Git is installed using Test-GitInstallation.
	If the source directory ($global:srcDir) doesn't exist, it clones the repository from $global:repoUrl.
	If the directory exists, it performs a 'git pull' to update the code.
.OUTPUTS
	[bool] Returns $true if the source code is present (cloned or updated successfully), $false otherwise.
.EXAMPLE
	if (Confirm-SourceCode) { # Proceed with build }
.NOTES
	Relies on Test-GitInstallation helper function.
	Uses global variables $global:srcDir and $global:repoUrl.
#>
function Confirm-SourceCode {
	Test-GitInstallation # Check if Git is available

	if (-not (Test-Path $global:srcDir)) {
		Write-Host "Cloning Qdrant MCP Server repository from '$($global:repoUrl)'..."
		git clone $global:repoUrl $global:srcDir
		if ($LASTEXITCODE -ne 0) {
			Write-Error "Failed to clone repository."
			return $false
		}
	}
	else {
		Write-Host "Source code directory found at '$($global:srcDir)'. Checking for updates..."
		Push-Location $global:srcDir
		git pull
		Pop-Location
	}
	return $true
}

#==============================================================================
# Function: Invoke-QdrantMCPServerImageBuild
#==============================================================================
<#
.SYNOPSIS
	Builds the Qdrant MCP Server container image from the source code.
.DESCRIPTION
	Checks if the Dockerfile exists in the source directory ($global:srcDir).
	Uses the selected container engine ('engine build') to build the image, tagging it
	with the value from $global:imageTag. Supports -WhatIf.
.OUTPUTS
	[bool] Returns $true if the image build is successful, $false otherwise or if skipped.
.EXAMPLE
	Invoke-QdrantMCPServerImageBuild -WhatIf
.NOTES
	Relies on global variables $global:srcDir, $global:imageTag, $global:enginePath.
	Uses Write-Host for status messages.
#>
function Invoke-QdrantMCPServerImageBuild {
	[CmdletBinding(SupportsShouldProcess = $true)]
	[OutputType([bool])]
	param()

	if (-not (Test-Path (Join-Path $global:srcDir "Dockerfile"))) {
		Write-Error "Dockerfile not found in '$($global:srcDir)'. Cannot build image."
		return $false
	}

	if ($PSCmdlet.ShouldProcess($global:imageName, "Build Container Image from '$($global:srcDir)'")) {
		# Use imageName
		Write-Host "Building Qdrant MCP Server image '$($global:imageName)'..." # Use imageName
		& $global:enginePath build --tag $global:imageName $global:srcDir # Use imageName
		if ($LASTEXITCODE -ne 0) {
			Write-Error "Failed to build Qdrant MCP Server image."
			return $false
		}
		Write-Host "Image built successfully."
		return $true
	}
 else {
		Write-Warning "Image build skipped due to -WhatIf."
		return $false
	}
}

#==============================================================================
# Function: Install-QdrantMCPServerContainer
#==============================================================================
<#
.SYNOPSIS
	Installs the Qdrant MCP Server container by cloning source, building, and running.
.DESCRIPTION
	Orchestrates the installation process:
	1. Ensures source code is present using Confirm-SourceCode.
	2. Builds the container image using Invoke-QdrantMCPServerImageBuild.
	3. Ensures the data volume exists using Confirm-ContainerVolume.
	4. Removes any existing container using Remove-ContainerAndVolume.
	5. Prompts the user for Qdrant URL, Collection Name, and API Key using Read-Host.
	6. Runs the new container using the built image, mapping port 8000, and setting environment variables based on user input.
	7. Waits 15 seconds and performs a TCP connectivity test.
.EXAMPLE
	Install-QdrantMCPServerContainer
.NOTES
	Relies on Confirm-SourceCode, Invoke-QdrantMCPServerImageBuild, Confirm-ContainerVolume,
	Remove-ContainerAndVolume, Test-TCPPort helper functions.
	Uses global variables for names, paths, etc.
	Requires user interaction via Read-Host for configuration.
#>
function Install-QdrantMCPServerContainer {
	# Step 1: Ensure source code is available
	if (-not (Confirm-SourceCode)) {
		return
	}

	# Step 2: Build the image
	if (-not (Invoke-QdrantMCPServerImageBuild)) {
		Write-Warning "Image build failed or was skipped. Cannot proceed with installation."
		return
	}

	# Step 3: Ensure the volume exists (optional, but good practice)
	if (-not (Confirm-ContainerResource -Engine $global:enginePath -ResourceType "volume" -ResourceName $global:volumeName)) {
		Write-Warning "Failed to ensure volume '$($global:volumeName)'. Container might not persist data if it uses this path."
		# Continue installation even if volume creation fails/skipped
	}

	# Step 4: Remove existing container (if any)
	Remove-ContainerAndVolume -Engine $global:enginePath -ContainerName $global:containerName -VolumeName $global:volumeName

	# Step 5: Get Configuration
	$qdrantUrlInput = Read-Host "Enter Qdrant URL (leave blank for default: $($global:qdrantUrlPromptDefault))"
	# If blank, use the default (host.containers.internal). Otherwise, use the user-provided URL.
	$qdrantUrlToUse = if ([string]::IsNullOrWhiteSpace($qdrantUrlInput)) { $global:qdrantUrlPromptDefault } else { $qdrantUrlInput }
	Write-Information "Using Qdrant URL for container: $qdrantUrlToUse" # Inform user of the actual URL being used by the container

	$collectionNameInput = Read-Host "Enter Qdrant Collection Name (leave blank for default: $($global:collectionName))"
	$collectionNameToUse = if ([string]::IsNullOrWhiteSpace($collectionNameInput)) { $global:collectionName } else { $collectionNameInput }

	$qdrantApiKey = Read-Host "Enter Qdrant API Key (if required, otherwise leave blank)"

	# Step 6: Run the container
	Write-Host "Starting Qdrant MCP Server container '$($global:containerName)'..."
	$runOptions = @(
		"--detach",
		"--name", $global:containerName,
		"--publish", "8000:8000", # Default port for the server
		#"--volume", "$($global:volumeName):/app/data", # Adjust path if needed based on actual app structure
		"--env", "QDRANT_URL=$qdrantUrlToUse",
		"--env", "COLLECTION_NAME=$collectionNameToUse"
	)
	if (-not [string]::IsNullOrWhiteSpace($qdrantApiKey)) {
		$runOptions += "--env", "QDRANT_API_KEY=$qdrantApiKey"
	}

	# Execute podman run with options and image.
	# The command and arguments (--transport sse) are specified by CMD in the Dockerfile.
	& $global:enginePath run @runOptions $global:imageName # Use imageName
	if ($LASTEXITCODE -ne 0) {
		Write-Error "Failed to start the Qdrant MCP Server container using the default CMD. Check container logs for details."
		return
	}

	# Step 7: Wait and Test
	Write-Host "Waiting 15 seconds for the container to start..."
	Start-Sleep -Seconds 15
	Test-TCPPort -ComputerName "localhost" -Port 8000 -serviceName "Qdrant MCP Server"
	# Add HTTP test if the server has a root endpoint or health check
	# Test-HTTPPort -Uri "http://localhost:8000" -serviceName "Qdrant MCP Server"
	Write-Host "Qdrant MCP Server container started. Check logs for details."
	Write-Host ""
	Write-Host "--------------------------------------------------" -ForegroundColor Green
	Write-Host " Cline Configuration Details:" -ForegroundColor Green
	Write-Host "--------------------------------------------------" -ForegroundColor Green
	Write-Host " Server Name: qdrant-mcp-server"
	Write-Host " URL:         http://localhost:8000/sse"
	Write-Host "--------------------------------------------------" -ForegroundColor Green
	Write-Host "Add the above details to your Cline MCP settings." -ForegroundColor Green
	Write-Host ""
}

# Note: Uninstall-QdrantMCPServerContainer, Backup-QdrantMCPServerContainer, Restore-QdrantMCPServerContainer functions removed. Shared functions called directly from menu.

#==============================================================================
# Function: Update-QdrantMCPServerContainer
#==============================================================================
<#
.SYNOPSIS
	Updates the Qdrant MCP Server by pulling the latest source code, rebuilding the image, and reinstalling the container.
.DESCRIPTION
	Performs the update workflow:
	1. Updates the source code repository using Confirm-SourceCode (which includes 'git pull').
	2. Rebuilds the container image using Invoke-QdrantMCPServerImageBuild.
	3. Calls Install-QdrantMCPServerContainer to handle removing the old container and running the new one with the updated image and configuration prompts.
	Supports -WhatIf for the source code pull step.
.EXAMPLE
	Update-QdrantMCPServerContainer -WhatIf
.NOTES
	Relies on Confirm-SourceCode, Invoke-QdrantMCPServerImageBuild, Install-QdrantMCPServerContainer helper functions.
#>
function Update-QdrantMCPServerContainer {
	[CmdletBinding(SupportsShouldProcess = $true)]
	param()

	Write-Host "Initiating update for Qdrant MCP Server..."

	# Step 1: Update source code
	if ($PSCmdlet.ShouldProcess($global:srcDir, "Pull Git Repository")) {
		if (-not (Confirm-SourceCode)) {
			# Ensure-SourceCode also does git pull
			Write-Error "Failed to update source code. Update aborted."
			return
		}
	}
 else {
		Write-Warning "Skipping source code update due to -WhatIf."
	}

	# Step 2: Rebuild the image
	if (-not (Invoke-QdrantMCPServerImageBuild)) {
		Write-Error "Image build failed or skipped. Update aborted."
		return
	}

	# Step 3: Re-install (which handles removal and running with prompts)
	# We call Install directly as it contains the logic to remove the old container
	# and run the new one with potentially updated ENV vars.
	Write-Host "Re-installing container with updated image..."
	Install-QdrantMCPServerContainer
}

################################################################################
# Main Menu Loop using Generic Function
################################################################################

# Define Menu Title and Items
$menuTitle = "Qdrant MCP Server Container Menu"
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
			-DisplayName "Qdrant MCP Server" `
			-TcpPort 8000 `
			# -HttpPort 8000 # Add if server has a root/health endpoint
			-AdditionalInfo @{
			"Source Dir" = $global:srcDir;
			"Qdrant URL" = $global:qdrantUrl;
			"Collection" = $global:collectionName
		}
	}
	"2" = { Install-QdrantMCPServerContainer }
	"3" = { Remove-ContainerAndVolume -Engine $global:enginePath -ContainerName $global:containerName -VolumeName $global:volumeName } # Call shared function directly
	"4" = { Backup-ContainerImage -Engine $global:enginePath -ContainerName $global:containerName } # Call shared function directly
	"5" = {
		Restore-ContainerImage -Engine $global:enginePath -ContainerName $global:containerName # Call shared function directly
		Write-Warning "Container image restored from backup. You may need to manually restart the container with correct environment variables if they were changed since the backup (use option 2)."
	}
	"6" = { Update-QdrantMCPServerContainer }
	"7" = { $null = Backup-ContainerVolume -EngineType $global:containerEngine -VolumeName $global:volumeName } # Call shared function directly
	"8" = {
		$null = Restore-ContainerVolume -EngineType $global:containerEngine -VolumeName $global:volumeName
		& $global:enginePath restart $global:containerName
	}
	"9" = { Test-ImageUpdateAvailable -Engine $global:enginePath -ImageName $global:imageName }
	# Note: "0" action is handled internally by Invoke-MenuLoop
}

# Invoke the Menu Loop
Invoke-MenuLoop -MenuTitle $menuTitle -MenuItems $menuItems -ActionMap $menuActions -ExitChoice "0"
