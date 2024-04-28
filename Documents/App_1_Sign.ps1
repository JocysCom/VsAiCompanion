Import-Module "d:\_Backup\Configuration\SSL\Tools\app_signModule.ps1" -Force

[string[]]$appFiles = @(
    "..\App\bin\Release\publish\JocysCom.VS.AiCompanion.App.exe",
    "..\Extension\bin\Release\JocysCom.VS.AiCompanion.Extension.vsix"
)
[string]$appName = "Jocys.com VS AI Companion"
[string]$appLink = "https://www.jocys.com"

ProcessFiles $appName $appLink $appFiles
pause