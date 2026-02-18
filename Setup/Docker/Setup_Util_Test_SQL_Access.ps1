################################################################################
# Description  : Test SQL Server access from Windows host, Podman machine VM,
#                and a specified container (default: n8n).
# Usage        : Run from repo root without parameters for a full automatic test.
#                Example:
#                  .\Setup_Util_Test_SQL_Access.ps1
#                  .\Setup_Util_Test_SQL_Access.ps1 -SqlHost "host.local" -SqlPort 1433
################################################################################

using namespace System

param(
    [Parameter(Mandatory = $false)]
    [string]$SqlHost = "host.local",

    [Parameter(Mandatory = $false)]
    [int]$SqlPort = 1433,

    [Parameter(Mandatory = $false)]
    [string]$ContainerName = "n8n",

    [Parameter(Mandatory = $false)]
    [ValidateSet("docker", "podman")]
    [string]$Engine = "podman"
)

. "$PSScriptRoot\Setup_Helper_CoreFunctions.ps1"
. "$PSScriptRoot\Setup_Helper_NetworkTests.ps1"
. "$PSScriptRoot\Setup_Helper_ContainerEngine.ps1"

Set-ScriptLocation

$enginePath = Get-EnginePath -EngineName $Engine
if (-not $enginePath) {
    Write-Error "Could not find container engine path for '$Engine'."
    exit 1
}

Write-Host "============================================================"
Write-Host "SQL Connectivity Test"
Write-Host "============================================================"
Write-Host ("SQL Target : {0}:{1}" -f $SqlHost, $SqlPort)
Write-Host "Engine     : $Engine ($enginePath)"
Write-Host "Container  : $ContainerName"
Write-Host ""

# ---------------------------------------------------------------------------
# Detect Windows WSL gateway IP via shared helper.
# On Azure / nested Hyper-V the Podman VM has its own 172.29.x.x IP.
# In that case the Windows host is reachable via the vEthernet (WSL) IP,
# NOT via 127.0.0.1 (which is the VM's own loopback).
# ---------------------------------------------------------------------------
$wslGatewayIp = Get-WSLGatewayIP
if ($wslGatewayIp) {
    Write-Host ("WSL Gateway  : {0} (vEthernet (WSL))" -f $wslGatewayIp)
}
Write-Host ""

# ---------------------------------------------------------------------------
# 1) Windows host -> SQL (always test real host loopback 127.0.0.1)
# ---------------------------------------------------------------------------
$windowsHost = "127.0.0.1"
Write-Host ("[1/3] Windows host -> {0}:{1}" -f $windowsHost, $SqlPort)
$hostTcpOk = Test-TCPPort -ComputerName $windowsHost -Port $SqlPort -ServiceName "WindowsHost"
Write-Host ""

# ---------------------------------------------------------------------------
# Helper: Run a POSIX shell TCP test in a remote context.
# Uses curl timeout-connect (most reliable across Alpine/Fedora).
# Returns $true if the target is reachable, $false otherwise.
# ---------------------------------------------------------------------------
function Test-RemoteTcpPort {
    param(
        [string]$Context,
        [string]$TargetHost,
        [int]$TargetPort,
        [string[]]$CommandPrefix
    )

    Write-Host ("  Testing {0}:{1} ..." -f $TargetHost, $TargetPort)

    # Run connection attempt and capture all output for PowerShell-side analysis.
    # curl: exit 0=ok, 28=timeout after connect (SQL protocol mismatch but TCP open).
    # BusyBox wget: "error getting response" = connected (SQL protocol mismatch, TCP open).
    #               "can't connect to remote host" = connection refused (TCP closed).
    $curlScript = 'curl -v --connect-timeout 1 --max-time 2 telnet://' + "${TargetHost}:${TargetPort}" + ' </dev/null 2>&1; echo curl_exit=$?'
    $wgetScript = 'wget -S --spider -T 2 http://' + "${TargetHost}:${TargetPort}" + '/ 2>&1; echo wget_exit=$?'
    $testScript = "if command -v curl >/dev/null 2>&1; then $curlScript; else $wgetScript; fi"

    $output = & $CommandPrefix[0] $CommandPrefix[1..($CommandPrefix.Length - 1)] $testScript 2>&1
    $outputStr = ($output | Out-String).Trim()

    # Determine success from output content (PowerShell-side parsing)
    $tcpOk = $false
    if ($outputStr -match "curl_exit=(\d+)") {
        $rc = [int]$Matches[1]
        $tcpOk = ($rc -eq 0 -or $rc -eq 28)
    }
    elseif ($outputStr -match "wget_exit=") {
        # BusyBox wget: "error getting response" means TCP connected but SQL binary protocol
        $tcpOk = ($outputStr -match "error getting response" -or $outputStr -match "can't read data")
    }

    if ($tcpOk) {
        Write-Host "  └─ ✅ $Context TCP test succeeded."
        return $true
    }
    else {
        Write-Warning "  └─ ❌ $Context TCP test failed."
        Write-Host "  Detail:`n$outputStr"
        return $false
    }
}

# ---------------------------------------------------------------------------
# 2) Podman machine VM -> SQL
# Try 127.0.0.1 first (works on bare-metal with WSL2 mirrored mode).
# Also try the detected vEthernet (WSL) IP if available (Azure/nested Hyper-V).
# ---------------------------------------------------------------------------
$vmHostsToTry = [System.Collections.Generic.List[string]]@("127.0.0.1")
if ($wslGatewayIp -and $wslGatewayIp -ne "127.0.0.1") {
    $vmHostsToTry.Add($wslGatewayIp)
}

$vmTcpOk = $false
foreach ($h in $vmHostsToTry) {
    Write-Host ("[2/3] Podman machine VM -> {0}:{1}" -f $h, $SqlPort)
    $vmTcpOk = Test-RemoteTcpPort -Context "VM" -TargetHost $h -TargetPort $SqlPort `
        -CommandPrefix @($enginePath, "machine", "ssh", "--")
    if ($vmTcpOk) { break }
}
Write-Host ""

# ---------------------------------------------------------------------------
# 3) Container -> SQL
# In host network mode, container shares the VM network namespace.
# Try host.local (mapped via --add-host), 127.0.0.1, and the WSL gateway IP.
# ---------------------------------------------------------------------------
$containerHostsToTry = [System.Collections.Generic.List[string]]@($SqlHost)
if ($SqlHost -ne "127.0.0.1") {
    $containerHostsToTry.Add("127.0.0.1")
}
if ($wslGatewayIp -and -not $containerHostsToTry.Contains($wslGatewayIp)) {
    $containerHostsToTry.Add($wslGatewayIp)
}

$ctTcpOk = $false
foreach ($h in $containerHostsToTry) {
    Write-Host ("[3/3] Container '{0}' -> {1}:{2}" -f $ContainerName, $h, $SqlPort)
    $ctTcpOk = Test-RemoteTcpPort -Context "Container" -TargetHost $h -TargetPort $SqlPort `
        -CommandPrefix @($enginePath, "exec", $ContainerName, "sh", "-c")
    if ($ctTcpOk) { break }
}
Write-Host ""

Write-Host "============================================================"
Write-Host "Summary"
Write-Host "============================================================"
Write-Host ("Windows host : {0}" -f $(if ($hostTcpOk) { "OK" } else { "FAIL" }))
Write-Host ("Podman VM    : {0}" -f $(if ($vmTcpOk) { "OK" } else { "FAIL" }))
Write-Host ("Container    : {0}" -f $(if ($ctTcpOk) { "OK" } else { "FAIL" }))

if (-not $hostTcpOk -or -not $vmTcpOk -or -not $ctTcpOk) {
    Write-Error ("One or more layers cannot reach SQL on port {0}." -f $SqlPort)
    exit 1
}
