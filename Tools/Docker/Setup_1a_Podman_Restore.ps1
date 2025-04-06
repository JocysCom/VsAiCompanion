# Define paths based on %USERPROFILE%
$UserProfile = $env:USERPROFILE
$ignitionPath = Join-Path $UserProfile ".config\containers\podman\machine\hyperv\podman-machine-default.ign"
$diskImagePath = Join-Path $UserProfile ".local\share\containers\podman\machine\hyperv\podman-machine-default-amd64.vhdx"

# Display the configuration paths
Write-Output "Podman Machine Configuration Paths:"
Write-Output "  Ignition File Path: $ignitionPath"
Write-Output "  Disk Image (bootable) Path: $diskImagePath"
Write-Output "---------------------------------------------"

# Verify that the disk image exists
if (-not (Test-Path $diskImagePath)) {
    Write-Output "Error: Disk image not found at '$diskImagePath'"
    exit 1
}

# Check if the ignition file exists (optional but recommended)
if (-not (Test-Path $ignitionPath)) {
    Write-Output "Warning: Ignition file not found at '$ignitionPath'."
}

# List existing Podman system connections
Write-Output ""
Write-Output "Existing Podman System Connections:"
podman system connection ls
Write-Output "---------------------------------------------"

# Check Hyper-V VM status for 'podman-machine-default'
Write-Output ""
$vm = Get-VM -Name "podman-machine-default" -ErrorAction SilentlyContinue
if ($null -ne $vm) {
    Write-Output "Hyper-V VM 'podman-machine-default' status:"
    Write-Output "  State:             $($vm.State)"
    Write-Output "  CPU Usage (%):     $($vm.CPUUsage)"
    Write-Output "  Memory Assigned:   $($vm.MemoryAssigned) MB"
    Write-Output "  Uptime:            $($vm.Uptime)"
} else {
    Write-Output "Hyper-V VM 'podman-machine-default' not found."
}
Write-Output "---------------------------------------------"

# Display menu options
Write-Output ""
Write-Output "Options:"
Write-Output "1 - Initialize (or re-register) the Podman machine with existing data"
Write-Output "2 - Start the Podman machine (if registered)"
Write-Output "3 - Stop the Podman machine (if running)"
Write-Output "4 - Start the Hyper-V VM (if it exists and is off)"
$choice = Read-Host "Enter your choice (1, 2, 3, or 4)"

switch ($choice) {
    "1" {
        # Check if a connection for "podman-machine-default" already exists
        $existingConnections = podman system connection ls 2>$null
        if ($existingConnections -match "podman-machine-default") {
            Write-Output "A connection for 'podman-machine-default' already exists."
            $removeChoice = Read-Host "Do you want to remove the existing connection? (y/n)"
            if ($removeChoice -eq "y") {
                Write-Output "Removing existing connection..."
                podman system connection rm podman-machine-default
                Write-Output "Existing connection removed."
            } else {
                Write-Output "Aborting initialization. You may choose option 2 to simply start the machine."
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
        Write-Output "Reinitializing the machine with the following command:"
        Write-Output "podman $($initArgs -join ' ')"
        & podman @initArgs
    }
    "2" {
        $startArgs = @("machine", "start", "podman-machine-default")
        Write-Output "Starting the Podman machine with the following command:"
        Write-Output "podman $($startArgs -join ' ')"
        & podman @startArgs
    }
    "3" {
        $stopArgs = @("machine", "stop", "podman-machine-default")
        Write-Output "Stopping the Podman machine with the following command:"
        Write-Output "podman $($stopArgs -join ' ')"
        & podman @stopArgs
    }
    "4" {
        if ($null -ne $vm) {
            if ($vm.State -eq "Off") {
                Write-Output "Starting the Hyper-V VM 'podman-machine-default' using Start-VM ..."
                Start-VM -Name "podman-machine-default"
                Write-Output "Hyper-V VM started."
            }
            else {
                Write-Output "Hyper-V VM 'podman-machine-default' is already running (State: $($vm.State))."
            }
        }
        else {
            Write-Output "Hyper-V VM 'podman-machine-default' not found. Cannot start it."
        }
    }
    default {
        Write-Output "Invalid choice. Exiting."
        exit 1
    }
}
