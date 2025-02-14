################################################################################
# File         : Setup_1b_BackupRestore.ps1
# Description  : Script to backup and restore container images using Docker or Podman.
#                Provides a menu-driven interface for backup and restore operations.
# Usage        : Run with appropriate privileges.
################################################################################

using namespace System
using namespace System.IO

# Dot-source the common functions file.
. "$PSScriptRoot\Setup_0.ps1"

# Setup_1b_BackupRestore.ps1
# This script provides a menu to backup or restore container images using Docker or Podman.
# Backups are stored as tar files in the Backup folder.
# Usage: Run this script in PowerShell with administrator privileges if required.

# Function: Backup-ContainerImages
function Backup-ContainerImages {
    param (
        [Parameter(Mandatory = $true)]
        [string] $Engine
    )

    $backupFolder = ".\Backup"

    if (-not (Test-Path $backupFolder)) {
        New-Item -ItemType Directory -Force -Path $backupFolder | Out-Null
        Write-Host "Created backup folder: $backupFolder"
    }

    Write-Host "Retrieving list of images for $Engine..."
    $images = & $Engine images --format "{{.Repository}}:{{.Tag}}"

    if (-not $images) {
        Write-Host "No images found for $Engine."
        return
    }

    foreach ($image in $images) {
        # Replace characters not allowed in file names (':' and '/' become '_').
        $safeName = $image -replace "[:/]", "_"
        $backupFile = Join-Path $backupFolder "$safeName.tar"
        Write-Host "Backing up image '$image' to '$backupFile'..."
        & $Engine save -o $backupFile $image
        if ($LASTEXITCODE -eq 0) {
            Write-Host "Successfully backed up image '$image'"
        }
        else {
            Write-Error "Failed to backup image '$image'"
        }
    }
}

# Function: Restore-ContainerImages
function Restore-ContainerImages {
    param (
        [Parameter(Mandatory = $true)]
        [string] $Engine
    )

    $backupFolder = ".\Backup"

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
        & $Engine load -i $file.FullName
        if ($LASTEXITCODE -eq 0) {
            Write-Host "Successfully restored image from '$($file.Name)'"
        }
        else {
            Write-Error "Failed to restore image from '$($file.Name)'"
        }
    }
}

# Function: Show-MainMenu displays backup/restore options.
function Show-MainMenu {
    Write-Host "Container Images Backup and Restore Menu"
    Write-Host "------------------------------------------"
    Write-Host "1) Backup all images"
    Write-Host "2) Restore all images from backup"
    Write-Host "3) Exit"
}

# MAIN SCRIPT EXECUTION

$containerEngine = Select-ContainerEngine

# Verify the chosen container engine is available in PATH.
if (-not (Get-Command $containerEngine -ErrorAction SilentlyContinue)) {
    Write-Error "$containerEngine command not found in PATH. Please install $containerEngine or ensure it's available."
    exit 1
}

do {
    Show-MainMenu
    $choice = Read-Host "Enter your choice (1, 2, or 3)"

    switch ($choice) {
        "1" { Backup-ContainerImages -Engine $containerEngine }
        "2" { Restore-ContainerImages -Engine $containerEngine }
        "3" { Write-Host "Exiting..."; break }
        default { Write-Host "Invalid selection. Please enter 1, 2, or 3." }
    }

    if ($choice -ne "3") {
        Write-Host "`nPress any key to continue..."
        $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
        Clear-Host
    }
} while ($choice -ne "3")