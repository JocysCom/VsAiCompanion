# ExportContainers.ps1
# IMPORTANT: Run this script in an Elevated (Administrator) PowerShell window.
# This script assumes:
#   - Your VHDX image is located at:
#       C:\Users\<Username>\.local\share\containers\podman\machine\hyperv\podman-machine-default-amd64.vhdx
#   - Your container data is stored inside the VHDX at /var/lib/containers
#   - You have WSL installed and are allowed to mount disks with "wsl --mount"
#
# IMPORTANT: This script requires XFS filesystem support in WSL to access Podman containers


Write-Output "[STEP: VHDX DETECTION]"
$machineInfo = podman machine ls --format json | ConvertFrom-Json
if ($machineInfo -and $machineInfo[0].State -eq "Running") {
    Write-Output "Stopping running Podman machine..."
    podman machine stop
}

# Define VHDX path
$VhdxPath = "$($env:USERPROFILE)\.local\share\containers\podman\machine\hyperv\podman-machine-default-amd64.vhdx"
if (!(Test-Path $VhdxPath)) {
    Write-Output "[ERROR: VHDX NOT FOUND]"
    Write-Output "VHDX image not found at '$VhdxPath'"
    exit 1
}
Write-Output "VHDX image found at: $VhdxPath"

# Define backup directory and file - using the current script directory
$backupDir = Join-Path $(Get-Location).Path "Backup"
Write-Output "Using current directory for backup: $backupDir"
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$backupFile = "$backupDir\podman_containers_$timestamp.tar.gz"

# Check and install xfsprogs if needed
Write-Output "[STEP: XFS SUPPORT CHECK]"
Write-Output "Checking XFS support in WSL..."
$null = wsl grep -q xfs /proc/filesystems
if ($LASTEXITCODE -ne 0) {
    Write-Output "[WARNING: XFS SUPPORT MISSING]"
    Write-Output "Installing xfsprogs in WSL..."
    $null = wsl sudo apt-get update
    $null = wsl sudo apt-get install -y xfsprogs

    # Verify XFS support again
    $null = wsl grep -q xfs /proc/filesystems
    if ($LASTEXITCODE -ne 0) {
        Write-Output "[ERROR: XFS SUPPORT UNAVAILABLE]"
        Write-Output "XFS support not available in WSL. Please install a distribution that supports XFS."
        exit 1
    }
}
Write-Output "XFS support verified in WSL."

Write-Output "[STEP: MOUNTING VHDX]"
Write-Output "Mounting VHDX Disk..."
try {
    $disk = Mount-VHD -Path $VhdxPath -PassThru -ErrorAction Stop | Get-Disk
    $physicalDrive = "\\.\PhysicalDrive$($disk.Number)"
    Write-Output "Mounted as Physical Drive: $physicalDrive"
} catch {
    Write-Output "[ERROR: VHDX MOUNT FAILED]"
    Write-Output "Failed to mount VHDX: $_"
    exit 1
}

Write-Output "[STEP: WSL DISK ATTACH]"
Write-Output "Attaching the disk to WSL for analysis..."
wsl --mount $physicalDrive --bare

Write-Output "[STEP: UNMOUNTING TEMPORARY ATTACHMENT]"
Write-Output "Unmounting disk to prepare for partition mount..."
wsl --unmount $physicalDrive

Write-Output "[STEP: MOUNTING PARTITION]"
Write-Output "Mounting Partition 4 with XFS filesystem..."
wsl --mount $physicalDrive --partition 4 --type xfs

Write-Output "[STEP: CHECKING MOUNT]"
Write-Output "Verifying mounted filesystems:"
wsl sudo lsblk -f

# Check if the mount was successful
$isMounted = wsl mount | Select-String "sdd4"
if (-not $isMounted) {
    Write-Output "[WARNING: PARTITION MOUNT FAILED]"
    Write-Output "Trying to mount manually..."

    # Create a mount point
    $null = wsl sudo mkdir -p /mnt/podman_temp

    # Try to mount manually
    $null = wsl sudo mount -t xfs /dev/sdd4 /mnt/podman_temp

    # Check if manual mount succeeded
    $isMounted = wsl mount | Select-String "sdd4"
    if (-not $isMounted) {
        Write-Output "[ERROR: MANUAL MOUNT FAILED]"
        Write-Output "Could not mount XFS partition. Please check dmesg:"
		$dmesgOutput = wsl dmesg | Select-String -Pattern "xfs|mount"
		$dmesgOutput | Select-Object -Last 10
        Write-Output "Cleaning up..."
        wsl --unmount $physicalDrive
        Dismount-VHD -Path $VhdxPath
        exit 1
    } else {
        Write-Output "Manual mount successful."
    }
}

Write-Output "[STEP: FINDING MOUNT POINT]"
# Extract the correct mountpoint - check for sdd4 specifically
$mountInfo = wsl mount | Select-String "sdd4"
if ($mountInfo) {
    # Extract the mount point
    if ($mountInfo -match "on\s+(/\S+)") {
        $mountPoint = $Matches[1]
        Write-Output "XFS Partition mount point found: $mountPoint"
    } else {
        $mountPoint = "/mnt/podman_temp"  # Fallback to our manual mount point
        Write-Output "Using default mount point: $mountPoint"
    }
} else {
    # If sdd4 is not mounted, check any recent sd* mount
    $mountInfo = wsl mount | Select-String "/dev/sd"
    if ($mountInfo -match "on\s+(/mnt/\S+)") {
        $mountPoint = $Matches[1]
        Write-Output "Mount point found: $mountPoint"
        Write-Output "[WARNING: USING NON-XFS MOUNT]"
    } else {
        Write-Output "[ERROR: MOUNT POINT NOT FOUND]"
        Write-Output "Could not determine mountpoint automatically."
        Write-Output "Available mount points:"
        wsl mount | Select-String "/dev/"
        $mountPoint = Read-Host "Please enter the mountpoint from the list above (e.g., /mnt/wsl/...)"

        if ([string]::IsNullOrWhiteSpace($mountPoint)) {
            Write-Output "[ERROR: NO MOUNT POINT PROVIDED]"
            # Clean up
            wsl --unmount $physicalDrive
            Dismount-VHD -Path $VhdxPath
            exit 1
        }
    }
}

Write-Output "[STEP: LOCATING CONTAINERS]"
# Check if /var/lib/containers exists in the mounted filesystem
$containerPath = "$mountPoint/var/lib/containers"
wsl sudo test -d "$containerPath"
if ($LASTEXITCODE -ne 0) {
    Write-Output "[WARNING: STANDARD CONTAINER PATH NOT FOUND]"
    Write-Output "Container directory not found at $containerPath"
    Write-Output "Listing top-level directories in mount point:"
    wsl sudo ls -la "$mountPoint"

    $customPath = Read-Host "Enter the path to containers relative to $mountPoint (e.g., 'var/lib/containers' or press Enter to search)"

    if ([string]::IsNullOrWhiteSpace($customPath)) {
        Write-Output "Searching for containers directory..."
        $foundPaths = wsl sudo find $mountPoint -name containers -type d
        if ([string]::IsNullOrWhiteSpace($foundPaths)) {
            Write-Output "[ERROR: NO CONTAINER DIRECTORIES FOUND]"
            # Clean up
            wsl --unmount $physicalDrive
            Dismount-VHD -Path $VhdxPath
            exit 1
        }
        Write-Output "[INFO: POTENTIAL CONTAINER DIRECTORIES]"
        Write-Output $foundPaths
        $customPath = Read-Host "Enter the full path to use or press Enter to exit"

        if ([string]::IsNullOrWhiteSpace($customPath)) {
            # Clean up
            Write-Output "[INFO: USER CANCELLED]"
            wsl --unmount $physicalDrive
            Dismount-VHD -Path $VhdxPath
            exit 1
        }
    } else {
        $customPath = "$mountPoint/$customPath"
    }

    $containerPath = $customPath
    Write-Output "Using container path: $containerPath"
} else {
    Write-Output "Container directory found at standard location: $containerPath"
}

Write-Output "[STEP: CREATING BACKUP]"
# Convert Windows backup path to WSL path
$driveLetter = (Split-Path -Qualifier $backupDir).Replace(":", "").ToLower()
$pathWithoutDrive = (Split-Path -NoQualifier $backupDir).Replace("\", "/")
$wslBackupDir = "/mnt/$driveLetter$pathWithoutDrive"
$wslBackupFile = "$wslBackupDir/podman_containers_$timestamp.tar.gz"

# Create the backup
Write-Output "Creating backup of container data to: $backupFile"
$tarCommand = "sudo tar czf $wslBackupFile -C $containerPath ."
Write-Output "Running tar command: $tarCommand"
wsl bash -c $tarCommand

if ($LASTEXITCODE -eq 0) {
    Write-Output "[SUCCESS: BACKUP COMPLETED]"
    Write-Output "Backup saved to: $backupFile"
} else {
    Write-Output "[ERROR: BACKUP FAILED]"
    Write-Output "Backup failed with exit code $LASTEXITCODE"
}

Write-Output "[STEP: CLEANUP]"
# Clean up
Write-Output "Unmounting Physical Disk..."
wsl --unmount $physicalDrive

Write-Output "Dismounting VHDX Disk..."
Dismount-VHD -Path $VhdxPath

Write-Output "[COMPLETE: OPERATION FINISHED]"
