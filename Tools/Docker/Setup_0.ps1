################################################################################
# File         : Setup_0.ps1
# Description  : Contains basic and generic helper functions for setup scripts.
#                Functions include administrator privilege verification, setting the
#                script location, Git command lookup, container engine path lookup,
#                network port testing, and backup restoration.
# Usage        : Dot-source this script in your other setup scripts:
#                . "$PSScriptRoot\Setup_0.ps1"
################################################################################

#------------------------------
# Function: Ensure-Elevated
#------------------------------
function Ensure-Elevated {
    if (-NOT ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
         Write-Error "Administrator privileges required. Please run this script as an Administrator."
         exit 1
    }
}

#------------------------------
# Function: Set-ScriptLocation
#------------------------------
function Set-ScriptLocation {
    if ($PSScriptRoot -and $PSScriptRoot -ne "") {
        $scriptPath = $PSScriptRoot
    }
    else {
        $scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
    }
    if ($scriptPath) {
        Set-Location $scriptPath
        Write-Host "Script Path: $scriptPath"
    }
    else {
        Write-Host "Script Path not found. Current directory remains unchanged."
    }
}

#------------------------------
# Function: Download-File
# Generic download function using Start-BitsTransfer (provides a fast download with a progress bar).
# Supports both -SourceUrl and -url as parameter aliases.
#------------------------------
function Download-File {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)]
        [Alias("url")]
        [string]$SourceUrl,
        [Parameter(Mandatory=$true)]
        [string]$DestinationPath,
        [switch]$ForceDownload,  # Optional switch to force re-download
        [switch]$UseFallback     # New parameter to force fallback method
    )
    
    if ((Test-Path $DestinationPath) -and (-not $ForceDownload)) {
        Write-Host "File already exists at $DestinationPath. Skipping download."
        return
    }
    
    # Check if BITS is available or if fallback is requested
    if ((Get-Command Start-BitsTransfer -ErrorAction SilentlyContinue) -and (-not $UseFallback)) {
        Write-Host "Downloading file from $SourceUrl to $DestinationPath using Start-BitsTransfer..."
        try {
            Start-BitsTransfer -Source $SourceUrl -Destination $DestinationPath
            Write-Host "Download succeeded: $DestinationPath" -ForegroundColor Green
            return
        }
        catch {
            Write-Warning "BITS transfer failed: $_. Trying fallback method..."
        }
    }
    
    # Fallback to Invoke-WebRequest
    try {
        Write-Host "Downloading file from $SourceUrl to $DestinationPath using Invoke-WebRequest..."
        $ProgressPreference = 'SilentlyContinue'  # Speeds up Invoke-WebRequest significantly
        Invoke-WebRequest -Uri $SourceUrl -OutFile $DestinationPath -UseBasicParsing
        $ProgressPreference = 'Continue'  # Restore default
        Write-Host "Download succeeded: $DestinationPath" -ForegroundColor Green
    }
    catch {
        Write-Error "Failed to download file from $SourceUrl. Error details: $_"
        exit 1
    }
}

#------------------------------
# Function: Check-Git
#------------------------------
function Check-Git {
    if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
        Write-Host "Git command not found in PATH. Attempting to locate Git via common installation paths..."
        $possibleGitPaths = @(
            "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Git\cmd",
            "C:\Program Files\Microsoft Visual Studio\2022\Professional\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Git\cmd"
        )
        foreach ($path in $possibleGitPaths) {
            if (Test-Path $path) {
                $env:Path += ";" + $path
                Write-Host "Added Git path: $path"
                break
            }
        }
        if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
            Write-Error "Git command not found. Please install Git and ensure it's in your PATH."
            exit 1
        }
    }
}

#------------------------------
# Function: Get-DockerPath
#------------------------------
function Get-DockerPath {
    $dockerCmd = Get-Command docker -ErrorAction SilentlyContinue
    if ($dockerCmd) {
        return $dockerCmd.Source
    }
    else {
        $dockerPath = Join-Path (Resolve-Path ".\docker") "docker.exe"
        if (Test-Path $dockerPath) {
            return $dockerPath
        }
        else {
            Write-Error "Docker executable not found."
            exit 1
        }
    }
}

#------------------------------
# Function: Get-PodmanPath
#------------------------------
function Get-PodmanPath {
    $podmanCmd = Get-Command podman -ErrorAction SilentlyContinue
    if ($podmanCmd) {
        return $podmanCmd.Source
    }
    else {
        $podmanPath = Join-Path (Resolve-Path ".\podman") "podman.exe"
        if (Test-Path $podmanPath) {
            return $podmanPath
        }
        else {
            Write-Error "Podman executable not found."
            exit 1
        }
    }
}

#------------------------------
# Function: Select-ContainerEngine prompts the user to choose Docker or Podman.
#------------------------------
function Select-ContainerEngine {
    Write-Host "Select container engine:"
    Write-Host "1) Docker"
    Write-Host "2) Podman"
    $selection = Read-Host "Enter your choice (1 or 2, default is 1)"
    if ([string]::IsNullOrWhiteSpace($selection)) {
        $selection = "1"
    }
    switch ($selection) {
        "1" { return "docker" }
        "2" { return "podman" }
        default {
            Write-Host "Invalid selection, defaulting to Docker."
            return "docker"
        }
    }
}

#------------------------------
# Function: Test-ApplicationInstalled
#------------------------------
function Test-ApplicationInstalled {
    <#
    .SYNOPSIS
        Determines whether a specified application is installed.
    .PARAMETER AppName
        The application name to search for (supports wildcards).
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$AppName
    )
    
    # First check registry for performance
    $uninstallPaths = @(
        "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*",
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*",
        "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*"
    )
    
    foreach ($path in $uninstallPaths) {
        try {
            $apps = Get-ItemProperty -Path $path -ErrorAction SilentlyContinue |
                    Where-Object { $_.DisplayName -like "*$AppName*" }
            if ($apps) { return $true }
        }
        catch { continue }
    }
    
    # Only if registry check fails, try Get-Package as fallback
    try {
        $package = Get-Package -Name "$AppName*" -ErrorAction SilentlyContinue
        if ($package) {
            return $true
        }
    }
    catch {
        # Ignore errors with Get-Package
    }
    
    # Not found by any method
    return $false
}

#------------------------------
# Function: Test-TCPPort
#------------------------------
function Test-TCPPort {
    param(
        [Parameter(Mandatory=$true)]
        [string] $ComputerName,
        [Parameter(Mandatory=$true)]
        [int] $Port,
        [Parameter(Mandatory=$true)]
        [string] $serviceName,
        [int] $TimeoutMilliseconds = 5000  # Added configurable timeout
    )

    try {
        # Try to resolve both IPv4 and IPv6 addresses but prioritize IPv4
        $ipAddresses = [System.Net.Dns]::GetHostAddresses($ComputerName)
        $ip = $ipAddresses | Where-Object { $_.AddressFamily -eq 'InterNetwork' } | Select-Object -First 1
        
        # Fallback to IPv6 if no IPv4 is available
        if (-not $ip) {
            $ip = $ipAddresses | Select-Object -First 1
            if (-not $ip) {
                throw "No IP address could be found for $ComputerName."
            }
            Write-Host "Using IPv6 address for connection test: $ip" -ForegroundColor Yellow
        }
        
        $client = New-Object System.Net.Sockets.TcpClient
        $async = $client.BeginConnect($ip.ToString(), $Port, $null, $null)
        $connected = $async.AsyncWaitHandle.WaitOne($TimeoutMilliseconds, $false)
        
        if ($connected -and $client.Connected) {
            Write-Host "$serviceName TCP test succeeded on port $Port at $ComputerName (IP: $ip)."
            $client.Close()
            return $true
        } else {
            Write-Error "$serviceName TCP test failed on port $Port at $ComputerName (IP: $ip)."
            $client.Close()
            return $false
        }
    }
    catch {
        Write-Error "$serviceName TCP test encountered an error: $_"
        return $false
    }
}

#------------------------------
# Function: Test-HTTPPort
#------------------------------
function Test-HTTPPort {
    param(
        [Parameter(Mandatory=$true)]
        [string] $Uri,
        [Parameter(Mandatory=$true)]
        [string] $serviceName
    )
    try {
        $response = Invoke-WebRequest -Uri $Uri -UseBasicParsing -TimeoutSec 15
        if ($response.StatusCode -eq 200) {
            Write-Host "$serviceName HTTP test succeeded at $Uri."
            return $true
        }
        else {
            Write-Error "$serviceName HTTP test failed at $Uri. Status code: $($response.StatusCode)."
            return $false
        }
    }
    catch {
        Write-Error "$serviceName HTTP test failed at $Uri. Error details: $_"
        return $false
    }
}


#------------------------------
# Function: Test-WebSocketPort
#------------------------------
function Test-WebSocketPort {
    param(
        [Parameter(Mandatory=$true)]
        [string] $Uri,
        [Parameter(Mandatory=$true)]
        [string] $serviceName
    )
    try {
        # Check if .NET Core WebSocket client is available
        if (-not ([System.Management.Automation.PSTypeName]'System.Net.WebSockets.ClientWebSocket').Type) {
            Write-Warning "WebSocket client not available in this PowerShell version. Falling back to HTTP check."
            return Test-HTTPPort -Uri $Uri.Replace("ws:", "http:").Replace("wss:", "https:") -serviceName $serviceName
        }
        
        $client = New-Object System.Net.WebSockets.ClientWebSocket
        $ct = New-Object System.Threading.CancellationTokenSource 5000
        $task = $client.ConnectAsync($Uri, $ct.Token)
        
        # Wait for 5 seconds max
        if ([System.Threading.Tasks.Task]::WaitAll(@($task), 5000)) {
            Write-Host "$serviceName WebSocket test succeeded at $Uri."
            $client.Dispose()
            return $true
        }
        else {
            Write-Error "$serviceName WebSocket test timed out at $Uri."
            $client.Dispose()
            return $false
        }
    }
    catch {
        Write-Error "$serviceName WebSocket test failed at $Uri. Error details: $_"
        return $false
    }
}

#------------------------------
# Function: Check-AndRestoreBackup
#
# Determines whether a backup file exists for the specified image in the Backup folder.
# If a backup is found, it offers the choice to restore it using the provided container engine.
# The backup file is expected to follow the naming convention where any ':' or '/' in the image name 
# is replaced with '_' and appended with a .tar extension.
#
# Parameters:
#   - Engine: The container engine command (e.g., "docker" or "podman").
#   - ImageName: The full image name (repository:tag) for which a backup may exist.
#   - BackupFolder (optional): The folder where backup tar files are stored (default ".\Backup").
#
# Returns:
#   $true if a backup was restored successfully, or $false otherwise.
#------------------------------
function Check-AndRestoreBackup {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Engine,      # container engine command ("docker" or "podman")
        
        [Parameter(Mandatory = $true)]
        [string]$ImageName,   # full image name, e.g. "open-webui/pipelines:custom"
        
        [string]$BackupFolder = ".\Backup"
    )
    
    # Compute the safe backup file name by replacing ':' and '/' with '_'
    $safeName = $ImageName -replace "[:/]", "_"
    $backupFile = Join-Path $BackupFolder "$safeName.tar"
    
    if (-not (Test-Path $backupFile)) {
        Write-Host "No backup file found for image '$ImageName' in folder '$BackupFolder'."
        return $false
    }
    
    Write-Host "Backup file found for image '$ImageName': $backupFile"
    $choice = Read-Host "Do you want to restore the backup for '$ImageName'? (Y/N, default N)"
    if ($choice -and $choice.ToUpper() -eq "Y") {
        Write-Host "Restoring backup from $backupFile..."
        # podman load [options]
        # load      Load an image from a tar archive into the container engine.
        # --input string   Specify the input file containing the saved image.
        & $Engine load --input $backupFile
        if ($LASTEXITCODE -eq 0) {
            Write-Host "Successfully restored backup for image '$ImageName'."
            return $true
        }
        else {
            Write-Error "Failed to restore backup for image '$ImageName'."
            return $false
        }
    }
    else {
        Write-Host "User opted not to restore backup for image '$ImageName'."
        return $false
    }
}

#############################################
# WSL and Service Health Functions
#############################################

function Check-WSLStatus {
    Write-Host "Verifying WSL installation and required service status..."
    
    # Check if the wsl command is available
    if (!(Get-Command wsl -ErrorAction SilentlyContinue)) {
         Write-Error "WSL (wsl.exe) is not available. Please install Windows Subsystem for Linux."
         exit 1
    }
    
    # Check WSL version - we need WSL2
    $wslVersionInfo = wsl --version 2>&1
    Write-Host "WSL Version Info:`n$wslVersionInfo"
    
    # Check if running WSL 2
    $wslVersion = wsl --status | Select-String -Pattern "Default Version: (\d+)" | ForEach-Object { $_.Matches.Groups[1].Value }
    if ($wslVersion -ne "2") {
        Write-Warning "WSL seems to be running version $wslVersion but WSL 2 is required."
        Write-Warning "Please run 'wsl --set-default-version 2' as Administrator to set WSL 2 as default."
        $setWsl2 = Read-Host "Would you like to set WSL 2 as default now? (Y/N, default is Y)"
        if ($setWsl2 -ne "N") {
            wsl --set-default-version 2
            if ($LASTEXITCODE -ne 0) {
                Write-Error "Failed to set WSL 2 as default. Please do this manually."
                exit 1
            }
            Write-Host "WSL 2 has been set as the default." -ForegroundColor Green
        } else {
            Write-Error "WSL 2 is required but not set as default. Exiting."
            exit 1
        }
    }
   
    # Check if the Windows Subsystem for Linux feature is enabled
    $wslFeature = Get-WindowsOptionalFeature -Online -FeatureName Microsoft-Windows-Subsystem-Linux
    if ($wslFeature.State -ne "Enabled") {
         Write-Warning "The Microsoft-Windows-Subsystem-Linux feature is not enabled."
         $choice = Read-Host "Do you want to enable it automatically? (Y/N)"
         if ($choice -and $choice.ToUpper() -eq "Y") {
             Write-Host "Enabling WSL feature..."
             dism.exe /Online /Enable-Feature /FeatureName:Microsoft-Windows-Subsystem-Linux /All /NoRestart | Out-Null
             Write-Host "WSL feature enabled. A system restart may be required to activate changes."
         } else {
             Write-Error "The Microsoft-Windows-Subsystem-Linux feature is required. Exiting."
             exit 1
         }
    }
    
    # Check if the Virtual Machine Platform feature is enabled
    $vmFeature = Get-WindowsOptionalFeature -Online -FeatureName VirtualMachinePlatform
    if ($vmFeature.State -ne "Enabled") {
         Write-Warning "The VirtualMachinePlatform feature is not enabled."
         $choice = Read-Host "Do you want to enable it automatically? (Y/N)"
         if ($choice -and $choice.ToUpper() -eq "Y") {
             Write-Host "Enabling VirtualMachinePlatform feature..."
             dism.exe /Online /Enable-Feature /FeatureName:VirtualMachinePlatform /All /NoRestart | Out-Null
             Write-Host "VirtualMachinePlatform feature enabled. A system restart may be required to activate changes."
         } else {
             Write-Error "The VirtualMachinePlatform feature is required. Exiting."
             exit 1
         }
    }
    
    Write-Host "WSL and required Windows features are enabled."
}


#--------------------------------------
# Function: Backup-ContainerState
# Description: Creates a backup of a live running container by committing its state 
#              to an image and saving that image as a tar file.
# Parameters:
#   -Engine: Path to the container engine (docker or podman)
#   -ContainerName: Name of the container to backup
#   -BackupFolder: Folder to store the backup file (default ".\Backup")
#--------------------------------------
function Backup-ContainerState {
    param(
        [Parameter(Mandatory=$true)]
        [string]$Engine,
        [Parameter(Mandatory=$true)]
        [string]$ContainerName,
        [string]$BackupFolder = ".\Backup"
    )
    
    if (-not (Test-Path $BackupFolder)) {
         New-Item -ItemType Directory -Path $BackupFolder -Force | Out-Null
         Write-Host "Created backup folder: $BackupFolder"
    }
    
    # Check if the container exists.
    $existingContainer = & $Engine ps -a --filter "name=^$ContainerName$" --format "{{.ID}}"
    if (-not $existingContainer) {
         Write-Error "Container '$ContainerName' does not exist. Cannot backup."
         return $false
    }
    
    # Commit the container to an image with tag "backup-<ContainerName>:latest"
    $backupImageTag = "backup-$ContainerName:latest"
    Write-Host "Committing container '$ContainerName' to image '$backupImageTag'..."
    # podman commit [OPTIONS] CONTAINER [REPOSITORY[:TAG]]
    # commit    Create a new image from a container's changes.
    & $Engine commit $ContainerName $backupImageTag
    if ($LASTEXITCODE -ne 0) {
         Write-Error "Failed to commit container '$ContainerName'."
         return $false
    }
    
    # Build backup tar file name.
    $safeName = $ContainerName -replace "[:/]", "_"
    $backupFile = Join-Path $BackupFolder "$safeName-backup.tar"
    
    Write-Host "Saving backup image '$backupImageTag' to '$backupFile'..."
    # podman save [options] IMAGE
    # save      Save an image to a tar archive.
    # --output string   Specify the output file for saving the image.
    & $Engine save --output $backupFile $backupImageTag
    if ($LASTEXITCODE -eq 0) {
         Write-Host "Backup successfully saved to '$backupFile'."
         return $true
    } else {
         Write-Error "Failed to save backup image to '$backupFile'."
         return $false
    }
}

#############################################
# Function: Refresh-EnvironmentVariables
#############################################
function Refresh-EnvironmentVariables {
    <#
    .SYNOPSIS
      Refreshes the current session's environment variables.
      
    .DESCRIPTION
      Re-reads the machine and user PATH from the registry and updates the current session.
      This allows newly installed executables (such as podman) to be found without restarting PowerShell.
    #>
    $machinePath = [System.Environment]::GetEnvironmentVariable("PATH", [System.EnvironmentVariableTarget]::Machine)
    $userPath = [System.Environment]::GetEnvironmentVariable("PATH", [System.EnvironmentVariableTarget]::User)
    $env:PATH = "$machinePath;$userPath"
    Write-Host "Environment variables refreshed. Current PATH:" 
    Write-Host $env:PATH
}

#--------------------------------------
# Function: Restore-ContainerState
# Description: Restores a container from a previously saved backup tar file.
#              Loads the backup image and runs a new container from it.
# Parameters:
#   -Engine: Path to the container engine (docker or podman)
#   -ContainerName: Name of the container to restore
#   -BackupFolder: Folder where the backup file is located (default ".\Backup")
#--------------------------------------
function Restore-ContainerState {
    param(
        [Parameter(Mandatory=$true)]
        [string]$Engine,
        [Parameter(Mandatory=$true)]
        [string]$ContainerName,
        [string]$BackupFolder = ".\Backup"
    )
    
    $safeName = $ContainerName -replace "[:/]", "_"
    $backupFile = Join-Path $BackupFolder "$safeName-backup.tar"
    
    if (-not (Test-Path $backupFile)) {
         Write-Host "Backup file '$backupFile' not found for container '$ContainerName'."
         return $false
    }
    
    Write-Host "Loading backup image from '$backupFile'..."
    # podman load [options]
    # load      Load an image from a tar archive.
    # --input string   Specify the input file containing the saved image.
    & $Engine load --input $backupFile
    if ($LASTEXITCODE -ne 0) {
         Write-Error "Failed to load backup image from '$backupFile'."
         return $false
    }
    
    # Assume the backup image tag is "backup-<ContainerName>:latest"
    $backupImageTag = "backup-$ContainerName:latest"
    
    # Stop and remove the existing container if it exists.
    $existingContainer = & $Engine ps -a --filter "name=^$ContainerName$" --format "{{.ID}}"
    if ($existingContainer) {
         Write-Host "Stopping and removing existing container '$ContainerName'..."
         # podman rm [options] CONTAINER [CONTAINER...]
         # rm        Remove one or more containers.
         # --force   Force removal of a running container.
         & $Engine rm --force $ContainerName
         if ($LASTEXITCODE -ne 0) {
              Write-Error "Failed to remove existing container '$ContainerName'."
              return $false
         }
    }
    
    Write-Host "Starting container '$ContainerName' from backup image '$backupImageTag'..."
    # podman run [options] IMAGE [COMMAND [ARG...]]
    # run         Run a command in a new container.
    # --detach    Run container in background and print container ID.
    & $Engine run --detach --name $ContainerName $backupImageTag
    if ($LASTEXITCODE -eq 0) {
         Write-Host "Container '$ContainerName' restored and running."
         return $true
    } else {
         Write-Error "Failed to start container from backup image."
         return $false
    }
}