<#
.SYNOPSIS
    Generate files of OpenAI Assitant
.NOTES
    Modified: 2023-11-11
#>
using namespace System;
using namespace System.IO;
Add-Type -AssemblyName "System.Web"
# ----------------------------------------------------------------------------
# Define the source folder, base name for output files, and excluded folders
$sourceFolder = ".."
$targetFolder = "AI_Files"
$baseOutputFile = "VsAiCompanion"
$indexFile = "VsAiCompanion.index.json"
$excludedFolders = @('\.git', '\.vs', '\\Resources\\', '\\Documents\\', "bin", "obj")
$excludedFiles = @('.ps1', '.exe')
$useExcludedFiles = $false
$includedFiles = @('.sln', '.csproj', '.xaml', '.c', '.cs', '.cpp', '.csv', '.docx', '.html', '.java', '.json', '.md', '.pdf', '.php', '.pptx', '.py', '.py', '.rb', '.tex', '.txt', '.css', '.jpeg', '.jpg', '.js', '.gif', '.png', '.tar', '.ts', '.xlsx', '.xml', '.zip')
$useIncludedFiles = $false
$maxFileSize = 1MB
# ----------------------------------------------------------------------------
# Get current command path.
[string]$current = $MyInvocation.MyCommand.Path;
# Get calling command path.
[string]$calling = @(Get-PSCallStack)[1].InvocationInfo.MyCommand.Path;
# If executed directly then...
if ($calling -ne "") {
	$current = $calling;
}
# ----------------------------------------------------------------------------
[FileInfo]$file = New-Object FileInfo($current);
# Set public parameters.
$global:scriptName = $file.Basename;
$global:scriptPath = $file.Directory.FullName;
# Change current directory.
[Environment]::CurrentDirectory = $scriptPath;
Set-Location $scriptPath;
# ----------------------------------------------------------------------------
$sourcePath = Join-Path $scriptPath $sourceFolder
$targetPath = Join-Path $scriptPath $targetFolder

# Use `GetFullPath` to fix dot notations.
$scriptPath = [Path]::GetFullPath($scriptPath) 
$sourcePath = [Path]::GetFullPath($sourcePath)
$targetPath = [Path]::GetFullPath($targetPath)

$indexPath = Join-Path $targetPath $indexFile

Write-Host "Script Path: $ScriptPath"
Write-Host "Source Path: $sourcePath"
Write-Host "Target Path: $targetPath"
Write-Host "Index  Path: $indexPath"

# Create or clear the target folder.
if (-not (Test-Path $targetFolder)) {
    $null = New-Item $targetFolder -ItemType Directory
}

# Create or clear the index file
if (Test-Path $indexPath) {
     Clear-Content $indexPath
}
else {
    $null = New-Item $indexPath -ItemType File
}

# Initialize variables
$indexArray = @()
[int]$global:currentFileIndex = 0
$currentOutputFile = "$baseOutputFile.{0:D4}.json"
$currentFileSize = 0
$maxOutputFileSize = 500KB
$jsonArray = @()

function Write-OutputFile {
    param (
        [ref]$currentOutputFile,
        [ref]$jsonArray
    )

    $formattedFileName = "$baseOutputFile.{0:D4}.json" -f $currentFileIndex
    $dataFilePath = Join-Path $targetFolder $formattedFileName
    # Use `GetFullPath` to fix dot notations.
    $dataFilePath = [Path]::GetFullPath($dataFilePath) 
    $jsonArray.Value | ConvertTo-Json -Depth 10 | Set-Content $dataFilePath -Encoding UTF8
    $jsonArray.Value = @()
}

# Function to compress and convert file content to base64
function Compress-AndConvertToBase64 {
    param (
        [string]$filePath
    )
    $relativePath = $filePath.Substring($sourcePath.Length + 1)
    Write-Host "Compress: $relativePath"

    $fileContent = Get-Content $filePath -Raw -Encoding Byte
    $memoryStream = New-Object System.IO.MemoryStream
    $gzipStream = New-Object System.IO.Compression.GZipStream $memoryStream, ([System.IO.Compression.CompressionMode]::Compress)
    $gzipStream.Write($fileContent, 0, $fileContent.Length)
    $gzipStream.Close()
    $compressedContent = $memoryStream.ToArray()
    $memoryStream.Close()
    $base64Content = [Convert]::ToBase64String($compressedContent)
    $fileName = Split-Path $filePath -Leaf

    $jsonObject = [PSCustomObject]@{
        #fileName = $fileName
        filePath = $relativePath
        contentType = "application/octet-stream"
        contentEncoding = "gzip+base64"
        content = $base64Content
    }

    # Note: We're returning the hashtable object directly now.
    return $jsonObject
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
    $jsonContentLength = ($jsonContent | ConvertTo-Json -Compress | Measure-Object -Character).Characters

    # Check if the current file size, plus this new content, exceeds the max size
    if ($currentFileSize + $jsonContentLength -gt $maxOutputFileSize) {
        Write-OutputFile ([ref]$currentOutputFile) ([ref]$jsonArray)
        $currentFileSize = 0
        $global:currentFileIndex = $global:currentFileIndex + 1
        # After writing the file you need to update the currentOutputFile for the next iteration
        $currentOutputFile = $formattedFileName
    }

    # Add the content to the current JSON array and update the size counter
    $jsonArray += $jsonContent
    $currentFileSize += $jsonContentLength

    $currentOutputFileName = "$baseOutputFile.{0:D4}.json" -f $currentFileIndex

    $fileContentType = [System.Web.MimeMapping]::GetMimeMapping($file.FullName)
    # Update index object using the current output file name
    $indexObject = [PSCustomObject]@{
        filePath = $file.FullName.Substring($sourcePath.Length + 1)
        fileContentType = $fileContentType
        contentFile = $currentOutputFileName
    }

    $indexArray += $indexObject
}

# Write remaining content in the JSON array to the last output file
if ($jsonArray.Count -gt 0) {
    Write-OutputFile ([ref]$currentOutputFile) ([ref]$jsonArray)
}

# Write index array to the index file
$indexArray | ConvertTo-Json -Depth 10 | Set-Content $indexPath  -Encoding UTF8

Write-Host "Conversion completed. Output files: $baseOutputFile.*.json"
Write-Host "Index file created: $indexPath"