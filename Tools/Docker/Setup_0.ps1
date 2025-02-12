# Setup_0.ps1 - Basic and Generic Functions for Setup Scripts
# This file contains helper functions for Git checks, Docker path lookup,
# HTTP/TCP port testing, and verifying administrator privileges.
# Dot-source this file in your other setup scripts using:
#    . "$PSScriptRoot\Setup_0.ps1"

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
    $result = Test-NetConnection -ComputerName $ComputerName -Port $Port
    if ($result.TcpTestSucceeded) {
        Write-Host "$serviceName TCP test succeeded on port $Port at $ComputerName."
        return $true
    }
    else {
        Write-Error "$serviceName TCP test failed on port $Port at $ComputerName."
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