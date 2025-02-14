################################################################################
# File         : Setup_1a_Podman.ps1
# Description  : Script to install Podman on Windows.
#                Provides options for installing Podman Desktop (setup executable)
#                or Podman Remote client (ZIP package).
# Usage        : Run the script; ensure proper administrator privileges where needed.
################################################################################

# Dot-source shared helper functions from Setup_0.ps1:
. "$PSScriptRoot\Setup_0.ps1"

# Ensure the script is running as Administrator (if needed) and set the working directory.
#Ensure-Elevated
Set-ScriptLocation

#############################################
# Function: Download-File
#############################################
function Download-File {
    param (
        [Parameter(Mandatory = $true)]
        [string]$url,
        [Parameter(Mandatory = $true)]
        [string]$destinationPath
    )
    if (-not (Test-Path $destinationPath)) {
        Write-Host "Downloading file from $url ..."
        try {
            Invoke-WebRequest -Uri $url -OutFile $destinationPath -UseBasicParsing
            Write-Host "Download succeeded: $destinationPath"
        }
        catch {
            Write-Error "Failed to download file from $url. Error details: $_"
            exit 1
        }
    }
    else {
        Write-Host "File already exists at $destinationPath. Skipping download."
    }
}

#############################################
# Function: Install-PodmanDesktop (Non-desktop Podman)
#############################################
function Install-PodmanDesktop {
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

    Write-Host "Launching Podman setup executable..."
    Start-Process -FilePath $exePath -Wait
}

#############################################
# Function: Install-PodmanRemote
# Installs the Podman Remote client by extracting a ZIP package.
#############################################
function Install-PodmanRemote {
    param(
        [string]$zipUrl = "https://github.com/containers/podman/releases/download/v5.4.0/podman-remote-release-windows_amd64.zip",
        [string]$downloadFolder = ".\downloads",
        [string]$destinationFolder = ".\podman"
    )
    if (-not (Test-Path $downloadFolder)) {
        Write-Host "Creating downloads folder at $downloadFolder..."
        New-Item -ItemType Directory -Force -Path $downloadFolder | Out-Null
    }
    
    $zipPath = Join-Path $downloadFolder "podman-remote-release-windows_amd64.zip"
    Download-File -url $zipUrl -destinationPath $zipPath

    if (-not (Test-Path $destinationFolder)) {
        Write-Host "Extracting Podman Remote package to $destinationFolder..."
        try {
            Expand-Archive -Path $zipPath -DestinationPath $destinationFolder
            Write-Host "Extraction completed."
        }
        catch {
            Write-Error "Failed to extract Podman Remote package. Error details: $_"
            exit 1
        }
    }
    else {
        Write-Host "Destination folder '$destinationFolder' exists. Skipping extraction."
    }
    
    # Add the destination folder to the current session PATH.
    $resolvedPath = (Resolve-Path $destinationFolder).Path
    $env:Path = "$resolvedPath;$env:Path"
    Write-Host "Added $resolvedPath to PATH."
}

#############################################
# Function: Ensure-PodmanInstalledAndWorking
# Verifies that non-desktop Podman is installed by checking:
#   1. If the podman command is available in PATH.
#   2. If Podman appears in the Windows installed programs (via Test-ApplicationInstalled).
#############################################
function Ensure-PodmanInstalledAndWorking {
    if (Get-Command podman -ErrorAction SilentlyContinue) {
         Write-Host "Podman command found in PATH. Skipping installation."
         return
    }
    if (Test-ApplicationInstalled "Podman") {
         Write-Host "Podman is registered as installed in Windows. Skipping installation."
         return
    }
    
    Write-Host "Podman is not installed."
    Write-Host "Select Podman installation method:"
    Write-Host "1) Install Podman Desktop (setup executable)"
    Write-Host "2) Install Podman Remote client (ZIP package)"
    $installMethod = Read-Host "Enter your choice (1 or 2), default is 1"
    if ([string]::IsNullOrWhiteSpace($installMethod)) {
         $installMethod = "1"
    }
    switch ($installMethod) {
        "1" { Install-PodmanDesktop }
        "2" { Install-PodmanRemote }
        default {
            Write-Error "Invalid selection for Podman installation method."
            exit 1
        }
    }
    
    Start-Sleep -Seconds 10
    
    if (Get-Command podman -ErrorAction SilentlyContinue) {
         Write-Host "Podman installation verified successfully."
    }
    elseif (Test-ApplicationInstalled "Podman") {
         Write-Host "Podman is now registered as installed in Windows."
    }
    else {
         Write-Error "Podman installation appears to have failed. Please check the installation steps."
         exit 1
    }
}

#############################################
# Function: Ensure-PodmanDesktopInstalled
# Checks if Podman Desktop (GUI) is installed, and if not, installs it.
#############################################
function Ensure-PodmanDesktopInstalled {
     if (Test-ApplicationInstalled "Podman Desktop") {
         Write-Host "Podman is registered as installed in Windows. Skipping installation."
         return
    }
    Write-Host "Podman Desktop is not installed. Installing Podman Desktop..."
    $desktopInstallerUrl = "https://github.com/podman-desktop/podman-desktop/releases/download/v1.16.2/podman-desktop-1.16.2-setup-x64.exe"
    $downloadFolder = ".\downloads"
    if (-not (Test-Path $downloadFolder)) {
        Write-Host "Creating downloads folder at $downloadFolder..."
        New-Item -ItemType Directory -Force -Path $downloadFolder | Out-Null
    }
    $installerPath = Join-Path $downloadFolder "podman-desktop-1.16.2-setup-x64.exe"
    Download-File -url $desktopInstallerUrl -destinationPath $installerPath
    Write-Host "Launching Podman Desktop installer..."
    Start-Process -FilePath $installerPath -Wait
    if (-not (Test-ApplicationInstalled "Podman Desktop")) {
        Write-Error "Podman Desktop installation failed. Please install manually."
        exit 1
    }
    Write-Host "Podman Desktop installed successfully."
}

#############################################
# Main Script Execution
#############################################
Ensure-PodmanInstalledAndWorking
Ensure-PodmanDesktopInstalled

Write-Host "Podman setup completed successfully."