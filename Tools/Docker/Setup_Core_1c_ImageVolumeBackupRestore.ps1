################################################################################
# Description  : Script to backup and restore container images and volumes using Docker or Podman.
#                Provides a comprehensive menu-driven interface for backup and restore operations.
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
$menuTitle = "Container Images & Volumes Backup/Restore Menu"
$menuItems = [ordered]@{
	"1" = "Backup All Images (App)"
	"2" = "Backup All Volumes (Data)"
	"3" = "Backup Single Image (App)"
	"4" = "Backup Single Volume (Data)"
	"5" = "Restore All Images (App)"
	"6" = "Restore All Volumes (Data)"
	"7" = "Restore Single Image (App)"
	"8" = "Restore Single Volume (Data)"
	"0" = "Exit"
}

# Define Menu Actions
$menuActions = @{
	"1" = { Invoke-ContainerImageBackup -Engine $containerEngine }
	"2" = { Invoke-ContainerVolumeBackup -EngineType $containerEngine }
	"3" = {
		$images = Get-AvailableImages -Engine $containerEngine
		if ($images.Count -eq 0) {
			Write-Host "No images found to backup."
			return
		}
		$selection = Invoke-OptionsMenu -Title "Select Image to Backup" -Options $images -ExitChoice "Exit menu"
		if ($selection -ne "Exit menu" -and -not [string]::IsNullOrWhiteSpace($selection)) {
			Backup-ContainerImage -Engine $containerEngine -ImageName $selection
		}
	}
	"4" = {
		$volumes = Get-AvailableVolumes -Engine $containerEngine
		if ($volumes.Count -eq 0) {
			Write-Host "No volumes found to backup."
			return
		}
		$selection = Invoke-OptionsMenu -Title "Select Volume to Backup" -Options $volumes -ExitChoice "Exit menu"
		if ($selection -ne "Exit menu" -and -not [string]::IsNullOrWhiteSpace($selection)) {
			Backup-ContainerVolume -EngineType $containerEngine -VolumeName $selection
		}
	}
	"5" = { Invoke-ContainerImageRestore -Engine $containerEngine }
	"6" = { Invoke-ContainerVolumeRestore -EngineType $containerEngine }
	"7" = {
		$backupFiles = Get-ChildItem -Path ".\Backup" -Filter "*-image-*.tar" | Sort-Object LastWriteTime -Descending | Select-Object -ExpandProperty Name
		if ($backupFiles.Count -eq 0) {
			Write-Host "No image backup files found."
			return
		}
		$selection = Invoke-OptionsMenu -Title "Select Image Backup to Restore" -Options $backupFiles -ExitChoice "Exit menu"
		if ($selection -ne "Exit menu" -and -not [string]::IsNullOrWhiteSpace($selection)) {
			Restore-ContainerImage -Engine $containerEngine -BackupFile (Join-Path ".\Backup" $selection)
		}
	}
	"8" = {
		$backupFiles = Get-ChildItem -Path ".\Backup" -Filter "*-volume-*.tar" | Sort-Object LastWriteTime -Descending | Select-Object -ExpandProperty Name
		if ($backupFiles.Count -eq 0) {
			Write-Host "No volume backup files found."
			return
		}
		$selection = Invoke-OptionsMenu -Title "Select Volume Backup to Restore" -Options $backupFiles -ExitChoice "Exit menu"
		if ($selection -ne "Exit menu" -and -not [string]::IsNullOrWhiteSpace($selection)) {
			Restore-ContainerVolume -EngineType $containerEngine -VolumeName ($selection -replace "-volume-\d{8}-\d{4}\.tar$", "") -BackupFile (Join-Path ".\Backup" $selection)
		}
	}
	# "0" action is handled internally by Invoke-MenuLoop
}

# Invoke the Menu Loop
Invoke-MenuLoop -MenuTitle $menuTitle -MenuItems $menuItems -ActionMap $menuActions -ExitChoice "0"
