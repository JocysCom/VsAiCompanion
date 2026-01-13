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

	Write-Host -NoNewline "$serviceName HTTP test at $Uri..."
	$deadline = [DateTime]::UtcNow.AddSeconds($Timeout)
	$didPrintDot = $false

	while ([DateTime]::UtcNow -lt $deadline) {
		try {
			# Some endpoints (or older PowerShell / TLS setups) behave better with HttpWebRequest.
			$request = [System.Net.HttpWebRequest]::Create($Uri)
			$request.Method = "GET"
			$request.Timeout = 1000
			$request.ReadWriteTimeout = 1000
			$request.AllowAutoRedirect = $true

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

		#==============================================================================
		# Function: Ensure-NetworkExists
		#==============================================================================
		function Ensure-NetworkExists {
			[CmdletBinding(SupportsShouldProcess = $true)]
			[OutputType([bool])]
			param(
				[Parameter(Mandatory = $true)]
				[string]$NetworkName,

				[Parameter(Mandatory = $true)]
				[string]$EnginePath
			)

			# Check if network exists
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
	}
	catch {
		Write-Error "$serviceName WebSocket test failed at $Uri. Error details: $_"
		return $false
	}
}
