################################################################################
# File         : Setup_1b_BackupRestore.ps1
# Description  : Script to backup and restore container images using Docker or Podman.
#                Provides a menu-driven interface for backup and restore operations.
# Usage        : Run with appropriate privileges.
################################################################################

using namespace System
using namespace System.IO

# Dot-source the necessary helper function files.
. "$PSScriptRoot\Setup_0_ContainerEngine.ps1"
. "$PSScriptRoot\Setup_0_BackupRestore.ps1"

# Function: Show-MainMenu displays backup/restore options.
function Show-MainMenu {
	Write-Information "==========================================="
    Write-Information "Container Images Backup and Restore Menu"
	Write-Information "==========================================="
    Write-Information "1. Backup all images"
    Write-Information "2. Restore all images from backup and run"
    Write-Information "0. Exit"
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
    $choice = Read-Host "Enter your choice (1, 2, or 0)"

    switch ($choice) {
        "1" { Invoke-ContainerImageBackup -Engine $containerEngine }
        "2" { Invoke-ContainerImageRestore -Engine $containerEngine -RunContainers }
        "0" { Write-Information "Exiting..."; break }
        default { Write-Warning "Invalid selection. Please enter 1, 2, or 0." }
    }

    if ($choice -ne "0") {
        Write-Information "`nPress any key to continue..."
        $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
        Clear-Host
    }
} while ($choice -ne "0")
