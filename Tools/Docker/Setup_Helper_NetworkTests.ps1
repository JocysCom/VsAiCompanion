################################################################################
# File         : Setup_0_Network.ps1
# Description  : Contains network testing helper functions for setup scripts:
#                - Test-TCPPort: Test connectivity to a specific TCP port.
#                - Test-HTTPPort: Test connectivity to an HTTP endpoint.
#                - Test-WebSocketPort: Test connectivity to a WebSocket endpoint.
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
	param(
		[Parameter(Mandatory = $true)]
		[string] $ComputerName,
		[Parameter(Mandatory = $true)]
		[int] $Port,
		[Parameter(Mandatory = $true)]
		[string] $serviceName,
		[int] $TimeoutMilliseconds = 5000
	)

	try {
		# Try to resolve both IPv4 and IPv6 addresses but prioritize IPv4
		$ipAddresses = [System.Net.Dns]::GetHostAddresses($ComputerName)
		$ip = $ipAddresses | Where-Object { $_.AddressFamily -eq 'InterNetwork' } | Select-Object -First 1

		# Fallback to IPv6 if no IPv4 is available
		if (-not $ip) {
			$ip = $ipAddresses | Select-Object -First 1
			if (-not $ip) {
				throw "No IP address could be found for $ComputerName."
			}
			Write-Host "Using IPv6 address for connection test: $ip"
		}

		$client = New-Object System.Net.Sockets.TcpClient
		$async = $client.BeginConnect($ip.ToString(), $Port, $null, $null)
		$connected = $async.AsyncWaitHandle.WaitOne($TimeoutMilliseconds, $false)

		if ($connected -and $client.Connected) {
			Write-Host "$serviceName TCP test succeeded on port $Port at $ComputerName (IP: $ip)."
			$client.Close()
			return $true
		}
		else {
			Write-Error "$serviceName TCP test failed on port $Port at $ComputerName (IP: $ip)."
			$client.Close()
			return $false
		}
	}
	catch {
		Write-Error "$serviceName TCP test encountered an error: $_"
		return $false
	}
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
	param(
		[Parameter(Mandatory = $true)]
		[string] $Uri,
		[Parameter(Mandatory = $true)]
		[string] $serviceName
	)
	try {
		$response = Invoke-WebRequest -Uri $Uri -UseBasicParsing -TimeoutSec 15
		if ($response.StatusCode -eq 200) {
			# Use Write-Host for status messages
			Write-Host "$serviceName HTTP test succeeded at $Uri."
			return $true
		}
		else {
			Write-Error "$serviceName HTTP test failed at $Uri. Status code: $($response.StatusCode)."
			return $false
		}
	}
	catch {
		Write-Error "$serviceName HTTP test failed at $Uri. Error details: $_"
		return $false
	}
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
			# Use Write-Host for status messages
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
