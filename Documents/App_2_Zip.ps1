
# Make sure the output directories exist
$filesDir = Join-Path $PSScriptRoot "Files"
$binDir = Join-Path $PSScriptRoot "..\Resources"

$file1="JocysCom.VS.AiCompanion.App.exe"
if (-not [System.IO.File]::Exists([System.IO.Path]::Combine($filesDir, $file1))){
    [System.IO.File]::Copy([System.IO.Path]::Combine($PSScriptRoot, "..\App\bin\Release\publish\", $file1), [System.IO.Path]::Combine($filesDir, $file1))
}
& "$binDir\ZipFiles.ps1" $filesDir "$filesDir\JocysCom.VS.AiCompanion.App.zip" $file1 $true


$file2="JocysCom.VS.AiCompanion.Extension.vsix"
if (-not [System.IO.File]::Exists([System.IO.Path]::Combine($filesDir, $file2))){
    [System.IO.File]::Copy([System.IO.Path]::Combine($PSScriptRoot, "..\Extension\bin\Release\", $file2), [System.IO.Path]::Combine($filesDir, $file2))
}
& "$binDir\ZipFiles.ps1" $filesDir "$filesDir\JocysCom.VS.AiCompanion.Extension.zip" $file2 $true
pause

