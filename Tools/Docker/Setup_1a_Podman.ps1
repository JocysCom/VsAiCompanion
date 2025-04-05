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
. "$PSScriptRoot\Setup_0_WSL.ps1" # Needed for Check-WSLStatus

# Optionally ensure the script is running as Administrator and set the working directory.
#Ensure-Elevated
Set-ScriptLocation

# Ensure the script stops immediately if any error occurs.
$ErrorActionPreference = 'Stop'

#############################################
# Function: CheckPodmanCliAvailable
# Checks if the Podman CLI is available in the path.
# Returns: $true if Podman CLI is available, $false otherwise.
#############################################
function CheckPodmanCliAvailable {
    $podmanCmd = Get-Command podman -ErrorAction SilentlyContinue
    return ($null -ne $podmanCmd)
}

#############################################
# Function: CheckPodmanServiceAvailable
# Checks if the Podman Service is installed and available.
# The service automatically starts the Podman machine on system boot.
# Returns: $true if Podman Service is installed, $false otherwise.
#############################################
function CheckPodmanServiceAvailable {
    $serviceName = "PodmanMachineStart"
    $service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
    return ($null -ne $service)
}

#############################################
# Function: CheckPodmanMachineAvailable
# Checks if a Podman machine exists and is accessible.
# The Podman machine is the WSL2-based virtual environment where containers run.
# Returns: $true if a Podman machine exists, $false otherwise.
#############################################
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

#############################################
# Function: CheckPodmanMachineRunning
# Checks if any Podman machine is actually running.
# A running machine has booted its Linux OS and is ready to run containers.
# Returns: $true if a Podman machine is running, $false otherwise.
#############################################
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

#############################################
# Function: CheckPodmanDesktopInstalled
# Checks if Podman Desktop is installed.
# Podman Desktop is the GUI management tool for Podman.
# Returns: $true if Podman Desktop is installed, $false otherwise.
#############################################
function CheckPodmanDesktopInstalled {
    return (Test-ApplicationInstalled "Podman Desktop")
}

#############################################
# Function: DisplayPodmanStatus
# Displays the current status of all Podman components.
#############################################
function DisplayPodmanStatus {
    Write-Host "`n==================== PODMAN STATUS ====================" -ForegroundColor Cyan
    
    # Check Podman CLI
    $cliAvailable = CheckPodmanCliAvailable
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
    $desktopInstalled = CheckPodmanDesktopInstalled
    if ($desktopInstalled) {
        Write-Host "Podman Desktop: INSTALLED" -ForegroundColor Green
    } else {
        Write-Host "Podman Desktop: NOT INSTALLED" -ForegroundColor Red
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
    $serviceAvailable = CheckPodmanServiceAvailable
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
# The CLI is the core component that provides the 'podman' command.
#############################################
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
            Write-Host "Podman CLI is already installed (Version: $installedVersion)." -ForegroundColor Green
            
            # Optionally check for upgrades
            if ($installedVersion -ne $PodmanVersion) {
                $upgrade = Read-Host "Would you like to upgrade from version $installedVersion to $PodmanVersion? (Y/N, default is N)"
                if ($upgrade -ne "Y") {
                    Write-Host "Keeping current version $installedVersion." -ForegroundColor Green
                    return $true
                }
            } else {
                Write-Host "You have the requested version. Skipping installation." -ForegroundColor Green
                return $true
            }
        } catch {
            Write-Host "Podman CLI is already installed. Skipping installation." -ForegroundColor Green
            return $true
        }
    }hh
    
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
    if (CheckPodmanCliAvailable) {
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
# Function: Select-DiskLocation
# Prompts the user to select a custom disk location for Podman machine or use default
# This determines where the VHDX (virtual disk) file will be stored
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
# Creates and initializes a new Podman machine with default options
# then offers to move the machine image to another location using WSL commands.
#############################################
function Initialize-PodmanMachine {
    # Ensure that WSL and required Windows features are enabled
    Check-WSLStatus
    
    # Verify a Linux distribution is installed via WSL
    Write-Host "Verifying that a Linux distribution is installed via wsl.exe..."
    $wslListOutput = & wsl.exe --list --quiet 2>&1
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($wslListOutput)) {
        Write-Error "wsl.exe did not return a valid list of distributions. Please install a Linux distro (e.g., Ubuntu) via 'wsl --install' or the Microsoft Store."
        return $false
    }
    
    # Initialize the machine with default settings first
    Write-Host "Initializing Podman machine with default settings..."
    $initCmd = "podman machine init"
    
    # Execute the command to create the machine
    Write-Host "Executing: $initCmd"
    $initOutput = Invoke-Expression $initCmd 2>&1  # -v for verbose
    Write-Host $initOutput
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to initialize Podman machine. Output: $initOutput"
        return $false
    }
    
    # Start the machine to ensure everything is properly set up
    Write-Host "Starting the Podman machine to complete initialization..."
    & podman machine start default
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to start Podman machine after initialization."
        return $false
    }
    
    Write-Host "Podman machine initialized and started successfully with default settings." -ForegroundColor Green
    
    # Show the current location of the machine's disk
    $userProfile = $env:USERPROFILE
    $podmanFolder = Join-Path $userProfile ".local\share\containers\podman\machine\wsl\wsldist\podman-machine-default"
    Write-Host "Current machine location: $podmanFolder"
    
    return $true
}

#############################################
# Function: Move-PodmanMachineImage
# Moves a Podman machine image to a new location using WSL commands.
# Follows the procedure:
# 1. Stop Podman machine
# 2. Copy the VHDX folder to the new location
# 3. Unregister the WSL distribution
# 4. Import the copied VHDX in-place
# 5. Start the Podman machine
#############################################
# Fix for the Move-PodmanMachineImage function in Setup_1a_Podman.ps1

function Move-PodmanMachineImage {
    param(
        [Parameter(Mandatory = $true)]
        [string]$DestinationPath,
        [string]$MachineName = "default"
    )
    
    $wslDistName = "podman-machine-$MachineName"
    
    # Step 1: Stop the Podman machine if it's running
    Write-Host "Stopping Podman machine..."
    & podman machine stop $wslDistName
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to stop Podman machine. Aborting move operation."
        return $false
    }
    
    # Verify the machine is stopped by checking WSL status
    Write-Host "Verifying machine is stopped using 'wsl -l -v'..."
    $wslStatus = & wsl -l -v 2>&1
    Write-Host $wslStatus
    
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
    Write-Host "Source folder size: $folderSizeGB GB"
    
    # Check destination disk space
    $destinationDrive = [System.IO.Path]::GetPathRoot($DestinationPath)
    # Extract just the drive letter with colon (e.g., "D:")
    $driveLetter = $destinationDrive.Substring(0, 2)
    $freeSpace = (Get-WmiObject -Class Win32_LogicalDisk -Filter "DeviceID='$driveLetter'").FreeSpace
    $freeSpaceGB = [math]::Round($freeSpace / 1GB, 2)
    Write-Host "Free space on destination drive ($driveLetter): $freeSpaceGB GB"
    
    if ($freeSpace -lt ($folderSize * 1.1)) {  # Add 10% buffer
        Write-Error "Insufficient disk space on destination drive. Need at least $([math]::Round($folderSize * 1.1 / 1GB, 2)) GB but only $freeSpaceGB GB available."
        return $false
    }
    
    # Create destination directory if it doesn't exist
    $destinationFolder = Join-Path $DestinationPath $wslDistName
    if (-not (Test-Path $destinationFolder)) {
        Write-Host "Creating destination directory: $destinationFolder"
        New-Item -ItemType Directory -Path $destinationFolder -Force | Out-Null
    }
    
    # Copy the entire folder (not just VHDX)
    Write-Host "Copying Podman machine folder to new location..."
    Write-Host "From: $podmanFolder"
    Write-Host "To: $destinationFolder"
    
    try {
        Copy-Item -Path "$podmanFolder\*" -Destination $destinationFolder -Recurse -Force
        Write-Host "Folder copied successfully."
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
        Write-Host "Unregistering WSL distribution: $wslDistName"
        Write-Host "This will remove the original VHDX file."
        $confirm = Read-Host "Continue? (Y/N, default is N)"
        if ($confirm -ne "Y") {
            Write-Host "Operation cancelled. The copied files remain at: $destinationFolder"
            Write-Host "Original Podman machine is untouched."
            return $false
        }

        & wsl --unregister $wslDistName
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Failed to unregister WSL distribution. Continuing with import step."
        }
    } else {
        Write-Host "Skipping WSL unregister step as distribution was not found."
    }

    # Step 4: Import the copied VHDX file in-place
    Write-Host "Importing VHDX in-place..."
    & wsl --import-in-place $wslDistName $vhdxPath
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to import VHDX in-place. Check if the path is correct and try again."
        Write-Host "VHDX path: $vhdxPath"

        # Alternate approach if import-in-place fails
        Write-Host "Trying alternate import method..."
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
        Write-Host "Updating Podman machine configuration..."

        # First stop any existing podman processes
        Get-Process | Where-Object { $_.Name -like "*podman*" } | ForEach-Object {
            Write-Host "Stopping process: $($_.Name) (ID: $($_.Id))"
            Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
        }

        # Make a backup of the current configuration
        $backupFolder = Join-Path $env:TEMP "podman-config-backup-$(Get-Date -Format 'yyyyMMddHHmmss')"
        Write-Host "Creating backup of Podman configuration at: $backupFolder"
        if (Test-Path $configDir) {
            Copy-Item -Path $configDir -Destination $backupFolder -Recurse
        }

        # Remove the configuration to force Podman to recreate it
        Write-Host "Removing current machine configuration to force recreation..."
        Remove-Item -Path $configDir -Recurse -Force -ErrorAction SilentlyContinue
    }

    # Step 6: Initialize/recreate the Podman machine
    Write-Host "Re-initializing Podman machine with the moved WSL distribution..."

    # Wait for WSL to fully register the imported distribution
    Start-Sleep -Seconds 5

    # Check if podman-machine-default is registered in WSL
    $wslCheck = & wsl --list | Select-String -Pattern $wslDistName
    if (-not $wslCheck) {
        Write-Error "WSL distribution '$wslDistName' was not properly registered. Please check WSL status."
        return $false
    }

    # Initialize a new Podman machine with "--now=false" to avoid starting it yet
    Write-Host "Creating new Podman machine configuration..."
    & podman machine init --now=false
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to initialize new Podman machine configuration."
        return $false
    }

    # Start the Podman machine
    Write-Host "Starting Podman machine..."
    & podman machine start $MachineName
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to start Podman machine after move. Check if the import was successful using 'wsl -l -v'."

        # If starting fails, try a different approach - remove and recreate
        Write-Host "Attempting recovery by recreating the machine..."

        # Keep the WSL distribution but remove the Podman machine
        & podman machine rm -f $MachineName 2>$null

        # Initialize a new machine pointing to the existing WSL distribution
        & podman machine init
        if ($LASTEXITCODE -eq 0) {
            & podman machine start $MachineName
            if ($LASTEXITCODE -eq 0) {
                Write-Host "Machine successfully recreated and started."
                return $true
            }
        }

        Write-Host "Recovery failed. You may need to: "
        Write-Host "1. Run 'podman machine rm -f $MachineName' to remove the failed configuration"
        Write-Host "2. Run 'podman machine init' to create a new machine"
        Write-Host "3. Run 'podman machine start $MachineName' to start it"
        return $false
    }

    Write-Host "Podman machine image successfully moved to: $destinationFolder" -ForegroundColor Green
    return $true
}

#############################################
# Function: Start-PodmanMachine
# Starts an existing Podman machine.
# This boots the Linux OS in the VHDX file using WSL2.
#############################################
function Start-PodmanMachine {
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
        Write-Host "Podman machine is already running." -ForegroundColor Green
        return $true
    }
    
    try {
        # Start the Podman machine (boots the Linux OS in WSL2)
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
# This service will automatically start the Podman machine on system boot.
#############################################
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
    Write-Host "Podman is installed at: $destinationFolder"
    
    # Remove any existing service with the same name
    if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
        Write-Host "Service '$ServiceName' already exists. Removing it..."
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
    exit 0
) else (
    echo [%date% %time%] Starting Podman machine... >> "%TEMP%\podman-service.log"
    podman machine start
    set START_RESULT=%errorlevel%
    if %START_RESULT%==0 (
        echo [%date% %time%] Podman machine started successfully >> "%TEMP%\podman-service.log"
    ) else (
        echo [%date% %time%] Failed to start Podman machine: error %START_RESULT% >> "%TEMP%\podman-service.log"
    )
    exit %START_RESULT%
)
"@
    $batchContent | Set-Content -Path $batchFilePath -Encoding ASCII
    if (-not (Test-Path $batchFilePath)) {
        Write-Error "Failed to create the batch file at $batchFilePath."
        return $false
    }
    Write-Host "Batch file created at: $batchFilePath"
    
    # Create the service via sc.exe
    # This registers a Windows service that runs the batch file on system startup
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
    if (CheckPodmanDesktopInstalled) {
        Write-Host "Podman Desktop is already installed. Skipping installation." -ForegroundColor Green
        return $true
    }
    
    # Check if Podman CLI is installed
    if (-not (CheckPodmanCliAvailable)) {
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
    if (CheckPodmanDesktopInstalled) {
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
            if (CheckPodmanMachineAvailable) {
                Write-Host "Removing Podman machines..."
                $machineListJson = & podman machine ls --format json 2>&1
                $machines = $machineListJson | ConvertFrom-Json
                
                foreach ($machine in $machines) {
                    Write-Host "Removing machine: $($machine.Name)..."
                    # podman machine rm [options] [MACHINE]
                    # rm     Remove an existing machine
                    # -f     Force the removal if the machine is running
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
Write-Host "   - This creates a VHDX file with Linux + Podman inside"
Write-Host "5) Move Podman Machine"
Write-Host "   - Moves a Podman machine image to a new location"
Write-Host "6) Register Podman Service"
Write-Host "   - Creates a Windows service to auto-start Podman (Requires Machine)"
Write-Host "7) Remove Podman Components"
Write-Host "   - Options to remove service, machine, or uninstall software"
Write-Host "=================================================="

$installOption = Read-Host "Enter your choice (1-6). Default is 6 if empty"
if ([string]::IsNullOrEmpty($installOption)) { $installOption = "6" }

switch ($installOption) {
    "1" {
        # Just show status (already displayed at start)
        DisplayPodmanStatus
    }
    "2" {
        Install-PodmanCLI
    }
    "3" {
        Install-PodmanDesktop
    }
    "4" {
        if (Initialize-PodmanMachine) {
            Start-PodmanMachine
        }
    }
    "5" {
        # This creates a VHDX file with Linux + Podman and registers it with WSL2
        $diskLocation = Select-DiskLocation
        Move-PodmanMachineImage -DestinationPath $diskLocation
    }
    "6" {
        # Install Podman Service
        # This creates a Windows service that starts the Podman machine on boot
        Install-PodmanService
    }
    "7" {
        Remove-PodmanComponents
    }
    default {
        Write-Error "Invalid selection. Exiting."
        exit 1
    }
}
