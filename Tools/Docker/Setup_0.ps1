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
        Determines whether a specified application appears in Windows installed programs (registry-based).

    .PARAMETER AppName
        The partial or full name of the application to look for in registry DisplayName entries.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$AppName
    )
    # Registry paths for standard 64-bit and 32-bit uninstall keys.
    $uninstallPaths = @(
        "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*",
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*"
    )
    foreach ($path in $uninstallPaths) {
        try {
            # Get installed programs from the registry path, filter by partial display name.
            $apps = Get-ItemProperty -Path $path -ErrorAction SilentlyContinue |
                    Where-Object { $_.DisplayName -like "*$AppName*" }
            if ($apps) {
                # If at least one match was found, return $true.
                return $true
            }
        }
        catch {
            # Ignore any access or other errors; proceed to next path.
            continue
        }
    }
    # If we reach here, no match was found.
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
        [string] $serviceName
    )

    try {
        # Resolve only IPv4 addresses.
        $ip = [System.Net.Dns]::GetHostAddresses($ComputerName) | Where-Object { $_.AddressFamily -eq 'InterNetwork' } | Select-Object -First 1
        if (-not $ip) {
            throw "No IPv4 address found for $ComputerName."
        }
        
        $client = New-Object System.Net.Sockets.TcpClient
        $async = $client.BeginConnect($ip.ToString(), $Port, $null, $null)
        # Wait up to 5 seconds for connection.
        $connected = $async.AsyncWaitHandle.WaitOne(5000, $false)
        if ($connected -and $client.Connected) {
            Write-Host "$serviceName TCP test succeeded on port $Port at $ComputerName (IPv4: $ip)."
            $client.Close()
            return $true
        } else {
            Write-Error "$serviceName TCP test failed on port $Port at $ComputerName (IPv4: $ip)."
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
        & $Engine load -i $backupFile
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