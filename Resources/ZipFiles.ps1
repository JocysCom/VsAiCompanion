param (
    [Parameter(Mandatory = $true, Position = 0)]
    [string] $sourceDir,

    [Parameter(Mandatory = $true, Position = 1)]
    [string] $destFile,
	
    # Optional. The search string to match against the names of files.
    [Parameter(Mandatory = $false, Position = 2)]
    [string] $searchPattern,

    # Optional. Use shell zipper if this parameter is set to true.
    [Parameter(Mandatory = $false, Position = 3)]
    [bool] $UseShellToZipFiles = $false,

    # Optional. Use comment for console.
    [Parameter(Mandatory = $false, Position = 4)]
    [string] $LogPrefix = ""
)

if (!(Test-Path -Path $sourceDir)) {
    return
}


Add-Type -Assembly "System.IO.Compression.FileSystem"

function Get-FileChecksums {
    param (
        [string] $directory,
        [string] $searchPattern = "*"
    )

    $checksums = @{}

    Get-ChildItem -Path $directory -Recurse -File -Filter $searchPattern |
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

    $sourceChecksums = Get-FileChecksums -directory $sourceDir -searchPattern $searchPattern

    $destChecksums = @{}
    if (Test-Path -Path $destFile) {
        $tempDir = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), [System.IO.Path]::GetRandomFileName())
        [IO.Compression.ZipFile]::ExtractToDirectory($destFile, $tempDir)
        $destChecksums = Get-FileChecksums -directory $tempDir -searchPattern $searchPattern
        Remove-Item -Path $tempDir -Recurse -Force
    }

    $checksumsChanged = $false
    foreach ($key in $sourceChecksums.Keys) {
        if (-not $destChecksums.ContainsKey($key) -or $sourceChecksums[$key] -ne $destChecksums[$key]) {
            $checksumsChanged = $true
            break
        }
    }

    if ($checksumsChanged) {
        Write-Host "$($logPrefix)Source and destination checksums do not match. Updating destination file..."
        if (Test-Path -Path $destFile) { Remove-Item -Path $destFile -Force }
        
        if ($UseShellToZipFiles) {
            Compress-ZipFileUsingShell -sourceDir $sourceDir -destFile $destFile -searchPattern $searchPattern
        } else {
            Compress-ZipFileUsingCSharp -sourceDir $sourceDir -destFile $destFile -searchPattern $searchPattern
        }
    } else {
        Write-Host "$($logPrefix)Source and destination checksums match. No update needed."
    }
}

function Compress-ZipFileUsingCSharp {
    param (
        [string] $sourceDir,
        [string] $destFile,
        [string] $searchPattern
    )
    # Handling optional search pattern for zipping files.
    if (![string]::IsNullOrEmpty($searchPattern)) {
        $tempSourceDir = New-Item -ItemType Directory -Path ([System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), [System.IO.Path]::GetRandomFileName()))
        Get-ChildItem -Path $sourceDir -Recurse -File -Filter $searchPattern | Copy-Item -Destination { Join-Path -Path $tempSourceDir -ChildPath ($_.FullName.Replace($sourceDir, "").TrimStart("\")) } -Container
        [IO.Compression.ZipFile]::CreateFromDirectory($tempSourceDir.FullName, $destFile)
        Remove-Item -Path $tempSourceDir -Recurse -Force
    } else {
        [IO.Compression.ZipFile]::CreateFromDirectory($sourceDir, $destFile)
    }
}

function Compress-ZipFileUsingShell {
    param (
        [string] $sourceDir,
        [string] $destFile,
        [string] $searchPattern
    )
    
    # Ensure the destination directory exists
    $destDir = [System.IO.Path]::GetDirectoryName($destFile)
    if (-not (Test-Path $destDir)) {
        New-Item -ItemType Directory -Path $destDir | Out-Null
    }

    # Create an empty zip if it doesn't exist
    if (-not (Test-Path $destFile)) {
        $null = Set-Content -Path $destFile -Value ("PK" + [char]5 + [char]6 + ("$([char]0)" * 18))
    }

    # Use Shell Application to manipulate the zip file
    $shellApplication = new-object -com shell.application
    $zipPackage = $shellApplication.NameSpace($destFile)

    if (-not $zipPackage) {
        Write-Error "$($logPrefix)Failed to create a zip package COM object for the destination file. Check the path and permissions."
        return
    }

    if (![string]::IsNullOrEmpty($searchPattern)) {
        $files = Get-ChildItem -Path $sourceDir -Recurse -File -Filter $searchPattern
    } else {
        $files = Get-ChildItem -Path $sourceDir -Recurse
    }

    foreach ($file in $files) {
        $path = $file.FullName
        $zipPackage.CopyHere($path)
        
        $maxRetries = 4
        $retryCount = 0
        Do {
            Start-Sleep -Seconds 2
            $retryCount++
            if ($retryCount -gt $maxRetries) {
                Write-Host "$($logPrefix)Max retries reached. Moving to next file..."
                break
            }
        } While (($shellApplication.NameSpace($destFile).Items() | Where-Object { $_.Path -eq $path }).Count -eq 0)
    }


    # Release COM objects
    [System.Runtime.InteropServices.Marshal]::ReleaseComObject($zipPackage) | Out-Null
    [System.Runtime.InteropServices.Marshal]::ReleaseComObject($shellApplication) | Out-Null
    [GC]::Collect()
    [GC]::WaitForPendingFinalizers()
}

$destName = [System.IO.Path]::GetFileName($destFile)

$logPrefix = "$($destName): $($LogPrefix)"

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
       
    Write-Host "$($logPrefix)Another instance is running. Waiting..."
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
