# ExportContainers.ps1
# IMPORTANT: Run this script in an Elevated (Administrator) PowerShell window.
# This script assumes:
#   - Your VHDX image is located at:
#       C:\Users\<Username>\.local\share\containers\podman\machine\hyperv\podman-machine-default-amd64.vhdx
#   - Your container data is stored inside the VHDX at /var/lib/containers
#   - You have WSL installed and are allowed to mount disks with "wsl --mount"
#
# IMPORTANT: This script requires XFS filesystem support in WSL to access Podman containers


Write-Output "[STEP: VHDX DETECTION]" # Replaced Write-Host
$machineInfo = podman machine ls --format json | ConvertFrom-Json
if ($machineInfo -and $machineInfo[0].State -eq "Running") {
    Write-Output "Stopping running Podman machine..." # Replaced Write-Host
    podman machine stop
}

# Define VHDX path
$VhdxPath = "$($env:USERPROFILE)\.local\share\containers\podman\machine\hyperv\podman-machine-default-amd64.vhdx"
if (!(Test-Path $VhdxPath)) {
    Write-Output "[ERROR: VHDX NOT FOUND]" # Replaced Write-Host
    Write-Output "VHDX image not found at '$VhdxPath'" # Replaced Write-Host
    exit 1
}
Write-Output "VHDX image found at: $VhdxPath" # Replaced Write-Host

# Define backup directory and file - using the current script directory
$backupDir = Join-Path $(Get-Location).Path "Backup"
Write-Output "Using current directory for backup: $backupDir" # Replaced Write-Host
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$backupFile = "$backupDir\podman_containers_$timestamp.tar.gz"

# Check and install xfsprogs if needed
Write-Output "[STEP: XFS SUPPORT CHECK]" # Replaced Write-Host
Write-Output "Checking XFS support in WSL..." # Replaced Write-Host
$null = wsl grep -q xfs /proc/filesystems
if ($LASTEXITCODE -ne 0) {
    Write-Output "[WARNING: XFS SUPPORT MISSING]" # Replaced Write-Host
    Write-Output "Installing xfsprogs in WSL..." # Replaced Write-Host
    $null = wsl sudo apt-get update
    $null = wsl sudo apt-get install -y xfsprogs

    # Verify XFS support again
    $null = wsl grep -q xfs /proc/filesystems # Removed trailing whitespace from original line 43
    if ($LASTEXITCODE -ne 0) {
        Write-Output "[ERROR: XFS SUPPORT UNAVAILABLE]" # Replaced Write-Host
        Write-Output "XFS support not available in WSL. Please install a distribution that supports XFS." # Replaced Write-Host
        exit 1
    }
}
Write-Output "XFS support verified in WSL." # Replaced Write-Host

Write-Output "[STEP: MOUNTING VHDX]" # Replaced Write-Host
Write-Output "Mounting VHDX Disk..." # Replaced Write-Host
try {
    $disk = Mount-VHD -Path $VhdxPath -PassThru -ErrorAction Stop | Get-Disk
    $physicalDrive = "\\.\PhysicalDrive$($disk.Number)"
    Write-Output "Mounted as Physical Drive: $physicalDrive" # Replaced Write-Host
} catch {
    Write-Output "[ERROR: VHDX MOUNT FAILED]" # Replaced Write-Host
    Write-Output "Failed to mount VHDX: $_" # Replaced Write-Host
    exit 1
}

Write-Output "[STEP: WSL DISK ATTACH]" # Replaced Write-Host
Write-Output "Attaching the disk to WSL for analysis..." # Replaced Write-Host
wsl --mount $physicalDrive --bare

Write-Output "[STEP: UNMOUNTING TEMPORARY ATTACHMENT]" # Replaced Write-Host
Write-Output "Unmounting disk to prepare for partition mount..." # Replaced Write-Host
wsl --unmount $physicalDrive

Write-Output "[STEP: MOUNTING PARTITION]" # Replaced Write-Host
Write-Output "Mounting Partition 4 with XFS filesystem..." # Replaced Write-Host
wsl --mount $physicalDrive --partition 4 --type xfs

Write-Output "[STEP: CHECKING MOUNT]" # Replaced Write-Host
Write-Output "Verifying mounted filesystems:" # Replaced Write-Host
wsl sudo lsblk -f

# Check if the mount was successful
$isMounted = wsl mount | Select-String "sdd4"
if (-not $isMounted) {
    Write-Output "[WARNING: PARTITION MOUNT FAILED]" # Replaced Write-Host
    Write-Output "Trying to mount manually..." # Replaced Write-Host

    # Create a mount point
    $null = wsl sudo mkdir -p /mnt/podman_temp # Removed trailing whitespace from original line 87

    # Try to mount manually
    $null = wsl sudo mount -t xfs /dev/sdd4 /mnt/podman_temp # Removed trailing whitespace from original line 90

    # Check if manual mount succeeded
    $isMounted = wsl mount | Select-String "sdd4" # Removed trailing whitespace from original line 93
    if (-not $isMounted) {
        Write-Output "[ERROR: MANUAL MOUNT FAILED]" # Replaced Write-Host
        Write-Output "Could not mount XFS partition. Please check dmesg:" # Replaced Write-Host
		$dmesgOutput = wsl dmesg | Select-String -Pattern "xfs|mount"
		$dmesgOutput | Select-Object -Last 10
        Write-Output "Cleaning up..." # Replaced Write-Host
        wsl --unmount $physicalDrive
        Dismount-VHD -Path $VhdxPath
        exit 1
    } else {
        Write-Output "Manual mount successful." # Replaced Write-Host
    }
}

Write-Output "[STEP: FINDING MOUNT POINT]" # Replaced Write-Host
# Extract the correct mountpoint - check for sdd4 specifically
$mountInfo = wsl mount | Select-String "sdd4"
if ($mountInfo) {
    # Extract the mount point
    if ($mountInfo -match "on\s+(/\S+)") {
        $mountPoint = $Matches[1]
        Write-Output "XFS Partition mount point found: $mountPoint" # Replaced Write-Host
    } else {
        $mountPoint = "/mnt/podman_temp"  # Fallback to our manual mount point
        Write-Output "Using default mount point: $mountPoint" # Replaced Write-Host
    }
} else {
    # If sdd4 is not mounted, check any recent sd* mount
    $mountInfo = wsl mount | Select-String "/dev/sd"
    if ($mountInfo -match "on\s+(/mnt/\S+)") {
        $mountPoint = $Matches[1]
        Write-Output "Mount point found: $mountPoint" # Replaced Write-Host
        Write-Output "[WARNING: USING NON-XFS MOUNT]" # Replaced Write-Host
    } else {
        Write-Output "[ERROR: MOUNT POINT NOT FOUND]" # Replaced Write-Host
        Write-Output "Could not determine mountpoint automatically." # Replaced Write-Host
        Write-Output "Available mount points:" # Replaced Write-Host
        wsl mount | Select-String "/dev/"
        $mountPoint = Read-Host "Please enter the mountpoint from the list above (e.g., /mnt/wsl/...)"

        if ([string]::IsNullOrWhiteSpace($mountPoint)) {
            Write-Output "[ERROR: NO MOUNT POINT PROVIDED]" # Replaced Write-Host
            # Clean up
            wsl --unmount $physicalDrive
            Dismount-VHD -Path $VhdxPath
            exit 1
        }
    }
}

Write-Output "[STEP: LOCATING CONTAINERS]" # Replaced Write-Host
# Check if /var/lib/containers exists in the mounted filesystem
$containerPath = "$mountPoint/var/lib/containers"
wsl sudo test -d "$containerPath"
if ($LASTEXITCODE -ne 0) {
    Write-Output "[WARNING: STANDARD CONTAINER PATH NOT FOUND]" # Replaced Write-Host
    Write-Output "Container directory not found at $containerPath" # Replaced Write-Host
    Write-Output "Listing top-level directories in mount point:" # Replaced Write-Host
    wsl sudo ls -la "$mountPoint"

    $customPath = Read-Host "Enter the path to containers relative to $mountPoint (e.g., 'var/lib/containers' or press Enter to search)" # Removed trailing whitespace from original line 155

    if ([string]::IsNullOrWhiteSpace($customPath)) {
        Write-Output "Searching for containers directory..." # Replaced Write-Host
        $foundPaths = wsl sudo find $mountPoint -name containers -type d # Removed trailing whitespace from original line 157
        if ([string]::IsNullOrWhiteSpace($foundPaths)) {
            Write-Output "[ERROR: NO CONTAINER DIRECTORIES FOUND]" # Replaced Write-Host
            # Clean up
            wsl --unmount $physicalDrive
            Dismount-VHD -Path $VhdxPath
            exit 1
        }
        Write-Output "[INFO: POTENTIAL CONTAINER DIRECTORIES]" # Replaced Write-Host
        Write-Output $foundPaths # Replaced Write-Host
        $customPath = Read-Host "Enter the full path to use or press Enter to exit"

        if ([string]::IsNullOrWhiteSpace($customPath)) {
            # Clean up
            Write-Output "[INFO: USER CANCELLED]" # Replaced Write-Host
            wsl --unmount $physicalDrive # Removed trailing whitespace from original line 171
            Dismount-VHD -Path $VhdxPath
            exit 1
        }
    } else {
        $customPath = "$mountPoint/$customPath"
    }

    $containerPath = $customPath # Removed trailing whitespace from original line 182
    Write-Output "Using container path: $containerPath" # Replaced Write-Host
} else {
    Write-Output "Container directory found at standard location: $containerPath" # Replaced Write-Host
}

Write-Output "[STEP: CREATING BACKUP]" # Replaced Write-Host
# Convert Windows backup path to WSL path
$driveLetter = (Split-Path -Qualifier $backupDir).Replace(":", "").ToLower()
$pathWithoutDrive = (Split-Path -NoQualifier $backupDir).Replace("\", "/")
$wslBackupDir = "/mnt/$driveLetter$pathWithoutDrive"
$wslBackupFile = "$wslBackupDir/podman_containers_$timestamp.tar.gz"

# Create the backup
Write-Output "Creating backup of container data to: $backupFile" # Replaced Write-Host
$tarCommand = "sudo tar czf $wslBackupFile -C $containerPath ."
Write-Output "Running tar command: $tarCommand" # Replaced Write-Host
wsl bash -c $tarCommand

if ($LASTEXITCODE -eq 0) {
    Write-Output "[SUCCESS: BACKUP COMPLETED]" # Replaced Write-Host
    Write-Output "Backup saved to: $backupFile" # Replaced Write-Host
} else {
    Write-Output "[ERROR: BACKUP FAILED]" # Replaced Write-Host
    Write-Output "Backup failed with exit code $LASTEXITCODE" # Replaced Write-Host
}

Write-Output "[STEP: CLEANUP]" # Replaced Write-Host
# Clean up
Write-Output "Unmounting Physical Disk..." # Replaced Write-Host
wsl --unmount $physicalDrive

Write-Output "Dismounting VHDX Disk..." # Replaced Write-Host
Dismount-VHD -Path $VhdxPath

Write-Output "[COMPLETE: OPERATION FINISHED]" # Replaced Write-Host
