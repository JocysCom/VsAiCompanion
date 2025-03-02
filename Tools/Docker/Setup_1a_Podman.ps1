################################################################################
# File         : Setup_1a_Podman.ps1
# Description  : Script to manage Podman installation on Windows.
#                Dependencies: 
#                - CLI required for Machine
#                - CLI required for Desktop
#                - Machine required for Service
#
# Usage        : Run the script; ensure proper administrator privileges.
################################################################################

# Define default values at script scope
$fileSystem = "xfs"     # Default file system
$diskLocation = ""      # Default disk location (empty means use default)

# Dot-source shared helper functions from Setup_0.ps1:
. "$PSScriptRoot\Setup_0.ps1"

# Optionally ensure the script is running as Administrator and set the working directory.
#Ensure-Elevated
Set-ScriptLocation

# Ensure the script stops immediately if any error occurs.
$ErrorActionPreference = 'Stop'

#############################################
# Function: Check-PodmanCliAvailable
# Checks if the Podman CLI is available in the path.
# Returns: $true if Podman CLI is available, $false otherwise.
#############################################
function Check-PodmanCliAvailable {
    $podmanCmd = Get-Command podman -ErrorAction SilentlyContinue
    return ($null -ne $podmanCmd)
}

#############################################
# Function: Check-PodmanServiceAvailable
# Checks if the Podman Service is installed and available.
# Returns: $true if Podman Service is installed, $false otherwise.
#############################################
function Check-PodmanServiceAvailable {
    $serviceName = "PodmanMachineStart"
    $service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
    return ($null -ne $service)
}

#############################################
# Function: Check-PodmanMachineAvailable
# Checks if a Podman machine exists and is accessible.
# Returns: $true if a Podman machine exists, $false otherwise.
#############################################
function Check-PodmanMachineAvailable {
    # First check if Podman CLI is available
    if (-not (Check-PodmanCliAvailable)) {
        return $false
    }
    
    # Check if any Podman machines exist
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

#############################################
# Function: Check-PodmanMachineRunning
# Checks if any Podman machine is actually running.
# Returns: $true if a Podman machine is running, $false otherwise.
#############################################
function Check-PodmanMachineRunning {
    # First check if Podman CLI and machine are available
    if (-not (Check-PodmanMachineAvailable)) {
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

#############################################
# Function: Check-PodmanDesktopInstalled
# Checks if Podman Desktop is installed.
# Returns: $true if Podman Desktop is installed, $false otherwise.
#############################################
function Check-PodmanDesktopInstalled {
    return (Test-ApplicationInstalled "Podman Desktop")
}

#############################################
# Function: Display-PodmanStatus
# Displays the current status of all Podman components.
#############################################
function Display-PodmanStatus {
    Write-Host "`n==================== PODMAN STATUS ====================" -ForegroundColor Cyan
    
    # Check Podman CLI
    $cliAvailable = Check-PodmanCliAvailable
    if ($cliAvailable) {
        try {
            $podmanVersion = & podman version --format "{{.Client.Version}}" 2>$null
            Write-Host "Podman CLI: INSTALLED (Version: $podmanVersion)" -ForegroundColor Green
        } catch {
            Write-Host "Podman CLI: INSTALLED" -ForegroundColor Green
        }
    } else {
        Write-Host "Podman CLI: NOT INSTALLED" -ForegroundColor Red
    }
    
    # Check Podman Desktop
    $desktopInstalled = Check-PodmanDesktopInstalled
    if ($desktopInstalled) {
        Write-Host "Podman Desktop: INSTALLED" -ForegroundColor Green
    } else {
        Write-Host "Podman Desktop: NOT INSTALLED" -ForegroundColor Red
    }
    
    # Check Podman Machine
    if ($cliAvailable) {
        $machineAvailable = Check-PodmanMachineAvailable
        if ($machineAvailable) {
            try {
                $machineListJson = & podman machine ls --format json 2>&1
                $machines = $machineListJson | ConvertFrom-Json
                
                foreach ($machine in $machines) {
                    $machineState = $machine.State
                    $machineName = $machine.Name
                    
                    if ($machineState -eq "Running") {
                        Write-Host "Podman Machine '$machineName': AVAILABLE (Status: $machineState)" -ForegroundColor Green
                    } else {
                        Write-Host "Podman Machine '$machineName': AVAILABLE (Status: $machineState)" -ForegroundColor Yellow
                    }
                }
            } catch {
                Write-Host "Podman Machine: ERROR CHECKING STATUS" -ForegroundColor Yellow
            }
        } else {
            Write-Host "Podman Machine: NOT AVAILABLE" -ForegroundColor Red
        }
    } else {
        Write-Host "Podman Machine: NOT AVAILABLE (CLI required)" -ForegroundColor Red
    }
    
    # Check Podman Service
    $serviceAvailable = Check-PodmanServiceAvailable
    if ($serviceAvailable) {
        $service = Get-Service -Name "PodmanMachineStart" -ErrorAction SilentlyContinue
        $serviceStatus = $service.Status
        Write-Host "Podman Service: INSTALLED (Status: $serviceStatus)" -ForegroundColor Green
    } else {
        Write-Host "Podman Service: NOT INSTALLED" -ForegroundColor Red
    }
    
    Write-Host "=====================================================`n" -ForegroundColor Cyan
   
}

#############################################
# Function: Install-PodmanCLI
# Installs the Podman Program (CLI) by running the remote installer package.
#############################################
function Install-PodmanCLI {
    param(
        [string]$setupExeUrl = "https://github.com/containers/podman/releases/download/v5.4.0/podman-5.4.0-setup.exe",
        [string]$downloadFolder = ".\downloads"
    )
    
    # Check if Podman CLI is already installed
    if (Check-PodmanCliAvailable) {
        try {
            $podmanVersion = & podman version --format "{{.Client.Version}}" 2>$null
            Write-Host "Podman CLI is already installed (Version: $podmanVersion). Skipping installation." -ForegroundColor Green
            return $true
        } catch {
            Write-Host "Podman CLI is already installed. Skipping installation." -ForegroundColor Green
            return $true
        }
    }
    
    # Create downloads folder if it doesn't exist
    if (-not (Test-Path $downloadFolder)) {
        Write-Host "Creating downloads folder at $downloadFolder..."
        New-Item -ItemType Directory -Force -Path $downloadFolder | Out-Null
    }
    
    # Download the installer
    $exePath = Join-Path $downloadFolder "podman-5.4.0-setup.exe"
    Download-File -url $setupExeUrl -destinationPath $exePath
    
    # Launch the installer
    Write-Host "Launching Podman installer..."
    Start-Process -FilePath $exePath -Wait
    
    # Refresh environment variables so that the new installation can be located
    Refresh-EnvironmentVariables
    
    # Verify installation
    if (Check-PodmanCliAvailable) {
        try {
            $podmanVersion = & podman version --format "{{.Client.Version}}" 2>$null
            Write-Host "Podman CLI installed successfully (Version: $podmanVersion)." -ForegroundColor Green
        } catch {
            Write-Host "Podman CLI installed successfully." -ForegroundColor Green
        }
        return $true
    } else {
        Write-Error "Podman CLI installation failed. Please try again or install manually."
        return $false
    }
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
            Write-Error "Invalid path format: $customPath. Using default location."
            return ""
        }
    }
    else {
        Write-Host "Invalid selection. Using default disk location."
        return ""
    }
}

#############################################
# Function: Initialize-PodmanMachine
# Creates and initializes a new Podman machine.
# Separated from the Start function for clarity.
#############################################
function Initialize-PodmanMachine {
    param(
        [Parameter(Mandatory = $false)]
        [string]$FileSystem = "xfs",
        
        [Parameter(Mandatory = $false)]
        [string]$DiskLocation = ""
    )
    
    # Ensure that WSL and required Windows features are enabled
    Check-WSLStatus
    
    # Verify a Linux distribution is installed via WSL
    Write-Host "Verifying that a Linux distribution is installed via wsl.exe..."
    $wslListOutput = & wsl.exe --list --quiet 2>&1
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($wslListOutput)) {
        Write-Error "wsl.exe did not return a valid list of distributions. Please install a Linux distro (e.g., Ubuntu) via 'wsl --install' or the Microsoft Store."
        return $false
    }
    
    # Check if a machine already exists
    if (Check-PodmanMachineAvailable) {
        Write-Host "A Podman machine already exists. Do you want to replace it?"
        $replace = Read-Host "Replace existing machine? (Y/N, default is N)"
        if ($replace -ne "Y") {
            Write-Host "Using existing Podman machine."
            return $true
        }
        
        # Remove existing machines
        try {
            Write-Host "Removing existing Podman machines..."
            $machineListJson = & podman machine ls --format json 2>&1
            $machines = $machineListJson | ConvertFrom-Json
            
            foreach ($machine in $machines) {
                Write-Host "Removing machine: $($machine.Name)..."
                & podman machine rm -f $machine.Name
            }
        }
        catch {
            Write-Error "Failed to remove existing Podman machines: $_"
            return $false
        }
    }
    
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
    return $true
}

#############################################
# Function: Start-PodmanMachine
# Starts an existing Podman machine.
#############################################
function Start-PodmanMachine {
    param(
        [string]$MachineName = "default"
    )
	
        # Initialize and start Podman Machine
        if (-not (Check-PodmanCliAvailable)) {
            Write-Error "Podman CLI is not installed. Please install it first using option 1."
            exit 1
        }
    
    # Check if machine exists
    if (-not (Check-PodmanMachineAvailable)) {
        Write-Error "No Podman machine available to start. Please initialize a machine first."
        return $false
    }
    
    # Check if machine is already running
    if (Check-PodmanMachineRunning) {
        Write-Host "Podman machine is already running." -ForegroundColor Green
        return $true
    }
    
    try {
        Write-Host "Starting Podman machine '$MachineName'..."
        $startOutput = & podman machine start $MachineName 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Failed to start Podman machine '$MachineName'. Output: $startOutput"
            return $false
        }
        Write-Host "Podman machine '$MachineName' started successfully." -ForegroundColor Green
        return $true
    }
    catch {
        Write-Error "Error starting Podman machine: $_"
        return $false
    }
}

#############################################
# Function: Install-PodmanService
# Registers Podman as a Windows service that ensures the machine is running.
#############################################
function Install-PodmanService {
    param(
        [string]$ServiceName = "PodmanMachineStart"
    )
    
    # Check if Podman CLI is installed
    if (-not (Check-PodmanCliAvailable)) {
        Write-Error "Podman CLI is not installed. Please install it first using option 1."
        return $false
    }
    
    # Check if a Podman machine exists
    if (-not (Check-PodmanMachineAvailable)) {
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
    Write-Host "Podman is installed at: $destinationFolder"
    
    # Remove any existing service with the same name
    if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
        Write-Host "Service '$ServiceName' already exists. Removing it..."
        Start-Process -FilePath "sc.exe" -ArgumentList "stop $ServiceName" -Wait -NoNewWindow
        Start-Process -FilePath "sc.exe" -ArgumentList "delete $ServiceName" -Wait -NoNewWindow
        Start-Sleep -Seconds 5
    }
    
    # Create startup batch file
    $batchFilePath = Join-Path $destinationFolder "start-podman.bat"
    $batchContent = @"
@echo off
REM Wait for 10 seconds before checking machine status.
timeout /t 10
REM List Podman machines in JSON format.
podman machine ls --format json | findstr /C:"Running" >nul 2>&1
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
        return $false
    }
    Write-Host "Batch file created at: $batchFilePath"
    
    # Create the service via sc.exe
    $argsCreate = "create $ServiceName binPath= `"$batchFilePath`" start= auto"
    Write-Host "Creating service using: sc.exe $argsCreate"
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
    
    Write-Host "Service '$ServiceName' installed and started successfully." -ForegroundColor Green
    return $true
}

#############################################
# Function: Install-PodmanDesktop
# Installs Podman Desktop (UI) for managing Podman.
# Only requires Podman CLI to be installed - can create machines itself.
#############################################
function Install-PodmanDesktop {
    param(
        [string]$setupExeUrl = "https://github.com/podman-desktop/podman-desktop/releases/download/v1.16.2/podman-desktop-1.16.2-setup-x64.exe",
        [string]$downloadFolder = ".\downloads"
    )
    
    # Check if Podman Desktop is already installed
    if (Check-PodmanDesktopInstalled) {
        Write-Host "Podman Desktop is already installed. Skipping installation." -ForegroundColor Green
        return $true
    }
    
    # Check if Podman CLI is installed
    if (-not (Check-PodmanCliAvailable)) {
        Write-Error "Podman CLI is required for Podman Desktop. Please install Podman first using option 1."
        return $false
    }
    
    # Create downloads folder if it doesn't exist
    if (-not (Test-Path $downloadFolder)) {
        Write-Host "Creating downloads folder at $downloadFolder..."
        New-Item -ItemType Directory -Force -Path $downloadFolder | Out-Null
    }
    
    # Download the installer
    $exePath = Join-Path $downloadFolder "podman-desktop-1.16.2-setup-x64.exe"
    Download-File -url $setupExeUrl -destinationPath $exePath
    
    # Launch the installer without waiting
    Write-Host "Launching Podman Desktop installer..."
    Write-Host "IMPORTANT: The script will continue executing after launching the installer."
    Write-Host "Please complete the installation process when prompted."
    
    # Start without -Wait to prevent hanging
    Start-Process -FilePath $exePath
    
    # Give user a chance to see that installer has started
    Write-Host "Waiting 5 seconds for installer to start..." -ForegroundColor Yellow
    Start-Sleep -Seconds 5
    
    # Prompt user to confirm installation is complete
    $confirmation = Read-Host "Has the Podman Desktop installation completed? (Y/N)"
    while ($confirmation.ToUpper() -ne "Y") {
        $confirmation = Read-Host "Please type Y when the Podman Desktop installation has completed"
    }
    
    # Verify installation
    if (Check-PodmanDesktopInstalled) {
        Write-Host "Podman Desktop installed successfully." -ForegroundColor Green
        return $true
    } else {
        Write-Error "Podman Desktop installation could not be verified. Please check manually."
        return $false
    }
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
            return $false
        }
        Write-Host "Deleting service '$serviceName'..."
        $svcDeleteProcess = Start-Process -FilePath "sc.exe" -ArgumentList "delete $serviceName" -Wait -NoNewWindow -PassThru
        if ($svcDeleteProcess.ExitCode -ne 0) {
            Write-Error "Failed to delete service '$serviceName'. sc.exe returned exit code $($svcDeleteProcess.ExitCode)."
            return $false
        }
        Write-Host "Service '$serviceName' removed successfully." -ForegroundColor Green
        return $true
    }
    else {
        Write-Host "Service '$serviceName' not found. Nothing to remove." -ForegroundColor Yellow
        return $true
    }
}

#############################################
# Function: Remove-PodmanComponents
# Provides options to remove various Podman components.
#############################################
function Remove-PodmanComponents {
    Write-Host "Select component to remove:"
    Write-Host "1) Remove Podman Service only"
    Write-Host "2) Remove Podman Machine only"
    Write-Host "3) [Not Implemented] Uninstall Podman Desktop only"
    Write-Host "4) [Not Implemented] Uninstall Podman CLI"
    Write-Host "5) Exit without removing anything"
    
    $removeOption = Read-Host "Enter option (1-5, default is 5)"
    if ([string]::IsNullOrWhiteSpace($removeOption)) {
        $removeOption = "5"
    }
    
    switch ($removeOption) {
        "1" {
            if (Remove-PodmanService) {
                Write-Host "Podman service removed successfully." -ForegroundColor Green
            } else {
                Write-Error "Failed to remove Podman service."
            }
        }
        "2" {
            if (Check-PodmanMachineAvailable) {
                Write-Host "Removing Podman machines..."
                $machineListJson = & podman machine ls --format json 2>&1
                $machines = $machineListJson | ConvertFrom-Json
                
                foreach ($machine in $machines) {
                    Write-Host "Removing machine: $($machine.Name)..."
                    & podman machine rm -f $machine.Name
                    if ($LASTEXITCODE -eq 0) {
                        Write-Host "Machine '$($machine.Name)' removed successfully." -ForegroundColor Green
                    } else {
                        Write-Error "Failed to remove machine '$($machine.Name)'."
                    }
                }
            } else {
                Write-Host "No Podman machines found to remove." -ForegroundColor Yellow
            }
        }
        "3" {
            Write-Host "Uninstall of Podman Desktop must be done through Windows Add/Remove Programs." -ForegroundColor Yellow
        }
        "4" {
            Write-Host "Uninstall of Podman CLI must be done through Windows Add/Remove Programs." -ForegroundColor Yellow
        }
        "5" {
            Write-Host "No components will be removed." -ForegroundColor Green
        }
        default {
            Write-Host "Invalid option. No components will be removed." -ForegroundColor Yellow
        }
    }
}

#############################################
# Main Script Execution - Logical Menu
#############################################
Write-Host "=================================================="
Write-Host "PODMAN SETUP AND MANAGEMENT"
Write-Host "=================================================="
Write-Host "Select an option:"
Write-Host "1) Check Podman Status"
Write-Host "   - Displays the current status of Podman components"
Write-Host "2) Install Podman CLI"
Write-Host "   - Installs the Podman command-line tool"
Write-Host "3) Install Podman Desktop (UI)"
Write-Host "   - Installs the Podman Desktop manager (Requires only CLI)"
Write-Host "4) Initialize Podman Machine"
Write-Host "   - Creates and starts a Podman machine (Requires CLI)"
Write-Host "5) Register Podman Service"
Write-Host "   - Creates a Windows service to auto-start Podman (Requires Machine)"
Write-Host "6) Remove Podman Components"
Write-Host "   - Options to remove service, machine, or uninstall software"
Write-Host "=================================================="

$installOption = Read-Host "Enter your choice (1-6). Default is 6 if empty"
if ([string]::IsNullOrEmpty($installOption)) { $installOption = "6" }

switch ($installOption) {
    "1" {
        # Just show status (already displayed at start)
        Display-PodmanStatus
    }
    "2" {
        # Install Podman CLI
        Install-PodmanCLI
    }
    "3" {
        # Install Podman Desktop
        Install-PodmanDesktop
    }
    "4" {
        # Initialize and start Podman Machine
        $fileSystem = Select-FileSystem
        $diskLocation = Select-DiskLocation
        if (Initialize-PodmanMachine -FileSystem $fileSystem -DiskLocation $diskLocation) {
            Start-PodmanMachine
        }
    }
    "5" {
        # Install Podman Service
        Install-PodmanService
    }
    "6" {
        # Remove Podman Components
        Remove-PodmanComponents
    }
    default {
        Write-Error "Invalid selection. Exiting."
        exit 1
    }
}
