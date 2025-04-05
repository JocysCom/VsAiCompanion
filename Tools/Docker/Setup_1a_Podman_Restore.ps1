# Define paths based on %USERPROFILE%
$UserProfile = $env:USERPROFILE
$ignitionPath = Join-Path $UserProfile ".config\containers\podman\machine\hyperv\podman-machine-default.ign"
$diskImagePath = Join-Path $UserProfile ".local\share\containers\podman\machine\hyperv\podman-machine-default-amd64.vhdx"

# Display the configuration paths
Write-Output "Podman Machine Configuration Paths:" # Replaced Write-Host
Write-Output "  Ignition File Path: $ignitionPath" # Replaced Write-Host
Write-Output "  Disk Image (bootable) Path: $diskImagePath" # Replaced Write-Host
Write-Output "---------------------------------------------" # Replaced Write-Host

# Verify that the disk image exists
if (-not (Test-Path $diskImagePath)) {
    Write-Output "Error: Disk image not found at '$diskImagePath'" # Replaced Write-Host
    exit 1
}

# Check if the ignition file exists (optional but recommended)
if (-not (Test-Path $ignitionPath)) {
    Write-Output "Warning: Ignition file not found at '$ignitionPath'." # Replaced Write-Host
}

# List existing Podman system connections
Write-Output "" # Replaced Write-Host
Write-Output "Existing Podman System Connections:" # Replaced Write-Host
podman system connection ls
Write-Output "---------------------------------------------" # Replaced Write-Host

# Check Hyper-V VM status for 'podman-machine-default'
Write-Output "" # Replaced Write-Host
$vm = Get-VM -Name "podman-machine-default" -ErrorAction SilentlyContinue
if ($null -ne $vm) { # Corrected null comparison
    Write-Output "Hyper-V VM 'podman-machine-default' status:" # Replaced Write-Host
    Write-Output "  State:             $($vm.State)" # Replaced Write-Host
    Write-Output "  CPU Usage (%):     $($vm.CPUUsage)" # Replaced Write-Host
    Write-Output "  Memory Assigned:   $($vm.MemoryAssigned) MB" # Replaced Write-Host
    Write-Output "  Uptime:            $($vm.Uptime)" # Replaced Write-Host
} else {
    Write-Output "Hyper-V VM 'podman-machine-default' not found." # Replaced Write-Host
}
Write-Output "---------------------------------------------" # Replaced Write-Host

# Display menu options
Write-Output "" # Replaced Write-Host
Write-Output "Options:" # Replaced Write-Host
Write-Output "1 - Initialize (or re-register) the Podman machine with existing data" # Replaced Write-Host
Write-Output "2 - Start the Podman machine (if registered)" # Replaced Write-Host
Write-Output "3 - Stop the Podman machine (if running)" # Replaced Write-Host
Write-Output "4 - Start the Hyper-V VM (if it exists and is off)" # Replaced Write-Host
$choice = Read-Host "Enter your choice (1, 2, 3, or 4)"

switch ($choice) {
    "1" {
        # Check if a connection for "podman-machine-default" already exists
        $existingConnections = podman system connection ls 2>$null
        if ($existingConnections -match "podman-machine-default") {
            Write-Output "A connection for 'podman-machine-default' already exists." # Replaced Write-Host
            $removeChoice = Read-Host "Do you want to remove the existing connection? (y/n)"
            if ($removeChoice -eq "y") {
                Write-Output "Removing existing connection..." # Replaced Write-Host
                podman system connection rm podman-machine-default
                Write-Output "Existing connection removed." # Replaced Write-Host
            } else {
                Write-Output "Aborting initialization. You may choose option 2 to simply start the machine." # Replaced Write-Host
                exit 0
            }
        }
        # Initialize (re-register) the machine using the --image parameter and positional machine name
        $initArgs = @(
            "machine", "init",
            "--image", $diskImagePath,
            "--cpus", "18",
            "--memory", "3814",
            "--disk-size", "93"
        )
        if (Test-Path $ignitionPath) {
            $initArgs += "--ignition-path", $ignitionPath
        }
        $initArgs += "podman-machine-default"
        Write-Output "Reinitializing the machine with the following command:" # Replaced Write-Host
        Write-Output "podman $($initArgs -join ' ')" # Replaced Write-Host
        & podman @initArgs # Replaced Invoke-Expression
    }
    "2" {
        $startArgs = @("machine", "start", "podman-machine-default")
        Write-Output "Starting the Podman machine with the following command:" # Replaced Write-Host
        Write-Output "podman $($startArgs -join ' ')" # Replaced Write-Host
        & podman @startArgs # Replaced Invoke-Expression
    }
    "3" {
        $stopArgs = @("machine", "stop", "podman-machine-default")
        Write-Output "Stopping the Podman machine with the following command:" # Replaced Write-Host
        Write-Output "podman $($stopArgs -join ' ')" # Replaced Write-Host
        & podman @stopArgs # Replaced Invoke-Expression
    }
    "4" {
        if ($null -ne $vm) { # Corrected null comparison
            if ($vm.State -eq "Off") {
                Write-Output "Starting the Hyper-V VM 'podman-machine-default' using Start-VM ..." # Replaced Write-Host
                Start-VM -Name "podman-machine-default"
                Write-Output "Hyper-V VM started." # Replaced Write-Host
            }
            else {
                Write-Output "Hyper-V VM 'podman-machine-default' is already running (State: $($vm.State))." # Replaced Write-Host
            }
        }
        else {
            Write-Output "Hyper-V VM 'podman-machine-default' not found. Cannot start it." # Replaced Write-Host
        }
    }
    default {
        Write-Output "Invalid choice. Exiting." # Replaced Write-Host
        exit 1
    }
}
