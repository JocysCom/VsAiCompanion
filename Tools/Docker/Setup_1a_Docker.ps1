################################################################################
# File         : Setup_1a_Docker.ps1
# Description  : Script to install and configure Docker and WSL on Windows.
#                Includes checks for WSL, installation of Docker Desktop/Engine,
#                and verification of Docker functionality.
# Usage        : Run as Administrator if required.
################################################################################

using namespace System
using namespace System.IO

# Dot-source the necessary helper function files.
. "$PSScriptRoot\Setup_0_Core.ps1"
. "$PSScriptRoot\Setup_0_ContainerEngine.ps1"
. "$PSScriptRoot\Setup_0_WSL.ps1"

# Ensure the script is running as Administrator and set the working directory.
Ensure-Elevated
Set-ScriptLocation

#############################################
# Docker and WSL Troubleshooting Guide
#############################################
# 1. Run Docker as Administrator:
#    Make sure to launch Docker Desktop with elevated privileges.
#
# 2. Check and Repair System Health:
#    Uncomment the commands below if needed.
# dism /online /cleanup-image /restorehealth
# sfc /scannow
#
# 3. Restart WSL Services:
#    e.g. wsl --shutdown ; wsl --update --web-download ; wsl -l -v
#
# 4. Enable Virtualization & Required Windows Features:
#    dism.exe /online /enable-feature /featurename:VirtualMachinePlatform /all /norestart
#    dism.exe /online /enable-feature /featurename:Microsoft-Windows-Subsystem-Linux /all /norestart
#
# 5. Ensure WSL Manager Service is Running:
#    sc.exe config LxssManager start=auto; sc.exe start LxssManager
#
# 6. Switch Docker to use the WSL 2 Backend:
#    "C:\Program Files\Docker\Docker\DockerCli.exe" -SwitchDaemon
#############################################

#############################################
# Define Options for Docker Installation Only
#############################################

$dockerStaticUrl  = "https://download.docker.com/win/static/stable/x86_64/docker-27.5.1.zip"

#############################################
# Docker Installation Functions
#############################################

function Invoke-DockerEngineDownload {
    param(
        [string]$dockerStaticUrl,
        [string]$downloadFolder = ".\downloads"
    )
    if (-not (Test-Path $downloadFolder)) {
        Write-Output "Creating downloads folder at $downloadFolder..."
        New-Item -ItemType Directory -Force -Path $downloadFolder | Out-Null
    }
    $zipPath = Join-Path $downloadFolder "docker.zip"
    if (Test-Path $zipPath) {
        Write-Output "Docker archive already exists at $zipPath. Skipping download."
    }
    else {
        Write-Output "Downloading Docker static binary archive from $dockerStaticUrl..."
        try {
            # Use Start-BitsTransfer for faster download with progress.
            Start-BitsTransfer -Source $dockerStaticUrl -Destination $zipPath
        }
        catch {
            Write-Error "Failed to download Docker static binary archive. Please check your internet connection or URL. Error details: $_"
            exit 1
        }
    }
    return $zipPath
}

function Install-DockerEngine {
    param(
        [string]$zipPath,
        [string]$destinationPath = ".\docker"
    )
    if (Test-Path $destinationPath) {
        Write-Output "Destination folder $destinationPath already exists. Skipping extraction and installation."
        return $destinationPath
    }

    $tempDestination = ".\docker_temp"
    if (Test-Path $tempDestination) {
        Remove-Item -Recurse -Force $tempDestination
    }
    try {
        Expand-Archive -Path $zipPath -DestinationPath $tempDestination
    }
    catch {
        Write-Error "Failed to extract Docker archive."
        exit 1
    }
    Write-Output "Processing extracted files..."
    $innerFolder = Join-Path $tempDestination "docker"
    if (Test-Path $innerFolder) {
        Write-Output "Detected inner 'docker' folder. Moving its contents to $destinationPath..."
        New-Item -ItemType Directory -Path $destinationPath | Out-Null
        Get-ChildItem -Path $innerFolder | ForEach-Object {
            Copy-Item -Path $_.FullName -Destination $destinationPath -Recurse -Force
        }
    }
    else {
        Write-Output "No inner folder detected. Renaming extraction folder to $destinationPath..."
        Rename-Item -Path $tempDestination -NewName "docker"
        return $destinationPath
    }
    Remove-Item -Recurse -Force $tempDestination
    return $destinationPath
}

function Register-DockerEngineService {
    param(
        [string]$destinationPath = ".\docker"
    )
    $dockerdPath = Join-Path $destinationPath "dockerd.exe"
    if (-not (Test-Path $dockerdPath)) {
        Write-Error "dockerd.exe not found in $destinationPath. Installation may have failed."
        exit 1
    }
    Write-Output "Checking if Docker Engine service is already registered..."
    $dockerService = Get-Service -Name docker -ErrorAction SilentlyContinue
    if ($null -eq $dockerService) {
        Write-Output "Registering Docker Engine as a service..."
        & "$dockerdPath" --register-service
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Failed to register Docker service."
            exit 1
        }
        $dockerService = Get-Service -Name docker -ErrorAction SilentlyContinue
    }
    else {
        Write-Output "Docker service is already registered."
    }

    if ($dockerService.Status -ne 'Running') {
        Write-Output "Starting Docker service..."
        Start-Service docker
        $dockerService = Get-Service -Name docker -ErrorAction SilentlyContinue
        if ($dockerService.Status -ne 'Running') {
            Write-Error "Failed to start Docker service."
            exit 1
        }
    }
    else {
        Write-Output "Docker service is already running."
    }
    Start-Sleep -Seconds 10
    $env:Path = "$(Resolve-Path $destinationPath);$env:Path"
}

function Test-DockerWorking {
    param(
        [string]$destinationPath = ".\docker"
    )
    if (Get-Command docker -ErrorAction SilentlyContinue) {
        $dockerExe = "docker"
    }
    else {
        $dockerExe = Join-Path $destinationPath "docker.exe"
    }
    Write-Output "Verifying Docker installation with hello-world image..."
    $env:DOCKER_HOST = "npipe:////./pipe/docker_engine"

    $existingHelloWorld = &$dockerExe images --filter "reference=hello-world" --format "{{.Repository}}"
    if (-not $existingHelloWorld) {
        Write-Output "hello-world image not found locally. Pulling hello-world image..."
        &$dockerExe pull hello-world | Out-Null
    }

    $helloWorldContainerName = "hello-world-test"
    $existingContainer = &$dockerExe ps -a --filter "name=^$helloWorldContainerName$" --format "{{.ID}}"

    if (-not $existingContainer) {
        Write-Output "No existing hello-world container found. Running a new one..."
        # docker run [options] IMAGE [COMMAND [ARG...]]
        # run         Run a command in a new container.
        # --name      Assign a name to the container.
        # --platform  Specify the platform for image selection.
        $output = &$dockerExe run --name $helloWorldContainerName --platform linux/amd64 hello-world 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Docker Engine installation verification failed. Output:`n$output"
            exit 1
        }
    }
    else {
        Write-Output "hello-world container already exists. Skipping container creation."
    }

    Write-Output "Docker Engine is working successfully."
}

function Test-DockerInstallation {
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
        Write-Host "Docker is not installed."
		Write-Host "==========================================="
        Write-Host "Select Docker installation method:"
		Write-Host "==========================================="
        Write-Host "1. Install Docker Desktop (requires winget)"
        Write-Host "2. Install Docker Engine (static binary installation)"
        $installMethod = Read-Host "Enter your choice (1 or 2), default is 1"
        if ([string]::IsNullOrWhiteSpace($installMethod)) {
            $installMethod = "1"
        }
        if ($installMethod -eq "1") {
            if (Get-Command winget -ErrorAction SilentlyContinue) {
                Write-Output "Installing Docker Desktop using winget..."
                winget install --id Docker.DockerDesktop -e --accept-package-agreements --accept-source-agreements
                Start-Sleep -Seconds 60
                if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
                    Write-Error "Docker Desktop installation via winget failed. Please install Docker manually."
                    exit 1
                }
                Write-Output "Docker Desktop installed successfully."
            }
            else {
                Write-Error "winget is not available for Docker Desktop installation. Please install Docker manually."
                exit 1
            }
        }
        elseif ($installMethod -eq "2") {
            $zipPath = Invoke-DockerEngineDownload -dockerStaticUrl $dockerStaticUrl
            $destinationPath = Install-DockerEngine -zipPath $zipPath -destinationPath ".\docker"
            Register-DockerEngineService -destinationPath $destinationPath
        }
        else {
            Write-Error "Invalid selection for Docker installation method."
            exit 1
        }
    }
    else {
        Write-Output "Docker command found in PATH. Using existing Docker installation."
    }
    Test-DockerWorking -destinationPath ".\docker"
}

function Test-DockerDaemonStatus {
    Write-Output "Ensuring Docker daemon is reachable..."
    $env:DOCKER_HOST = "npipe:////./pipe/docker_engine"
    $dockerInfo = docker info 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Docker daemon is not reachable. Please ensure Docker is running. Error details: $dockerInfo"
        exit 1
    }
    Write-Output "Docker daemon is reachable."
}

#############################################
# Main Script Logic for Docker Setup
#############################################

Test-WSLStatus
Test-DockerInstallation
Test-DockerDaemonStatus

# Use the common Get-DockerPath function from Setup_0.ps1.

Write-Output "Docker installation and verification completed successfully."
