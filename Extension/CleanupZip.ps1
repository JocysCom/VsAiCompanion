param (
    [Parameter(Mandatory = $true, Position = 0)]
    [string] $TargetPath
)

# Define the path to the ZIP file
$zipPath = $TargetPath

# Define the folders to remove
$foldersToRemove = @(
    "be", "cs", "cs-CZ", "da", "de", "es", "fa", "fi", "fr", "ja", "it",
    "ko", "mk", "nl", "pl", "pt", "pt-BR", "ru", "sv", "tr", "uk",
    "zh-CN", "zh-CHS", "zh-CHT", "zh-Hans", "zh-Hant",
    "arm", "arm64", "musl-arm64", "musl-x64", "musl-x86", "x86"
)

# Load the .NET ZIP libraries
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

# Open the ZIP file
$zipFile = [System.IO.Compression.ZipFile]::Open($zipPath, [System.IO.Compression.ZipArchiveMode]::Update)

# Iterate through the entries in the ZIP file
foreach ($entry in @($zipFile.Entries)) {
    foreach ($folder in $foldersToRemove) {
        # Check if the entry belongs to one of the folders to remove
        if ($entry.FullName -like "$folder/*" -or $entry.FullName -eq "$folder/") {
            $entry.Delete()
            Write-Host "Removed: $($entry.FullName)"
        }
    }
}

# Close the ZIP file
$zipFile.Dispose()

Write-Host "Specified folders have been removed from the ZIP file."
