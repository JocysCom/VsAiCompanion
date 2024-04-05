<#
.SYNOPSIS
	Optional script to split binary strings in files
    to prevent them from breaking certain editors.
.NOTES
    Modified:   2024-02-27
#>

# Set the path to the directory containing the sql files
$directoryPath = "..\"

# Get the latest Migration*.sql file based on the last write time
$latestMigrationFile = Get-ChildItem -Path $directoryPath -Filter "*.sql" |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

# Read the content of the latest Migration file
$content = Get-Content -Path $latestMigrationFile.FullName -Raw

# Define the limit for binary string length (9216 characters) and the desired block size
$binaryStringLimit = 9216
$blockSize = 8192

# Use a regex to find all binary strings that exceed the limit
$pattern = "0x[0-9A-Fa-f]{$binaryStringLimit,}"
[regex]::Matches($content, $pattern) | ForEach-Object {
    $binaryString = $_.Value
    $binaryStringLength = $binaryString.Length
    $splitStrings = @()
    $binaryStringComp = ""

    # Split the binary string into chunks of desired block size
    for ($i = 0; $i -lt $binaryStringLength; $i += $blockSize) {
        $endIndex = [math]::Min($i + $blockSize, $binaryStringLength)
        $length = $endIndex - $i
        $splitStrings += "'" + $binaryString.Substring($i, $length) + "'"
        $binaryStringComp += $binaryString.Substring($i, $length)
    }

    If ($binaryString -ne $binaryStringComp) {
        Write-Host "Not Equal"
        Set-Content -Path ".\string.old" -Value $binaryString
        Set-Content -Path ".\string.new" -Value $binaryStringComp
    }

    # Create the replacement string using the split chunks
    $replacement = "CONVERT(varbinary(max),`r`n`t" + ($splitStrings -join "+`r`n`t") + ",`r`n`t 1)`r`n"

    # Replace the long binary string with the converted version
    $content = $content.Replace($binaryString, $replacement)
}

# Write the modified content back to the file
Set-Content -Path $latestMigrationFile.FullName -Value $content

Write-Host "The latest migration file has been processed and updated."
Write-Host "Press any key to continue..."
$null = [System.Console]::ReadKey($true)
