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

# Copy Visual Studio Extention (VSIX file)
$file2="JocysCom.VS.AiCompanion.Extension.vsix"
$file2source=[System.IO.Path]::Combine($PSScriptRoot, "..\Extension\bin\Release\", $file2)
$file2target=[System.IO.Path]::Combine($filesDir, $file2)
if ([System.IO.File]::Exists($file2source) -and -not [System.IO.File]::Exists($file2target)){
    [System.IO.File]::Copy($file2source, $file2target)
}
