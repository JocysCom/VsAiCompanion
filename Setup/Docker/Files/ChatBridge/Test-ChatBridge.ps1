#requires -Version 7.0
param(
	[string]$BaseUrl,
	[string]$Origin = '',
	[switch]$SkipOriginCheck
)
# ChatBridge end-to-end test (PowerShell 7+)
# - Prompts for BaseUrl if missing (e.g., https://n8n.example.com)
# - Prompts for WS Origin if not provided; defaults to BaseUrl (e.g., https://localhost:12345)
# - Verifies Origin is reachable (unless -SkipOriginCheck)
# - Opens a WS responder for a unique sessionId and waits for one tool call
# - Confirms the server registered the session via /proxy/api/chatbridge/session/{id}
# - Invokes POST /proxy/api/chatbridge/invoke and prints the result

if (-not $BaseUrl) {
	$baseUrl = Read-Host "Enter base host URL (e.g., https://n8n.example.com)"
}
else {
	$baseUrl = $BaseUrl
}
if ([string]::IsNullOrWhiteSpace($baseUrl)) { Write-Error "Base URL is required."; exit 1 }
if ($baseUrl -notmatch '^[a-z]+://') { $baseUrl = "https://$baseUrl" }

try {
	$base = [Uri]$baseUrl
}
catch {
	Write-Error "Invalid URL: $baseUrl"
	exit 1
}

$sessionId = "test-$([Guid]::NewGuid().ToString('n'))"

$healthUrl = [Uri]::new($base, "/proxy/api/health").AbsoluteUri
$postUrl = [Uri]::new($base, "/proxy/api/chatbridge/invoke").AbsoluteUri

$wsScheme = if ($base.Scheme -eq 'https') { 'wss' } else { 'ws' }
$hostPort = if ($base.IsDefaultPort) { $base.DnsSafeHost } else { "$($base.DnsSafeHost):$($base.Port)" }
$wsUrl = "${wsScheme}://${hostPort}/proxy/ws?sessionId=$sessionId"
Write-Host ("SessionId: {0}" -f $sessionId)

# Determine Origin header for WS handshake
if ([string]::IsNullOrWhiteSpace($Origin)) {
	$originInput = Read-Host "Enter WS Origin to send (press Enter to use base host: $baseUrl)"
	if ([string]::IsNullOrWhiteSpace($originInput)) { $origin = $baseUrl } else { $origin = $originInput }
}
else {
	$origin = $Origin
}
Write-Host ("Using WS Origin header: {0}" -f $origin)

Write-Host "Health: $healthUrl"
try {
	# Use a WebSession to capture any affinity cookies (if any)
	$healthResp = Invoke-WebRequest -Uri $healthUrl -TimeoutSec 15 -SessionVariable sess -ErrorAction Stop
	$json = $null
	try { $json = $healthResp.Content | ConvertFrom-Json } catch { }
	if ($json) { Write-Host ("Health OK: " + ($json | ConvertTo-Json -Compress)) -ForegroundColor Green }
	else { Write-Host ("Health OK: " + $healthResp.StatusCode) -ForegroundColor Green }
}
catch {
	Write-Error "Health check failed: $($_.Exception.Message)"
	exit 1
}

# Origin preflight check (ensure the declared Origin is live, unless skipped)
if ($origin -and -not $SkipOriginCheck) {
	Write-Host ("Origin check: {0}" -f $origin)
	try {
		$oResp = Invoke-WebRequest -Uri $origin -Method Get -TimeoutSec 5 -SkipCertificateCheck -MaximumRedirection 0 -ErrorAction Stop
		Write-Host ("Origin OK: HTTP {0}" -f $oResp.StatusCode) -ForegroundColor Green
	}
 catch {
		Write-Error ("Origin NOT reachable: {0}" -f $_.Exception.Message)
		exit 1
	}
}
elseif (-not $origin) {
	Write-Host "Skipping Origin header and origin check (no Origin provided)." -ForegroundColor Yellow
}
else {
	Write-Host "Skipping Origin check (SkipOriginCheck set)." -ForegroundColor Yellow
}

# Build Cookie header string from captured session cookies (helps hit same backend instance as WS/HTTP)
$cookiesForBase = @()
# WS route probe — confirm /proxy/ws is served by ChatBridge app (expects HTTP 400 for non-WS)
$wsProbeUrl = [Uri]::new($base, "/proxy/ws").AbsoluteUri
Write-Host ("WS route probe: {0}" -f $wsProbeUrl)
try {
	$probe = Invoke-WebRequest -Uri $wsProbeUrl -Method Get -TimeoutSec 10 -WebSession $sess -SkipHttpErrorCheck
	Write-Host ("WS route probe status: {0}" -f $probe.StatusCode)
	if ($probe.StatusCode -ne 400) {
		Write-Error ("WS route probe expected HTTP 400 from ChatBridge, got {0}. Check main site URL Rewrite to exclude /proxy from reverse proxy." -f $probe.StatusCode)
		exit 2
	}
}
catch {
	Write-Host ("WS route probe failed: {0}" -f $_.Exception.Message) -ForegroundColor Yellow
	# Continue to WS connect; some deployments may block plain GET to /ws
}
try { $cookiesForBase = $sess.Cookies.GetCookies($base) } catch { }
$cookieHeader = ''
if ($cookiesForBase -and $cookiesForBase.Count -gt 0) {
	$cookieHeader = ($cookiesForBase | ForEach-Object { '{0}={1}' -f $_.Name, $_.Value }) -join '; '
	Write-Host ("Affinity cookies: {0}" -f $cookieHeader)
}
else {
	Write-Host "No affinity cookies detected." -ForegroundColor Yellow
}

# Start minimal WS responder job (echoes 'pong' once)
$script = {
	param($wsUrl, $origin, $cookieHeader)
	$ws = [System.Net.WebSockets.ClientWebSocket]::new()
	if ($origin) { $ws.Options.SetRequestHeader('Origin', $origin) }
	if ($cookieHeader) { $ws.Options.SetRequestHeader('Cookie', $cookieHeader) }

	[void]$ws.ConnectAsync([Uri]$wsUrl, [Threading.CancellationToken]::None).GetAwaiter().GetResult()
	'CONNECTED'

	$buf = New-Object byte[] 131072
	$r = $ws.ReceiveAsync($buf, [Threading.CancellationToken]::None).GetAwaiter().GetResult()
	$msg = [Text.Encoding]::UTF8.GetString($buf, 0, $r.Count) | ConvertFrom-Json

	$reply = @{ id = $msg.id; ok = $true; result = 'pong' } | ConvertTo-Json -Compress
	$out = [Text.Encoding]::UTF8.GetBytes($reply)
	[void]$ws.SendAsync($out, [System.Net.WebSockets.WebSocketMessageType]::Text, $true, [Threading.CancellationToken]::None).GetAwaiter().GetResult()

	[void]$ws.CloseAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure, 'bye', [Threading.CancellationToken]::None).GetAwaiter().GetResult()
}

$job = Start-Job -ScriptBlock $script -ArgumentList $wsUrl, $origin, $cookieHeader

# Wait for WS to report it connected
$connected = $false
for ($i = 0; $i -lt 50 -and -not $connected; $i++) {
	$out = Receive-Job -Job $job -Keep -ErrorAction SilentlyContinue
	if ($out -match 'CONNECTED') { $connected = $true; break }
	Start-Sleep -Milliseconds 100
}

if ($connected) { Write-Host "WebSocket connected." -ForegroundColor Green }
else { Write-Host "Proceeding without WS connect confirmation..." -ForegroundColor Yellow }

# Debug: confirm server sees this session (requires deployed debug endpoints)
try {
	$pidUrl = [Uri]::new($base, "/proxy/api/chatbridge/pid").AbsoluteUri
	$pidObj = Invoke-RestMethod -Uri $pidUrl -WebSession $sess -TimeoutSec 10
	if ($pidObj -and $pidObj.pid) { Write-Host ("HTTP PID: {0}" -f $pidObj.pid) }
}
catch { Write-Host ("HTTP PID check failed: {0}" -f $_.Exception.Message) -ForegroundColor Yellow }

try {
	$sessionsUrl = [Uri]::new($base, "/proxy/api/chatbridge/sessions").AbsoluteUri
	$sessObj = Invoke-RestMethod -Uri $sessionsUrl -WebSession $sess -TimeoutSec 10
	if ($sessObj) { Write-Host ("HTTP sees {0} session(s)" -f $sessObj.count) }
}
catch { Write-Host ("HTTP sessions check failed: {0}" -f $_.Exception.Message) -ForegroundColor Yellow }

# Verify server registers this specific session before invoking HTTP
$regOk = $false
for ($i = 0; $i -lt 20 -and -not $regOk; $i++) {
	try {
		$chkUrl = [Uri]::new($base, "/proxy/api/chatbridge/session/$sessionId").AbsoluteUri
		$chkObj = Invoke-RestMethod -Uri $chkUrl -WebSession $sess -TimeoutSec 5
		if ($null -ne $chkObj.exists -and [bool]$chkObj.exists) {
			Write-Host ("HTTP sees session {0}: True" -f $sessionId) -ForegroundColor Green
			$regOk = $true
			break
		}
	}
 catch { }
	Start-Sleep -Milliseconds 150
}
if (-not $regOk) {
	Write-Error "WebSocket connected but server did not register session. Ensure site-level rewrite excludes /proxy and WS reaches ChatBridge. Aborting."
	try { Stop-Job -Job $job -ErrorAction SilentlyContinue | Out-Null } catch {}
	try { Remove-Job -Job $job -Force -ErrorAction SilentlyContinue | Out-Null } catch {}
	exit 2
}

# Invoke the tool call (ping) via HTTP with retries, reusing the same WebSession (cookies)
$body = @{ sessionId = $sessionId; tool = 'ping'; args = @() } | ConvertTo-Json -Compress
$maxAttempts = 10
$delayMs = 500

for ($attempt = 1; $attempt -le $maxAttempts; $attempt++) {
	Write-Host ("Attempt {0}/{1}: POST {2}" -f $attempt, $maxAttempts, $postUrl)
	try {
		$resp = Invoke-WebRequest -Method POST -Uri $postUrl -ContentType 'application/json' -Body $body -WebSession $sess -SkipHttpErrorCheck -TimeoutSec 30
		$status = $resp.StatusCode
		$content = $resp.Content
		Write-Host ("Status : {0}" -f $status)
		Write-Host ("Content: {0}" -f $content)

		$ok = $false
		try {
			$jo = $content | ConvertFrom-Json
			if ($null -ne $jo.ok -and [bool]$jo.ok) { $ok = $true }
		}
		catch { }

		if ($ok) { break }

		if ($content -match 'No active client' -and $attempt -lt $maxAttempts) {
			Start-Sleep -Milliseconds $delayMs
			continue
		}
		break
	}
 catch {
		Write-Host ("Error: {0}" -f $_.Exception.Message) -ForegroundColor Yellow
		if ($attempt -lt $maxAttempts) {
			Start-Sleep -Milliseconds $delayMs
			continue
		}
		break
	}
}

# Cleanup
[void](Wait-Job -Job $job -Timeout 5)
try { Receive-Job -Job $job -ErrorAction SilentlyContinue | Out-Null } catch {}
try { Remove-Job -Job $job -Force -ErrorAction SilentlyContinue | Out-Null } catch {}