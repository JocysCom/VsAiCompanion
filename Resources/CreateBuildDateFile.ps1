param (
    [string]$FilePath
)

# Ensure the directory exists
New-Item -ItemType Directory -Force -Path (Split-Path -Path $FilePath) | Out-Null

if (Test-Path -Path $FilePath) {
    # Get the original creation and modification dates
    $originalCreationTime = (Get-Item $FilePath).CreationTime
    $originalModificationTime = (Get-Item $FilePath).LastWriteTime
} else {
    $originalCreationTime = Get-Date
    $originalModificationTime = Get-Date
}

# Write the current date in ISO 8601 format to the file
(Get-Date).ToString('o') | Out-File -Force -FilePath $FilePath

# Set the creation and modification dates to the original date + 1 second
$futureCreationTime = $originalCreationTime.AddSeconds(1)
$futureModificationTime = $originalModificationTime.AddSeconds(1)

# Apply the new creation and modification times to the file
(Get-Item $FilePath).CreationTime = $futureCreationTime
(Get-Item $FilePath).LastWriteTime = $futureModificationTime