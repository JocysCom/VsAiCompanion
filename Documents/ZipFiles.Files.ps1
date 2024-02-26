..\Resources\ZipFiles.ps1 "$($PSScriptRoot)\Files" "$($PSScriptRoot)\Files\JocysCom.VS.AiCompanion.App.zip" JocysCom.VS.AiCompanion.App.exe $true
..\Resources\ZipFiles.ps1 "$($PSScriptRoot)\Files" "$($PSScriptRoot)\Files\JocysCom.VS.AiCompanion.Extension.zip" JocysCom.VS.AiCompanion.Extension.vsix $true
$source = "$($PSScriptRoot)\Files\JocysCom.VS.AiCompanion.App.exe"
[System.IO.File]::Copy($source, "$($PSScriptRoot)\Files\AiCompanion.Settings.exe", $true)
[System.IO.File]::Copy($source, "$($PSScriptRoot)\Files\AiCompanion.Settings.CompanyName.exe", $true)