# ExportContainers.ps1
# IMPORTANT: Run this script in an Elevated (Administrator) PowerShell window.
# This script assumes:
#   - Your VHDX image is located at:
#       C:\Users\<Username>\.local\share\containers\podman\machine\hyperv\podman-machine-default-amd64.vhdx
#   - Your container data is stored inside the VHDX at /var/lib/containers
#   - You have WSL installed and are allowed to mount disks with "wsl --mount"
#
# IMPORTANT: This script requires XFS filesystem support in WSL to access Podman containers


Write-Host "[STEP: VHDX DETECTION]" -ForegroundColor Cyan
$machineInfo = podman machine ls --format json | ConvertFrom-Json
if ($machineInfo -and $machineInfo[0].State -eq "Running") {
    Write-Host "Stopping running Podman machine..." -ForegroundColor Yellow
    podman machine stop
}

# Define VHDX path
$VhdxPath = "$($env:USERPROFILE)\.local\share\containers\podman\machine\hyperv\podman-machine-default-amd64.vhdx"
if (!(Test-Path $VhdxPath)) {
    Write-Host "[ERROR: VHDX NOT FOUND]" -ForegroundColor Red
    Write-Host "VHDX image not found at '$VhdxPath'" -ForegroundColor Red
    exit 1
}
Write-Host "VHDX image found at: $VhdxPath" -ForegroundColor Green

# Define backup directory and file - using the current script directory
$backupDir = Join-Path $(Get-Location).Path "Backup"
Write-Host "Using current directory for backup: $backupDir" -ForegroundColor Green
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$backupFile = "$backupDir\podman_containers_$timestamp.tar.gz"

# Check and install xfsprogs if needed
Write-Host "[STEP: XFS SUPPORT CHECK]" -ForegroundColor Cyan
Write-Host "Checking XFS support in WSL..." -ForegroundColor Yellow
$null = wsl grep -q xfs /proc/filesystems
if ($LASTEXITCODE -ne 0) {
    Write-Host "[WARNING: XFS SUPPORT MISSING]" -ForegroundColor Yellow
    Write-Host "Installing xfsprogs in WSL..." -ForegroundColor Yellow
    $null = wsl sudo apt-get update
    $null = wsl sudo apt-get install -y xfsprogs
    
    # Verify XFS support again
    $null = wsl grep -q xfs /proc/filesystems
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[ERROR: XFS SUPPORT UNAVAILABLE]" -ForegroundColor Red
        Write-Host "XFS support not available in WSL. Please install a distribution that supports XFS." -ForegroundColor Red
        exit 1
    }
}
Write-Host "XFS support verified in WSL." -ForegroundColor Green

Write-Host "[STEP: MOUNTING VHDX]" -ForegroundColor Cyan
Write-Host "Mounting VHDX Disk..." -ForegroundColor Yellow
try {
    $disk = Mount-VHD -Path $VhdxPath -PassThru -ErrorAction Stop | Get-Disk
    $physicalDrive = "\\.\PhysicalDrive$($disk.Number)"
    Write-Host "Mounted as Physical Drive: $physicalDrive" -ForegroundColor Green
} catch {
    Write-Host "[ERROR: VHDX MOUNT FAILED]" -ForegroundColor Red
    Write-Host "Failed to mount VHDX: $_" -ForegroundColor Red
    exit 1
}

Write-Host "[STEP: WSL DISK ATTACH]" -ForegroundColor Cyan
Write-Host "Attaching the disk to WSL for analysis..." -ForegroundColor Yellow
wsl --mount $physicalDrive --bare

Write-Host "[STEP: UNMOUNTING TEMPORARY ATTACHMENT]" -ForegroundColor Cyan
Write-Host "Unmounting disk to prepare for partition mount..." -ForegroundColor Yellow
wsl --unmount $physicalDrive

Write-Host "[STEP: MOUNTING PARTITION]" -ForegroundColor Cyan
Write-Host "Mounting Partition 4 with XFS filesystem..." -ForegroundColor Yellow
wsl --mount $physicalDrive --partition 4 --type xfs

Write-Host "[STEP: CHECKING MOUNT]" -ForegroundColor Cyan
Write-Host "Verifying mounted filesystems:" -ForegroundColor Yellow
wsl sudo lsblk -f

# Check if the mount was successful
$isMounted = wsl mount | Select-String "sdd4"
if (-not $isMounted) {
    Write-Host "[WARNING: PARTITION MOUNT FAILED]" -ForegroundColor Yellow
    Write-Host "Trying to mount manually..." -ForegroundColor Yellow
    
    # Create a mount point
    $null = wsl sudo mkdir -p /mnt/podman_temp
    
    # Try to mount manually
    $null = wsl sudo mount -t xfs /dev/sdd4 /mnt/podman_temp
    
    # Check if manual mount succeeded
    $isMounted = wsl mount | Select-String "sdd4"
    if (-not $isMounted) {
        Write-Host "[ERROR: MANUAL MOUNT FAILED]" -ForegroundColor Red
        Write-Host "Could not mount XFS partition. Please check dmesg:" -ForegroundColor Red
        wsl dmesg | Select-String -Pattern "xfs|mount" -Last 10
        Write-Host "Cleaning up..." -ForegroundColor Yellow
        wsl --unmount $physicalDrive
        Dismount-VHD -Path $VhdxPath
        exit 1
    } else {
        Write-Host "Manual mount successful." -ForegroundColor Green
    }
}

Write-Host "[STEP: FINDING MOUNT POINT]" -ForegroundColor Cyan
# Extract the correct mountpoint - check for sdd4 specifically
$mountInfo = wsl mount | Select-String "sdd4"
if ($mountInfo) {
    # Extract the mount point
    if ($mountInfo -match "on\s+(/\S+)") {
        $mountPoint = $Matches[1]
        Write-Host "XFS Partition mount point found: $mountPoint" -ForegroundColor Green
    } else {
        $mountPoint = "/mnt/podman_temp"  # Fallback to our manual mount point
        Write-Host "Using default mount point: $mountPoint" -ForegroundColor Yellow
    }
} else {
    # If sdd4 is not mounted, check any recent sd* mount
    $mountInfo = wsl mount | Select-String "/dev/sd"
    if ($mountInfo -match "on\s+(/mnt/\S+)") {
        $mountPoint = $Matches[1]
        Write-Host "Mount point found: $mountPoint" -ForegroundColor Yellow
        Write-Host "[WARNING: USING NON-XFS MOUNT]" -ForegroundColor Yellow
    } else {
        Write-Host "[ERROR: MOUNT POINT NOT FOUND]" -ForegroundColor Red
        Write-Host "Could not determine mountpoint automatically." -ForegroundColor Red
        Write-Host "Available mount points:" -ForegroundColor Yellow
        wsl mount | Select-String "/dev/"
        $mountPoint = Read-Host "Please enter the mountpoint from the list above (e.g., /mnt/wsl/...)"
        
        if ([string]::IsNullOrWhiteSpace($mountPoint)) {
            Write-Host "[ERROR: NO MOUNT POINT PROVIDED]" -ForegroundColor Red
            # Clean up
            wsl --unmount $physicalDrive
            Dismount-VHD -Path $VhdxPath
            exit 1
        }
    }
}

Write-Host "[STEP: LOCATING CONTAINERS]" -ForegroundColor Cyan
# Check if /var/lib/containers exists in the mounted filesystem
$containerPath = "$mountPoint/var/lib/containers"
wsl sudo test -d "$containerPath"
if ($LASTEXITCODE -ne 0) {
    Write-Host "[WARNING: STANDARD CONTAINER PATH NOT FOUND]" -ForegroundColor Yellow
    Write-Host "Container directory not found at $containerPath" -ForegroundColor Red
    Write-Host "Listing top-level directories in mount point:" -ForegroundColor Yellow
    wsl sudo ls -la "$mountPoint"
    
    $customPath = Read-Host "Enter the path to containers relative to $mountPoint (e.g., 'var/lib/containers' or press Enter to search)"
    
    if ([string]::IsNullOrWhiteSpace($customPath)) {
        Write-Host "Searching for containers directory..." -ForegroundColor Yellow
        $foundPaths = wsl sudo find $mountPoint -name containers -type d
        if ([string]::IsNullOrWhiteSpace($foundPaths)) {
            Write-Host "[ERROR: NO CONTAINER DIRECTORIES FOUND]" -ForegroundColor Red
            # Clean up
            wsl --unmount $physicalDrive
            Dismount-VHD -Path $VhdxPath
            exit 1
        }
        Write-Host "[INFO: POTENTIAL CONTAINER DIRECTORIES]" -ForegroundColor Cyan
        Write-Host $foundPaths
        $customPath = Read-Host "Enter the full path to use or press Enter to exit"
        
        if ([string]::IsNullOrWhiteSpace($customPath)) {
            # Clean up
            Write-Host "[INFO: USER CANCELLED]" -ForegroundColor Cyan
            wsl --unmount $physicalDrive
            Dismount-VHD -Path $VhdxPath
            exit 1
        }
    } else {
        $customPath = "$mountPoint/$customPath"
    }
    
    $containerPath = $customPath
    Write-Host "Using container path: $containerPath" -ForegroundColor Green
} else {
    Write-Host "Container directory found at standard location: $containerPath" -ForegroundColor Green
}

Write-Host "[STEP: CREATING BACKUP]" -ForegroundColor Cyan
# Convert Windows backup path to WSL path
$driveLetter = (Split-Path -Qualifier $backupDir).Replace(":", "").ToLower()
$pathWithoutDrive = (Split-Path -NoQualifier $backupDir).Replace("\", "/")
$wslBackupDir = "/mnt/$driveLetter$pathWithoutDrive"
$wslBackupFile = "$wslBackupDir/podman_containers_$timestamp.tar.gz"

# Create the backup
Write-Host "Creating backup of container data to: $backupFile" -ForegroundColor Green
$tarCommand = "sudo tar czf $wslBackupFile -C $containerPath ."
Write-Host "Running tar command: $tarCommand" -ForegroundColor Yellow
wsl bash -c $tarCommand

if ($LASTEXITCODE -eq 0) {
    Write-Host "[SUCCESS: BACKUP COMPLETED]" -ForegroundColor Green
    Write-Host "Backup saved to: $backupFile" -ForegroundColor Green
} else {
    Write-Host "[ERROR: BACKUP FAILED]" -ForegroundColor Red
    Write-Host "Backup failed with exit code $LASTEXITCODE" -ForegroundColor Red
}

Write-Host "[STEP: CLEANUP]" -ForegroundColor Cyan
# Clean up
Write-Host "Unmounting Physical Disk..." -ForegroundColor Yellow
wsl --unmount $physicalDrive

Write-Host "Dismounting VHDX Disk..." -ForegroundColor Yellow
Dismount-VHD -Path $VhdxPath

Write-Host "[COMPLETE: OPERATION FINISHED]" -ForegroundColor Green