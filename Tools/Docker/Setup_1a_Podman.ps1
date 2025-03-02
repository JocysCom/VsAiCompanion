################################################################################
# File         : Setup_1a_Podman.ps1
# Description  : Script to manage Podman installation on Windows.
#                Provides four options:
#                   1) Install Podman Program (CLI)
#                   2) Install Podman UI (Podman Desktop)
#                   3) Register Podman Program as a Service
#                   4) Remove Podman Service
#                Option 3 ensures the Podman machine is running before
#                creating the service. This version includes an explicit check
#                for WSL (Windows Subsystem for Linux), which is required for
#                machine initialization.
#
# Usage        : Run the script; ensure proper administrator privileges.
################################################################################

# Dot-source shared helper functions from Setup_0.ps1:
. "$PSScriptRoot\Setup_0.ps1"

# Optionally ensure the script is running as Administrator and set the working directory.
#Ensure-Elevated
Set-ScriptLocation

# Ensure the script stops immediately if any error occurs.
$ErrorActionPreference = 'Stop'

#############################################
# Function: Ensure-And-Start-PodmanMachine
# Verifies that a Podman machine exists by checking the JSON output of
# "podman machine ls --format json". If the array is empty, it attempts to
# initialize a default machine and then starts it. If a machine exists but is
# not running, it starts the machine.
#############################################
function Ensure-And-Start-PodmanMachine {
	param(
		[Parameter(Mandatory = $false)]
		[string]$FileSystem = "xfs", # Default to xfs, can be changed to ext4 or other compatible fs
		
		[Parameter(Mandatory = $false)]
		[string]$DiskLocation = ""    # Empty string means use default location
	)
	
	# Ensure that WSL and required Windows features are enabled.
	Check-WSLStatus

	Write-Host "Verifying that a Linux distribution is installed via wsl.exe..."
	$wslListOutput = & wsl.exe --list --quiet 2>&1
	if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($wslListOutput)) {
		Write-Error "wsl.exe did not return a valid list of distributions. Please install a Linux distro (e.g., Ubuntu) via 'wsl --install' or the Microsoft Store."
		return $false
	}

	Write-Host "Checking for an existing Podman machine..."
	# podman machine ls [options]
	# ls          List Podman virtual machines.
	# --format string  Format the output using a Go template (e.g., "json").
	$machineListJson = & podman machine ls --format json 2>&1

	# If the output mentions wsl.exe usage errors, output a friendly message.
	if ($machineListJson -match "wsl.exe") {
		Write-Error "Podman encountered an issue with wsl.exe: $machineListJson"
		return $false
	}
	
	try {
		$machines = $machineListJson | ConvertFrom-Json
	}
	catch {
		Write-Error "Error parsing machine list JSON: $_"
		return $false
	}
	
	if (($machines -eq $null) -or ($machines.Count -eq 0)) {
		Write-Host "No Podman machine detected (empty array). Initializing a default machine with $FileSystem filesystem..."
		 
		# Build the init command with required parameters
		$initCmd = "podman machine init --filesystem $FileSystem"
		 
		# Add disk location if specified
		if (-not [string]::IsNullOrWhiteSpace($DiskLocation)) {
			# Ensure the directory exists
			if (-not (Test-Path $DiskLocation)) {
				try {
					New-Item -ItemType Directory -Path $DiskLocation -Force | Out-Null
					Write-Host "Created disk location directory: $DiskLocation"
				}
				catch {
					Write-Error "Failed to create disk location directory: $DiskLocation. Error: $_"
					return $false
				}
			}
			 
			# Add the image-path option to the command
			$initCmd += " --image-path `"$DiskLocation`""
			Write-Host "Using custom disk location: $DiskLocation"
		}
		else {
			Write-Host "Using default disk location"
		}
		 
		# Add the machine name to the command
		$initCmd += " default"
		 
		# Execute the command
		Write-Host "Executing: $initCmd"
		$initOutput = Invoke-Expression $initCmd 2>&1
		 
		if ($LASTEXITCODE -ne 0) {
			Write-Error "Failed to initialize Podman machine. Output: $initOutput"
			return $false
		}
		 
		Write-Host "Podman machine initialized successfully with $FileSystem filesystem."
		Start-Sleep -Seconds 2
		 
		# podman machine start [options] MACHINE_NAME
		# start    Start a Podman virtual machine.
		# 'default' specifies the machine name.
		$startOutput = & podman machine start default 2>&1
		if ($LASTEXITCODE -ne 0) {
			Write-Error "Failed to start Podman machine after initialization. Output: $startOutput"
			return $false
		}
		Write-Host "Podman machine started successfully."
		return $true
	}
	else {
		$machine = $machines[0]
		if ($machine.State -ne "Running") {
			Write-Host "Podman machine '$($machine.Name)' is not running. Starting it..."
			# podman machine start [options] MACHINE_NAME
			# start    Starts the specified Podman virtual machine.
			$startOutput = & podman machine start $machine.Name 2>&1
			if ($LASTEXITCODE -ne 0) {
				Write-Error "Failed to start Podman machine '$($machine.Name)'. Output: $startOutput"
				return $false
			}
			Write-Host "Podman machine '$($machine.Name)' started successfully."
		}
		else {
			Write-Host "Podman machine '$($machine.Name)' is already running."
		}
		return $true
	}
}

#############################################
# Function: Select-DiskLocation
# Prompts the user to select a custom disk location for Podman machine or use default
#############################################
function Select-DiskLocation {
	Write-Host "Select disk location for Podman machine virtual disk:"
	Write-Host "1) Default location (user profile)"
	Write-Host "2) Custom location"
	$locationChoice = Read-Host "Enter your choice (1 or 2, default is 1)"
	
	if ([string]::IsNullOrWhiteSpace($locationChoice) -or $locationChoice -eq "1") {
		Write-Host "Using default disk location"
		return ""  # Return empty string to use default location
	}
	elseif ($locationChoice -eq "2") {
		$customPath = Read-Host "Enter custom disk location path (e.g., D:\VM\Disks)"
		
		# Validate the path format
		if ([string]::IsNullOrWhiteSpace($customPath)) {
			Write-Host "No path provided. Using default location."
			return ""
		}
		
		# Check if the path is valid
		try {
			# Test if path is in valid format
			$null = [System.IO.Path]::GetFullPath($customPath)
			
			# If drive doesn't exist, inform the user but continue (we'll create the directory later)
			$drive = [System.IO.Path]::GetPathRoot($customPath)
			if (-not [System.IO.Directory]::Exists($drive)) {
				Write-Warning "Drive $drive does not exist. Please ensure it's available before continuing."
				$confirm = Read-Host "Continue with this path anyway? (Y/N, default is N)"
				if ($confirm -ne "Y") {
					Write-Host "Using default disk location instead."
					return ""
				}
			}
			
			Write-Host "Custom disk location selected: $customPath"
			return $customPath
		}
		catch {
			Wrhite-Error "Invalid path format: $customPath. Using default location."
			return ""
		}
	}
	else {
		Write-Host "Invalid selection. Using default disk location."
		return ""
	}
}

#############################################
# Function: Install-PodmanRemote
# Installs the Podman Program (CLI) by running the remote installer package.
#############################################
function Install-PodmanRemote {
	param(
		[string]$setupExeUrl = "https://github.com/containers/podman/releases/download/v5.4.0/podman-5.4.0-setup.exe",
		[string]$downloadFolder = ".\downloads"
	)
	if (-not (Test-Path $downloadFolder)) {
		Write-Host "Creating downloads folder at $downloadFolder..."
		New-Item -ItemType Directory -Force -Path $downloadFolder | Out-Null
	}
	
	$exePath = Join-Path $downloadFolder "podman-5.4.0-setup.exe"
	Download-File -url $setupExeUrl -destinationPath $exePath

	Write-Host "Launching Podman installer..."
	Start-Process -FilePath $exePath -Wait
}

#############################################
# Function: Install-PodmanDesktop
# Installs Podman Desktop (UI) for managing an existing Podman installation.
#############################################
function Install-PodmanDesktop {
	param(
		[string]$setupExeUrl = "https://github.com/podman-desktop/podman-desktop/releases/download/v1.16.2/podman-desktop-1.16.2-setup-x64.exe",
		[string]$downloadFolder = ".\downloads"
	)
	if (-not (Test-Path $downloadFolder)) {
		Write-Host "Creating downloads folder at $downloadFolder..."
		New-Item -ItemType Directory -Force -Path $downloadFolder | Out-Null
	}
	
	$exePath = Join-Path $downloadFolder "podman-desktop-1.16.2-setup-x64.exe"
	Download-File -url $setupExeUrl -destinationPath $exePath

	Write-Host "Launching Podman Desktop installer..."
	Start-Process -FilePath $exePath -Wait
}

#############################################
# Function: Install-PodmanService
# Registers Podman as a Windows service that ensures the machine is running.
# It checks that Podman CLI is installed and that a machine exists (or gets
# initialized using Ensure-And-Start-PodmanMachine). It then creates a batch file
# that verifies the machine state before attempting to start it and registers the
# service.
#############################################
function Install-PodmanService {
	param(
		[string]$downloadFolder = ".\downloads",
		[string]$FileSystem = "xfs", # Default to xfs
		[string]$DiskLocation = ""    # Empty string means use default location
	)

	# Ensure Podman CLI is installed.
	$podmanCmd = Get-Command podman -ErrorAction SilentlyContinue
	if (-not $podmanCmd) {
		Write-Error "Podman CLI is not installed. Please install it first."
		return
	}

	# Ensure a Podman machine exists and is running.
	if (-not (Ensure-And-Start-PodmanMachine -FileSystem $FileSystem -DiskLocation $DiskLocation)) {
		Write-Error "Podman machine is not available; service installation aborted."
		return
	}

	# Retrieve the installation folder.
	$destinationFolder = Split-Path $podmanCmd.Source
	Write-Host "Podman is installed at: $destinationFolder"

	# Remove any existing service with the same name.
	$serviceName = "PodmanMachineStart"
	if (Get-Service -Name $serviceName -ErrorAction SilentlyContinue) {
		Write-Host "Service '$serviceName' already exists. Removing it..."
		Start-Process -FilePath "sc.exe" -ArgumentList "stop $serviceName" -Wait -NoNewWindow
		Start-Process -FilePath "sc.exe" -ArgumentList "delete $serviceName" -Wait -NoNewWindow
		Start-Sleep -Seconds 5
	}

	# Add comment: This batch file uses the following Podman commands:
	# podman machine ls [options] -> List virtual machines in JSON format.
	# podman machine start [MACHINE_NAME] -> Start the Podman machine if not running.
	$batchFilePath = Join-Path $destinationFolder "start-podman.bat"
	$batchContent = @"
@echo off
REM Wait for 10 seconds before checking machine status.
timeout /t 10
REM List Podman machines in JSON format.
podman machine ls --format json | findstr /C:""Running"" >nul 2>&1
if %errorlevel%==0 (
	exit 0
) else (
	REM Start Podman machine.
	podman machine start
	exit %errorlevel%
)
"@
	$batchContent | Set-Content -Path $batchFilePath -Encoding ASCII
	if (-not (Test-Path $batchFilePath)) {
		Write-Error "Failed to create the batch file at $batchFilePath."
		return
	}
	Write-Host "Batch file created at: $batchFilePath"

	# Create the service via sc.exe.
	$argsCreate = "create $serviceName binPath= `"$batchFilePath`" start= auto"
	Write-Host "Creating service using: sc.exe $argsCreate"
	$svcCreateProcess = Start-Process -FilePath "sc.exe" -ArgumentList $argsCreate -Wait -NoNewWindow -PassThru
	if ($svcCreateProcess.ExitCode -ne 0) {
		Write-Error "Failed to create service '$serviceName'. sc.exe returned exit code $($svcCreateProcess.ExitCode)."
		return
	}

	# Start the service.
	$svcStartProcess = Start-Process -FilePath "sc.exe" -ArgumentList "start $serviceName" -Wait -NoNewWindow -PassThru
	if ($svcStartProcess.ExitCode -ne 0) {
		Write-Error "Failed to start service '$serviceName'. sc.exe returned exit code $($svcStartProcess.ExitCode)."
		return
	}
	Write-Host "Service '$serviceName' installed and started successfully."
}

#############################################
# Function: Remove-PodmanService
# Stops and removes the Podman service if it exists.
#############################################
function Remove-PodmanService {
	$serviceName = "PodmanMachineStart"
	if (Get-Service -Name $serviceName -ErrorAction SilentlyContinue) {
		Write-Host "Stopping service '$serviceName'..."
		$svcStopProcess = Start-Process -FilePath "sc.exe" -ArgumentList "stop $serviceName" -Wait -NoNewWindow -PassThru
		if ($svcStopProcess.ExitCode -ne 0) {
			Write-Error "Failed to stop service '$serviceName'. sc.exe returned exit code $($svcStopProcess.ExitCode)."
			return
		}
		Write-Host "Deleting service '$serviceName'..."
		$svcDeleteProcess = Start-Process -FilePath "sc.exe" -ArgumentList "delete $serviceName" -Wait -NoNewWindow -PassThru
		if ($svcDeleteProcess.ExitCode -ne 0) {
			Write-Error "Failed to delete service '$serviceName'. sc.exe returned exit code $($svcDeleteProcess.ExitCode)."
			return
		}
		Write-Host "Service '$serviceName' removed successfully."
	}
	else {
		Write-Host "Service '$serviceName' not found."
	}
}

#############################################
# Function: Ensure-PodmanDesktopInstalled
# Installs Podman Desktop if not present, ensuring that a Podman machine exists.
#############################################
function Ensure-PodmanDesktopInstalled {
	if (-not (Get-Command podman -ErrorAction SilentlyContinue)) {
		Write-Error "Podman CLI is required for Podman Desktop. Please install Podman first."
		return
	}
	if (-not (Ensure-And-Start-PodmanMachine)) {
		Write-Error "Podman Desktop requires an initialized and running Podman machine."
		return
	}
	if (Test-ApplicationInstalled "Podman Desktop") {
		Write-Host "Podman Desktop is already installed. Skipping."
		return
	}
	Write-Host "Installing Podman Desktop (UI)..."
	Install-PodmanDesktop
}

#############################################
# Function: Select-FileSystem
# Prompts the user to select a file system for Podman machine
#############################################
function Select-FileSystem {
	Write-Host "Select file system for Podman machine:"
	Write-Host "1) XFS (default, better performance but less compatible for recovery)"
	Write-Host "2) EXT4 (more compatible for mounting and container recovery)"
	$fsChoice = Read-Host "Enter your choice (1 or 2, default is 1)"
	
	if ([string]::IsNullOrWhiteSpace($fsChoice) -or $fsChoice -eq "1") {
		return "xfs"
	}
	elseif ($fsChoice -eq "2") {
		return "ext4"
	}
	else {
		Write-Host "Invalid selection. Using default (xfs)."
		return "xfs"
	}
}

#############################################
# Main Script Execution - Logical Menu
#############################################
Write-Host "=================================================="
Write-Host "Select installation option:"
Write-Host "1) Install Podman Program (CLI)"
Write-Host "   - Installs the Podman command-line tool via the remote package."
Write-Host "2) Install Podman UI (Podman Desktop)"
Write-Host "   - Installs the Podman Desktop manager (UI). (Requires Podman CLI)"
Write-Host "3) Register Podman Program as a Service"
Write-Host "   - Creates a native Windows service to ensure the Podman machine is running."
Write-Host "     (Requires Podman CLI and an initialized Podman machine)"
Write-Host "4) Remove Podman Service"
Write-Host "   - Stops and removes the Podman service if it exists."
$installOption = Read-Host "Enter your choice (1, 2, 3, or 4). Default is 1 if empty."
if ([string]::IsNullOrEmpty($installOption)) { $installOption = "1" }

# Define default values at script scope
$fileSystem = "xfs"     # Default file system
$diskLocation = ""      # Default disk location (empty means use default)

switch ($installOption) {
	"1" {
		if (-not (Get-Command podman -ErrorAction SilentlyContinue)) {
			$fileSystem = Select-FileSystem
			$diskLocation = Select-DiskLocation
			Install-PodmanRemote

			# Refresh environment variables so that the new installation can be located.
			Refresh-EnvironmentVariables
			# After installation, ensure machine is initialized with selected filesystem and disk location.
			if (-not (Ensure-And-Start-PodmanMachine -FileSystem $fileSystem -DiskLocation $diskLocation)) {
				Write-Error "Podman machine initialization failed."
				exit 1
			}
		}
		else {
			Write-Host "Podman Program (CLI) is already installed. Skipping installation."
		}
	}
	"2" {
		if (Get-Command podman -ErrorAction SilentlyContinue) {
			$fileSystem = Select-FileSystem
			$diskLocation = Select-DiskLocation
			# When ensuring Podman Desktop installation, use the selected filesystem and disk location.
			# Update the machine if needed with the selected configuration.
			if (-not (Ensure-And-Start-PodmanMachine -FileSystem $fileSystem -DiskLocation $diskLocation)) {
				Write-Error "Podman machine initialization failed."
				exit 1
			}
			else {
				Ensure-PodmanDesktopInstalled
			}
		}
		else {
			Write-Host "Podman CLI is required for the UI. Installing Podman Program first."
			$fileSystem = Select-FileSystem
			$diskLocation = Select-DiskLocation
			Install-PodmanRemote
			# After installation, ensure machine is initialized with selected filesystem and disk location.
			if (-not (Ensure-And-Start-PodmanMachine -FileSystem $fileSystem -DiskLocation $diskLocation)) {
				Write-Error "Podman machine initialization failed."
				exit 1
			}
		}
	}
	"3" {
		if (Get-Command podman -ErrorAction SilentlyContinue) {
			$fileSystem = Select-FileSystem
			$diskLocation = Select-DiskLocation
			if (-not (Install-PodmanService -FileSystem $fileSystem -DiskLocation $diskLocation)) {
				Write-Error "Podman Service installation failed."
				exit 1
			}
		}
		else {
			Write-Error "Podman CLI is required for service registration. Exiting."
			exit 1
		}
	}
	"4" {
		Remove-PodmanService
	}
	default {
		Write-Error "Invalid selection. Exiting."
		exit 1
	}
}