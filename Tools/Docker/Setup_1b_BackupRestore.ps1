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
    Write-Host "Container Images Backup and Restore Menu"
    Write-Host "------------------------------------------"
    Write-Host "1) Backup all images"
    Write-Host "2) Restore all images from backup and run"
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
        "2" { Restore-ContainerImages -Engine $containerEngine -RunContainers }
        "3" { Write-Information "Exiting..."; break }
        default { Write-Warning "Invalid selection. Please enter 1, 2, or 3." }
    }

    if ($choice -ne "3") {
        Write-Host "`nPress any key to continue..."
        $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
        Clear-Host
    }
} while ($choice -ne "3")
