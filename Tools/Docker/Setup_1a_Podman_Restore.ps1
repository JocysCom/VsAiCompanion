# Define paths based on %USERPROFILE%
$UserProfile = $env:USERPROFILE
$ignitionPath = Join-Path $UserProfile ".config\containers\podman\machine\hyperv\podman-machine-default.ign"
$diskImagePath = Join-Path $UserProfile ".local\share\containers\podman\machine\hyperv\podman-machine-default-amd64.vhdx"

# Display the configuration paths
Write-Host "Podman Machine Configuration Paths:"
Write-Host "  Ignition File Path: $ignitionPath"
Write-Host "  Disk Image (bootable) Path: $diskImagePath"
Write-Host "---------------------------------------------"

# Verify that the disk image exists
if (-not (Test-Path $diskImagePath)) {
    Write-Host "Error: Disk image not found at '$diskImagePath'" -ForegroundColor Red
    exit 1
}

# Check if the ignition file exists (optional but recommended)
if (-not (Test-Path $ignitionPath)) {
    Write-Host "Warning: Ignition file not found at '$ignitionPath'." -ForegroundColor Yellow
}

# List existing Podman system connections
Write-Host ""
Write-Host "Existing Podman System Connections:"
podman system connection ls
Write-Host "---------------------------------------------"

# Check Hyper-V VM status for 'podman-machine-default'
Write-Host ""
$vm = Get-VM -Name "podman-machine-default" -ErrorAction SilentlyContinue
if ($vm -ne $null) {
    Write-Host "Hyper-V VM 'podman-machine-default' status:"
    Write-Host "  State:             $($vm.State)"
    Write-Host "  CPU Usage (%):     $($vm.CPUUsage)"
    Write-Host "  Memory Assigned:   $($vm.MemoryAssigned) MB"
    Write-Host "  Uptime:            $($vm.Uptime)"
} else {
    Write-Host "Hyper-V VM 'podman-machine-default' not found."
}
Write-Host "---------------------------------------------"

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
            Write-Host "A connection for 'podman-machine-default' already exists."
            $removeChoice = Read-Host "Do you want to remove the existing connection? (y/n)"
            if ($removeChoice -eq "y") {
                Write-Host "Removing existing connection..."
                podman system connection rm podman-machine-default
                Write-Host "Existing connection removed."
            } else {
                Write-Host "Aborting initialization. You may choose option 2 to simply start the machine."
                exit 0
            }
        }
        # Initialize (re-register) the machine using the --image parameter and positional machine name
        $initCommand = "podman machine init --image `"$diskImagePath`" --cpus 18 --memory 3814 --disk-size 93"
        if (Test-Path $ignitionPath) {
            $initCommand += " --ignition-path `"$ignitionPath`""
        }
        $initCommand += " podman-machine-default"
        Write-Host "Reinitializing the machine with the following command:"
        Write-Host $initCommand
        Invoke-Expression $initCommand
    }
    "2" {
        $startCommand = "podman machine start podman-machine-default"
        Write-Host "Starting the Podman machine with the following command:"
        Write-Host $startCommand
        Invoke-Expression $startCommand
    }
    "3" {
        $stopCommand = "podman machine stop podman-machine-default"
        Write-Host "Stopping the Podman machine with the following command:"
        Write-Host $stopCommand
        Invoke-Expression $stopCommand
    }
    "4" {
        if ($vm -ne $null) {
            if ($vm.State -eq "Off") {
                Write-Host "Starting the Hyper-V VM 'podman-machine-default' using Start-VM ..."
                Start-VM -Name "podman-machine-default"
                Write-Host "Hyper-V VM started."
            }
            else {
                Write-Host "Hyper-V VM 'podman-machine-default' is already running (State: $($vm.State))."
            }
        }
        else {
            Write-Host "Hyper-V VM 'podman-machine-default' not found. Cannot start it."
        }
    }
    default {
        Write-Host "Invalid choice. Exiting." -ForegroundColor Red
        exit 1
    }
}