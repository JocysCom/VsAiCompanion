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
    Write-Output "Container Images Backup and Restore Menu" # Replaced Write-Host
    Write-Output "------------------------------------------" # Replaced Write-Host
    Write-Output "1) Backup all images" # Replaced Write-Host
    Write-Output "2) Restore all images from backup and run" # Replaced Write-Host
    Write-Output "3) Exit" # Replaced Write-Host
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
        "3" { Write-Output "Exiting..."; break } # Replaced Write-Host
        default { Write-Output "Invalid selection. Please enter 1, 2, or 3." } # Replaced Write-Host
    }

    if ($choice -ne "3") {
        Write-Output "`nPress any key to continue..." # Replaced Write-Host
        $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
        Clear-Host
    }
} while ($choice -ne "3")
