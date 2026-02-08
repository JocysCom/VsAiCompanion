# Sets environment variables for trusted root certificates
$certFile = Join-Path $PSScriptRoot 'Files\trusted_root_certificates.pem'
if (Test-Path $certFile) {
    $env:REQUESTS_CA_BUNDLE = $certFile
    $env:CURL_CA_BUNDLE = $certFile

    Write-Host "Setting certificate bundle environment variables..." -ForegroundColor Green
    Write-Host "REQUESTS_CA_BUNDLE: $env:REQUESTS_CA_BUNDLE" -ForegroundColor Gray
    Write-Host "CURL_CA_BUNDLE: $env:CURL_CA_BUNDLE" -ForegroundColor Gray
    Write-Host ""
} else {
    Write-Warning "Certificate file not found: $certFile"
}
