@echo off
cd /d "%~dp0"
SET srcExe=..\..\App\bin\Release\publish\JocysCom.VS.AiCompanion.App.exe
SET sFile=%~dp0\Settings.zip
SET sPath=%~dp0\Settings
start "" "%srcExe%" /SettingsFile="%sFile%" /SettingsPath="%sPath%"