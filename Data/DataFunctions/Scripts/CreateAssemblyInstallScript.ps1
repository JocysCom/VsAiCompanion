param([string]$targetPath)


# Add line to Release post-build event:
# PowerShell -NoProfile -ExecutionPolicy Bypass -File "$(ProjectDir)Deployment\CreateSetupFiles.ps1"

# Get the path of the directory where the script is located
$scriptDirectory = $PSScriptRoot
# Define the target directory for the installation files relative to the script's directory
$targetDirectory = Join-Path $scriptDirectory "..\Deployment"

# Get the file base name from target path
$fileBaseName = [System.IO.Path]::GetFileNameWithoutExtension($targetPath)
$filePath = [System.IO.Directory]::GetParent($targetPath)

# Define SQL script template
$sqlScriptTemplate = @"
GO
CREATE ASSEMBLY [{0}]
    AUTHORIZATION [dbo]
    FROM 0x{1};
GO
ALTER ASSEMBLY [{0}]
    DROP FILE ALL
    ADD FILE FROM 0x{2} AS N'{0}.pdb';
"@

# Get byte array of DLL and PDB files
$dllBytes = [System.IO.File]::ReadAllBytes([System.IO.Path]::Combine($filePath, "$fileBaseName.dll"))
$pdbBytes = [System.IO.File]::ReadAllBytes([System.IO.Path]::Combine($filePath, "$fileBaseName.pdb"))

# Convert byte arrays to hex string
$dllHexString = [System.BitConverter]::ToString($dllBytes) -replace '-', ''
$pdbHexString = [System.BitConverter]::ToString($pdbBytes) -replace '-', ''

# Replace placeholders in SQL script template with actual hex strings
$sqlScript = $sqlScriptTemplate -f $fileBaseName, $dllHexString, $pdbHexString

# Write SQL script to file
[System.IO.File]::WriteAllText([System.IO.Path]::Combine($targetDirectory, "$fileBaseName.sql"), $sqlScript)

Write-Host "SQL assembly install script has been created."
