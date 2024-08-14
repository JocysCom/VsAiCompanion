<#
.SYNOPSIS
    A PowerShell script to determine if an application needs updating based on EXE and registry version checks.

.DESCRIPTION
    This script checks the current installed version of an application via its executable file and Windows registry, then compares it to a required version.
    It outputs whether an update is required or if the application is up-to-date, and is designed for use within the context of Microsoft Intune.
    https://learn.microsoft.com/en-us/mem/intune/apps/intune-management-extension
#>

# Replace this with the actual info you aim to install (the version in the MSI)
$company = "Jocys.com"
$product = "VS AI Companion"
$currentVersionMsi = [Version]"1.12.53"
$exeFile = "JocysCom.VS.AiCompanion.App.exe"

# Get the current EXE version
$exePath = "$Env:LOCALAPPDATA\$company\$product\$exeFile"
$currentVersionExe = $null
$currentVersionReg = $null

if (Test-Path $exePath) {
    try {
        $exeVersionInfo = Get-Item $exePath -ErrorAction SilentlyContinue
        $currentVersionExe = [Version]$exeVersionInfo.VersionInfo.FileVersion
    } catch {
        Write-Output "Failed to get EXE version."
    }
}

# Get the current REG version
try {
    $regPath = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*"
    $regEntry = Get-ItemProperty $regPath -ErrorAction SilentlyContinue | Where-Object { $_.DisplayName -eq $product }
    $currentVersionReg = [Version]$regEntry.DisplayVersion
} catch {
    Write-Output "Failed to get REG version."
}

Write-Output "MSI version: $currentVersionMsi"
Write-Output "EXE version: $currentVersionExe"
Write-Output "REG version: $currentVersionReg"

# Determine version to use for comparison.
if ($currentVersionExe) {
    $currentVersion = $currentVersionExe
} elseif ($currentVersionReg) {
    $currentVersion = $currentVersionReg
}

# Handle version determination and update prevention logic.
if (-not ($currentVersion)) {
    Write-Output "Application not found. Installation required"
    exit 1
} elseif ($currentVersion -lt $currentVersionMsi) {
    Write-Output "The current version is older. Update required."
    exit 1
} elseif ($currentVersion -eq $currentVersionMsi) {
    Write-Output "The current version is same. No update required."
    exit 0
} else {
    Write-Output "The current version is newer. No update required."
    exit 0
}
