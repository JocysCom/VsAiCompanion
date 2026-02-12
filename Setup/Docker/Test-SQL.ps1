################################################################################
# File         : Test-SQL.ps1
# Description  : Tests SQL Server TCP connectivity from four layers:
#                  1. Windows host (localhost)
#                  2. Podman VM (via podman machine ssh)
#                  3. Container (via podman exec)
#                Also checks SQL Server service state, TCP/IP protocol status,
#                Windows Firewall rules, and port listening status.
#                Reuses helper functions from Setup_Helper_*.ps1.
# Usage        : .\Test-SQL.ps1
#                .\Test-SQL.ps1 -ContainerName "n8n" -SqlPort 1433
################################################################################

using namespace System
using namespace System.IO

param(
	[Parameter(Mandatory = $false, HelpMessage = "Container name to test from (e.g., 'n8n').")]
	[string]$ContainerName = "n8n",

	[Parameter(Mandatory = $false, HelpMessage = "SQL Server TCP port to test.")]
	[int]$SqlPort = 1433,

	[Parameter(Mandatory = $false, HelpMessage = "Hostname used inside containers to reach the host machine.")]
	[string]$HostAlias = "host.local",

	[Parameter(Mandatory = $false, HelpMessage = "SQL Server instance registry key path segment (e.g., 'MSSQL16.MSSQLSERVER').")]
	[string]$SqlInstanceKey = "MSSQL16.MSSQLSERVER",

	[Parameter(Mandatory = $false, HelpMessage = "SQL Server Windows service name.")]
	[string]$SqlServiceName = "MSSQLSERVER",

	[Parameter(Mandatory = $false, HelpMessage = "Container engine to use ('docker' or 'podman'). If omitted, prompts interactively.")]
	[ValidateSet("docker", "podman")]
	[string]$ContainerEngine
)

# Dot-source the necessary helper function files.
. "$PSScriptRoot\Setup_Helper_CoreFunctions.ps1"
. "$PSScriptRoot\Setup_Helper_NetworkTests.ps1"
. "$PSScriptRoot\Setup_Helper_ContainerEngine.ps1"

# Ensure the script working directory is set.
Set-ScriptLocation

# --- Engine Selection ---
if ($ContainerEngine) {
	$global:containerEngine = $ContainerEngine
}
else {
	$global:containerEngine = Select-ContainerEngine
}
if (-not $global:containerEngine) {
	Write-Warning "No container engine selected. Exiting script."
	exit 1
}
$global:enginePath = Get-EnginePath -EngineName $global:containerEngine

################################################################################
# Tracking
################################################################################
$testResults = [ordered]@{}

#==============================================================================
# Function: Write-TestResult
#==============================================================================
<#
.SYNOPSIS
	Records and displays a single test result.
.DESCRIPTION
	Adds the result to the tracking hashtable and writes a colour-coded line
	to the console. PASS is green, FAIL is red, SKIP/WARN is yellow.
.PARAMETER Name
	Short label for the test (used as the hashtable key).
.PARAMETER Passed
	$true if the test passed, $false if it failed, $null if skipped.
.PARAMETER Detail
	Optional extra detail shown after the status.
.OUTPUTS
	[void]
#>
function Write-TestResult {
	[CmdletBinding()]
	param(
		[Parameter(Mandatory = $true)]
		[string]$Name,

		[Parameter(Mandatory = $false)]
		[Nullable[bool]]$Passed,

		[Parameter(Mandatory = $false)]
		[string]$Detail = ""
	)

	if ($null -eq $Passed) {
		$status = "SKIP"
		$colour = "Yellow"
	}
	elseif ($Passed) {
		$status = "PASS"
		$colour = "Green"
	}
	else {
		$status = "FAIL"
		$colour = "Red"
	}

	$testResults[$Name] = $status
	$line = "  [$status] $Name"
	if ($Detail) { $line += " - $Detail" }
	Write-Host $line -ForegroundColor $colour
}

################################################################################
# 1  Windows-side checks (service, registry, netstat, firewall)
################################################################################

Write-Host ""
Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host "  SQL Server Connectivity Test" -ForegroundColor Cyan
Write-Host "  Port: $SqlPort  |  Host alias: $HostAlias  |  Container: $ContainerName" -ForegroundColor Cyan
Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "--- Windows Host Checks ---" -ForegroundColor Cyan

# 1a  Service running?
$svcStatus = $null
try {
	$svc = Get-Service -Name $SqlServiceName -ErrorAction Stop
	$svcStatus = $svc.Status -eq 'Running'
	Write-TestResult -Name "SQL Server service ($SqlServiceName)" -Passed $svcStatus -Detail "$($svc.Status)"
}
catch {
	Write-TestResult -Name "SQL Server service ($SqlServiceName)" -Passed $false -Detail "Service not found"
}

# 1b  TCP/IP protocol enabled in registry?
$regPath = "HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server\$SqlInstanceKey\MSSQLServer\SuperSocketNetLib\Tcp"
$tcpEnabled = $null
try {
	$regValue = Get-ItemProperty -Path $regPath -Name "Enabled" -ErrorAction Stop
	$tcpEnabled = $regValue.Enabled -eq 1
	$detail = if ($tcpEnabled) { "Enabled = 1" } else { "Enabled = 0  ** TCP/IP is DISABLED — enable it in SQL Server Configuration Manager and restart the service **" }
	Write-TestResult -Name "TCP/IP protocol (registry)" -Passed $tcpEnabled -Detail $detail
}
catch {
	Write-TestResult -Name "TCP/IP protocol (registry)" -Passed $null -Detail "Registry key not found at $regPath"
}

# 1c  Configured port in registry
try {
	$regIpAll = "HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server\$SqlInstanceKey\MSSQLServer\SuperSocketNetLib\Tcp\IPAll"
	$portValue = Get-ItemProperty -Path $regIpAll -Name "TcpPort" -ErrorAction Stop
	$configuredPort = $portValue.TcpPort
	$portMatch = $configuredPort -eq "$SqlPort"
	Write-TestResult -Name "Configured TCP port (IPAll)" -Passed $portMatch -Detail "TcpPort = $configuredPort"
}
catch {
	Write-TestResult -Name "Configured TCP port (IPAll)" -Passed $null -Detail "Registry key not found"
}

# 1d  Listening on port? (netstat)
$listening = $false
try {
	$listeners = Get-NetTCPConnection -LocalPort $SqlPort -State Listen -ErrorAction Stop
	if ($listeners) { $listening = $true }
}
catch {
	$listening = $false
}
$listenDetail = if ($listening) { "Listener found" } else { "No listener on port $SqlPort" }
Write-TestResult -Name "Port $SqlPort listening (netstat)" -Passed $listening -Detail $listenDetail

# 1e  TCP connection test from Windows host (reuse shared helper)
Write-Host ""
Write-Host "--- Layer 1: Windows Host -> localhost:$SqlPort ---" -ForegroundColor Cyan
$hostTcp = Test-TCPPort -ComputerName "localhost" -Port $SqlPort -ServiceName "SQL Server (Windows)" -Timeout 5
Write-TestResult -Name "TCP from Windows host" -Passed $hostTcp

################################################################################
# 2  Podman VM layer
################################################################################

Write-Host ""
Write-Host "--- Layer 2: Podman VM -> host ---" -ForegroundColor Cyan

$machineRunning = $false
try {
	$machineListJson = & $global:enginePath machine ls --format json 2>$null
	$machines = $machineListJson | ConvertFrom-Json
	$machineRunning = ($null -ne $machines) -and ($machines.Count -gt 0) -and ($machines[0].Running -eq $true)
}
catch {
	$machineRunning = $false
}

if ($machineRunning) {
	# Note: $HostAlias (e.g. host.local) only exists inside containers via --add-host.
	# In the VM, use 'localhost' which works in mirrored networking mode.
	$vmTarget = "localhost"

	# DNS resolution in VM (informational — check if host.local is resolvable at VM level)
	$vmResolveRaw = & $global:enginePath machine ssh "getent hosts $HostAlias 2>/dev/null || echo NXDOMAIN" 2>$null
	if ($vmResolveRaw -and $vmResolveRaw -notmatch "NXDOMAIN") {
		$vmIp = ($vmResolveRaw -split '\s+')[0]
		Write-TestResult -Name "DNS $HostAlias in Podman VM" -Passed $true -Detail "Resolves to $vmIp"
	}
	else {
		Write-TestResult -Name "DNS $HostAlias in Podman VM" -Passed $null -Detail "$HostAlias not in VM DNS (expected — only defined inside containers via --add-host)"
	}

	# TCP test from VM using localhost (mirrored mode shares host network)
	$vmTcpRaw = & $global:enginePath machine ssh "timeout 3 bash -c '</dev/tcp/$vmTarget/$SqlPort' 2>/dev/null && echo OK || echo FAIL" 2>$null
	$vmTcp = $vmTcpRaw -match "OK"
	Write-TestResult -Name "TCP from Podman VM" -Passed $vmTcp -Detail "$vmTarget`:$SqlPort (mirrored mode)"
}
else {
	Write-TestResult -Name "DNS $HostAlias in Podman VM" -Passed $null -Detail "Podman machine not running"
	Write-TestResult -Name "TCP from Podman VM" -Passed $null -Detail "Podman machine not running"
}

################################################################################
# 3  Container layer
################################################################################

Write-Host ""
Write-Host "--- Layer 3: Container '$ContainerName' -> $HostAlias`:$SqlPort ---" -ForegroundColor Cyan

$containerRunning = $false
if ($machineRunning) {
	$containerIdRaw = & $global:enginePath ps --filter "name=^${ContainerName}$" --format "{{.ID}}" 2>$null
	$containerRunning = -not [string]::IsNullOrWhiteSpace($containerIdRaw)
}

$containerHostGatewayFail = $false
if ($containerRunning) {
	# /etc/hosts entry
	$hostsLine = & $global:enginePath exec $ContainerName sh -c "grep '$HostAlias' /etc/hosts 2>/dev/null || echo NOT_FOUND" 2>$null
	if ($hostsLine -and $hostsLine -notmatch "NOT_FOUND") {
		$containerHostIp = ($hostsLine -split '\s+')[0]
		Write-TestResult -Name "/etc/hosts $HostAlias in container" -Passed $true -Detail "Maps to $containerHostIp"
	}
	else {
		Write-TestResult -Name "/etc/hosts $HostAlias in container" -Passed $false -Detail "$HostAlias not found in /etc/hosts (was --add-host used?)"
	}

	# TCP test via host.local (host-gateway / bridge gateway IP)
	$nodeScript = "const s=require('net').connect($SqlPort,'$HostAlias',()=>{console.log('OK');s.destroy()});s.setTimeout(5000);s.on('timeout',()=>{console.log('TIMEOUT');s.destroy()});s.on('error',(e)=>{console.log('ERR:'+e.code);s.destroy()})"
	$containerTcpRaw = & $global:enginePath exec $ContainerName node -e $nodeScript 2>$null
	$containerTcp = $containerTcpRaw -match "OK"
	Write-TestResult -Name "TCP from container ($HostAlias)" -Passed $containerTcp -Detail "$HostAlias`:$SqlPort"

	# If host.local fails, test localhost to diagnose mirrored-mode bridge routing
	if (-not $containerTcp) {
		$containerHostGatewayFail = $true
		$nodeScriptLo = "const s=require('net').connect($SqlPort,'localhost',()=>{console.log('OK');s.destroy()});s.setTimeout(5000);s.on('timeout',()=>{console.log('TIMEOUT');s.destroy()});s.on('error',(e)=>{console.log('ERR:'+e.code);s.destroy()})"
		$containerLoRaw = & $global:enginePath exec $ContainerName node -e $nodeScriptLo 2>$null
		$containerLo = $containerLoRaw -match "OK"
		Write-TestResult -Name "TCP from container (localhost)" -Passed $containerLo -Detail "localhost:$SqlPort (bridge network — localhost stays in container)"

		# Check container network mode
		$netModeRaw = & $global:enginePath inspect $ContainerName --format "{{.HostConfig.NetworkMode}}" 2>$null
		$isHostNet = ($netModeRaw -match "host")
		if (-not $isHostNet) {
			Write-TestResult -Name "Container network mode" -Passed $false -Detail "Mode: $netModeRaw — bridge network cannot route to Windows host in WSL2 mirrored mode"
		}
		else {
			Write-TestResult -Name "Container network mode" -Passed $true -Detail "Mode: host"
		}
	}
}
else {
	$skipReason = if (-not $machineRunning) { "Podman machine not running" } else { "Container '$ContainerName' not running" }
	Write-TestResult -Name "/etc/hosts $HostAlias in container" -Passed $null -Detail $skipReason
	Write-TestResult -Name "TCP from container ($HostAlias)" -Passed $null -Detail $skipReason
}

################################################################################
# Summary
################################################################################

Write-Host ""
Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host "  Summary" -ForegroundColor Cyan
Write-Host "======================================================================" -ForegroundColor Cyan

$passCount = ($testResults.Values | Where-Object { $_ -eq "PASS" }).Count
$failCount = ($testResults.Values | Where-Object { $_ -eq "FAIL" }).Count
$skipCount = ($testResults.Values | Where-Object { $_ -eq "SKIP" }).Count

foreach ($key in $testResults.Keys) {
	$val = $testResults[$key]
	$colour = switch ($val) { "PASS" { "Green" } "FAIL" { "Red" } default { "Yellow" } }
	Write-Host "  [$val] $key" -ForegroundColor $colour
}

Write-Host ""
Write-Host "  Total: $($testResults.Count)  |  PASS: $passCount  |  FAIL: $failCount  |  SKIP: $skipCount"
Write-Host ""

if ($failCount -gt 0) {
	Write-Host "  Troubleshooting tips:" -ForegroundColor Yellow
	if (-not $tcpEnabled) {
		Write-Host "    - Enable TCP/IP: SQL Server Configuration Manager -> Protocols -> TCP/IP -> Enable" -ForegroundColor Yellow
		Write-Host "      Then restart the SQL Server service." -ForegroundColor Yellow
	}
	if (-not $listening) {
		Write-Host "    - SQL Server is not listening on port $SqlPort. Restart the service after enabling TCP/IP." -ForegroundColor Yellow
	}
	if ($containerHostGatewayFail) {
		Write-Host "" -ForegroundColor Yellow
		Write-Host "    - WSL2 MIRRORED MODE + BRIDGE NETWORK ISSUE:" -ForegroundColor Yellow
		Write-Host "      In WSL2 mirrored networking, only 'localhost' traffic from the Podman VM" -ForegroundColor Yellow
		Write-Host "      is forwarded to Windows. The container bridge gateway (10.88.0.1) is inside" -ForegroundColor Yellow
		Write-Host "      the VM — host-gateway/--add-host cannot reach Windows services on bridge networks." -ForegroundColor Yellow
		Write-Host "" -ForegroundColor Yellow
		Write-Host "      Fix: Recreate the n8n container with --network=host so it shares the VM's" -ForegroundColor Yellow
		Write-Host "      network namespace (where localhost reaches Windows). Then use 'localhost'" -ForegroundColor Yellow
		Write-Host "      instead of 'host.local' as the SQL Server hostname in n8n:" -ForegroundColor Yellow
		Write-Host "        1. Update container creation to use: --network host" -ForegroundColor Yellow
		Write-Host "        2. In n8n, set SQL Server host to: localhost" -ForegroundColor Yellow
		Write-Host "        3. Port mapping (--publish) is not needed with --network host" -ForegroundColor Yellow
	}
	Write-Host ""
}

if ($failCount -eq 0 -and $skipCount -eq 0) {
	Write-Host "  All tests passed. SQL Server is reachable from all layers." -ForegroundColor Green
}

Write-Host "======================================================================" -ForegroundColor Cyan
