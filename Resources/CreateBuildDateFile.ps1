param (
    [string]$FilePath,
    [bool]$UpdateTime = $false
)

# Ensure the directory exists
New-Item -ItemType Directory -Force -Path (Split-Path -Path $FilePath) | Out-Null

if (Test-Path -Path $FilePath) {
    # Get the original creation and modification dates
    $modificationTime = (Get-Item $FilePath).LastWriteTime
    # Read the current content of the file
    $currentContent = Get-Content -Path $FilePath -Raw
} else {
    $modificationTime = Get-Date
    $currentContent = ""
}

# Set the creation and modification dates to the original date + 1 second
$newModificationTime = $modificationTime.AddSeconds(1)
$contentDate = (Get-Date)

if (-not $UpdateTime) {
    # Set the time part to zeroes
    $newModificationTime = Get-Date `
        -Year $modificationTime.Year -Month $modificationTime.Month -Day $modificationTime.Day `
        -Hour 0 -Minute 0 -Second 0 -Millisecond 0
    $contentDate = Get-Date `
        -Year $contentDate.Year -Month $contentDate.Month -Day $contentDate.Day `
        -Hour 0 -Minute 0 -Second 0 -Millisecond 0
}

$newContent = $contentDate.ToString('o')

# Check if the file content is the same
if ($currentContent -ne $newContent) {
    # Write the current date in ISO 8601 format to the file
    $newContent | Out-File -Force -FilePath $FilePath
    # Apply the new creation and modification times to the file
    (Get-Item $FilePath).CreationTime = $newModificationTime
    (Get-Item $FilePath).LastWriteTime = $newModificationTime
} else {
    Write-Host "No change in file content. File not updated."
}
