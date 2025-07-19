################################################################################
# Description  : Script to backup and restore container images using Docker or Podman.
#                Provides a menu-driven interface for backup and restore operations.
# Usage        : Run with appropriate privileges.
################################################################################

using namespace System
using namespace System.IO

# Dot-source the necessary helper function files.
. "$PSScriptRoot\Setup_Helper_CoreFunctions.ps1" # Need this for Invoke-MenuLoop
. "$PSScriptRoot\Setup_Helper_ContainerEngine.ps1"
. "$PSScriptRoot\Setup_Helper_BackupRestore.ps1"

# MAIN SCRIPT EXECUTION

$containerEngine = Select-ContainerEngine

# Verify the chosen container engine is available in PATH.
if (-not (Get-Command $containerEngine -ErrorAction SilentlyContinue)) {
	Write-Error "$containerEngine command not found in PATH. Please install $containerEngine or ensure it's available."
	exit 1
}

# Define Menu Title and Items
$menuTitle = "Container Images Backup and Restore Menu"
$menuItems = [ordered]@{
	"1" = "Backup all images"
	"2" = "Restore all images from backup and run"
	"0" = "Exit"
}

# Define Menu Actions
$menuActions = @{
	"1" = { Invoke-ContainerImageBackup -Engine $containerEngine }
	"2" = { Invoke-ContainerImageRestore -Engine $containerEngine -RunContainers }
	# "0" action is handled internally by Invoke-MenuLoop
}

# Invoke the Menu Loop
Invoke-MenuLoop -MenuTitle $menuTitle -MenuItems $menuItems -ActionMap $menuActions -ExitChoice "0"
