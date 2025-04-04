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
# Function: Backup-ContainerImage
# Description: Backs up a single container image to a tar file.
# Parameters:
#   -Engine: Path to the container engine (docker or podman)
#   -ImageName: Name of the image to backup
#   -BackupFolder: Folder to store the backup file (default ".\Backup")
#------------------------------
function Backup-ContainerImage {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)]
        [string]$Engine,
        
        [Parameter(Mandatory=$true)]
        [string]$ImageName,
        
        [string]$BackupFolder = ".\Backup"
    )
    
    if (-not (Test-Path $BackupFolder)) {
        New-Item -ItemType Directory -Force -Path $BackupFolder | Out-Null
        Write-Host "Created backup folder: $BackupFolder"
    }
    
    # Replace characters not allowed in file names (':' and '/' become '_')
    $safeName = $ImageName -replace "[:/]", "_"
    $backupFile = Join-Path $BackupFolder "$safeName.tar"
    
    Write-Host "Backing up image '$ImageName' to '$backupFile'..."
    # podman save [options] IMAGE
    # save      Save an image to a tar archive.
    # --output string   Specify the output file for saving the image.
    & $Engine save --output $backupFile $ImageName
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Successfully backed up image '$ImageName'" -ForegroundColor Green
        return $true
    }
    else {
        Write-Error "Failed to backup image '$ImageName'"
        return $false
    }
}

#------------------------------
# Function: Backup-ContainerImages
# Description: Backs up all container images to tar files.
# Parameters:
#   -Engine: Path to the container engine (docker or podman)
#   -BackupFolder: Folder to store the backup files (default ".\Backup")
#------------------------------
function Backup-ContainerImages {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)]
        [string]$Engine,
        
        [string]$BackupFolder = ".\Backup"
    )
    
    Write-Host "Retrieving list of images for $Engine..."
    $images = & $Engine images --format "{{.Repository}}:{{.Tag}}" | Where-Object { $_ -ne "<none>:<none>" }
    
    if (-not $images) {
        Write-Host "No images found for $Engine."
        return $false
    }
    
    $successCount = 0
    foreach ($image in $images) {
        if (Backup-ContainerImage -Engine $Engine -ImageName $image -BackupFolder $BackupFolder) {
            $successCount++
        }
    }
    
    Write-Host "Backed up $successCount out of $($images.Count) images."
    return ($successCount -gt 0)
}

#------------------------------
# Function: Restore-ContainerImage
# Description: Restores a container image from a tar file.
# Parameters:
#   -Engine: Path to the container engine (docker or podman)
#   -BackupFile: Path to the backup tar file
#   -RunContainer: Whether to run a container from the restored image
#------------------------------
function Restore-ContainerImage {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)]
        [string]$Engine,
        
        [Parameter(Mandatory=$true)]
        [string]$BackupFile,
        
        [switch]$RunContainer = $false
    )
    
    if (-not (Test-Path $BackupFile)) {
        Write-Error "Backup file '$BackupFile' not found."
        return $false
    }
    
    Write-Host "Restoring image from '$BackupFile'..."
    # podman load [options]
    # load       Load an image from a tar archive.
    # --input string   Specify the input file containing the saved image.
    $output = & $Engine load --input $BackupFile
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Successfully restored image from '$BackupFile'." -ForegroundColor Green
        
        # Attempt to parse the image name from the load output
        # Expected output example: "Loaded image: docker.io/open-webui/pipelines:custom"
        $imageName = $null
        if ($output -match "Loaded image:\s*(\S+)") {
            $imageName = $matches[1].Trim()
            Write-Host "Parsed image name: $imageName"
            
            if ($RunContainer) {
                Run-RestoredContainer -Engine $Engine -ImageName $imageName
            }
            return $true
        }
        else {
            Write-Host "Could not parse image name from the load output."
            return $true
        }
    }
    else {
        Write-Error "Failed to restore image from '$BackupFile'."
        return $false
    }
}

#------------------------------
# Function: Run-RestoredContainer
# Description: Runs a container from a restored image.
# Parameters:
#   -Engine: Path to the container engine (docker or podman)
#   -ImageName: Name of the image to run
#------------------------------
function Run-RestoredContainer {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)]
        [string]$Engine,
        
        [Parameter(Mandatory=$true)]
        [string]$ImageName
    )
    
    # Generate a container name by replacing ':' and '/' with underscores
    $containerName = ($ImageName -replace "[:/]", "_") + "_container"
    
    Write-Host "Starting container from image '$ImageName' with container name '$containerName'..."
    # podman run [options] IMAGE [COMMAND [ARG...]]
    # run         Run a command in a new container.
    # --detach    Run container in background and print container ID.
    # --name      Assign a name to the container.
    & $Engine run --detach --name $containerName $ImageName
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Container '$containerName' started successfully." -ForegroundColor Green
        return $true
    }
    else {
        Write-Error "Failed to start container from image '$ImageName'."
        return $false
    }
}

#------------------------------
# Function: Restore-ContainerImages
# Description: Restores all container images from tar files in a folder.
# Parameters:
#   -Engine: Path to the container engine (docker or podman)
#   -BackupFolder: Folder containing the backup tar files (default ".\Backup")
#   -RunContainers: Whether to run containers from the restored images
#------------------------------
function Restore-ContainerImages {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)]
        [string]$Engine,
        
        [string]$BackupFolder = ".\Backup",
        
        [switch]$RunContainers = $false
    )
    
    if (-not (Test-Path $BackupFolder)) {
        Write-Host "Backup folder '$BackupFolder' does not exist. Nothing to restore."
        return $false
    }
    
    $tarFiles = Get-ChildItem -Path $BackupFolder -Filter "*.tar"
    if (-not $tarFiles) {
        Write-Host "No backup tar files found in '$BackupFolder'."
        return $false
    }
    
    $successCount = 0
    foreach ($file in $tarFiles) {
        if (Restore-ContainerImage -Engine $Engine -BackupFile $file.FullName -RunContainer:$RunContainers) {
            $successCount++
        }
    }
    
    Write-Host "Restored $successCount out of $($tarFiles.Count) images."
    return ($successCount -gt 0)
}

#------------------------------
# Function: Check-AndRestoreBackup
# Description: Checks if a backup exists for an image and offers to restore it.
# Parameters:
#   -Engine: Path to the container engine (docker or podman)
#   -ImageName: Name of the image to check for backup
#   -BackupFolder: Folder containing the backup tar files (default ".\Backup")
#------------------------------
function Check-AndRestoreBackup {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Engine,
        
        [Parameter(Mandatory = $true)]
        [string]$ImageName,
        
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
        return (Restore-ContainerImage -Engine $Engine -BackupFile $backupFile)
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
#              to an image and saving that image as a tar file. Also backs up 
#              associated volumes if specified.
# Parameters:
#   -Engine: Path to the container engine (docker or podman)
#   -ContainerName: Name of the container to backup
#   -BackupFolder: Folder to store the backup file (default ".\Backup")
#   -BackupVolumes: Whether to also backup volumes associated with the container
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
    
    # Debug output to verify container name
    Write-Host "DEBUG: Container name is '$ContainerName'"
    
    # Create a simple image tag without any container- prefix that might be causing issues
    $backupImageTag = "backup-$ContainerName"
    
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
    if ($safeName -eq "") {
        $safeName = "unknown"
    }
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
# Function: Check-ImageUpdateAvailable
# Description: Checks if a newer version of a container image is available
#              from its registry. Works with multiple registries including
#              docker.io, ghcr.io, and others.
# Parameters:
#   -Engine: Path to the container engine (docker or podman)
#   -ImageName: Full image name including registry (e.g., ghcr.io/open-webui/open-webui:main)
# Returns: $true if an update is available, $false otherwise
#############################################
function Check-ImageUpdateAvailable {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)]
        [string]$Engine,
        
        [Parameter(Mandatory=$true)]
        [string]$ImageName
    )
    
    Write-Host "Checking for updates to $ImageName..."
    
    # First, check if we have the image locally
    $localImageInfo = & $Engine inspect $ImageName 2>$null | ConvertFrom-Json
    if (-not $localImageInfo) {
        Write-Host "Image '$ImageName' not found locally. Update is available." -ForegroundColor Yellow
        return $true
    }
    
    # Get local image digest
    $localDigest = $null
    try {
        if ($localImageInfo -is [array]) {
            $localDigest = $localImageInfo[0].Id
        } else {
            $localDigest = $localImageInfo.Id
        }
    } catch {
        Write-Warning "Could not determine local image digest: $_"
        # If we can't determine local digest, assume update is needed
        return $true
    }
    
    Write-Host "Local image digest: $localDigest"
    
    # Determine container engine type (docker or podman)
    $engineType = "docker"
    if ((Get-Item $Engine).Name -like "*podman*") {
        $engineType = "podman"
    }
    
    # Pull the image with latest tag but don't update the local image
    Write-Host "Checking remote registry for latest version..."
    
    # Different approach for Docker vs Podman
    if ($engineType -eq "docker") {
        # For Docker, we can use the manifest inspect command
        try {
            $remoteDigest = & $Engine manifest inspect $ImageName --verbose 2>$null | ConvertFrom-Json | 
                Select-Object -ExpandProperty Descriptor -ErrorAction SilentlyContinue | 
                Select-Object -ExpandProperty digest -ErrorAction SilentlyContinue
        } catch {
            $remoteDigest = $null
            Write-Warning "Error checking remote manifest: $_"
        }
        
        if (-not $remoteDigest) {
            Write-Warning "Could not determine remote image digest. Using fallback method."
            # Fallback method - pull image info
            & $Engine pull $ImageName 2>&1 | Out-Null
            $remoteImageInfo = & $Engine inspect $ImageName 2>$null | ConvertFrom-Json
            if ($remoteImageInfo -is [array]) {
                $remoteDigest = $remoteImageInfo[0].Id
            } else {
                $remoteDigest = $remoteImageInfo.Id
            }
        }
    } else {
        # For Podman, we need to pull the image to check its digest
        $tempTag = "temp-check-update-$(Get-Random):latest"
        
        # First try skopeo if available (more efficient)
        $skopeo = Get-Command skopeo -ErrorAction SilentlyContinue
        if ($skopeo) {
            try {
                # Convert docker:// or podman:// prefix if needed
                $skopeoUri = $ImageName
                if (-not $skopeoUri.StartsWith("docker://") -and -not $skopeoUri.StartsWith("podman://")) {
                    $skopeoUri = "docker://$skopeoUri"
                }
                
                $skopeoOutput = & skopeo inspect $skopeoUri --raw 2>$null
                $skopeoJson = $skopeoOutput | ConvertFrom-Json
                $remoteDigest = $skopeoJson.config.digest
            } catch {
                $remoteDigest = $null
                Write-Warning "Skopeo inspection failed: $_"
            }
        }
        
        # If skopeo failed or isn't available, fall back to podman pull
        if (-not $remoteDigest) {
            # Use --quiet to avoid downloading the entire image if possible
            & $Engine pull --quiet $ImageName 2>&1 | Out-Null
            
            # Tag it temporarily to avoid affecting the current image
            & $Engine tag $ImageName $tempTag 2>&1 | Out-Null
            
            # Get the digest
            $remoteImageInfo = & $Engine inspect $tempTag 2>$null | ConvertFrom-Json
            if ($remoteImageInfo -is [array]) {
                $remoteDigest = $remoteImageInfo[0].Id
            } else {
                $remoteDigest = $remoteImageInfo.Id
            }
            
            # Remove the temporary tag
            & $Engine rmi $tempTag 2>&1 | Out-Null
        }
    }
    
    if (-not $remoteDigest) {
        Write-Warning "Could not determine remote image digest. Assuming update is needed."
        return $true
    }
    
    Write-Host "Remote image digest: $remoteDigest"
    
    # Compare digests
    if ($localDigest -ne $remoteDigest) {
        Write-Host "Update available! Local and remote image digests differ." -ForegroundColor Green
        return $true
    } else {
        Write-Host "No update available. You have the latest version." -ForegroundColor Green
        return $false
    }
}

#############################################
# Function: Update-Container
# Description: Generic function to update a container while preserving its configuration
# Parameters:
#   -Engine: Path to the container engine (docker or podman)
#   -ContainerName: Name of the container to update
#   -ImageName: Full image name to update to
#   -Platform: Container platform (default: linux/amd64)
#   -RunFunction: A script block that runs the container with the appropriate options
# Returns: $true if successful, $false otherwise
#############################################
function Update-Container {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)]
        [string]$Engine,
        
        [Parameter(Mandatory=$true)]
        [string]$ContainerName,
        
        [Parameter(Mandatory=$true)]
        [string]$ImageName,
        
        [string]$Platform = "linux/amd64",
        
        [Parameter(Mandatory=$true)]
        [scriptblock]$RunFunction
    )
    
    Write-Host "Initiating update for container '$ContainerName'..."
    
    # Step 1: Check if container exists
    $containerInfo = & $Engine inspect $ContainerName 2>$null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Container '$ContainerName' not found. Nothing to update." -ForegroundColor Yellow
        return $false
    }
    
    # Step 2: Check if an update is available
    $updateAvailable = Check-ImageUpdateAvailable -Engine $Engine -ImageName $ImageName
    if (-not $updateAvailable) {
        $forceUpdate = Read-Host "No update available. Do you want to force an update anyway? (Y/N, default is N)"
        if ($forceUpdate -ne "Y") {
            Write-Host "Update canceled. No changes made."
            return $false
        }
        Write-Host "Proceeding with forced update..."
    }
    
    # Step 3: Optionally backup the container
    $createBackup = Read-Host "Create backup before updating? (Y/N, default is Y)"
    if ($createBackup -ne "N") {
        Write-Host "Creating backup of current container..."
        Backup-ContainerState -Engine $Engine -ContainerName $ContainerName
    }
    
    # Step 4: Remove the existing container
    Write-Host "Removing existing container '$ContainerName' as part of the update..."
    & $Engine rm --force $ContainerName
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to remove container '$ContainerName'. Update aborted."
        return $false
    }
    
    # Step 5: Pull the latest image
    Write-Host "Pulling latest image '$ImageName'..."
    & $Engine pull --platform $Platform $ImageName
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to pull the latest image. Update aborted."
        
        # Offer to restore from backup if one was created
        if ($createBackup -ne "N") {
            $restore = Read-Host "Would you like to restore from backup? (Y/N, default is Y)"
            if ($restore -ne "N") {
                Restore-ContainerState -Engine $Engine -ContainerName $ContainerName
            }
        }
        return $false
    }
    
    # Step 6: Run the container using the provided function
    Write-Host "Starting updated container..."
    try {
        & $RunFunction
        Write-Host "Container '$ContainerName' updated successfully!" -ForegroundColor Green
        return $true
    }
    catch {
        Write-Error "Failed to start updated container: $_"
        
        # Offer to restore from backup if one was created
        if ($createBackup -ne "N") {
            $restore = Read-Host "Would you like to restore from backup? (Y/N, default is Y)"
            if ($restore -ne "N") {
                Restore-ContainerState -Engine $Engine -ContainerName $ContainerName
            }
        }
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
#              Also restores associated volumes if backup files exist.
# Parameters:
#   -Engine: Path to the container engine (docker or podman)
#   -ContainerName: Name of the container to restore
#   -BackupFolder: Folder where the backup file is located (default ".\Backup")
#   -RestoreVolumes: Whether to also restore volumes associated with the container
#--------------------------------------
function Restore-ContainerState {
    param(
        [Parameter(Mandatory=$true)]
        [string]$Engine,
        [Parameter(Mandatory=$true)]
        [string]$ContainerName,
        [string]$BackupFolder = ".\Backup",
        [switch]$RestoreVolumes = $true
    )
    
    # First try the container-specific backup format
    $safeName = $ContainerName -replace "[:/]", "_"
    $backupFile = Join-Path $BackupFolder "$safeName-backup.tar"
    
    # If container-specific backup not found, try to find a matching image backup
    if (-not (Test-Path $backupFile)) {
        Write-Host "Container-specific backup file '$backupFile' not found."
        Write-Host "Looking for image backups that might match this container..."
        
        # Get all tar files in the backup folder
        $tarFiles = Get-ChildItem -Path $BackupFolder -Filter "*.tar"
        
        # Try to find a matching image backup
        $matchingBackup = $null
        foreach ($file in $tarFiles) {
            # Extract potential image name from filename (remove .tar extension)
            $potentialImageName = $file.Name -replace '\.tar$', ''
            
            # Check if this backup file might be for the container we're looking for
            if ($potentialImageName -match $ContainerName) {
                $matchingBackup = $file.FullName
                Write-Host "Found potential matching backup: $matchingBackup"
                break
            }
        }
        
        if ($matchingBackup) {
            $backupFile = $matchingBackup
        } else {
            Write-Error "No backup file found for container '$ContainerName'."
            return $false
        }
    }
    
    Write-Host "Loading backup image from '$backupFile'..."
    # podman load [options]
    # load      Load an image from a tar archive.
    # --input string   Specify the input file containing the saved image.
    $loadOutput = & $Engine load --input $backupFile 2>&1
    if ($LASTEXITCODE -ne 0) {
         Write-Error "Failed to load backup image from '$backupFile'."
         return $false
    }
    
    # Parse the actual image name from the load output
    $imageName = $null
    if ($loadOutput -match "Loaded image: (.+)") {
        $imageName = $matches[1].Trim()
        Write-Host "Loaded image: $imageName"
    } else {
        Write-Error "Could not determine the loaded image name from output: $loadOutput"
        return $false
    }
    
    # If RestoreVolumes is true, check for volume backups
    if ($RestoreVolumes) {
        # For n8n, we know the volume name is "n8n_data"
        if ($ContainerName -eq "n8n") {
            $volumeName = "n8n_data"
            $volumeBackupFile = Join-Path $BackupFolder "$volumeName-data.tar"
            
            if (Test-Path $volumeBackupFile) {
                Write-Host "Found volume backup for '$volumeName': $volumeBackupFile"
                
                # Check if volume exists, create if not
                $volumeExists = & $Engine volume ls --filter "name=$volumeName" --format "{{.Name}}"
                if (-not $volumeExists) {
                    Write-Host "Creating volume '$volumeName'..."
                    & $Engine volume create $volumeName
                }
                
                # Ask for confirmation before restoring volume
                $restoreVolumeConfirm = Read-Host "Restore volume data for '$volumeName'? This will merge with existing data. (Y/N, default is Y)"
                if ($restoreVolumeConfirm -ne "N") {
                    Write-Host "Restoring volume '$volumeName' from '$volumeBackupFile'..."
                    
                    # Create a temporary container to restore the volume data
                    $tempContainerName = "restore-volume-$volumeName-$(Get-Random)"
                    
                    # Run a temporary container with the volume mounted and extract the backup
                    & $Engine run --rm --volume ${volumeName}:/target --volume ${BackupFolder}:/backup --name $tempContainerName alpine tar -xf /backup/$(Split-Path $volumeBackupFile -Leaf) -C /target
                    
                    if ($LASTEXITCODE -eq 0) {
                        Write-Host "Successfully restored volume '$volumeName' from '$volumeBackupFile'" -ForegroundColor Green
                    } else {
                        Write-Error "Failed to restore volume '$volumeName'"
                    }
                } else {
                    Write-Host "Skipping volume restore as requested."
                }
            } else {
                Write-Host "No volume backup found for '$volumeName' at '$volumeBackupFile'."
                Write-Host "Will continue with container image restore only. Existing volume data will be preserved."
            }
        }
        # For other containers, we would need to determine volume names differently
    }
    
    # Return the loaded image name
    return $imageName
}