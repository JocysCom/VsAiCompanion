param (
    [Parameter(Mandatory = $true, Position = 0)]
    [string] $sourceDir,

    [Parameter(Mandatory = $true, Position = 1)]
    [string] $destFile
)

if (!(Test-Path -Path $sourceDir)) {
    return
}


Add-Type -Assembly "System.IO.Compression.FileSystem"

function Get-FileChecksums {
    param (
        [string] $directory
    )

    $checksums = @{}

    Get-ChildItem -Path $directory -Recurse -File |
    ForEach-Object {
        $hashAlgorithm = [System.Security.Cryptography.SHA256]::Create()
        try {
            $stream = [System.IO.File]::OpenRead($_.FullName)
            $hashBytes = $hashAlgorithm.ComputeHash($stream)
            $stream.Close()

            $checksum = -join ($hashBytes | ForEach-Object { $_.ToString("x2") })
            $checksums[$_.FullName.Replace($directory, "").TrimStart("\")] = $checksum
        }
        finally {
            $hashAlgorithm.Dispose()
            if ($stream) {
                $stream.Dispose()
            }
        }
    }

    return $checksums
}

function CheckAndZipFiles {

    $sourceChecksums = Get-FileChecksums -directory $sourceDir

    $destChecksums = @{}
    if (Test-Path -Path $destFile) {
        $tempDir = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), [System.IO.Path]::GetRandomFileName())
        [IO.Compression.ZipFile]::ExtractToDirectory($destFile, $tempDir)
        $destChecksums = Get-FileChecksums -directory $tempDir
        Remove-Item -Path $tempDir -Recurse -Force
    }

    $checksumsChanged = $false
    foreach ($key in $sourceChecksums.Keys) {
        if (-not $destChecksums.ContainsKey($key) -or $sourceChecksums[$key] -ne $destChecksums[$key]) {
            $checksumsChanged = $true
            break
        }
    }

    $name = [System.IO.Path]::GetFileName($destFile)

    if ($checksumsChanged) {
        Write-Host "$($name): Source and destination checksums do not match. Updating destination file..."
        if (Test-Path -Path $destFile) { Remove-Item -Path $destFile -Force }
        [IO.Compression.ZipFile]::CreateFromDirectory($sourceDir, $destFile)
    } else {
        Write-Host "$($name): Source and destination checksums match. No update needed."
    }
}

#==============================================================
# Ensure that only one instance of this script can run.
# Other instances wait for the previous one to complete.
#--------------------------------------------------------------
# Use the full script name with path as the lock name.
$scriptName = $MyInvocation.MyCommand.Name
$mutexName = "Global\$scriptName"
$mutexCreated = $false
$mutex = New-Object System.Threading.Mutex($true, $mutexName, [ref] $mutexCreated)
if (-not $mutexCreated) {
       
    Write-Host "Another $scriptName instance is running. Waiting..."
    $mutex.WaitOne() > $null  # Wait indefinitely for the mutex
}
try {
    # Main script logic goes here...
    CheckAndZipFiles
}
finally {
    # Release the mutex so that other instances can proceed.
    $mutex.ReleaseMutex()
    $mutex.Dispose()
}
#==============================================================
