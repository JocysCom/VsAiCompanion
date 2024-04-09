@echo off
:: Change to the directory where the batch file is located
cd /d "%~dp0"
mklink /D "Files\App.Settings"             "..\..\Engine\Resources\Settings"
mklink /D "Files\App.Settings.CompanyName" "..\..\Engine\Resources\Settings.CompanyName"