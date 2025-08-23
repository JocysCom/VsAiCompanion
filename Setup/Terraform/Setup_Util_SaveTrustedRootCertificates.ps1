# The hostname from which to retrieve the corporate certificate
$HostNames = @('login.microsoftonline.com')
$Port = 443

# Save the corporate root certificates to this file and export each certificate to separate .crt files (trusted_root_certificates_{index}.crt)
$OutputPemFile = '.\Files\trusted_root_certificates.pem'

# Ensure the Files directory exists
$OutputDir = Split-Path $OutputPemFile -Parent
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
    Write-Host "Created directory: $OutputDir"
}

# Use SslStream callback to ignore SSL errors per connection
$PemCerts = @()
$Index = 0
$SeenThumbs = @()

foreach ($HostName in $HostNames) {
	$HostAdded = $false
	# Create a TCP client and connect to the specified host and port
	$TcpClient = New-Object System.Net.Sockets.TcpClient
	$TcpClient.Connect($HostName, $Port)
	# Create an SSL stream that will close the client's stream
	$SslStream = New-Object System.Net.Security.SslStream(
		$TcpClient.GetStream(),
		$true,
		[System.Net.Security.RemoteCertificateValidationCallback] { param($sender, $certificate, $chain, $sslPolicyErrors) return $true },
		$null
	)
	Write-Host "Performing SSL handshake for $HostName"
	$SslStream.AuthenticateAsClient($HostName)
	# Build the certificate chain
	$Chain = New-Object System.Security.Cryptography.X509Certificates.X509Chain
	$Chain.Build($SslStream.RemoteCertificate) | Out-Null
	foreach ($Element in $Chain.ChainElements) {
		# Avoid saving the end-entity certificate
		if ($Element.Certificate -ne $SslStream.RemoteCertificate) {
			$thumb = $Element.Certificate.Thumbprint
			if ($SeenThumbs -notcontains $thumb) {
				$SeenThumbs += $thumb
				if (-not $HostAdded) {
					Write-Host $HostName
					$HostAdded = $true
				}
				# Export certificate
				$CertData = $Element.Certificate.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Cert)
				$PemData = [System.Convert]::ToBase64String($CertData, 'InsertLineBreaks')
				$PemCert = "-----BEGIN CERTIFICATE-----`n$PemData`n-----END CERTIFICATE-----"
				$PemCerts += $PemCert
				$Index++
				$IndividualFile = Join-Path -Path (Split-Path $OutputPemFile) -ChildPath ("trusted_root_certificates_$Index.crt")
				Set-Content -Path $IndividualFile -Value $PemCert -Encoding ASCII
				# Output concise certificate info
				$cn = $Element.Certificate.GetNameInfo([System.Security.Cryptography.X509Certificates.X509NameType]::SimpleName, $false)
				$issuer = $Element.Certificate.GetNameInfo([System.Security.Cryptography.X509Certificates.X509NameType]::SimpleName, $true)
				$type = if ($Element.Certificate.Subject -eq $Element.Certificate.Issuer) { 'Root CA' } else { 'Intermediate CA' }
				Write-Host "  - [$type] CN=$cn, Issuer=$issuer"
			}
		}
	}
	# Clean up streams
	$SslStream.Close()
	$TcpClient.Close()
}

# Write consolidated PEM file
Set-Content -Path $OutputPemFile -Value ($PemCerts -join "`n`n") -Encoding ASCII
Write-Host "Trusted root certificates saved to $OutputPemFile"
