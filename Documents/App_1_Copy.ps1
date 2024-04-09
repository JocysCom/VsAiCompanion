$file1="JocysCom.VS.AiCompanion.App.exe"
$file2="JocysCom.VS.AiCompanion.Extension.vsix"

# Make sure the output directories exist
$filesDir = Join-Path $PSScriptRoot "Files"
$binDir = Join-Path $PSScriptRoot "Resources"

$file1source=[System.IO.Path]::Combine($PSScriptRoot, "..\App\bin\Release\publish\", $file1)
$file2source=[System.IO.Path]::Combine($PSScriptRoot, "..\Extension\bin\Release\", $file2)

$file1target=[System.IO.Path]::Combine($filesDir, $file1)
$file2target=[System.IO.Path]::Combine($filesDir, $file2)

if ([System.IO.File]::Exists($file1source) -and -not [System.IO.File]::Exists($file1target)){
    [System.IO.File]::Copy($file1source, $file1target)
	[System.IO.File]::Copy($file1target, "$filesDir\App.Settings.exe")
	[System.IO.File]::Copy($file1target, "$filesDir\App.Settings.CompanyName.exe")
}
if ([System.IO.File]::Exists($file2source) -and -not [System.IO.File]::Exists($file2target)){
    [System.IO.File]::Copy($file2source, $file2target)
}
