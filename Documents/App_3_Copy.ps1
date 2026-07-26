# Make sure the output directories exist
$filesDir = Join-Path $PSScriptRoot "Files"
$binDir = Join-Path $PSScriptRoot "Resources"

# Copy Desktop Application (EXE file)
$file1="JocysCom.VS.AiCompanion.App.exe"
$file1source=[System.IO.Path]::Combine($PSScriptRoot, "..\App\bin\Release\publish\", $file1)
$file1target=[System.IO.Path]::Combine($filesDir, $file1)
if ([System.IO.File]::Exists($file1source) -and -not [System.IO.File]::Exists($file1target)){
    [System.IO.File]::Copy($file1source, $file1target)
}
