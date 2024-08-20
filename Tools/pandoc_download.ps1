Add-Type -AssemblyName System.IO.Compression.FileSystem

# Define the paths
$zipSource = "https://github.com/jgm/pandoc/releases/download/3.3/pandoc-3.3-windows-x86_64.zip"
$zipFilePath = ".\pandoc-3.3-windows-x86_64.zip"
#$destinationPath = ".\"
$targetFilePath = "$($env:APPDATA)\Jocys.com\VS AI Companion\Tools\pandoc.exe"

# Fast downloads (Progress bar in PowerShell).
function DownloadFile {
	param($sourcePath, $targetPath)
	Start-BitsTransfer -Source $sourcePath -Destination $targetPath
}

function ExtractFile {
    param (
        [string]$ZipFilePath,
        [string]$EntryFullName,
        [string]$TargetFilePath
    )
    # Open the ZIP archive
    $zip = [System.IO.Compression.ZipFile]::OpenRead($ZipFilePath)
    try {

		# Ensure the directory exists
		$directoryPath = [System.IO.Path]::GetDirectoryName($TargetFilePath)
		if (-not (Test-Path -Path $directoryPath)) {
			New-Item -Path $directoryPath -ItemType Directory | Out-Null
			Write-Host "Created directory: $directoryPath"
		}


        # Iterate through each entry in the ZIP file
        foreach ($entry in $zip.Entries) {
            # Check if the entry matches the desired file
            if ($entry.FullName -eq $EntryFullName) {
                # Open the entry as a stream
                $inputStream = $entry.Open()
                # Create a file stream for the destination
                $outputStream = [System.IO.File]::OpenWrite($TargetFilePath)

                try {
                    # Copy from the ZIP entry to the file stream
                    $inputStream.CopyTo($outputStream)
                    Write-Host "Extracted: $TargetFilePath"
                } finally {
                    # Close streams to release resources
                    $inputStream.Close()
                    $outputStream.Close()
                }
            }
        }
    } finally {
        # Ensure the ZIP file is closed after processing
        $zip.Dispose()
    }
}

# Check if the file exists
if (-not (Test-Path $zipFilePath)) {
	DownloadFile $zipSource $zipFilePath
}

# Usage of the function
if (Test-Path $zipFilePath) {
	ExtractFile -ZipFilePath $zipFilePath -EntryFullName "pandoc-3.3/pandoc.exe" -TargetFilePath $targetFilePath
}

Remove-Item $zipFilePath
pause