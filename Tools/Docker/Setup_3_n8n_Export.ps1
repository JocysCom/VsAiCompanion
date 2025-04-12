################################################################################
# File         : Setup_3_n8n_ExportImport.ps1 # Renamed conceptually
# Description  : Provides menu options to export or import n8n workflows and
#                credentials for a running container.
# Usage        : Run with appropriate permissions. Requires the n8n container
#                to be running. Run as Administrator if using Docker.
################################################################################

# Dot-source the necessary helper function files.
. "$PSScriptRoot\Setup_0_Core.ps1"
. "$PSScriptRoot\Setup_0_ContainerEngine.ps1"
. "$PSScriptRoot\Setup_0_ContainerMgmt.ps1" # Needed for Show-ContainerStatus potentially
. "$PSScriptRoot\Setup_0_BackupRestore.ps1" # Needed for Copy-MachineToHost

# Ensure the script working directory is set.
Set-ScriptLocation

#############################################
# Global Variables
#############################################
$global:containerName = "n8n"
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

# Define common paths
$localDownloadsDir = Join-Path -Path $PSScriptRoot -ChildPath "downloads"
$localWorkflowsPath = Join-Path -Path $localDownloadsDir -ChildPath "n8n_workflows.json"
$localCredentialsPath = Join-Path -Path $localDownloadsDir -ChildPath "n8n_credentials.json"
$containerTempDir = "/tmp"
$containerWorkflowsPath = "$containerTempDir/n8n_workflows.json"
$containerCredentialsPath = "$containerTempDir/n8n_credentials.json"

#==============================================================================
# Function: ConvertTo-WSLPath
#==============================================================================
<#
.SYNOPSIS
   Converts a Windows path into a WSL (Linux) path.
.DESCRIPTION
   Needed for Podman on Windows when copying files via 'podman machine ssh "podman cp ..."'.
.NOTES
   Copied from Setup_2a_Pipelines.ps1
#>
function ConvertTo-WSLPath {
	param(
		[Parameter(Mandatory = $true)]
		[string]$winPath
	)
	# Ensure the path exists before trying to resolve (needed for destination paths)
	$itemExists = Test-Path -Path $winPath
	if (-not $itemExists) {
		# If it doesn't exist, try resolving the parent directory
		$parentDir = Split-Path -Path $winPath -Parent
		if (Test-Path -Path $parentDir) {
			$resolvedParent = (Resolve-Path $parentDir).Path
			$filename = Split-Path -Path $winPath -Leaf
			$absPath = Join-Path -Path $resolvedParent -ChildPath $filename
		}
		else {
			Write-Warning "Cannot resolve path or its parent: '$winPath'. Using original path for conversion attempt."
			$absPath = $winPath # Fallback
		}
	}
	else {
		$absPath = (Resolve-Path $winPath).Path
	}

	if ($absPath -match '^([A-Z]):\\(.*)$') {
		$drive = $matches[1].ToLower()
		$pathWithoutDrive = $matches[2]
		$unixPath = $pathWithoutDrive -replace '\\', '/'
		return "/mnt/$drive/$unixPath"
	}
	else {
		Write-Warning "Path '$winPath' does not match the expected Windows absolute path format."
		return $absPath # Return original path on failure
	}
}

#==============================================================================
# Function: Invoke-ExportWorkflow
#==============================================================================
<#
.SYNOPSIS
    Exports n8n workflows from the container to a local file.
.DESCRIPTION
    Executes 'n8n export:workflow --all' inside the container as the 'node' user,
    saving the output to a temporary file. Then copies the file to the local
    'downloads' directory using the appropriate cp command (Docker direct, Podman via ssh).
    Cleans up the temporary file in the container.
.OUTPUTS
    [bool] $true if successful, $false otherwise.
#>
function Invoke-ExportWorkflow {
	[CmdletBinding(SupportsShouldProcess = $true)]
	[OutputType([bool])]
	param()

	$targetDescription = "n8n workflows from container '$($global:containerName)'"
	if (-not $PSCmdlet.ShouldProcess($targetDescription, "Export")) {
		Write-Host "Skipped export due to -WhatIf."
		return $false
	}

	Write-Host "--- Starting Workflow Export ---"
	$workflowExported = $false
	$exportSuccess = $true
	try {
		# 1. Execute export command in container
		$workflowCmdArgs = @("export:workflow", "--all", "--output", $containerWorkflowsPath)
		Write-Host "Running exec command: $($global:enginePath) exec --user node $($global:containerName) n8n $($workflowCmdArgs -join ' ')"
		$execResult = & $global:enginePath exec --user node $global:containerName n8n $workflowCmdArgs 2>&1
		Write-Host "Exec result: $execResult"
		if ($LASTEXITCODE -ne 0) {
			Write-Error "The 'n8n export:workflow' command failed. Exit Code: $LASTEXITCODE. Output: $execResult"
			throw "Exec command failed."
		}
		Write-Host "'n8n export:workflow' command executed successfully."
		$workflowExported = $true # Mark as exported in container

		# 2. Copy file from container to host using shared function
		$copySuccess = Copy-MachineToHost -EnginePath $global:enginePath `
			-ContainerEngineType $global:containerEngine `
			-ContainerName $global:containerName `
			-ContainerSourcePath $containerWorkflowsPath `
			-HostDestinationPath $localWorkflowsPath
		if (-not $copySuccess) {
			# Error is logged within Copy-MachineToHost, just need to stop execution here
			throw "Copy-MachineToHost command failed for workflows."
		}
		# Success message is now inside Copy-MachineToHost if successful
	}
	catch {
		Write-Error "Workflow export step failed: $_"
		$exportSuccess = $false
	}
	finally {
		# 3. Cleanup temp file in container if it was created
		if ($workflowExported) {
			Write-Host "Cleaning up temporary workflow file in container..."
			& $global:enginePath exec --user node $global:containerName rm $containerWorkflowsPath 2>$null
		}
		Write-Host "--- Finished Workflow Export ---"
	}
	return $exportSuccess
}

#==============================================================================
# Function: Invoke-ExportCredential
#==============================================================================
<#
.SYNOPSIS
    Exports n8n credentials from the container to a local file.
.DESCRIPTION
    Executes 'n8n export:credentials --all' inside the container as the 'node' user,
    saving the output to a temporary file. Handles the "No credentials found" case
    as a warning. Copies the file (if created) to the local 'downloads' directory
    using the appropriate cp command. Cleans up the temporary file.
.OUTPUTS
    [bool] $true if successful or if no credentials found, $false on other errors.
#>
function Invoke-ExportCredential {
	[CmdletBinding(SupportsShouldProcess = $true)]
	[OutputType([bool])]
	param()

	$targetDescription = "n8n credentials from container '$($global:containerName)'"
	if (-not $PSCmdlet.ShouldProcess($targetDescription, "Export")) {
		Write-Host "Skipped export due to -WhatIf."
		return $false
	}

	Write-Host "--- Starting Credentials Export ---"
	$credentialsExported = $false
	$exportSuccess = $true
	$commandSucceeded = $false
	try {
		# 1. Execute export command in container
		$credentialsCmdArgs = @("export:credentials", "--all", "--output", $containerCredentialsPath)
		Write-Host "Running exec command: $($global:enginePath) exec --user node $($global:containerName) n8n $($credentialsCmdArgs -join ' ')"
		$execResult = & $global:enginePath exec --user node $global:containerName n8n $credentialsCmdArgs 2>&1
		Write-Host "Exec result: $execResult"

		if ($LASTEXITCODE -eq 0) {
			Write-Host "'n8n export:credentials' command executed successfully."
			$credentialsExported = $true # Mark as exported in container
			$commandSucceeded = $true
		}
		elseif ($LASTEXITCODE -eq 1 -and $execResult -match "No credentials found") {
			Write-Warning "No credentials found to export. Skipping credentials file copy."
			$credentialsExported = $false # Ensure flag is false
			$commandSucceeded = $true # Treat this specific case as non-fatal for the overall step
		}
		else {
			Write-Error "The 'n8n export:credentials' command failed. Exit Code: $LASTEXITCODE. Output: $execResult"
			throw "Exec command failed."
		}

		# 2. Copy file from container to host (only if export command created a file) using shared function
		if ($credentialsExported) {
			$copySuccess = Copy-MachineToHost -EnginePath $global:enginePath `
				-ContainerEngineType $global:containerEngine `
				-ContainerName $global:containerName `
				-ContainerSourcePath $containerCredentialsPath `
				-HostDestinationPath $localCredentialsPath
			if (-not $copySuccess) {
				# Error is logged within Copy-MachineToHost, just need to stop execution here
				throw "Copy-MachineToHost command failed for credentials."
			}
			# Success message is now inside Copy-MachineToHost if successful
		}
	}
	catch {
		Write-Error "Credentials export step failed: $_"
		$exportSuccess = $false
	}
	finally {
		# 3. Cleanup temp file in container if it was created
		if ($credentialsExported) {
			Write-Host "Cleaning up temporary credentials file in container..."
			& $global:enginePath exec --user node $global:containerName rm $containerCredentialsPath 2>$null
		}
		Write-Host "--- Finished Credentials Export ---"
	}
	# Return true if the command succeeded OR if the only issue was "no credentials found"
	return $exportSuccess -and $commandSucceeded
}

#==============================================================================
# Function: Invoke-ImportWorkflow
#==============================================================================
<#
.SYNOPSIS
    Imports n8n workflows into the container from a local file.
.DESCRIPTION
    Checks if the local workflow export file exists. Copies the file into the
    container's temporary directory using the appropriate cp command (Docker direct,
    Podman via ssh with WSL path). Executes 'n8n import:workflow' inside the
    container as the 'node' user. Cleans up the temporary file.
.OUTPUTS
    [bool] $true if successful, $false otherwise.
#>
function Invoke-ImportWorkflow {
	[CmdletBinding(SupportsShouldProcess = $true)]
	[OutputType([bool])]
	param()

	$targetDescription = "n8n workflows into container '$($global:containerName)'"
	if (-not $PSCmdlet.ShouldProcess($targetDescription, "Import")) {
		Write-Host "Skipped import due to -WhatIf."
		return $false
	}

	# Check if source file exists
	if (-not (Test-Path -Path $localWorkflowsPath -PathType Leaf)) {
		Write-Error "Local workflow file not found: '$localWorkflowsPath'. Please export workflows first."
		return $false
	}

	Write-Host "--- Starting Workflow Import ---"
	$importSuccess = $true
	$fileCopied = $false
	try {
		# 1. Copy file from host to container
		Write-Host "Copying local workflow file to container..."
		if ($global:containerEngine -eq "docker") {
			$cpCommand = "$($global:enginePath) cp ""$localWorkflowsPath"" ""$($global:containerName):$containerWorkflowsPath"""
			Write-Host "Running cp command: $cpCommand"
			$cpResult = & $global:enginePath cp $localWorkflowsPath "$($global:containerName):$containerWorkflowsPath" 2>&1
		}
		else {
			# Podman - use machine ssh workaround for host-to-container
			$wslSourceFilePath = ConvertTo-WSLPath -winPath $localWorkflowsPath
			if ($wslSourceFilePath -eq $localWorkflowsPath) { throw "Failed to convert Windows path '$localWorkflowsPath' to WSL path." }
			$innerCpCommand = "podman cp '$wslSourceFilePath' '$($global:containerName):$containerWorkflowsPath'"
			$cpCommand = "$($global:enginePath) machine ssh ""$innerCpCommand"""
			Write-Host "Running cp command via podman machine ssh: $cpCommand"
			$cpResult = & $global:enginePath machine ssh "$innerCpCommand" 2>&1
		}
		Write-Host "Cp result: $cpResult"
		if ($LASTEXITCODE -ne 0) {
			Write-Error "Copying the local workflow file to the container failed. Exit Code: $LASTEXITCODE. Output: $cpResult"
			throw "Cp command failed."
		}
		Write-Host "Successfully copied workflow file to container."
		$fileCopied = $true

		# 2. Execute import command in container
		$importCmdArgs = @("import:workflow", "--input", $containerWorkflowsPath)
		Write-Host "Running exec command: $($global:enginePath) exec --user node $($global:containerName) n8n $($importCmdArgs -join ' ')"
		$execResult = & $global:enginePath exec --user node $global:containerName n8n $importCmdArgs 2>&1
		Write-Host "Exec result: $execResult"
		if ($LASTEXITCODE -ne 0) {
			Write-Error "The 'n8n import:workflow' command failed. Exit Code: $LASTEXITCODE. Output: $execResult"
			throw "Exec command failed."
		}
		Write-Host "'n8n import:workflow' command executed successfully." -ForegroundColor Green
	}
	catch {
		Write-Error "Workflow import step failed: $_"
		$importSuccess = $false
	}
	finally {
		# 3. Cleanup temp file in container if it was copied
		if ($fileCopied) {
			Write-Host "Cleaning up temporary workflow file in container..."
			& $global:enginePath exec --user node $global:containerName rm $containerWorkflowsPath 2>$null
		}
		Write-Host "--- Finished Workflow Import ---"
	}
	return $importSuccess
}

#==============================================================================
# Function: Invoke-ImportCredential
#==============================================================================
<#
.SYNOPSIS
    Imports n8n credentials into the container from a local file.
.DESCRIPTION
    Checks if the local credential export file exists. Copies the file into the
    container's temporary directory using the appropriate cp command. Executes
    'n8n import:credentials' inside the container as the 'node' user.
    Cleans up the temporary file.
.OUTPUTS
    [bool] $true if successful, $false otherwise.
#>
function Invoke-ImportCredential {
	[CmdletBinding(SupportsShouldProcess = $true)]
	[OutputType([bool])]
	param()

	$targetDescription = "n8n credentials into container '$($global:containerName)'"
	if (-not $PSCmdlet.ShouldProcess($targetDescription, "Import")) {
		Write-Host "Skipped import due to -WhatIf."
		return $false
	}

	# Check if source file exists
	if (-not (Test-Path -Path $localCredentialsPath -PathType Leaf)) {
		Write-Error "Local credentials file not found: '$localCredentialsPath'. Please export credentials first."
		return $false
	}

	Write-Host "--- Starting Credentials Import ---"
	$importSuccess = $true
	$fileCopied = $false
	try {
		# 1. Copy file from host to container
		Write-Host "Copying local credentials file to container..."
		if ($global:containerEngine -eq "docker") {
			$cpCommand = "$($global:enginePath) cp ""$localCredentialsPath"" ""$($global:containerName):$containerCredentialsPath"""
			Write-Host "Running cp command: $cpCommand"
			$cpResult = & $global:enginePath cp $localCredentialsPath "$($global:containerName):$containerCredentialsPath" 2>&1
		}
		else {
			# Podman
			$wslSourceFilePath = ConvertTo-WSLPath -winPath $localCredentialsPath
			if ($wslSourceFilePath -eq $localCredentialsPath) { throw "Failed to convert Windows path '$localCredentialsPath' to WSL path." }
			$innerCpCommand = "podman cp '$wslSourceFilePath' '$($global:containerName):$containerCredentialsPath'"
			$cpCommand = "$($global:enginePath) machine ssh ""$innerCpCommand"""
			Write-Host "Running cp command via podman machine ssh: $cpCommand"
			$cpResult = & $global:enginePath machine ssh "$innerCpCommand" 2>&1
		}
		Write-Host "Cp result: $cpResult"
		if ($LASTEXITCODE -ne 0) {
			Write-Error "Copying the local credentials file to the container failed. Exit Code: $LASTEXITCODE. Output: $cpResult"
			throw "Cp command failed."
		}
		Write-Host "Successfully copied credentials file to container."
		$fileCopied = $true

		# 2. Execute import command in container
		$importCmdArgs = @("import:credentials", "--input", $containerCredentialsPath)
		Write-Host "Running exec command: $($global:enginePath) exec --user node $($global:containerName) n8n $($importCmdArgs -join ' ')"
		$execResult = & $global:enginePath exec --user node $global:containerName n8n $importCmdArgs 2>&1
		Write-Host "Exec result: $execResult"
		if ($LASTEXITCODE -ne 0) {
			Write-Error "The 'n8n import:credentials' command failed. Exit Code: $LASTEXITCODE. Output: $execResult"
			throw "Exec command failed."
		}
		Write-Host "'n8n import:credentials' command executed successfully." -ForegroundColor Green
	}
	catch {
		Write-Error "Credentials import step failed: $_"
		$importSuccess = $false
	}
	finally {
		# 3. Cleanup temp file in container if it was copied
		if ($fileCopied) {
			Write-Host "Cleaning up temporary credentials file in container..."
			& $global:enginePath exec --user node $global:containerName rm $containerCredentialsPath 2>$null
		}
		Write-Host "--- Finished Credentials Import ---"
	}
	return $importSuccess
}


################################################################################
# Main Menu Loop using Generic Function
################################################################################

# Define Menu Title and Items
$menuTitle = "n8n Export/Import Menu"
$menuItems = [ordered]@{
	"1" = "Export Workflows"
	"2" = "Export Credentials"
	"3" = "Import Workflows"
	"4" = "Import Credentials"
	"S" = "Show Container Status" # Added for convenience
	"0" = "Exit menu"
}

# Define Menu Actions
$menuActions = @{
	"1" = { Invoke-ExportWorkflow }
	"2" = { Invoke-ExportCredential }
	"3" = { Invoke-ImportWorkflow }
	"4" = { Invoke-ImportCredential }
	"S" = {
		Show-ContainerStatus -ContainerName $global:containerName `
			-ContainerEngine $global:containerEngine `
			-EnginePath $global:enginePath `
			-DisplayName $global:containerName `
			-TcpPort 5678 ` # Assuming default n8n port
		-HttpPort 5678
	}
	# Note: "0" action is handled internally by Invoke-MenuLoop
}

# Ensure downloads directory exists before showing menu
if (-not (Test-Path -Path $localDownloadsDir -PathType Container)) {
	Write-Host "Creating downloads directory: $localDownloadsDir"
	New-Item -Path $localDownloadsDir -ItemType Directory -Force | Out-Null
}

# Invoke the Menu Loop
Invoke-MenuLoop -MenuTitle $menuTitle -MenuItems $menuItems -ActionMap $menuActions -ExitChoice "0"

Write-Host "Script finished."
