################################################################################
# File         : Setup_0_WSL.ps1
# Description  : Contains WSL helper functions for setup scripts:
#                - Check-WSLStatus: Verify WSL installation and required features.
# Usage        : Dot-source this script in other setup scripts:
#                . "$PSScriptRoot\Setup_0_WSL.ps1"
################################################################################

#############################################
# WSL and Service Health Functions
#############################################

function Test-WSLStatus { # Renamed function
    Write-Output "Verifying WSL installation and required service status..."

    # Check if the wsl command is available
    if (!(Get-Command wsl -ErrorAction SilentlyContinue)) {
         Write-Error "WSL (wsl.exe) is not available. Please install Windows Subsystem for Linux."
         exit 1
    }

    # Check WSL version - we need WSL2
    $wslVersionInfo = wsl --version 2>&1
    Write-Output "WSL Version Info:`n$wslVersionInfo"

    # Check if running WSL 2
    $wslVersion = wsl --status | Select-String -Pattern "Default Version: (\d+)" | ForEach-Object { $_.Matches.Groups[1].Value }
    if ($wslVersion -ne "2") {
        Write-Warning "WSL seems to be running version $wslVersion but WSL 2 is required."
        Write-Warning "Please run 'wsl --set-default-version 2' as Administrator to set WSL 2 as default."
        $setWsl2 = Read-Host "Would you like to set WSL 2 as default now? (Y/N, default is Y)"
        if ($setWsl2 -ne "N") {
            wsl --set-default-version 2
            if ($LASTEXITCODE -ne 0) {
                 Write-Error "Failed to set WSL 2 as default. Please do this manually."
                 exit 1
             }
             Write-Output "WSL 2 has been set as the default." # Removed ForegroundColor Green
         } else {
             Write-Error "WSL 2 is required but not set as default. Exiting."
            exit 1
        }
    }

    # Check if the Windows Subsystem for Linux feature is enabled
    $wslFeature = Get-WindowsOptionalFeature -Online -FeatureName Microsoft-Windows-Subsystem-Linux
    if ($wslFeature.State -ne "Enabled") {
         Write-Warning "The Microsoft-Windows-Subsystem-Linux feature is not enabled."
          $choice = Read-Host "Do you want to enable it automatically? (Y/N)"
          if ($choice -and $choice.ToUpper() -eq "Y") {
              Write-Output "Enabling WSL feature..."
              dism.exe /Online /Enable-Feature /FeatureName:Microsoft-Windows-Subsystem-Linux /All /NoRestart | Out-Null
              Write-Output "WSL feature enabled. A system restart may be required to activate changes."
          } else {
              Write-Error "The Microsoft-Windows-Subsystem-Linux feature is required. Exiting."
             exit 1
         }
    }

    # Check if the Virtual Machine Platform feature is enabled
    $vmFeature = Get-WindowsOptionalFeature -Online -FeatureName VirtualMachinePlatform
    if ($vmFeature.State -ne "Enabled") {
         Write-Warning "The VirtualMachinePlatform feature is not enabled."
          $choice = Read-Host "Do you want to enable it automatically? (Y/N)"
          if ($choice -and $choice.ToUpper() -eq "Y") {
              Write-Output "Enabling VirtualMachinePlatform feature..."
              dism.exe /Online /Enable-Feature /FeatureName:VirtualMachinePlatform /All /NoRestart | Out-Null
              Write-Output "VirtualMachinePlatform feature enabled. A system restart may be required to activate changes."
          } else {
              Write-Error "The VirtualMachinePlatform feature is required. Exiting."
             exit 1
          }
     }

     Write-Output "WSL and required Windows features are enabled."
 }
