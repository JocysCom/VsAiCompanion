param (
    [string]$sourceFile,
    [string]$destinationFile
)

function Get-FileChecksum {
    param (
        [string] $filePath
    )
    $checksum = $null
    if (Test-Path -Path $filePath -PathType Leaf) {
        $hashAlgorithm = [System.Security.Cryptography.SHA256]::Create()
        try {
            $stream = [System.IO.File]::OpenRead($filePath)
            $hashBytes = $hashAlgorithm.ComputeHash($stream)
            $stream.Close()
            $checksum = -join ($hashBytes | ForEach-Object { $_.ToString("x2") })
        }
        finally {
            $hashAlgorithm.Dispose()
            if ($stream) {
                $stream.Dispose()
            }
        }
    } else {
        Write-Host "File does not exist: $filePath"
    }
    return $checksum
}

# Check if the destination file exists
if (Test-Path $destinationFile) {
    # Compare the contents of the source and destination files
    $sourceHash = Get-FileChecksum -filePath $sourceFile
    $destinationHash = Get-FileChecksum -filePath $destinationFile

    if ($sourceHash -ne $destinationHash) {
        # The files are different, so copy the source file to the destination
        Copy-Item $sourceFile $destinationFile -Force
        Write-Output "Updating: $destinationFile"
    } else {
        Write-Output "Unchanged: $destinationFile"
    }
} else {
    # Destination file does not exist, so copy the source file to the destination
    Copy-Item $sourceFile $destinationFile -Force
    Write-Output "Creating: $destinationFile."
}