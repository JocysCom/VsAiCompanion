################################################################################
# File         : Setup_5_Qdrant_MCP_Server.ps1
# Description  : Script to build, run, update, backup, restore, and uninstall the
#                Qdrant MCP Server container using Docker/Podman.
#                Clones the source code, builds the image, and runs the container.
# Usage        : Run as Administrator if using Docker.
################################################################################

using namespace System
using namespace System.IO
using namespace System.Diagnostics.CodeAnalysis # For SuppressMessageAttribute

# Dot-source the necessary helper function files.
. "$PSScriptRoot\Setup_0_Core.ps1"
. "$PSScriptRoot\Setup_0_Network.ps1"
. "$PSScriptRoot\Setup_0_ContainerEngine.ps1"
. "$PSScriptRoot\Setup_0_BackupRestore.ps1"
. "$PSScriptRoot\Setup_0_ContainerMgmt.ps1"

# Ensure the script working directory is set.
Set-ScriptLocation

#############################################
# Global Variables
#############################################
# Note: PSAvoidGlobalVars warnings are ignored here as these are used across menu actions.
$global:containerName = "qdrant-mcp-server"
$global:imageTag = "qdrant-mcp-server:latest" # Tag for the built image
$global:volumeName = "qdrant_mcp_server_data" # Define a volume name (may not be used by app)
$global:srcDir = Join-Path $PSScriptRoot "downloads\mcp-server-qdrant" # Source code directory
$global:repoUrl = "https://github.com/qdrant/mcp-server-qdrant.git"
$global:qdrantUrl = "http://localhost:6333" # Default Qdrant URL (assuming local install)
$global:collectionName = "mcp-default-collection" # Default collection name

$global:containerEngine = Select-ContainerEngine
if ($global:containerEngine -eq "docker") {
	Test-AdminPrivilege
	$global:enginePath = Get-DockerPath
}
else {
	$global:enginePath = Get-PodmanPath
}

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
		Write-Information "Cloning Qdrant MCP Server repository from '$($global:repoUrl)'..."
		git clone $global:repoUrl $global:srcDir
		if ($LASTEXITCODE -ne 0) {
			Write-Error "Failed to clone repository."
			return $false
		}
	}
	else {
		Write-Information "Source code directory found at '$($global:srcDir)'. Checking for updates..."
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
	Uses Write-Information for status messages.
#>
function Invoke-QdrantMCPServerImageBuild {
	[CmdletBinding(SupportsShouldProcess = $true)]
	[OutputType([bool])]
	param()

	if (-not (Test-Path (Join-Path $global:srcDir "Dockerfile"))) {
		Write-Error "Dockerfile not found in '$($global:srcDir)'. Cannot build image."
		return $false
	}

	if ($PSCmdlet.ShouldProcess($global:imageTag, "Build Container Image from '$($global:srcDir)'")) {
		Write-Information "Building Qdrant MCP Server image '$($global:imageTag)'..."
		& $global:enginePath build --tag $global:imageTag $global:srcDir
		if ($LASTEXITCODE -ne 0) {
			Write-Error "Failed to build Qdrant MCP Server image."
			return $false
		}
		Write-Information "Image built successfully."
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
	if (-not (Confirm-ContainerVolume -Engine $global:enginePath -VolumeName $global:volumeName)) {
		Write-Warning "Failed to ensure volume '$($global:volumeName)'. Container might not persist data if it uses this path."
		# Continue installation even if volume creation fails/skipped
	}

	# Step 4: Remove existing container (if any)
	Remove-ContainerAndVolume -Engine $global:enginePath -ContainerName $global:containerName -VolumeName $global:volumeName

	# Step 5: Get Configuration
	$qdrantUrlInput = Read-Host "Enter Qdrant URL (leave blank for default: $($global:qdrantUrl))"
	$qdrantUrlToUse = if ([string]::IsNullOrWhiteSpace($qdrantUrlInput)) { $global:qdrantUrl } else { $qdrantUrlInput }

	$collectionNameInput = Read-Host "Enter Qdrant Collection Name (leave blank for default: $($global:collectionName))"
	$collectionNameToUse = if ([string]::IsNullOrWhiteSpace($collectionNameInput)) { $global:collectionName } else { $collectionNameInput }

	$qdrantApiKey = Read-Host "Enter Qdrant API Key (if required, otherwise leave blank)"

	# Step 6: Run the container
	Write-Information "Starting Qdrant MCP Server container '$($global:containerName)'..."
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

	& $global:enginePath run @runOptions $global:imageTag
	if ($LASTEXITCODE -ne 0) {
		Write-Error "Failed to start the Qdrant MCP Server container."
		return
	}

	# Step 7: Wait and Test
	Write-Information "Waiting 15 seconds for the container to start..."
	Start-Sleep -Seconds 15
	Test-TCPPort -ComputerName "localhost" -Port 8000 -serviceName "Qdrant MCP Server"
	# Add HTTP test if the server has a root endpoint or health check
	# Test-HTTPPort -Uri "http://localhost:8000" -serviceName "Qdrant MCP Server"
	Write-Information "Qdrant MCP Server container started. Check logs for details."
}

#==============================================================================
# Function: Uninstall-QdrantMCPServerContainer
#==============================================================================
<#
.SYNOPSIS
	Uninstalls the Qdrant MCP Server container and optionally removes its data volume.
.DESCRIPTION
	Calls the Remove-ContainerAndVolume helper function, specifying 'qdrant-mcp-server' as the container
	and 'qdrant_mcp_server_data' as the volume. This will stop/remove the container and prompt the user
	about removing the volume. Supports -WhatIf.
.EXAMPLE
	Uninstall-QdrantMCPServerContainer -Confirm:$false
.NOTES
	Relies on Remove-ContainerAndVolume helper function.
#>
function Uninstall-QdrantMCPServerContainer {
	Remove-ContainerAndVolume -Engine $global:enginePath -ContainerName $global:containerName -VolumeName $global:volumeName
}

#==============================================================================
# Function: Backup-QdrantMCPServerContainer
#==============================================================================
<#
.SYNOPSIS
	Backs up the state of the running Qdrant MCP Server container.
.DESCRIPTION
	Calls the Backup-ContainerState helper function, specifying 'qdrant-mcp-server' as the container name.
	This commits the container state to an image and saves it as a tar file.
.EXAMPLE
	Backup-QdrantMCPServerContainer
.NOTES
	Relies on Backup-ContainerState helper function. Supports -WhatIf via the helper function.
#>
function Backup-QdrantMCPServerContainer {
	Backup-ContainerState -Engine $global:enginePath -ContainerName $global:containerName
}

#==============================================================================
# Function: Restore-QdrantMCPServerContainer
#==============================================================================
<#
.SYNOPSIS
	Restores the Qdrant MCP Server container image from a backup tar file.
.DESCRIPTION
	Calls the Restore-ContainerState helper function, specifying 'qdrant-mcp-server' as the container name.
	This loads the image from the backup tar file. Note: This only restores the image,
	it does not automatically start the container or ensure environment variables are correct.
.EXAMPLE
	Restore-QdrantMCPServerContainer
.NOTES
	Relies on Restore-ContainerState helper function. Warns user about potential need to restart manually.
#>
function Restore-QdrantMCPServerContainer {
	# Note: Restoring state might require re-running with correct ENV vars if they changed.
	# The generic Restore-ContainerState only restores the image.
	# A more robust restore might need to re-run Install after loading the image.
	Restore-ContainerState -Engine $global:enginePath -ContainerName $global:containerName
	Write-Warning "Container state restored from image backup. You may need to manually restart the container with correct environment variables if they were changed since the backup."
}

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

	Write-Information "Initiating update for Qdrant MCP Server..."

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
	Write-Information "Re-installing container with updated image..."
	Install-QdrantMCPServerContainer
}

#==============================================================================
# Function: Show-ContainerMenu
#==============================================================================
<#
.SYNOPSIS
	Displays the main menu options for Qdrant MCP Server container management.
.DESCRIPTION
	Writes the available menu options (Show Info, Install/Rebuild, Uninstall, Backup, Restore, Update, Exit)
	to the console using Write-Output.
.EXAMPLE
	Show-ContainerMenu
.NOTES
	Uses Write-Output for direct console display.
#>
function Show-ContainerMenu {
	Write-Output "==========================================="
	Write-Output "Qdrant MCP Server Container Menu"
	Write-Output "==========================================="
	Write-Output "1. Show Info & Test Connection"
	Write-Output "2. Install/Rebuild container"
	Write-Output "3. Uninstall container"
	Write-Output "4. Backup container state"
	Write-Output "5. Restore container state"
	Write-Output "6. Update container (Pull source & Rebuild)"
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
	"3" = { Uninstall-QdrantMCPServerContainer }
	"4" = { Backup-QdrantMCPServerContainer }
	"5" = { Restore-QdrantMCPServerContainer }
	"6" = { Update-QdrantMCPServerContainer }
}

Invoke-MenuLoop -ShowMenuScriptBlock ${function:Show-ContainerMenu} -ActionMap $menuActions -ExitChoice "0"
