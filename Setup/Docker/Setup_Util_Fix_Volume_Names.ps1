<#
.SYNOPSIS
    Fix container volume names to match the {container_name}-data pattern.
.DESCRIPTION
    This utility script scans all running and stopped containers, identifies volumes
    that don't follow the {container_name}-data naming pattern, and presents a menu
    to rename them individually.
.NOTES
    Author: Utility Script
    Version: 1.0
    Dependencies: Setup_Helper_ContainerEngine.ps1, Setup_Helper_CoreFunctions.ps1
    Requires: Administrator privileges for volume operations
#>

#==============================================================================
# Script Configuration
#==============================================================================

. "$PSScriptRoot\Setup_Helper_ContainerEngine.ps1"
. "$PSScriptRoot\Setup_Helper_CoreFunctions.ps1"
. "$PSScriptRoot\Setup_Helper_ContainerManagement.ps1"

Set-ScriptLocation

#==============================================================================
# Global Variables
#==============================================================================

$global:volumePattern = "-data$"
$global:scanResults = @()

# --- Engine Selection ---
$global:containerEngine = Select-ContainerEngine
if (-not $global:containerEngine) {
    Write-Warning "No container engine selected. Exiting script."
    exit 1
}

if ($global:containerEngine -eq "docker") {
    Test-AdminPrivilege
}

$global:enginePath = Get-EnginePath -EngineName $global:containerEngine

#==============================================================================
# Function: Get-ContainerVolumeInfo
#==============================================================================
<#
.SYNOPSIS
    Retrieves detailed volume information for all containers.
.DESCRIPTION
    Scans all containers (running and stopped) and extracts their volume mount
    information, including volume names and mount paths.
.OUTPUTS
    [Array] Array of custom objects containing container and volume information.
.EXAMPLE
    PS C:\> Get-ContainerVolumeInfo
#>
function Get-ContainerVolumeInfo {
    [CmdletBinding()]
    [OutputType([array])]
    param()

    Write-Host "`nScanning containers for volume information..."

    $allContainers = & $global:enginePath ps -a --format "{{.Names}}"

    if (-not $allContainers) {
        Write-Warning "No containers found."
        return @()
    }

    $results = @()

    foreach ($containerName in $allContainers) {
        if ([string]::IsNullOrWhiteSpace($containerName)) { continue }

        Write-Verbose "Inspecting container: $containerName"

        $volumeInfo = & $global:enginePath inspect $containerName --format "{{range .Mounts}}{{if eq .Type `"volume`"}}{{.Name}}|{{.Destination}};{{end}}{{end}}" 2>$null

        if ($volumeInfo -and $volumeInfo -ne "") {
            $volumes = $volumeInfo -split ";" | Where-Object { $_ -ne "" }

            foreach ($vol in $volumes) {
                $parts = $vol -split "\|"
                if ($parts.Count -eq 2) {
                    $volumeName = $parts[0].Trim()
                    $mountPath = $parts[1].Trim()

                    $expectedVolumeName = "$containerName-data"
                    $matchesPattern = $volumeName -eq $expectedVolumeName

                    $results += [PSCustomObject]@{
                        ContainerName = $containerName
                        VolumeName = $volumeName
                        MountPath = $mountPath
                        ExpectedName = $expectedVolumeName
                        MatchesPattern = $matchesPattern
                    }
                }
            }
        }
    }

    return $results
}

#==============================================================================
# Function: Rename-ContainerVolume
#==============================================================================
<#
.SYNOPSIS
    Renames a container volume to match the expected pattern.
.DESCRIPTION
    Stops the container, creates a new volume with the correct name, copies data
    from the old volume to the new one, updates the container configuration, and
    removes the old volume.
.PARAMETER ContainerName
    The name of the container using the volume.
.PARAMETER OldVolumeName
    The current name of the volume.
.PARAMETER NewVolumeName
    The desired name for the volume.
.PARAMETER MountPath
    The mount path inside the container.
.OUTPUTS
    [bool] True if successful, False otherwise.
.EXAMPLE
    PS C:\> Rename-ContainerVolume -ContainerName "myapp" -OldVolumeName "old-vol" -NewVolumeName "myapp-data" -MountPath "/app/data"
#>
function Rename-ContainerVolume {
    [CmdletBinding(SupportsShouldProcess=$true)]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory=$true)]
        [string]$ContainerName,

        [Parameter(Mandatory=$true)]
        [string]$OldVolumeName,

        [Parameter(Mandatory=$true)]
        [string]$NewVolumeName,

        [Parameter(Mandatory=$true)]
        [string]$MountPath
    )

    if (-not $PSCmdlet.ShouldProcess("Volume '$OldVolumeName' -> '$NewVolumeName'", "Rename volume")) {
        return $false
    }

    try {
        Write-Host "`nRenaming volume for container: $ContainerName"
        Write-Host "  From: $OldVolumeName"
        Write-Host "  To:   $NewVolumeName"

        $isRunning = & $global:enginePath ps --filter "name=^${ContainerName}$" --format "{{.Names}}"
        $wasRunning = $isRunning -eq $ContainerName

        if ($wasRunning) {
            Write-Host "  Stopping container..."
            & $global:enginePath stop $ContainerName | Out-Null
            if ($LASTEXITCODE -ne 0) {
                throw "Failed to stop container"
            }
        }

        Write-Host "  Creating new volume: $NewVolumeName"
        & $global:enginePath volume create $NewVolumeName | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to create new volume"
        }

        Write-Host "  Copying data from old volume to new volume..."
        $tempContainerName = "temp-volume-copy-$([Guid]::NewGuid().ToString('N').Substring(0,8))"

        & $global:enginePath run --rm `
            --name $tempContainerName `
            -v "${OldVolumeName}:/source" `
            -v "${NewVolumeName}:/destination" `
            alpine sh -c "cp -a /source/. /destination/" | Out-Null

        if ($LASTEXITCODE -ne 0) {
            throw "Failed to copy volume data"
        }

        Write-Host "  Updating container configuration..."
        $containerConfig = & $global:enginePath inspect $ContainerName | ConvertFrom-Json

        & $global:enginePath rm $ContainerName | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to remove old container"
        }

        $config = $containerConfig[0]
        $runArgs = @("run", "-d", "--name", $ContainerName)

        $runArgs += "-v"
        $runArgs += "${NewVolumeName}:${MountPath}"

        foreach ($mount in $config.Mounts) {
            if ($mount.Type -eq "volume" -and $mount.Name -ne $OldVolumeName) {
                $runArgs += "-v"
                $runArgs += "$($mount.Name):$($mount.Destination)"
            }
        }

        if ($config.HostConfig.PortBindings) {
            $config.HostConfig.PortBindings.PSObject.Properties | ForEach-Object {
                $containerPort = $_.Name
                $hostBindings = $_.Value
                foreach ($binding in $hostBindings) {
                    if ($binding.HostPort) {
                        $runArgs += "-p"
                        $runArgs += "$($binding.HostPort):$($containerPort -replace '/.*$', '')"
                    }
                }
            }
        }

        if ($config.Config.Env) {
            foreach ($env in $config.Config.Env) {
                $runArgs += "-e"
                $runArgs += $env
            }
        }

        if ($config.HostConfig.NetworkMode) {
            $runArgs += "--network"
            $runArgs += $config.HostConfig.NetworkMode
        }

        if ($config.HostConfig.RestartPolicy.Name) {
            $runArgs += "--restart"
            $runArgs += $config.HostConfig.RestartPolicy.Name
        }

        $runArgs += $config.Config.Image

        if ($config.Config.Cmd) {
            $runArgs += $config.Config.Cmd
        }

        Write-Host "  Recreating container with new volume..."
        & $global:enginePath $runArgs | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to recreate container"
        }

        Write-Host "  Removing old volume..."
        & $global:enginePath volume rm $OldVolumeName | Out-Null
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Failed to remove old volume: $OldVolumeName. You may need to remove it manually."
        }

        Write-Host "  Volume renamed successfully!" -ForegroundColor Green
        return $true

    } catch {
        Write-Error "Failed to rename volume: $_"
        return $false
    }
}

#==============================================================================
# Main Script Execution
#==============================================================================

# Perform initial scan
Write-Host "`nScanning containers using $global:containerEngine..."
$global:scanResults = Get-ContainerVolumeInfo

if ($global:scanResults.Count -eq 0) {
    Write-Host "No volumes found in any containers." -ForegroundColor Yellow
    Read-Host "`nPress Enter to exit"
    exit 0
}

# Get mismatched volumes
$mismatchedVolumes = $global:scanResults | Where-Object { -not $_.MatchesPattern }

if ($mismatchedVolumes.Count -eq 0) {
    Write-Host "All volumes already match the expected pattern!" -ForegroundColor Green
    Read-Host "`nPress Enter to exit"
    exit 0
}

Write-Host "Found $($mismatchedVolumes.Count) volume(s) that need renaming.`n"

#==============================================================================
# Main Menu Loop
#==============================================================================

do {
    # Get current mismatched volumes
    $mismatchedVolumes = $global:scanResults | Where-Object { -not $_.MatchesPattern }

    if ($mismatchedVolumes.Count -eq 0) {
        Write-Host "`nAll volumes now match the expected pattern!" -ForegroundColor Green
        Read-Host "`nPress Enter to exit"
        exit 0
    }

    # Build dynamic menu with only mismatched containers
    $menuTitle = "Select Volume to Rename"
    $menuItems = [ordered]@{}
    $menuActions = @{}

    # Add numbered items for each mismatched container
    $index = 1
    foreach ($vol in $mismatchedVolumes) {
        $menuKey = "$index"
        $menuLabel = "Container: $($vol.ContainerName), Volume: $($vol.VolumeName) -> $($vol.ExpectedName)"
        $menuItems[$menuKey] = $menuLabel

        # Create closure to capture current volume data
        $currentVol = $vol
        $menuActions[$menuKey] = [ScriptBlock]::Create(@"
`$volumeData = [PSCustomObject]@{
    ContainerName = '$($currentVol.ContainerName)'
    VolumeName = '$($currentVol.VolumeName)'
    ExpectedName = '$($currentVol.ExpectedName)'
    MountPath = '$($currentVol.MountPath)'
}
Write-Host "``n=========================================="
Write-Host "Renaming Volume"
Write-Host "==========================================``n"
Write-Host "Container:    `$(`$volumeData.ContainerName)"
Write-Host "Current name: `$(`$volumeData.VolumeName)"
Write-Host "New name:     `$(`$volumeData.ExpectedName)"
Write-Host "Mount path:   `$(`$volumeData.MountPath)"
Write-Host ""

`$renameVolume = Read-Host "Do you want to rename this volume? (Y/N, default is N)"
if (`$renameVolume -eq "Y") {
    `$result = Rename-ContainerVolume ``
        -ContainerName `$volumeData.ContainerName ``
        -OldVolumeName `$volumeData.VolumeName ``
        -NewVolumeName `$volumeData.ExpectedName ``
        -MountPath `$volumeData.MountPath ``
        -Confirm:`$false

    if (`$result) {
        Write-Host "``nVolume renamed successfully!" -ForegroundColor Green

        Write-Host "``nRescanning containers..."
        `$global:scanResults = Get-ContainerVolumeInfo
    }
} else {
    Write-Host "Volume rename cancelled." -ForegroundColor Yellow
}

Read-Host "``nPress Enter to continue"
"@)

        $index++
    }

    # Add exit option
    $menuItems["0"] = "Exit menu"

    # Run the menu loop
    Invoke-MenuLoop -MenuTitle $menuTitle -MenuItems $menuItems -ActionMap $menuActions -ExitChoice "0" -DefaultChoice "0"

    # Exit the outer loop when menu returns (user selected exit)
    break

} while ($true)