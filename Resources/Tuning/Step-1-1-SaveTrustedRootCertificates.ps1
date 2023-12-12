# The hostname from which to retrieve the corporate certificate
$HostNames = @('huggingface.co', 'pypi.org')
$Port = 443

# Save the corporate root certificates to this file
$OutputPemFile = '.\Data\trusted_root_certificates.pem'

# Ignore SSL errors for fetching the certificate
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }

try {
    # Export the trusted root and intermediate certificates from the chain
    $PemCerts = @()
    foreach ($HostName in $HostNames) {
        # Create a TCP client and connect to the specified host and port
        $TcpClient = New-Object System.Net.Sockets.TcpClient
        $TcpClient.Connect($HostName, $Port)
        # Create an SSL stream that will close the client's stream
        $SslStream = New-Object System.Net.Security.SslStream($TcpClient.GetStream(), $true)
        # Perform the SSL handshake
        $SslStream.AuthenticateAsClient($HostName)
        # Access the certificate chain that was built during authentication
        $Chain = New-Object System.Security.Cryptography.X509Certificates.X509Chain
        $Chain.Build($SslStream.RemoteCertificate)
        $ChainElements = $Chain.ChainElements
        foreach ($Element in $ChainElements) {
            # Avoid saving the end-entity certificate (we only want root and intermediates)
            if ($Element.Certificate -ne $SslStream.RemoteCertificate) {
                $CertData = $Element.Certificate.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Cert)
                $PemData = [System.Convert]::ToBase64String($CertData, 'InsertLineBreaks')
                $PemCerts += "-----BEGIN CERTIFICATE-----`n$PemData`n-----END CERTIFICATE-----"
            }
        }
        # Close the SSL stream and TCP client
        $SslStream.Close()
        $TcpClient.Close() 
    }
    Set-Content -Path $OutputPemFile -Value ($PemCerts -join "`n`n") -Encoding ASCII
    Write-Host "Trusted root certificates saved to $OutputPemFile"
}
catch {
    Write-Error "An error occurred: $_"
}
finally {
    # Reset ServerCertificateValidationCallback to default behavior
    [System.Net.ServicePointManager]::ServerCertificateValidationCallback = $null
}
