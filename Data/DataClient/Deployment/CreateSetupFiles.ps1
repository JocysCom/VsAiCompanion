# Add line to Release post-build event:
# PowerShell -NoProfile -ExecutionPolicy Bypass -File "$(ProjectDir)Deployment\CreateSetupFiles.ps1"

# Get the path of the directory where the script is located
$scriptDirectory = $PSScriptRoot
# Define the root directory of your project relative to the script's directory
$rootDirectory = Join-Path $scriptDirectory "..\..\DataFunctions"
# Define the target directory for the installation files relative to the script's directory
$targetDirectory = Join-Path $scriptDirectory "Setup\MSSQL"

# Define the directories for stored procedures and tables within your project
$storedProceduresDir = Join-Path $rootDirectory "Embedding\Stored Procedures"
$tablesDir = Join-Path $rootDirectory "Embedding\Tables"

# Create the deployment directory if it does not exist
if (-not (Test-Path $targetDirectory)) {
    New-Item -ItemType Directory -Path $targetDirectory | Out-Null
}

function Create-InstallationFile($objectType, $objectName, $sqlContent) {
    
    # Check if there is a "GO" line in the SQL script and split accordingly
    $sqlParts = $sqlContent -split '(?im)^\s*GO\s*$' 
    $scriptPart1 = $sqlParts[0]
    # Now we want to skip the first part (which is $scriptPart1) and join all the remaining parts
    $scriptPart2 = if ($sqlParts.Length -gt 1) { $sqlParts[1..($sqlParts.Length - 1)] -join "`nGO`n" } 
    $checkScript = "";
    $checkScript += "SET ANSI_NULLS ON`r`n";
    $checkScript += "SET QUOTED_IDENTIFIER ON`r`n";
    $checkScript += "`r`nGO`r`n`r`n";
    if ($objectType -eq 'Stored Procedures') {
        #$checkScript += "IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[Embedding].[$objectName]') AND type in (N'P', N'PC'))`r`n";
        #$checkScript += "BEGIN`r`n";
        #$checkScript += "PRINT 'Creating stored procedure [Embedding].[$objectName]'`r`n";
    } elseif ($objectType -eq 'Tables') {
        #$checkScript += "IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'Embedding' AND TABLE_NAME = '$objectName')`r`n";
        #$checkScript += "BEGIN`r`n";
        #$checkScript += "PRINT 'Creating table [Embedding].[$objectName]'`r`n";
    }
    $checkScript += "$scriptPart1`r`n"
    #$checkScript += "END`r`n"
    # Append a "GO" followed by the second part of the script, if it exists
    if ($scriptPart2 -ne $null) {
        $checkScript = $checkScript.TrimEnd() + "`n`nGO`n`n" + $scriptPart2.TrimStart()
    } else {
        # Add the closing "GO" to the first (and only) part of the script
        $checkScript = $checkScript.TrimEnd()
    }
    $checkScript = ConvertTo-WindowsLineEnding($checkScript)

    # Write the checkScript to the output file with explicit encoding for Windows-style line endings
    $outType= $objectType -replace " ", ""
    $outputPath = Join-Path $targetDirectory "$objectName.sql"
    Set-Content -Path $outputPath -Value $checkScript -Force
    Write-Host "Installation script created: $outputPath"
}

# Function to replace line endings
function ConvertTo-WindowsLineEnding($content) {
    # Replace all \r\n to \n to avoid duplicating \r\r\n
    $content = $content -replace "`r`n", "`n"
    # Now replace all \n (Unix line endings) to \r\n (Windows line endings)
    $content -replace "`n", "`r`n"
}

# Process stored procedures
Get-ChildItem -Path $storedProceduresDir -Filter *.sql | ForEach-Object {
    $content = Get-Content $_.FullName -Raw
    Create-InstallationFile 'Stored Procedures' $_.BaseName $content
}

# Process tables
Get-ChildItem -Path $tablesDir -Filter *.sql | ForEach-Object {
    $content = Get-Content $_.FullName -Raw
    Create-InstallationFile 'Tables' $_.BaseName $content
}
