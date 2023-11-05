param (
    [Parameter(Mandatory = $true, Position = 0)]
    [string] $sourceDir,

    [Parameter(Mandatory = $true, Position = 1)]
    [string] $destFile
)

if (!(Test-Path -Path $sourceDir)) { return }

Add-Type -Assembly "System.IO.Compression.FileSystem"

$sourceModified = Get-ChildItem -Path $sourceDir -Recurse | 
                  ForEach-Object { $_.LastWriteTimeUtc } | 
                  Sort-Object -Descending | Select-Object -First 1

$destModified = if (Test-Path -Path $destFile) { 
        [IO.File]::GetLastWriteTimeUtc($destFile)
    } else {
        [DateTime]::MinValue
    }

if ($sourceModified -gt $destModified){
    if (Test-Path -Path $destFile) { Remove-Item -Path $destFile }
    [IO.Compression.ZipFile]::CreateFromDirectory($sourceDir, $destFile)
}