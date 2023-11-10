# Define the source folder, base name for output files, and excluded folders
$sourceFolder = "VsAiCompanion"
$baseOutputFile = "VsAiCompanion"
$indexFile = "VsAiCompanion.index.json"
$excludedFolders = @('\\\.git\\', '\\Documents\\')
$excludedFiles = @('.ps1', '.exe')
$useExcludedFiles = $false
$includedFiles = @('.sln', '.csproj', '.xaml', '.c', '.cs', '.cpp', '.csv', '.docx', '.html', '.java', '.json', '.md', '.pdf', '.php', '.pptx', '.py', '.py', '.rb', '.tex', '.txt', '.css', '.jpeg', '.jpg', '.js', '.gif', '.png', '.tar', '.ts', '.xlsx', '.xml', '.zip')
$useIncludedFiles = $false
$maxFileSize = 1MB

# Create or clear the index file
if (Test-Path $indexFile) { Clear-Content $indexFile } else { New-Item $indexFile -ItemType File }

# Initialize variables
$indexArray = @()
$fileCounter = 1
$currentOutputFile = "$baseOutputFile.{0:D4}.json"
$currentFileSize = 0
$maxOutputFileSize = 500KB
$jsonArray = @()

function Write-OutputFile {
    param (
        [ref]$fileCounter,
        [ref]$currentOutputFile,
        [ref]$jsonArray
    )

    $formattedFileName = "$baseOutputFile.{0:D4}.json" -f ($fileCounter.Value - 1)
    $jsonArray.Value | ConvertTo-Json -Depth 10 | Set-Content $formattedFileName -Encoding UTF8
    $fileCounter.Value++
    $jsonArray.Value = @()
    return $formattedFileName
}

# Function to compress and convert file content to base64
function Compress-AndConvertToBase64 {
    param (
        [string]$filePath
    )

    $fileContent = Get-Content $filePath -Raw -Encoding Byte
    $memoryStream = New-Object System.IO.MemoryStream
    $gzipStream = New-Object System.IO.Compression.GZipStream $memoryStream, ([System.IO.Compression.CompressionMode]::Compress)
    $gzipStream.Write($fileContent, 0, $fileContent.Length)
    $gzipStream.Close()
    $compressedContent = $memoryStream.ToArray()
    $memoryStream.Close()
    $base64Content = [Convert]::ToBase64String($compressedContent)
    $fileName = Split-Path $filePath -Leaf
    $relativePath = $filePath.Substring((Get-Location).Path.Length + 1)

    $jsonObject = @{
        fileName = $fileName
        filePath = $relativePath
        contentType = "application/octet-stream"
        contentEncoding = "gzip+base64"
        content = $base64Content
    }

    return ($jsonObject | ConvertTo-Json -Compress)
}

# Iterate through the files in the folder, excluding specified folders and files larger than 1MB
$files = Get-ChildItem $sourceFolder -Recurse -File

foreach ($file in $files) {
    $exclude = $false
    foreach ($folder in $excludedFolders) {
        if ($file.FullName -match $folder) {
            $exclude = $true
            break
        }
    }
    
    if ($useExcludedFiles){
        # Check for excluded file extensions
        foreach ($fileExt in $excludedFiles) {
            if ($file.Extension -eq $fileExt) {
                $exclude = $true
                break
            }
        }
    }

    # Check for included file extensions
    if ($useIncludedFiles){
        $include = $false
        foreach ($fileExt in $includedFiles) {
            if ($file.Extension -eq $fileExt) {
                Write-Host "Include $($file.Name)"
                $include = $true
                break
            }
        }
        if (-not $include){
            $exclude = $true
        }
    }

    if ($exclude -or $file.Length -gt $maxFileSize) {
        continue
    }
    $jsonContent = Compress-AndConvertToBase64 $file.FullName
    $jsonContentLength = ($jsonContent | Measure-Object -Character).Characters

    # Check if adding this content exceeds the max output file size
    if ($currentFileSize + $jsonContentLength -gt $maxOutputFileSize) {
        $currentOutputFile = Write-OutputFile ([ref]$fileCounter) ([ref]$currentOutputFile) ([ref]$jsonArray)
        $currentFileSize = 0
    }

    $jsonArray += $jsonContent
    $currentFileSize += $jsonContentLength

    $indexObject = @{
        filePath = $file.FullName.Substring((Get-Location).Path.Length + 1)
        contentType = "application/octet-stream"
        fileIndex = $fileCounter - 1
        contentFile = $formattedFileName = "$baseOutputFile.{0:D4}.json" -f ($fileCounter - 1)
    }
    $indexArray += $indexObject
}

# Write remaining content in the JSON array to the last output file
if ($jsonArray.Count -gt 0) {
    $currentOutputFile = Write-OutputFile ([ref]$fileCounter) ([ref]$currentOutputFile) ([ref]$jsonArray)
}

# Write index array to the index file
$indexArray | ConvertTo-Json -Depth 10 | Set-Content $indexFile  -Encoding UTF8

Write-Host "Conversion completed. Output files: $baseOutputFile.*.json"
Write-Host "Index file created: $indexFile"