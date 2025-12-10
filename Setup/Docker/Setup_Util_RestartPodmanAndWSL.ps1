################################################################################
# Description  : Simple utility to restart Podman machine and WSL so changes to
#                %UserProfile%\.wslconfig take effect, and to reinitialize the
#                WSL-backed Podman VM (podman-machine-default by default).
# Usage        : .\Setup_Util_RestartPodmanAndWSL.ps1
#                .\Setup_Util_RestartPodmanAndWSL.ps1 -DistroName "podman-machine-default" -FullShutdown -VerboseDNSCheck
################################################################################

[CmdletBinding()]
param(
    [Parameter(Mandatory=$false, HelpMessage="WSL distro name to restart (default: podman-machine-default).")]
    [string]$DistroName = "podman-machine-default",

    [Parameter(Mandatory=$false, HelpMessage="Perform full WSL shutdown instead of terminating a single distro.")]
    [switch]$FullShutdown,

    [Parameter(Mandatory=$false, HelpMessage="Seconds to wait between steps.")]
    [int]$DelaySeconds = 2,

    [Parameter(Mandatory=$false, HelpMessage="Show resolv.conf and DNS checks inside Podman VM after restart.")]
    [switch]$VerboseDNSCheck
)

#==============================================================================
# Function: Restart-PodmanAndWSL
#==============================================================================
<#
.SYNOPSIS
    Restarts the Podman machine and WSL so global .wslconfig changes are applied.
.DESCRIPTION
    Performs these steps:
      1) Stop the Podman machine if running
      2) Restart WSL (either terminate a specific distro or full shutdown)
      3) Start the Podman machine again
      4) Optionally verify DNS inside the Podman VM (resolv.conf + basic checks)

    This ensures that changes to %UserProfile%\.wslconfig (e.g., dnsTunneling=true,
    networkingMode=mirrored, autoProxy=true) take effect for the WSL-backed
    Podman VM.
.PARAMETER DistroName
    WSL distro to terminate (defaults to "podman-machine-default").
.PARAMETER FullShutdown
    When specified, performs "wsl.exe --shutdown" to stop all WSL distros, not
    just the specified one.
.PARAMETER DelaySeconds
    Pause between operations to allow services to settle.
.PARAMETER VerboseDNSCheck
    When specified, prints /etc/resolv.conf, getent hosts, and curl status
    inside the Podman VM after restart.
.EXAMPLE
    PS C:\> .\Setup_Util_RestartPodmanAndWSL.ps1
.EXAMPLE
    PS C:\> .\Setup_Util_RestartPodmanAndWSL.ps1 -FullShutdown -VerboseDNSCheck
.NOTES
    Requires Podman CLI in PATH.
#>
function Restart-PodmanAndWSL {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$false)]
        [string]$DistroName = "podman-machine-default",

        [Parameter(Mandatory=$false)]
        [switch]$FullShutdown,

        [Parameter(Mandatory=$false)]
        [int]$DelaySeconds = 2,

        [Parameter(Mandatory=$false)]
        [switch]$VerboseDNSCheck
    )

    Write-Host "==================== Restart Podman + WSL ===================="

    # Step 1: Stop Podman machine
    try {
        Write-Host "Stopping Podman machine..."
        & podman machine stop 2>&1 | Out-Null
        Write-Host "Podman machine stop command issued."
    } catch {
        Write-Warning ("Failed to stop Podman machine: {0}" -f $_.Exception.Message)
    }

    Start-Sleep -Seconds $DelaySeconds

    # Step 2: Restart WSL
    try {
        if ($FullShutdown) {
            Write-Host "Performing full WSL shutdown (wsl.exe --shutdown)..."
            & wsl.exe --shutdown 2>&1 | Out-Null
        } else {
            Write-Host ("Terminating WSL distro '{0}' (wsl.exe --terminate)..." -f $DistroName)
            & wsl.exe --terminate $DistroName 2>&1 | Out-Null
        }
        Write-Host "WSL termination issued."
    } catch {
        Write-Warning ("Failed to terminate/shutdown WSL: {0}" -f $_.Exception.Message)
    }

    Start-Sleep -Seconds $DelaySeconds

    # Optional: show running distros
    try {
        Write-Host "WSL distros running after termination:"
        & wsl.exe --list --running
    } catch {
        Write-Warning ("Failed to list running WSL distros: {0}" -f $_.Exception.Message)
    }

    Start-Sleep -Seconds $DelaySeconds

    # Step 3: Start Podman machine
    try {
        Write-Host "Starting Podman machine..."
        & podman machine start 2>&1 | Out-Null
        Write-Host "Podman machine started."
    } catch {
        Write-Warning ("Failed to start Podman machine: {0}" -f $_.Exception.Message)
    }

    Start-Sleep -Seconds $DelaySeconds

    # Step 4: Optional DNS verification inside VM
    if ($VerboseDNSCheck) {
        Write-Host "`n==================== Podman VM DNS Check ===================="
        try {
            Write-Host "Showing first 20 lines of /etc/resolv.conf..."
            & podman machine ssh "head -n 20 /etc/resolv.conf" 2>&1

            Write-Host "`ngetent ahosts registry-1.docker.io:"
            & podman machine ssh "sh -lc 'getent ahosts registry-1.docker.io || echo unresolved'" 2>&1

            Write-Host "`ncurl to https://registry-1.docker.io/v2/ (HTTP code expected 200/401/404 or 'no-curl'):"
            & podman machine ssh "sh -lc 'command -v curl >/dev/null 2>&1 && curl -fsS -o /dev/null -w \"%{http_code}\n\" https://registry-1.docker.io/v2/ || echo no-curl'" 2>&1
        } catch {
            Write-Warning ("DNS verification encountered an error: {0}" -f $_.Exception.Message)
        }
    }

    Write-Host "=============================================================="
}

# Invoke with provided parameters
Restart-PodmanAndWSL -DistroName $DistroName -FullShutdown:$FullShutdown -DelaySeconds $DelaySeconds -VerboseDNSCheck:$VerboseDNSCheck