# DockerImagesBackupRestore.ps1
# This script provides a menu to backup or restore all docker images.
# Backups are stored as tar files in the Backup folder.
# Usage: Run this script in PowerShell with administrator privileges if required.

# Define the backup folder relative to the script's current location.
$backupFolder = ".\Backup"

# Function to backup all Docker images.
function Backup-DockerImages {
    # Create the backup folder if it does not exist.
    if (-not (Test-Path $backupFolder)) {
        New-Item -ItemType Directory -Force -Path $backupFolder | Out-Null
        Write-Host "Created backup folder: $backupFolder"
    }
    
    Write-Host "Retrieving list of Docker images..."
    $images = docker images --format "{{.Repository}}:{{.Tag}}"
    
    if (-not $images) {
        Write-Host "No Docker images found."
        return
    }
    
    foreach ($image in $images) {
        # Replace characters not allowed in file names (':' and '/' are replaced with '_').
        $safeName = $image -replace "[:/]", "_"
        $backupFile = Join-Path $backupFolder "$safeName.tar"
        Write-Host "Backing up image '$image' to '$backupFile'..."
        docker save -o $backupFile $image
        if ($LASTEXITCODE -eq 0) {
            Write-Host "Successfully backed up image '$image'"
        }
        else {
            Write-Error "Failed to backup image '$image'"
        }
    }
}

# Function to restore all Docker images from backup folder.
function Restore-DockerImages {
    # Check if backup folder exists.
    if (-not (Test-Path $backupFolder)) {
        Write-Host "Backup folder '$backupFolder' does not exist. Nothing to restore."
        return
    }
    
    $tarFiles = Get-ChildItem -Path $backupFolder -Filter *.tar
    if (-not $tarFiles) {
        Write-Host "No backup tar files found in '$backupFolder'"
        return
    }
    
    foreach ($file in $tarFiles) {
        Write-Host "Restoring image from '$($file.FullName)'..."
        docker load -i $file.FullName
        if ($LASTEXITCODE -eq 0) {
            Write-Host "Successfully restored image from '$($file.Name)'"
        }
        else {
            Write-Error "Failed to restore image from '$($file.Name)'"
        }
    }
}

# Function to display the menu.
function Show-Menu {
    Write-Host "Docker Backup and Restore Menu"
    Write-Host "--------------------------------"
    Write-Host "1) Backup all Docker images"
    Write-Host "2) Restore all Docker images from backup"
    Write-Host "3) Exit"
}

do {
    Show-Menu
    $choice = Read-Host "Enter your choice (1, 2, or 3)"
    
    switch ($choice) {
        "1" {
            Backup-DockerImages
        }
        "2" {
            Restore-DockerImages
        }
        "3" {
            Write-Host "Exiting..."
            break
        }
        default {
            Write-Host "Invalid selection. Please enter 1, 2, or 3."
        }
    }
    
    if ($choice -ne "3") {
        Write-Host ""
        Write-Host "Press any key to continue..."
        $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
        Clear-Host
    }
} while ($choice -ne "3")