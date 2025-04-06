################################################################################
# File         : Setup_0_ContainerEngine.ps1
# Description  : Contains container engine helper functions for setup scripts:
#                - Get-DockerPath: Find the path to the Docker executable.
#                - Get-PodmanPath: Find the path to the Podman executable.
#                - Select-ContainerEngine: Prompt user to choose Docker or Podman.
# Usage        : Dot-source this script in other setup scripts:
#                . "$PSScriptRoot\Setup_0_ContainerEngine.ps1"
################################################################################

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
    [OutputType([string])] # Explicitly declare return type as string
    param() # Add empty param block for OutputType attribute

    # Use Write-Information for prompts so they don't pollute the output stream and are controllable
    Write-Information "Select container engine:"
    Write-Information "1) Docker"
    Write-Information "2) Podman"
    # Make the prompt more descriptive
    $selection = Read-Host "Select Container Engine (1=Docker, 2=Podman, default=1)"
    if ([string]::IsNullOrWhiteSpace($selection)) {
        $selection = "1"
    }
    switch ($selection) {
        "1" { return "docker" }
        "2" { return "podman" }
        default {
            # Use Write-Information for prompts so they don't pollute the output stream and are controllable
            Write-Information "Invalid selection, defaulting to Docker."
            return "docker"
        }
    }
}
