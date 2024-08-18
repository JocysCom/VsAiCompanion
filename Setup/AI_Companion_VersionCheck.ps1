<#
.SYNOPSIS
    A PowerShell script to determine if an application needs updating based on EXE and registry version checks, including crash and installation rate checks.

.DESCRIPTION
    This script checks the current installed version of an application via its executable file and Windows registry, then compares it to a required version.
    It outputs whether an update is required or if the application is up-to-date. It also checks crash rates and controls the installation rate based on predefined limits.
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

#==============================================================================
# Check the crash log. Calculate the crash percentage for the new version.
# The script halts the installation if too many crashes are detected.
# The script speeds up installations if Successful.
#------------------------------------------------------------------------------
# 
# Insallation info accessible via API:
# |-----------------|-----------|----------------|------------------|------------------------|
# | Product         | Version   | Installations  | SuccessfulStarts | TerminatedUnexpectedly |
# |-----------------|-----------|----------------|------------------|------------------------|
# | VS AI Companion | 1.12.53   |          10000 |             9700 |                    300 |
# |-----------------|-----------|----------------|------------------|------------------------|
#
# API settings
$logEnabled = $true
$apiBaseURL = "https://jocys.com/webservices/logs"
$crashThresholdPercentage = 1

function Get-LogValue {
    param (
        [string]$product,
        [Version]$version,
        [string]$key
    )
    $apiURL = "$apiBaseURL/get"
    $body = @{
        Product = $product
        Version = $version.ToString()
        Key = $key
    }
    $response = Invoke-RestMethod -Uri $apiURL -Method Get -Body $body -ContentType "application/json"
    return $response.value
}

function Set-LogValue {
    param (
        [string]$product,
        [Version]$version,
        [string]$key,
        [int]$value
    )
    $apiURL = "$apiBaseURL/set"
    $body = @{
        Product = $product
        Version = $version.ToString()
        Key = $key
        Value = $value
    }
    $response = Invoke-RestMethod -Uri $apiURL -Method Post -Body $body -ContentType "application/json"
    return $response.status
}

# Check the logs for crashes and installations
if ($logEnabled) {
    $successfulStarts = [int](Get-LogValue -product $product -version $currentVersionMsi -key "SuccessfulStarts")
    $installations = [int](Get-LogValue -product $product -version $currentVersionMsi -key "Installations")
    $installationsMax = $successfulStarts * 2
    $terminatedUnexpectedly = [int](Get-LogValue -product $product -version $currentVersionMsi -key "TerminatedUnexpectedly")

    Write-Output "Successful Starts: $successfulStarts"
    Write-Output "Installations: $installations"
    Write-Output "Maximum Installations Allowed: $installationsMax"
    Write-Output "Terminated Unexpectedly: $terminatedUnexpectedly"

    if ($installations -ge $installationsMax) {
        Write-Output "Installation limit reached for the new version. Installation halted."
        exit 0
    }

    if ($installations -gt 0) {
        $crashPercentage = ($terminatedUnexpectedly / $installations) * 100
        Write-Output "Crash percentage for version ${currentVersionMsi}: $crashPercentage%"
        if ($crashPercentage -ge $crashThresholdPercentage) {
            Write-Output "New version has too many crashes. Installation halted."
            exit 0
        }
    }
}

#==============================================================================


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
