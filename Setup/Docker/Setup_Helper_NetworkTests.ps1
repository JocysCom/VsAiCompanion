################################################################################
# File         : Setup_Helper_NetworkTests.ps1
# Description  : Contains network testing helper functions for setup scripts:
#                - Test-TCPPort: Test connectivity to a specific TCP port.
#                - Test-HTTPPort: Test connectivity to an HTTP endpoint.
#                - Test-WebSocketPort: Test connectivity to a WebSocket endpoint.
#                - Test-NetworkIPConsistency: Compare external IPs across layers.
#                - Assert-NetworkExists: Create a container network if missing.
################################################################################

#==============================================================================
# Function: Test-TCPPort
#==============================================================================
<#
.SYNOPSIS
	Tests TCP connectivity to a specified port on a computer.
.DESCRIPTION
	Attempts to establish a TCP connection to the given port on the target computer name.
	Resolves the computer name to an IP address (preferring IPv4) and attempts connection
	with a specified timeout.
.PARAMETER ComputerName
	The hostname or IP address of the target computer. Mandatory.
.PARAMETER Port
	The TCP port number to test. Mandatory.
.PARAMETER ServiceName
	A friendly name for the service being tested, used in output messages. Mandatory.
.PARAMETER TimeoutMilliseconds
	The maximum time in milliseconds to wait for the connection attempt. Defaults to 5000.
.OUTPUTS
	[bool] Returns $true if the connection is successful within the timeout, $false otherwise.
.EXAMPLE
	Test-TCPPort -ComputerName "localhost" -Port 80 -ServiceName "Web Server"
.EXAMPLE
	Test-TCPPort -ComputerName "db.example.com" -Port 5432 -ServiceName "Database" -TimeoutMilliseconds 10000
.NOTES
	Uses System.Net.Sockets.TcpClient for the connection attempt.
#>
function Test-TCPPort {
	[CmdletBinding()]
	[OutputType([bool])]
	param(
		[Parameter(Mandatory = $true)]
		[string] $ComputerName,

		[Parameter(Mandatory = $true)]
		[int] $Port,

		[Parameter(Mandatory = $true)]
		[string] $serviceName,

		[Parameter(Mandatory = $false)]
		[int] $Timeout = 60
	)

	if ($Timeout -lt 1) {
		$Timeout = 1
	}

	$deadline = [DateTime]::UtcNow.AddSeconds($Timeout)
	$didPrintDot = $false

	$ip = $null
	while ([DateTime]::UtcNow -lt $deadline) {
		try {
			if ($null -eq $ip) {
				# Try to resolve both IPv4 and IPv6 addresses but prioritize IPv4
				$ipAddresses = [System.Net.Dns]::GetHostAddresses($ComputerName)
				$ip = $ipAddresses | Where-Object { $_.AddressFamily -eq 'InterNetwork' } | Select-Object -First 1

				# Fallback to IPv6 if no IPv4 is available
				if (-not $ip) {
					$ip = $ipAddresses | Select-Object -First 1
					if (-not $ip) {
						throw "No IP address could be found for $ComputerName."
					}
				}

				Write-Host -NoNewline "$serviceName TCP test on port $Port at $ComputerName (IP: $ip)..."
			}

			$client = New-Object System.Net.Sockets.TcpClient
			try {
				# Each attempt gets ~1s to connect; loop controls overall timeout.
				$async = $client.BeginConnect($ip.ToString(), $Port, $null, $null)
				$connected = $async.AsyncWaitHandle.WaitOne(1000, $false)
				if ($connected -and $client.Connected) {
					$client.Close()
					if ($didPrintDot) { Write-Host "" }
					Write-Host "$serviceName TCP test succeeded on port $Port at $ComputerName (IP: $ip)."
					return $true
				}
			}
			finally {
				$client.Close()
			}
		}
		catch {
			Write-Verbose "TCP test attempt failed: $($_.Exception.Message)"
		}

		Write-Host -NoNewline "."
		$didPrintDot = $true
		Start-Sleep -Seconds 1
	}

	if ($didPrintDot) { Write-Host "" }
	Write-Error "$serviceName TCP test failed on port $Port at $ComputerName after $Timeout seconds."
	return $false
}

#==============================================================================
# Function: Test-HTTPPort
#==============================================================================
<#
.SYNOPSIS
	Tests HTTP connectivity to a specified URI.
.DESCRIPTION
	Uses Invoke-WebRequest to send a request to the given URI. Checks if the response
	status code is 200 (OK).
.PARAMETER Uri
	The full HTTP or HTTPS URI to test (e.g., 'http://localhost:8080/status'). Mandatory.
.PARAMETER ServiceName
	A friendly name for the service being tested, used in output messages. Mandatory.
.OUTPUTS
	[bool] Returns $true if the request is successful and the status code is 200, $false otherwise.
.EXAMPLE
	Test-HTTPPort -Uri "http://localhost:5000/api/health" -ServiceName "API Health Check"
.NOTES
	Uses Invoke-WebRequest with -UseBasicParsing and a 15-second timeout.
#>
function Test-HTTPPort {
	[CmdletBinding()]
	[OutputType([bool])]
	param(
		[Parameter(Mandatory = $true)]
		[string] $Uri,

		[Parameter(Mandatory = $true)]
		[string] $serviceName,

		[Parameter(Mandatory = $false)]
		[int] $Timeout = 60
	)

	if ($Timeout -lt 1) {
		$Timeout = 1
	}

	# Resolve hostname to IPv4 to avoid IPv6 timeouts when Podman only binds to 0.0.0.0.
	$uriObj = [Uri]$Uri
	$hostName = $uriObj.Host
	try {
		$ipAddresses = [System.Net.Dns]::GetHostAddresses($hostName)
		$ipv4 = $ipAddresses | Where-Object { $_.AddressFamily -eq 'InterNetwork' } | Select-Object -First 1
		if ($ipv4) {
			$builder = [UriBuilder]$uriObj
			$builder.Host = $ipv4.ToString()
			$resolvedUri = $builder.Uri.ToString()
		}
		else {
			$resolvedUri = $Uri
		}
	}
	catch {
		$resolvedUri = $Uri
	}

	Write-Host -NoNewline "$serviceName HTTP test at $Uri (resolved: $resolvedUri)..."
	$deadline = [DateTime]::UtcNow.AddSeconds($Timeout)
	$didPrintDot = $false

	while ([DateTime]::UtcNow -lt $deadline) {
		try {
			$request = [System.Net.HttpWebRequest]::Create($resolvedUri)
			$request.Method = "GET"
			$request.Timeout = 1000
			$request.ReadWriteTimeout = 1000
			$request.AllowAutoRedirect = $true
			$request.Proxy = [System.Net.WebProxy]::new()

			$response = $null
			try {
				$response = [System.Net.HttpWebResponse]$request.GetResponse()
				$statusCode = [int]$response.StatusCode
			}
			finally {
				if ($null -ne $response) {
					$response.Close()
				}
			}

			if ($statusCode -ge 200 -and $statusCode -lt 400) {
				if ($didPrintDot) { Write-Host "" }
				Write-Host "$serviceName HTTP test succeeded at $Uri. Status code: $statusCode."
				return $true
			}
		}
		catch {
			# Treat HTTP protocol errors (e.g. 401/403/404) as "service is up".
			$webEx = $_.Exception
			if ($webEx -is [System.Net.WebException] -and $null -ne $webEx.Response) {
				try {
					$statusCode = [int]([System.Net.HttpWebResponse]$webEx.Response).StatusCode
					if ($statusCode -ge 100 -and $statusCode -lt 600) {
						if ($didPrintDot) { Write-Host "" }
						Write-Host "$serviceName HTTP test succeeded at $Uri. Status code: $statusCode."
						return $true
					}
				}
				finally {
					try { $webEx.Response.Close() } catch { Write-Verbose "HTTP test response close failed: $($_.Exception.Message)" }
				}
			}
			Write-Verbose "HTTP test attempt failed: $($_.Exception.Message)"
		}

		Write-Host -NoNewline "."
		$didPrintDot = $true
		Start-Sleep -Seconds 1
	}

	if ($didPrintDot) { Write-Host "" }
	Write-Error "$serviceName HTTP test failed at $Uri after $Timeout seconds."
	return $false
}


#==============================================================================
# Function: Test-WebSocketPort
#==============================================================================
<#
.SYNOPSIS
	Tests WebSocket connectivity to a specified URI.
.DESCRIPTION
	Attempts to establish a WebSocket connection using System.Net.WebSockets.ClientWebSocket.
	If the connection is successful within a 5-second timeout, it returns $true.
	If the WebSocket client is unavailable (older PowerShell versions), it falls back to
	calling Test-HTTPPort on the equivalent http/https URI.
.PARAMETER Uri
	The full WebSocket URI to test (e.g., 'ws://localhost:8081/socket'). Mandatory.
.PARAMETER ServiceName
	A friendly name for the service being tested, used in output messages. Mandatory.
.OUTPUTS
	[bool] Returns $true if the WebSocket connection (or HTTP fallback) is successful, $false otherwise.
.EXAMPLE
	Test-WebSocketPort -Uri "ws://localhost:9000/events" -ServiceName "Event Stream"
.NOTES
	Requires .NET Core or PowerShell 7+ for native WebSocket support.
	Uses a 5-second timeout for the connection attempt.
#>
function Test-WebSocketPort {
	param(
		[Parameter(Mandatory = $true)]
		[string] $Uri,
		[Parameter(Mandatory = $true)]
		[string] $serviceName
	)
	try {
		# Check if .NET Core WebSocket client is available
		if (-not ([System.Management.Automation.PSTypeName]'System.Net.WebSockets.ClientWebSocket').Type) {
			Write-Warning "WebSocket client not available in this PowerShell version. Falling back to HTTP check."
			return Test-HTTPPort -Uri $Uri.Replace("ws:", "http:").Replace("wss:", "https:") -serviceName $serviceName
		}

		$client = New-Object System.Net.WebSockets.ClientWebSocket
		$ct = New-Object System.Threading.CancellationTokenSource 5000
		$task = $client.ConnectAsync($Uri, $ct.Token)

		# Wait for 5 seconds max
		if ([System.Threading.Tasks.Task]::WaitAll(@($task), 5000)) {
			Write-Host "$serviceName WebSocket test succeeded at $Uri."
			$client.Dispose()
			return $true
		}
		else {
			Write-Error "$serviceName WebSocket test timed out at $Uri."
			$client.Dispose()
			return $false
		}
	}
	catch {
		Write-Error "$serviceName WebSocket test failed at $Uri. Error details: $_"
		return $false
	}
}

#==============================================================================
# Function: Test-NetworkIPConsistency
#==============================================================================
<#
.SYNOPSIS
	Compares the public IP address seen from Windows, the Podman VM, and optionally a running container.
.DESCRIPTION
	Queries https://api.ipify.org from three vantage points to verify that all layers
	of the WSL2/Podman stack present the same external IP address. This is critical
	in corporate environments where firewalls whitelist traffic by source IP.

	When WSL2 uses the default NAT networking mode, the Podman VM and containers
	may show a different external IP than the Windows host. Switching to mirrored
	networking mode (networkingMode=mirrored in .wslconfig) ensures all layers
	share the same IP.

	The function tests up to three layers:
	  1. Windows host (always tested)
	  2. Podman VM (tested if Podman CLI is available and machine is running)
	  3. Container (tested if a container name is provided and the container is running)
.PARAMETER ContainerName
	Optional name of a running container to test from (e.g., 'n8n').
.PARAMETER EnginePath
	Optional path to the container engine executable. If not provided, uses 'podman'.
.OUTPUTS
	[bool] Returns $true if all tested layers show the same external IP, $false otherwise.
.EXAMPLE
	Test-NetworkIPConsistency
.EXAMPLE
	Test-NetworkIPConsistency -ContainerName "n8n" -EnginePath "C:\Program Files\Podman\podman.exe"
.NOTES
	Requires internet access to reach api.ipify.org.
	Uses Invoke-RestMethod for the Windows test and curl inside the VM/container.
#>
function Test-NetworkIPConsistency {
	[CmdletBinding()]
	[OutputType([bool])]
	param(
		[Parameter(Mandatory = $false)]
		[string]$ContainerName,

		[Parameter(Mandatory = $false)]
		[string]$EnginePath
	)

	if ([string]::IsNullOrWhiteSpace($EnginePath)) {
		$podmanCmd = Get-Command podman -ErrorAction SilentlyContinue
		if ($podmanCmd) {
			$EnginePath = $podmanCmd.Source
		}
	}

	$ipifyUrl = "https://api.ipify.org?format=text"
	$allIPs = @()
	$allMatch = $true
	$machineRunning = $false

	Write-Host ""
	Write-Host "==================== External IP Consistency Test ===================="
	Write-Host "Querying $ipifyUrl from each network layer..."
	Write-Host ""

	# Layer 1: Windows host
	$windowsIP = $null
	try {
		$windowsIP = (Invoke-RestMethod -Uri "$ipifyUrl" -TimeoutSec 10).Trim()
		Write-Host "  Windows host   : $windowsIP" -ForegroundColor Green
		$allIPs += $windowsIP
	}
	catch {
		Write-Host "  Windows host   : FAILED ($($_.Exception.Message))" -ForegroundColor Red
		$allMatch = $false
	}

	# Layer 2: Podman VM
	$podmanIP = $null
	if ($EnginePath) {
		try {
			$machineListJson = & $EnginePath machine ls --format json 2>$null
			$machines = $machineListJson | ConvertFrom-Json
			$machineRunning = ($null -ne $machines) -and ($machines.Count -gt 0) -and ($machines[0].Running -eq $true)
		}
		catch {
			$machineRunning = $false
		}

		if ($machineRunning) {
			try {
				$podmanIPRaw = & $EnginePath machine ssh "curl -s $ipifyUrl" 2>$null
				if (-not [string]::IsNullOrWhiteSpace($podmanIPRaw)) {
					$podmanIP = $podmanIPRaw.Trim()
					$color = if ($windowsIP -and $podmanIP -eq $windowsIP) { "Green" } else { "Red" }
					Write-Host "  Podman VM      : $podmanIP" -ForegroundColor $color
					$allIPs += $podmanIP
				}
				else {
					Write-Host "  Podman VM      : FAILED (empty response)" -ForegroundColor Red
					$allMatch = $false
				}
			}
			catch {
				Write-Host "  Podman VM      : FAILED ($($_.Exception.Message))" -ForegroundColor Red
				$allMatch = $false
			}
		}
		else {
			Write-Host "  Podman VM      : SKIPPED (machine not running)" -ForegroundColor Yellow
		}
	}
	else {
		Write-Host "  Podman VM      : SKIPPED (Podman CLI not found)" -ForegroundColor Yellow
	}

	# Layer 3: Container (optional — only if Podman VM is running)
	$containerIP = $null
	if (-not [string]::IsNullOrWhiteSpace($ContainerName) -and $EnginePath -and $machineRunning) {
		try {
			$containerRunning = & $EnginePath ps --filter "name=^${ContainerName}$" --format "{{.ID}}" 2>$null
			if (-not [string]::IsNullOrWhiteSpace($containerRunning)) {
				$nodeScript = "const h=require('https');h.get('$ipifyUrl',r=>{let d='';r.on('data',c=>d+=c);r.on('end',()=>console.log(d))}).on('error',e=>process.exit(1))"
				$containerIPRaw = & $EnginePath exec $ContainerName node -e $nodeScript 2>$null
				if (-not [string]::IsNullOrWhiteSpace($containerIPRaw)) {
					$containerIP = $containerIPRaw.Trim()
					$color = if ($windowsIP -and $containerIP -eq $windowsIP) { "Green" } else { "Red" }
					Write-Host "  Container '$ContainerName': $containerIP" -ForegroundColor $color
					$allIPs += $containerIP
				}
				else {
					Write-Host "  Container '$ContainerName': FAILED (empty response from container)" -ForegroundColor Yellow
				}
			}
			else {
				Write-Host "  Container '$ContainerName': SKIPPED (container not running)" -ForegroundColor Yellow
			}
		}
		catch {
			Write-Host "  Container '$ContainerName': FAILED ($($_.Exception.Message))" -ForegroundColor Red
		}
	}
	elseif (-not [string]::IsNullOrWhiteSpace($ContainerName)) {
		Write-Host "  Container '$ContainerName': SKIPPED (Podman VM not running)" -ForegroundColor Yellow
	}

	# Compare results
	Write-Host ""
	$uniqueIPs = $allIPs | Sort-Object -Unique
	if ($uniqueIPs.Count -eq 1 -and $allIPs.Count -ge 2) {
		Write-Host "RESULT: All $($allIPs.Count) tested layers share the same external IP address." -ForegroundColor Green
		$allMatch = $true
	}
	elseif ($uniqueIPs.Count -eq 1 -and $allIPs.Count -eq 1) {
		Write-Host "RESULT: Only 1 layer tested (IP: $($allIPs[0])). Run with Podman machine running for full comparison." -ForegroundColor Yellow
		$allMatch = $true
	}
	elseif ($allIPs.Count -gt 1) {
		Write-Host "RESULT: MISMATCH detected! Different layers have different external IPs." -ForegroundColor Red
		Write-Host "This indicates WSL2 NAT networking is in use." -ForegroundColor Red
		Write-Host "Fix: Configure mirrored networking in Setup_Core_1_WSL2.ps1 (option 3)" -ForegroundColor Yellow
		Write-Host "  or run: Set-WSLMirroredNetworking" -ForegroundColor Yellow
		$allMatch = $false
	}
	else {
		Write-Host "RESULT: Could not compare (only one layer tested successfully)." -ForegroundColor Yellow
		$allMatch = $false
	}

	Write-Host "======================================================================"
	return $allMatch
}

#==============================================================================
# Function: Assert-NetworkExists
#==============================================================================
<#
.SYNOPSIS
	Creates a container network if it does not already exist.
.DESCRIPTION
	Checks if the specified container network exists using the engine's 'network ls'
	command. If not found, creates it using 'network create'. Supports -WhatIf.
.PARAMETER NetworkName
	The name of the network to ensure exists. Mandatory.
.PARAMETER EnginePath
	The path to the container engine executable (docker or podman). Mandatory.
.OUTPUTS
	[bool] Returns $true if the network exists or was created successfully, $false otherwise.
.EXAMPLE
	Assert-NetworkExists -NetworkName "firecrawl-net" -EnginePath "C:\Program Files\Podman\podman.exe"
#>
function Assert-NetworkExists {
	[CmdletBinding(SupportsShouldProcess = $true)]
	[OutputType([bool])]
	param(
		[Parameter(Mandatory = $true)]
		[string]$NetworkName,

		[Parameter(Mandatory = $true)]
		[string]$EnginePath
	)

	$networkExists = & $EnginePath network ls --format "{{.Name}}" | Where-Object { $_ -eq $NetworkName }
	if ($networkExists) {
		Write-Host "Network '$NetworkName' already exists."
		return $true
	}
	if ($PSCmdlet.ShouldProcess($NetworkName, "Create Network")) {
		Write-Host "Creating network '$NetworkName'..."
		& $EnginePath network create $NetworkName
		if ($LASTEXITCODE -eq 0) {
			Write-Host "Network '$NetworkName' created successfully."
			return $true
		}
		else {
			Write-Warning "Failed to create network '$NetworkName'."
			return $false
		}
	}

	return $false
}
