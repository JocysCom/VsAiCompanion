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

#############################################
# Function: Ensure-And-Start-PodmanMachine
# Verifies that a Podman machine exists by checking the JSON output of
# "podman machine ls --format json". If the array is empty, it attempts to
# initialize a default machine and then starts it. If a machine exists but is
# not running, it starts the machine.
#############################################
function Ensure-And-Start-PodmanMachine {
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
         Write-Host "No Podman machine detected (empty array). Initializing a default machine..."
         # podman machine init [options] MACHINE_NAME
         # init   Initialize a Podman virtual machine.
         # 'default' is the name that will be assigned to the new machine.
         $initOutput = & podman machine init default 2>&1
         if ($LASTEXITCODE -ne 0) {
              Write-Error "Failed to initialize Podman machine. Output: $initOutput"
              return $false
         }
         Write-Host "Podman machine initialized successfully."
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
        [string]$downloadFolder = ".\downloads"
    )

    # Ensure Podman CLI is installed.
    $podmanCmd = Get-Command podman -ErrorAction SilentlyContinue
    if (-not $podmanCmd) {
         Write-Error "Podman CLI is not installed. Please install it first."
         return
    }

    # Ensure a Podman machine exists and is running.
    if (-not (Ensure-And-Start-PodmanMachine)) {
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

switch ($installOption) {
    "1" {
         if (-not (Get-Command podman -ErrorAction SilentlyContinue)) {
              Install-PodmanRemote
         }
         else {
            Write-Host "Podman Program (CLI) is already installed. Skipping installation."
         }
    }
    "2" {
         if (Get-Command podman -ErrorAction SilentlyContinue) {
            Ensure-PodmanDesktopInstalled
         }
         else {
            Write-Host "Podman CLI is required for the UI. Installing Podman Program first."
            Install-PodmanRemote
         }
    }
    "3" {
         if (Get-Command podman -ErrorAction SilentlyContinue) {
            Install-PodmanService
         }
         else {
           Write-Error "Podman CLI is required for service registration. Exiting."
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