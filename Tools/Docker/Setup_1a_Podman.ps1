################################################################################
# File         : Setup_1a_Podman.ps1
# Description  : Script to manage Podman installation on Windows.
#                Dependencies hierarchy:
#                - CLI required for Machine
#                - CLI required for Desktop
#                - Machine required for Service
#
# Usage        : Run the script; ensure proper administrator privileges.
################################################################################

# Define default values at script scope
$diskLocation = ""      # Default disk location (empty means use default user profile location)

# Dot-source the necessary helper function files.
. "$PSScriptRoot\Setup_0_Core.ps1"
. "$PSScriptRoot\Setup_0_WSL.ps1" # Needed for Test-WSLStatus

# Optionally ensure the script is running as Administrator and set the working directory.
#Test-AdminPrivilege
Set-ScriptLocation

# Ensure the script stops immediately if any error occurs.
$ErrorActionPreference = 'Stop'

#==============================================================================
# Function: CheckPodmanCliAvailable
#==============================================================================
<#
.SYNOPSIS
	Checks if the Podman CLI command ('podman') is available in the system PATH.
.DESCRIPTION
	Uses Get-Command to check if 'podman' can be resolved.
.OUTPUTS
	[bool] Returns $true if the 'podman' command is found, $false otherwise.
.EXAMPLE
	if (CheckPodmanCliAvailable) { Write-Host "Podman CLI found." }
#>
function CheckPodmanCliAvailable {
	$podmanCmd = Get-Command podman -ErrorAction SilentlyContinue
	return ($null -ne $podmanCmd)
}

#==============================================================================
# Function: CheckPodmanServiceAvailable
#==============================================================================
<#
.SYNOPSIS
	Checks if the Podman Machine startup service ('PodmanMachineStart') is installed.
.DESCRIPTION
	Uses Get-Service to check for the existence of a Windows service named 'PodmanMachineStart'.
	This service is typically created by Install-PodmanService to auto-start the machine.
.OUTPUTS
	[bool] Returns $true if the service exists, $false otherwise.
.EXAMPLE
	if (CheckPodmanServiceAvailable) { Write-Host "Podman service found." }
#>
function CheckPodmanServiceAvailable {
	$serviceName = "PodmanMachineStart"
	$service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
	return ($null -ne $service)
}

#==============================================================================
# Function: CheckPodmanMachineAvailable
#==============================================================================
<#
.SYNOPSIS
	Checks if any Podman machine configuration exists.
.DESCRIPTION
	First checks if the Podman CLI is available. If so, it runs 'podman machine ls --format json'
	and checks if the output indicates that at least one machine is configured.
.OUTPUTS
	[bool] Returns $true if the CLI is available and at least one machine is listed, $false otherwise.
.EXAMPLE
	if (CheckPodmanMachineAvailable) { Write-Host "Podman machine(s) configured." }
.NOTES
	Doesn't check if the machine is running, only if it's configured.
#>
function CheckPodmanMachineAvailable {
	# First check if Podman CLI is available
	if (-not (CheckPodmanCliAvailable)) {
		return $false
	}

	# Check if any Podman machines exist by querying for their status
	try {
		$machineListJson = & podman machine ls --format json 2>&1
		$machines = $machineListJson | ConvertFrom-Json
		return ($null -ne $machines) -and ($machines.Count -gt 0)
	}
	catch {
		Write-Verbose "Error checking Podman machine: $_"
		return $false
	}
}

#==============================================================================
# Function: CheckPodmanMachineRunning
#==============================================================================
<#
.SYNOPSIS
	Checks if the default Podman machine is currently running.
.DESCRIPTION
	First checks if a Podman machine is available. If so, it runs 'podman machine ls --format json'
	and checks the 'State' property of the first machine listed (usually 'default').
.OUTPUTS
	[bool] Returns $true if a machine exists and its state is 'Running', $false otherwise.
.EXAMPLE
	if (CheckPodmanMachineRunning) { Write-Host "Podman machine is running." }
#>
function CheckPodmanMachineRunning {
	# First check if Podman CLI and machine are available
	if (-not (CheckPodmanMachineAvailable)) {
		return $false
	}

	# Check if any Podman machine is running
	try {
		$machineListJson = & podman machine ls --format json 2>&1
		$machines = $machineListJson | ConvertFrom-Json
		return ($null -ne $machines) -and ($machines.Count -gt 0) -and ($machines[0].State -eq "Running")
	}
	catch {
		Write-Verbose "Error checking Podman machine running state: $_"
		return $false
	}
}

#==============================================================================
# Function: CheckPodmanDesktopInstalled
#==============================================================================
<#
.SYNOPSIS
	Checks if Podman Desktop appears to be installed via Add/Remove Programs registry entries.
.DESCRIPTION
	Calls the Test-ApplicationInstalled helper function (from Setup_0_Core.ps1)
	to search for registry entries matching "Podman Desktop".
.OUTPUTS
	[bool] Returns $true if an entry matching "Podman Desktop" is found, $false otherwise.
.EXAMPLE
	if (CheckPodmanDesktopInstalled) { Write-Host "Podman Desktop is installed." }
.NOTES
	Relies on Test-ApplicationInstalled from Setup_0_Core.ps1.
#>
function CheckPodmanDesktopInstalled {
	return (Test-ApplicationInstalled "Podman Desktop")
}

#==============================================================================
# Function: DisplayPodmanStatus
#==============================================================================
<#
.SYNOPSIS
	Displays a summary of the installation and running status of various Podman components.
.DESCRIPTION
	Calls the various Check* functions (CheckPodmanCliAvailable, CheckPodmanDesktopInstalled,
	CheckPodmanMachineAvailable, CheckPodmanMachineRunning, CheckPodmanServiceAvailable)
	and formats the results into a user-friendly status summary written to the information stream.
.EXAMPLE
	DisplayPodmanStatus
.NOTES
	Uses Write-Information for output.
	Retrieves version and machine state details if components are available.
#>
function DisplayPodmanStatus {
	Write-Information "`n==================== PODMAN STATUS ===================="

	# Check Podman CLI
	$cliAvailable = CheckPodmanCliAvailable
	if ($cliAvailable) {
		try {
			$podmanVersion = & podman version --format "{{.Client.Version}}" 2>$null
			Write-Information "Podman CLI: INSTALLED (Version: $podmanVersion)"
		}
		catch {
			Write-Information "Podman CLI: INSTALLED"
		}
	}
	else {
		Write-Information "Podman CLI: NOT INSTALLED"
	}

	# Check Podman Desktop
	$desktopInstalled = CheckPodmanDesktopInstalled
	if ($desktopInstalled) {
		Write-Information "Podman Desktop: INSTALLED"
	}
	else {
		Write-Information "Podman Desktop: NOT INSTALLED"
	}

	# Check Podman Machine
	if ($cliAvailable) {
		$machineAvailable = CheckPodmanMachineAvailable
		if ($machineAvailable) {
			try {
				$machineListJson = & podman machine ls --format json 2>&1
				$machines = $machineListJson | ConvertFrom-Json

				foreach ($machine in $machines) {
					$machineState = $machine.State
					$machineName = $machine.Name

					if ($machineState -eq "Running") {
						Write-Information "Podman Machine '$machineName': AVAILABLE (Status: $machineState)"
					}
					else {
						Write-Information "Podman Machine '$machineName': AVAILABLE (Status: $machineState)"
					}
				}
			}
			catch {
				Write-Warning "Podman Machine: ERROR CHECKING STATUS"
			}
		}
		else {
			Write-Information "Podman Machine: NOT AVAILABLE"
		}
	}
	else {
		Write-Information "Podman Machine: NOT AVAILABLE (CLI required)"
	}

	# Check Podman Service
	$serviceAvailable = CheckPodmanServiceAvailable
	if ($serviceAvailable) {
		$service = Get-Service -Name "PodmanMachineStart" -ErrorAction SilentlyContinue
		$serviceStatus = $service.Status
		Write-Information "Podman Service: INSTALLED (Status: $serviceStatus)"
	}
	else {
		Write-Information "Podman Service: NOT INSTALLED"
	}

	Write-Information "=====================================================`n"

}

#==============================================================================
# Function: Install-PodmanCLI
#==============================================================================
<#
.SYNOPSIS
	Installs or upgrades the Podman Command Line Interface (CLI).
.DESCRIPTION
	Checks if Podman CLI is already installed. If installed, checks if the version matches the requested version
	and prompts for upgrade if different. If not installed or upgrade confirmed, it downloads the specified
	Podman setup executable, runs the installer, refreshes environment variables, and verifies the installation.
.PARAMETER PodmanVersion
	The target version of Podman to install (e.g., "5.4.0"). Defaults to "5.4.0".
.PARAMETER setupExeUrl
	Optional. The direct URL to the Podman setup executable. If not provided, it's constructed based on the PodmanVersion.
.PARAMETER downloadFolder
	The local folder to download the installer to. Defaults to '.\downloads'.
.OUTPUTS
	[bool] Returns $true if installation/upgrade is successful or skipped, $false on failure.
.EXAMPLE
	Install-PodmanCLI -PodmanVersion "5.1.0"
.EXAMPLE
	Install-PodmanCLI -setupExeUrl "http://example.com/podman-custom-setup.exe"
.NOTES
	Requires administrative privileges to run the installer.
	Uses Invoke-DownloadFile and Update-EnvironmentVariable helper functions.
	Uses Write-Information for status messages.
#>
function Install-PodmanCLI {
	param(
		[string]$PodmanVersion = "5.4.0",
		[string]$setupExeUrl = "",
		[string]$downloadFolder = ".\downloads"
	)

	# If URL is not provided, construct it from the version
	if ([string]::IsNullOrEmpty($setupExeUrl)) {
		$setupExeUrl = "https://github.com/containers/podman/releases/download/v$PodmanVersion/podman-$PodmanVersion-setup.exe"
	}

	# Check if Podman CLI is already installed
	if (CheckPodmanCliAvailable) {
		try {
			$installedVersion = & podman version --format "{{.Client.Version}}" 2>$null
			Write-Information "Podman CLI is already installed (Version: $installedVersion)."

			# Optionally check for upgrades
			if ($installedVersion -ne $PodmanVersion) {
				$upgrade = Read-Host "Would you like to upgrade from version $installedVersion to $PodmanVersion? (Y/N, default is N)"
				if ($upgrade -ne "Y") {
					Write-Information "Keeping current version $installedVersion."
					return $true
				}
			}
			else {
				Write-Information "You have the requested version. Skipping installation."
				return $true
			}
		}
		catch {
			Write-Information "Podman CLI is already installed. Skipping installation."
			return $true
		}
	}

	# Create downloads folder if it doesn't exist
	if (-not (Test-Path $downloadFolder)) {
		Write-Information "Creating downloads folder at $downloadFolder..."
		New-Item -ItemType Directory -Force -Path $downloadFolder | Out-Null
	}

	# Download the installer
	$exePath = Join-Path $downloadFolder "podman-5.4.0-setup.exe"
	Invoke-DownloadFile -url $setupExeUrl -destinationPath $exePath

	# Launch the installer
	Write-Information "Launching Podman installer..."
	Start-Process -FilePath $exePath -Wait

	# Refresh environment variables so that the new installation can be located
	Update-EnvironmentVariable

	# Verify installation
	if (CheckPodmanCliAvailable) {
		try {
			$podmanVersion = & podman version --format "{{.Client.Version}}" 2>$null
			Write-Information "Podman CLI installed successfully (Version: $podmanVersion)."
		}
		catch {
			Write-Information "Podman CLI installed successfully."
		}
		return $true
	}
	else {
		Write-Error "Podman CLI installation failed. Please try again or install manually."
		return $false
	}
}

#==============================================================================
# Function: Select-DiskLocation
#==============================================================================
<#
.SYNOPSIS
	Prompts the user to choose between the default or a custom disk location for the Podman machine VHDX file.
.DESCRIPTION
	Presents a menu asking the user to select the default location (within the user profile) or a custom path.
	If custom is chosen, prompts the user to enter the path. Validates the path format and checks if the drive exists.
.OUTPUTS
	[string] Returns the selected custom path, or an empty string if the default location is chosen or if the custom path is invalid/rejected.
.EXAMPLE
	$location = Select-DiskLocation
	if ($location) { Initialize-PodmanMachine -Rootful -DiskPath $location } else { Initialize-PodmanMachine -Rootful }
.NOTES
	Uses Write-Information for prompts and status messages.
	Uses Read-Host for user input.
#>
function Select-DiskLocation {
	# Define Menu Title and Items
	$menuTitle = "Select disk location machine virtual disk"
	$menuItems = [ordered]@{
		"1" = "Default location (user profile)"
		"2" = "Custom location"
		# "0" = "Exit/Use Default" # Implicit exit choice
	}

	# Define Menu Actions
	$selectedPath = "" # Variable to store the result from the action block
	$actionCompleted = $false # Flag to exit loop after one action

	$menuActions = @{
		"1" = {
			Write-Information "Using default disk location"
			$script:selectedPath = "" # Use script scope to modify outer variable
			$script:actionCompleted = $true
		}
		"2" = {
			$customPath = Read-Host "Enter custom disk location path (e.g., D:\VM\Disks)"

			# Validate the path format
			if ([string]::IsNullOrWhiteSpace($customPath)) {
				Write-Information "No path provided. Using default location."
				$script:selectedPath = ""
			}
			else {
				# Check if the path is valid
				try {
					$null = [System.IO.Path]::GetFullPath($customPath) # Test format
					$drive = [System.IO.Path]::GetPathRoot($customPath)
					if (-not [System.IO.Directory]::Exists($drive)) {
						Write-Warning "Drive $drive does not exist. Please ensure it's available before continuing."
						$confirm = Read-Host "Continue with this path anyway? (Y/N, default is N)"
						if ($confirm -ne "Y") {
							Write-Information "Using default disk location instead."
							$script:selectedPath = ""
						}
						else {
							Write-Information "Custom disk location selected: $customPath"
							$script:selectedPath = $customPath
						}
					}
					else {
						Write-Information "Custom disk location selected: $customPath"
						$script:selectedPath = $customPath
					}
				}
				catch {
					Write-Error "Invalid path format: $customPath. Using default location."
					$script:selectedPath = ""
				}
			}
			$script:actionCompleted = $true
		}
		# "0" action will exit the loop, returning the initial $selectedPath ("")
	}

	# Invoke the Menu Loop - modify the loop slightly to exit after one action
	# We can't use 'exit' inside the action block as it would exit the whole script.
	# Instead, we use a flag.
	do {
		Invoke-MenuLoop -MenuTitle $menuTitle -MenuItems $menuItems -ActionMap $menuActions -ExitChoice "0"
		# Check if an action was completed or if the user chose to exit (choice 0)
		if ($actionCompleted -or $choice -eq "0") {
			break # Exit the custom do-while loop
		}
		# If an invalid choice was made, Invoke-MenuLoop handles the warning and repeats.
	} while ($true) # Loop until explicitly broken

	# Return the path selected by the action block (or default "")
	return $selectedPath
}

#==============================================================================
# Function: Initialize-PodmanMachine
#==============================================================================
<#
.SYNOPSIS
	Initializes a new default Podman machine using WSL.
.DESCRIPTION
	Ensures WSL is correctly set up by calling Test-WSLStatus.
	Verifies that at least one Linux distribution is installed via WSL.
	Runs 'podman machine init' to create the default machine configuration and WSL distribution.
	Starts the newly created machine using 'podman machine start'.
.OUTPUTS
	[bool] Returns $true if the machine is initialized and started successfully, $false otherwise.
.EXAMPLE
	Initialize-PodmanMachine
.NOTES
	Requires administrative privileges if WSL features need enabling.
	Uses Test-WSLStatus helper function.
	Uses Write-Information for status messages.
#>
function Initialize-PodmanMachine {
	# Ensure that WSL and required Windows features are enabled
	Test-WSLStatus

	# Verify a Linux distribution is installed via WSL
	Write-Information "Verifying that a Linux distribution is installed via wsl.exe..."
	$wslListOutput = & wsl.exe --list --quiet 2>&1
	if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($wslListOutput)) {
		Write-Error "wsl.exe did not return a valid list of distributions. Please install a Linux distro (e.g., Ubuntu) via 'wsl --install' or the Microsoft Store."
		return $false
	}

	# Initialize the machine with default settings first
	Write-Information "Initializing Podman machine with default settings..."
	$initArgs = @("machine", "init") # Args for splatting

	# Execute the command to create the machine
	Write-Information "Executing: podman $($initArgs -join ' ')"
	$initOutput = & podman @initArgs 2>&1
	Write-Information $initOutput

	if ($LASTEXITCODE -ne 0) {
		Write-Error "Failed to initialize Podman machine. Output: $initOutput"
		return $false
	}

	# Start the machine to ensure everything is properly set up
	Write-Information "Starting the Podman machine to complete initialization..."
	& podman machine start default
	if ($LASTEXITCODE -ne 0) {
		Write-Error "Failed to start Podman machine after initialization."
		return $false
	}

	Write-Information "Podman machine initialized and started successfully with default settings."

	# Show the current location of the machine's disk
	$userProfile = $env:USERPROFILE
	$podmanFolder = Join-Path $userProfile ".local\share\containers\podman\machine\wsl\wsldist\podman-machine-default"
	Write-Information "Current machine location: $podmanFolder"

	return $true
}

#############################################
#==============================================================================
# Function: Move-PodmanMachineImage
#==============================================================================
<#
.SYNOPSIS
	Moves an existing Podman WSL machine's VHDX and associated files to a new location.
.DESCRIPTION
	Performs the following steps:
	1. Stops the specified Podman machine.
	2. Verifies the machine is stopped via 'wsl -l -v'.
	3. Locates the source VHDX folder in the user's profile.
	4. Checks for sufficient disk space at the destination.
	5. Copies the entire source folder to the destination.
	6. Prompts the user to confirm unregistering the original WSL distribution.
	7. Unregisters the original WSL distribution ('wsl --unregister').
	8. Imports the copied VHDX using 'wsl --import-in-place' or 'wsl --import'.
	9. Removes the old Podman machine configuration files.
	10. Re-initializes the Podman machine configuration ('podman machine init').
	11. Starts the Podman machine in the new location ('podman machine start').
	Includes recovery steps if starting the moved machine fails initially.
.PARAMETER DestinationPath
	The target directory where the Podman machine folder should be moved. Mandatory.
.PARAMETER MachineName
	The name of the Podman machine to move. Defaults to 'default'.
.OUTPUTS
	[bool] Returns $true if the move and restart are successful, $false otherwise.
.EXAMPLE
	Move-PodmanMachineImage -DestinationPath "D:\PodmanMachines"
.NOTES
	Requires administrative privileges for WSL commands.
	This is a complex operation involving direct manipulation of WSL distributions. Use with caution.
	Uses Write-Information for status messages.
	User interaction required via Read-Host to confirm unregistration.
#>
function Move-PodmanMachineImage {
	param(
		[Parameter(Mandatory = $true)]
		[string]$DestinationPath,
		[string]$MachineName = "default"
	)

	$wslDistName = "podman-machine-$MachineName"

	# Step 1: Stop the Podman machine if it's running
	Write-Information "Stopping Podman machine..."
	& podman machine stop $wslDistName
	if ($LASTEXITCODE -ne 0) {
		Write-Error "Failed to stop Podman machine. Aborting move operation."
		return $false
	}

	# Verify the machine is stopped by checking WSL status
	Write-Information "Verifying machine is stopped using 'wsl -l -v'..."
	$wslStatus = & wsl -l -v 2>&1
	Write-Information $wslStatus

	# Check if the machine is actually stopped
	$machineStatus = $wslStatus | Select-String -Pattern $wslDistName -SimpleMatch
	if ($machineStatus -match "Running") {
		Write-Error "Podman machine is still running. Please stop it manually using 'wsl --terminate $wslDistName'."
		return $false
	}

	# Step 2: Locate the VHDX folder and check disk space
	$userProfile = $env:USERPROFILE
	$podmanFolder = Join-Path $userProfile ".local\share\containers\podman\machine\wsl\wsldist\$wslDistName"

	if (-not (Test-Path $podmanFolder)) {
		Write-Error "Podman machine folder not found at: $podmanFolder"
		return $false
	}

	# Get source folder size
	$folderSize = (Get-ChildItem -Path $podmanFolder -Recurse | Measure-Object -Property Length -Sum).Sum
	$folderSizeGB = [math]::Round($folderSize / 1GB, 2)
	Write-Information "Source folder size: $folderSizeGB GB"

	# Check destination disk space
	$destinationDrive = [System.IO.Path]::GetPathRoot($DestinationPath)
	# Extract just the drive letter with colon (e.g., "D:")
	$driveLetter = $destinationDrive.Substring(0, 2)
	$freeSpace = (Get-CimInstance -ClassName Win32_LogicalDisk -Filter "DeviceID='$driveLetter'").FreeSpace
	$freeSpaceGB = [math]::Round($freeSpace / 1GB, 2)
	Write-Information "Free space on destination drive ($driveLetter): $freeSpaceGB GB"

	if ($freeSpace -lt ($folderSize * 1.1)) {
		# Add 10% buffer
		Write-Error "Insufficient disk space on destination drive. Need at least $([math]::Round($folderSize * 1.1 / 1GB, 2)) GB but only $freeSpaceGB GB available."
		return $false
	}

	# Create destination directory if it doesn't exist
	$destinationFolder = Join-Path $DestinationPath $wslDistName
	if (-not (Test-Path $destinationFolder)) {
		Write-Information "Creating destination directory: $destinationFolder"
		New-Item -ItemType Directory -Path $destinationFolder -Force | Out-Null
	}

	# Copy the entire folder (not just VHDX)
	Write-Information "Copying Podman machine folder to new location..."
	Write-Information "From: $podmanFolder"
	Write-Information "To: $destinationFolder"

	try {
		Copy-Item -Path "$podmanFolder\*" -Destination $destinationFolder -Recurse -Force
		Write-Information "Folder copied successfully."
	}
	catch {
		Write-Error "Failed to copy Podman machine folder: $_"
		return $false
	}

	# Verify the VHDX file was copied
	$vhdxPath = Join-Path $destinationFolder "ext4.vhdx"
	if (-not (Test-Path $vhdxPath)) {
		Write-Error "VHDX file not found after copy at: $vhdxPath"
		return $false
	}

	# Step 3: Unregister the WSL distribution only if it exists
	if (-not $skipUnregister) {
		Write-Information "Unregistering WSL distribution: $wslDistName"
		Write-Information "This will remove the original VHDX file."
		$confirm = Read-Host "Continue? (Y/N, default is N)"
		if ($confirm -ne "Y") {
			Write-Information "Operation cancelled. The copied files remain at: $destinationFolder"
			Write-Information "Original Podman machine is untouched."
			return $false
		}

		& wsl --unregister $wslDistName
		if ($LASTEXITCODE -ne 0) {
			Write-Warning "Failed to unregister WSL distribution. Continuing with import step."
		}
	}
	else {
		Write-Information "Skipping WSL unregister step as distribution was not found."
	}

	# Step 4: Import the copied VHDX file in-place
	Write-Information "Importing VHDX in-place..."
	& wsl --import-in-place $wslDistName $vhdxPath
	if ($LASTEXITCODE -ne 0) {
		Write-Error "Failed to import VHDX in-place. Check if the path is correct and try again."
		Write-Information "VHDX path: $vhdxPath"

		# Alternate approach if import-in-place fails
		Write-Information "Trying alternate import method..."
		& wsl --import $wslDistName $destinationFolder $vhdxPath
		if ($LASTEXITCODE -ne 0) {
			Write-Error "All import methods failed. Manual intervention required."
			return $false
		}
	}

	# Step 5: Update Podman configuration if needed
	# The .config directory contains Podman's machine configuration files
	$configDir = Join-Path $env:USERPROFILE ".config\containers\podman\machine"

	if (Test-Path $configDir) {
		Write-Information "Updating Podman machine configuration..."

		# First stop any existing podman processes
		Get-Process | Where-Object { $_.Name -like "*podman*" } | ForEach-Object {
			Write-Information "Stopping process: $($_.Name) (ID: $($_.Id))"
			Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
		}

		# Make a backup of the current configuration
		$backupFolder = Join-Path $env:TEMP "podman-config-backup-$(Get-Date -Format 'yyyyMMddHHmmss')"
		Write-Information "Creating backup of Podman configuration at: $backupFolder"
		if (Test-Path $configDir) {
			Copy-Item -Path $configDir -Destination $backupFolder -Recurse
		}

		# Remove the configuration to force Podman to recreate it
		Write-Information "Removing current machine configuration to force recreation..."
		Remove-Item -Path $configDir -Recurse -Force -ErrorAction SilentlyContinue
	}

	# Step 6: Initialize/recreate the Podman machine
	Write-Information "Re-initializing Podman machine with the moved WSL distribution..."

	# Wait for WSL to fully register the imported distribution
	Start-Sleep -Seconds 5

	# Check if podman-machine-default is registered in WSL
	$wslCheck = & wsl --list | Select-String -Pattern $wslDistName
	if (-not $wslCheck) {
		Write-Error "WSL distribution '$wslDistName' was not properly registered. Please check WSL status."
		return $false
	}

	# Initialize a new Podman machine with "--now=false" to avoid starting it yet
	Write-Information "Creating new Podman machine configuration..."
	& podman machine init --now=false
	if ($LASTEXITCODE -ne 0) {
		Write-Error "Failed to initialize new Podman machine configuration."
		return $false
	}

	# Start the Podman machine
	Write-Information "Starting Podman machine..."
	& podman machine start $MachineName
	if ($LASTEXITCODE -ne 0) {
		Write-Error "Failed to start Podman machine after move. Check if the import was successful using 'wsl -l -v'."

		# If starting fails, try a different approach - remove and recreate
		Write-Information "Attempting recovery by recreating the machine..."

		# Keep the WSL distribution but remove the Podman machine
		& podman machine rm -f $MachineName 2>$null

		# Initialize a new machine pointing to the existing WSL distribution
		& podman machine init
		if ($LASTEXITCODE -eq 0) {
			& podman machine start $MachineName
			if ($LASTEXITCODE -eq 0) {
				Write-Information "Machine successfully recreated and started."
				return $true
			}
		}

		Write-Information "Recovery failed. You may need to: "
		Write-Information "1. Run 'podman machine rm -f $MachineName' to remove the failed configuration"
		Write-Information "2. Run 'podman machine init' to create a new machine"
		Write-Information "3. Run 'podman machine start $MachineName' to start it"
		return $false
	}

	Write-Information "Podman machine image successfully moved to: $destinationFolder"
	return $true
}

#==============================================================================
# Function: Start-PodmanMachine
#==============================================================================
<#
.SYNOPSIS
	Starts an existing Podman machine.
.DESCRIPTION
	Checks if the Podman CLI is available and if the specified machine exists.
	Checks if the machine is already running. If not running, it executes 'podman machine start'.
	Supports -WhatIf.
.PARAMETER MachineName
	The name of the Podman machine to start. Defaults to 'default'.
.OUTPUTS
	[bool] Returns $true if the machine is already running or starts successfully.
		   Returns $false if the machine fails to start or if the start is skipped due to -WhatIf.
.EXAMPLE
	Start-PodmanMachine
.EXAMPLE
	Start-PodmanMachine -MachineName "my-custom-machine" -WhatIf
.NOTES
	Uses CheckPodmanCliAvailable, CheckPodmanMachineAvailable, CheckPodmanMachineRunning helper functions.
	Uses Write-Information for status messages.
#>
function Start-PodmanMachine {
	[CmdletBinding(SupportsShouldProcess = $true)]
	param(
		[string]$MachineName = "default"
	)

	# Initialize and start Podman Machine
	if (-not (CheckPodmanCliAvailable)) {
		Write-Error "Podman CLI is not installed. Please install it first using option 1."
		exit 1
	}

	# Check if machine exists
	if (-not (CheckPodmanMachineAvailable)) {
		Write-Error "No Podman machine available to start. Please initialize a machine first."
		return $false
	}

	# Check if machine is already running
	if (CheckPodmanMachineRunning) {
		Write-Information "Podman machine is already running."
		return $true
	}

	try {
		# Start the Podman machine (boots the Linux OS in WSL2)
		if ($PSCmdlet.ShouldProcess($MachineName, "Start Podman Machine")) {
			Write-Information "Starting Podman machine '$MachineName'..."
			$startOutput = & podman machine start $MachineName 2>&1
			if ($LASTEXITCODE -ne 0) {
				Write-Error "Failed to start Podman machine '$MachineName'. Output: $startOutput"
				return $false
			}
			Write-Information "Podman machine '$MachineName' started successfully."
			return $true
		}
		else {
			return $false # Indicate action was skipped due to -WhatIf
		}
	}
	catch {
		Write-Error "Error starting Podman machine: $_"
		return $false
	}
}

#==============================================================================
# Function: Install-PodmanService
#==============================================================================
<#
.SYNOPSIS
	Installs a Windows service ('PodmanMachineStart') to automatically start the Podman machine on system boot.
.DESCRIPTION
	Checks if Podman CLI and a machine are available.
	Attempts to start the machine using Start-PodmanMachine.
	Removes any existing service with the same name.
	Creates a batch file ('start-podman.bat') in the Podman installation directory with logic to check network/WSL status and start the machine.
	Creates a Windows service using 'sc.exe create' that runs the batch file with 'start= auto'.
	Starts the newly created service using 'sc.exe start'.
.PARAMETER ServiceName
	The name for the Windows service. Defaults to 'PodmanMachineStart'.
.OUTPUTS
	[bool] Returns $true if the service is installed and started successfully, $false otherwise.
.EXAMPLE
	Install-PodmanService
.NOTES
	Requires administrative privileges to create/start services.
	The created batch file includes checks for network and WSL availability before attempting to start the machine.
	Uses Write-Information for status messages.
#>
function Install-PodmanService {
	param(
		[string]$ServiceName = "PodmanMachineStart"
	)

	# Check if Podman CLI is installed
	if (-not (CheckPodmanCliAvailable)) {
		Write-Error "Podman CLI is not installed. Please install it first using option 1."
		return $false
	}

	# Check if a Podman machine exists
	if (-not (CheckPodmanMachineAvailable)) {
		Write-Error "No Podman machine available. Please initialize a machine first using option 2."
		return $false
	}

	# Verify machine can be started
	if (-not (Start-PodmanMachine)) {
		Write-Error "Failed to start Podman machine. Cannot install service."
		return $false
	}

	# Retrieve the installation folder
	$podmanCmd = Get-Command podman
	$destinationFolder = Split-Path $podmanCmd.Source
	Write-Information "Podman is installed at: $destinationFolder"

	# Remove any existing service with the same name
	if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
		Write-Information "Service '$ServiceName' already exists. Removing it..."
		Start-Process -FilePath "sc.exe" -ArgumentList "stop $ServiceName" -Wait -NoNewWindow
		Start-Process -FilePath "sc.exe" -ArgumentList "delete $ServiceName" -Wait -NoNewWindow
		Start-Sleep -Seconds 5
	}

	# Create startup batch file with more robust startup logic
	$batchFilePath = Join-Path $destinationFolder "start-podman.bat"
	$batchContent = @"
@echo off
echo [%date% %time%] Podman machine startup service triggered >> "%TEMP%\podman-service.log"

REM Wait for network to be available
:check_network
ping -n 1 8.8.8.8 >nul 2>&1
if %errorlevel% neq 0 (
	echo [%date% %time%] Waiting for network... >> "%TEMP%\podman-service.log"
	timeout /t 10 /nobreak >nul
	goto check_network
)

REM Wait for WSL to be available
:check_wsl
wsl --list >nul 2>&1
if %errorlevel% neq 0 (
	echo [%date% %time%] Waiting for WSL availability... >> "%TEMP%\podman-service.log"
	timeout /t 10 /nobreak >nul
	goto check_wsl
)

REM List Podman machines in JSON format
echo [%date% %time%] Checking if Podman machine is already running... >> "%TEMP%\podman-service.log"
podman machine ls --format json | findstr /C:"Running" >nul 2>&1
if %errorlevel%==0 (
	echo [%date% %time%] Podman machine already running >> "%TEMP%\podman-service.log"
	exit /b 0
) else (
	echo [%date% %time%] Starting Podman machine... >> "%TEMP%\podman-service.log"
	podman machine start
	set START_RESULT=%errorlevel%
	if %START_RESULT%==0 (
		echo [%date% %time%] Podman machine started successfully >> "%TEMP%\podman-service.log"
	) else (
		echo [%date% %time%] Failed to start Podman machine: error %START_RESULT% >> "%TEMP%\podman-service.log"
	)
	exit /b %START_RESULT%
)
"@
	$batchContent | Set-Content -Path $batchFilePath -Encoding ASCII
	if (-not (Test-Path $batchFilePath)) {
		Write-Error "Failed to create the batch file at $batchFilePath."
		return $false
	}
	Write-Information "Batch file created at: $batchFilePath"

	# Create the service via sc.exe
	# This registers a Windows service that runs the batch file on system startup
	$argsCreate = "create $ServiceName binPath= `"$batchFilePath`" start= auto"
	Write-Information "Creating service using: sc.exe $argsCreate"
	$svcCreateProcess = Start-Process -FilePath "sc.exe" -ArgumentList $argsCreate -Wait -NoNewWindow -PassThru
	if ($svcCreateProcess.ExitCode -ne 0) {
		Write-Error "Failed to create service '$ServiceName'. sc.exe returned exit code $($svcCreateProcess.ExitCode)."
		return $false
	}

	# Start the service
	$svcStartProcess = Start-Process -FilePath "sc.exe" -ArgumentList "start $ServiceName" -Wait -NoNewWindow -PassThru
	if ($svcStartProcess.ExitCode -ne 0) {
		Write-Error "Failed to start service '$ServiceName'. sc.exe returned exit code $($svcStartProcess.ExitCode)."
		return $false
	}

	Write-Information "Service '$ServiceName' installed and started successfully."
	return $true
}

#==============================================================================
# Function: Install-PodmanDesktop
#==============================================================================
<#
.SYNOPSIS
	Installs Podman Desktop UI.
.DESCRIPTION
	Checks if Podman Desktop is already installed.
	Checks if Podman CLI is installed (required dependency).
	Downloads the specified Podman Desktop setup executable.
	Launches the installer asynchronously (does not wait for completion).
	Prompts the user to confirm when the installation is finished.
	Verifies the installation by checking again if Podman Desktop is installed.
.PARAMETER setupExeUrl
	The direct URL to the Podman Desktop setup executable. Defaults to a specific version URL.
.PARAMETER downloadFolder
	The local folder to download the installer to. Defaults to '.\downloads'.
.OUTPUTS
	[bool] Returns $true if installation is successful (based on user confirmation and re-check), $false otherwise.
.EXAMPLE
	Install-PodmanDesktop
.NOTES
	Requires user interaction to complete the installation GUI and confirm completion.
	Uses Invoke-DownloadFile helper function.
	Uses Write-Information for status messages.
#>
function Install-PodmanDesktop {
	param(
		[string]$setupExeUrl = "https://github.com/podman-desktop/podman-desktop/releases/download/v1.16.2/podman-desktop-1.16.2-setup-x64.exe",
		[string]$downloadFolder = ".\downloads"
	)

	# Check if Podman Desktop is already installed
	if (CheckPodmanDesktopInstalled) {
		Write-Information "Podman Desktop is already installed. Skipping installation."
		return $true
	}

	# Check if Podman CLI is installed
	if (-not (CheckPodmanCliAvailable)) {
		Write-Error "Podman CLI is required for Podman Desktop. Please install Podman first using option 1."
		return $false
	}

	# Create downloads folder if it doesn't exist
	if (-not (Test-Path $downloadFolder)) {
		Write-Information "Creating downloads folder at $downloadFolder..."
		New-Item -ItemType Directory -Force -Path $downloadFolder | Out-Null
	}

	# Download the installer
	$exePath = Join-Path $downloadFolder "podman-desktop-1.16.2-setup-x64.exe"
	Invoke-DownloadFile -url $setupExeUrl -destinationPath $exePath

	# Launch the installer without waiting
	Write-Information "Launching Podman Desktop installer..."
	Write-Information "IMPORTANT: The script will continue executing after launching the installer."
	Write-Information "Please complete the installation process when prompted."

	# Start without -Wait to prevent hanging
	Start-Process -FilePath $exePath

	# Give user a chance to see that installer has started
	Write-Information "Waiting 5 seconds for installer to start..."
	Start-Sleep -Seconds 5

	# Prompt user to confirm installation is complete
	$confirmation = Read-Host "Has the Podman Desktop installation completed? (Y/N)"
	while ($confirmation.ToUpper() -ne "Y") {
		$confirmation = Read-Host "Please type Y when the Podman Desktop installation has completed"
	}

	# Verify installation
	if (CheckPodmanDesktopInstalled) {
		Write-Information "Podman Desktop installed successfully."
		return $true
	}
	else {
		Write-Error "Podman Desktop installation could not be verified. Please check manually."
		return $false
	}
}

#==============================================================================
# Function: Remove-PodmanService
#==============================================================================
<#
.SYNOPSIS
	Stops and removes the Podman machine auto-start service ('PodmanMachineStart').
.DESCRIPTION
	Checks if the 'PodmanMachineStart' service exists using Get-Service.
	If it exists, it stops the service using 'sc.exe stop' and then deletes it using 'sc.exe delete'.
	Supports -WhatIf.
.OUTPUTS
	[bool] Returns $true if the service is removed successfully or didn't exist, $false if stop/delete fails.
.EXAMPLE
	Remove-PodmanService -WhatIf
.NOTES
	Requires administrative privileges to stop/delete services.
	Uses Write-Information for status messages.
#>
function Remove-PodmanService {
	[CmdletBinding(SupportsShouldProcess = $true)]
	param()

	$serviceName = "PodmanMachineStart"
	if (Get-Service -Name $serviceName -ErrorAction SilentlyContinue) {
		if ($PSCmdlet.ShouldProcess($serviceName, "Stop Service")) {
			Write-Information "Stopping service '$serviceName'..."
			$svcStopProcess = Start-Process -FilePath "sc.exe" -ArgumentList "stop $serviceName" -Wait -NoNewWindow -PassThru
			if ($svcStopProcess.ExitCode -ne 0) {
				Write-Error "Failed to stop service '$serviceName'. sc.exe returned exit code $($svcStopProcess.ExitCode)."
				return $false
			}
		}
		if ($PSCmdlet.ShouldProcess($serviceName, "Delete Service")) {
			Write-Information "Deleting service '$serviceName'..."
			$svcDeleteProcess = Start-Process -FilePath "sc.exe" -ArgumentList "delete $serviceName" -Wait -NoNewWindow -PassThru
			if ($svcDeleteProcess.ExitCode -ne 0) {
				Write-Error "Failed to delete service '$serviceName'. sc.exe returned exit code $($svcDeleteProcess.ExitCode)."
				return $false
			}
			Write-Information "Service '$serviceName' removed successfully."
		}
		return $true
	}
	else {
		Write-Information "Service '$serviceName' not found. Nothing to remove."
		return $true
	}
}

#==============================================================================
# Function: Remove-PodmanComponent
#==============================================================================
<#
.SYNOPSIS
	Provides a menu to select and remove specific Podman components like the service or machine.
.DESCRIPTION
	Displays a menu with options to:
	1. Remove the Podman Service (calls Remove-PodmanService).
	2. Remove the Podman Machine (calls 'podman machine rm -f' for all listed machines).
	3. (Placeholder) Uninstall Podman Desktop (directs user to Add/Remove Programs).
	4. (Placeholder) Uninstall Podman CLI (directs user to Add/Remove Programs).
	Prompts the user for a choice and executes the corresponding action. Supports -WhatIf for service/machine removal.
.EXAMPLE
	Remove-PodmanComponent
.NOTES
	Uses Read-Host for menu selection.
	Actual uninstallation of Desktop/CLI is not implemented and requires manual user action.
	Uses Write-Information for status messages.
#>
function Remove-PodmanComponent {
	[CmdletBinding(SupportsShouldProcess = $true)]
	param()

	# Define Menu Title and Items
	$menuTitle = "Select component to remove"
	$menuItems = [ordered]@{
		"1" = "Remove Podman Service only"
		"2" = "Remove Podman Machine only"
		"3" = "[Not Implemented] Uninstall Podman Desktop only"
		"4" = "[Not Implemented] Uninstall Podman CLI"
		"0" = "Exit without removing anything"
	}

	# Define Menu Actions
	$actionCompleted = $false # Flag to exit loop after one action

	$menuActions = @{
		"1" = {
			if (Remove-PodmanService) { # Remove-PodmanService handles ShouldProcess
				Write-Information "Podman service removed successfully."
			}
			else {
				Write-Error "Failed to remove Podman service."
			}
			$script:actionCompleted = $true
		}
		"2" = {
			if (CheckPodmanMachineAvailable) {
				Write-Information "Removing Podman machines..."
				$machineListJson = & podman machine ls --format json 2>&1
				$machines = $machineListJson | ConvertFrom-Json

				foreach ($machine in $machines) {
					# Check ShouldProcess here before calling rm
					if ($PSCmdlet.ShouldProcess($machine.Name, "Remove Podman Machine")) {
						Write-Information "Removing machine: $($machine.Name)..."
						& podman machine rm -f $machine.Name
						if ($LASTEXITCODE -eq 0) {
							Write-Information "Machine '$($machine.Name)' removed successfully."
						}
						else {
							Write-Error "Failed to remove machine '$($machine.Name)'."
						}
					}
				}
			}
			else {
				Write-Information "No Podman machines found to remove."
			}
			$script:actionCompleted = $true
		}
		"3" = {
			Write-Information "Uninstall of Podman Desktop must be done through Windows Add/Remove Programs."
			$script:actionCompleted = $true
		}
		"4" = {
			Write-Information "Uninstall of Podman CLI must be done through Windows Add/Remove Programs."
			$script:actionCompleted = $true
		}
		# "0" action will exit the loop
	}

	# Invoke the Menu Loop - modify the loop slightly to exit after one action
	do {
		Invoke-MenuLoop -MenuTitle $menuTitle -MenuItems $menuItems -ActionMap $menuActions -ExitChoice "0"
		# Check if an action was completed or if the user chose to exit (choice 0)
		if ($actionCompleted -or $choice -eq "0") {
			break # Exit the custom do-while loop
		}
	} while ($true) # Loop until explicitly broken

	if ($choice -eq "0") {
		Write-Information "Exited without removing components."
	}
}

#############################################
# Main Script Execution - Using Invoke-MenuLoop
#############################################

# Define Menu Title and Items
$menuTitle = "PODMAN SETUP AND MANAGEMENT"
$menuItems = [ordered]@{
	"1" = "Check Podman Status"
	"2" = "Install Podman CLI"
	"3" = "Install Podman Desktop (UI)"
	"4" = "Initialize Podman Machine"
	"5" = "Move Podman Machine"
	"6" = "Register Podman Service"
	"7" = "Remove Podman Components"
	"0" = "Exit"
}

# Define Menu Actions
$menuActions = @{
	"1" = { DisplayPodmanStatus }
	"2" = { Install-PodmanCLI }
	"3" = { Install-PodmanDesktop }
	"4" = {
		if (Initialize-PodmanMachine) {
			Start-PodmanMachine
		}
	}
	"5" = {
		$diskLocation = Select-DiskLocation
		# Only proceed if a valid custom path was returned or default is used (empty string)
		if ($diskLocation -ne $null) { # Check if Select-DiskLocation didn't error/cancel
			Move-PodmanMachineImage -DestinationPath $diskLocation
		}
	}
	"6" = { Install-PodmanService }
	"7" = { Remove-PodmanComponent }
	# "0" action is handled internally by Invoke-MenuLoop
}

# Invoke the Menu Loop
Invoke-MenuLoop -MenuTitle $menuTitle -MenuItems $menuItems -ActionMap $menuActions -ExitChoice "0"
