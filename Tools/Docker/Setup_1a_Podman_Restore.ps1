# Define paths based on %USERPROFILE%
$UserProfile = $env:USERPROFILE
$ignitionPath = Join-Path $UserProfile ".config\containers\podman\machine\hyperv\podman-machine-default.ign"
$diskImagePath = Join-Path $UserProfile ".local\share\containers\podman\machine\hyperv\podman-machine-default-amd64.vhdx"

# Display the configuration paths
Write-Information "Podman Machine Configuration Paths:"
Write-Information "  Ignition File Path: $ignitionPath"
Write-Information "  Disk Image (bootable) Path: $diskImagePath"
Write-Information "---------------------------------------------"

# Verify that the disk image exists
if (-not (Test-Path $diskImagePath)) {
    Write-Error "Error: Disk image not found at '$diskImagePath'"
    exit 1
}

# Check if the ignition file exists (optional but recommended)
if (-not (Test-Path $ignitionPath)) {
    Write-Warning "Warning: Ignition file not found at '$ignitionPath'."
}

# List existing Podman system connections
Write-Information ""
Write-Information "Existing Podman System Connections:"
podman system connection ls
Write-Information "---------------------------------------------"

# Check Hyper-V VM status for 'podman-machine-default'
Write-Information ""
$vm = Get-VM -Name "podman-machine-default" -ErrorAction SilentlyContinue
if ($null -ne $vm) {
    Write-Information "Hyper-V VM 'podman-machine-default' status:"
    Write-Information "  State:             $($vm.State)"
    Write-Information "  CPU Usage (%):     $($vm.CPUUsage)"
    Write-Information "  Memory Assigned:   $($vm.MemoryAssigned) MB"
    Write-Information "  Uptime:            $($vm.Uptime)"
} else {
    Write-Information "Hyper-V VM 'podman-machine-default' not found."
}
Write-Information "---------------------------------------------"

# Display menu options
Write-Host ""
Write-Host "Options:"
Write-Host "1 - Initialize (or re-register) the Podman machine with existing data"
Write-Host "2 - Start the Podman machine (if registered)"
Write-Host "3 - Stop the Podman machine (if running)"
Write-Host "4 - Start the Hyper-V VM (if it exists and is off)"
$choice = Read-Host "Enter your choice (1, 2, 3, or 4)"

switch ($choice) {
    "1" {
        # Check if a connection for "podman-machine-default" already exists
        $existingConnections = podman system connection ls 2>$null
        if ($existingConnections -match "podman-machine-default") {
            Write-Information "A connection for 'podman-machine-default' already exists."
            $removeChoice = Read-Host "Do you want to remove the existing connection? (y/n)"
            if ($removeChoice -eq "y") {
                Write-Information "Removing existing connection..."
                podman system connection rm podman-machine-default
                Write-Information "Existing connection removed."
            } else {
                Write-Information "Aborting initialization. You may choose option 2 to simply start the machine."
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
        Write-Information "Reinitializing the machine with the following command:"
        Write-Information "podman $($initArgs -join ' ')"
        & podman @initArgs
    }
    "2" {
        $startArgs = @("machine", "start", "podman-machine-default")
        Write-Information "Starting the Podman machine with the following command:"
        Write-Information "podman $($startArgs -join ' ')"
        & podman @startArgs
    }
    "3" {
        $stopArgs = @("machine", "stop", "podman-machine-default")
        Write-Information "Stopping the Podman machine with the following command:"
        Write-Information "podman $($stopArgs -join ' ')"
        & podman @stopArgs
    }
    "4" {
        if ($null -ne $vm) {
            if ($vm.State -eq "Off") {
                Write-Information "Starting the Hyper-V VM 'podman-machine-default' using Start-VM ..."
                Start-VM -Name "podman-machine-default"
                Write-Information "Hyper-V VM started."
            }
            else {
                Write-Information "Hyper-V VM 'podman-machine-default' is already running (State: $($vm.State))."
            }
        }
        else {
            Write-Information "Hyper-V VM 'podman-machine-default' not found. Cannot start it."
        }
    }
    default {
        Write-Warning "Invalid choice. Exiting."
        exit 1
    }
}
