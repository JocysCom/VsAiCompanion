param (
    [string]$sourceFile,
    [string]$destinationFile
)

# Check if the destination file exists
if (Test-Path $destinationFile) {
    # Compare the contents of the source and destination files
    $sourceHash = Get-FileHash $sourceFile
    $destinationHash = Get-FileHash $destinationFile

    if ($sourceHash.Hash -ne $destinationHash.Hash) {
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