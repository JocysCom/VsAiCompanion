### JocysCom.VS.AiCompanion.Setup

Platform: 64-bit
Company Name: Jocys.com
Product Name: VS AI Companion
Product Code: {CDFE42E6-4264-4A82-83AD-154DD995A2C3}

### Commands Require elevated properties.

Returns True if VS AI Companion is installed:

	Test-Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{CDFE42E6-4264-4A82-83AD-154DD995A2C3}"

Install:

	msiexec /i "JocysCom.VS.AiCompanion.Setup.msi" /quiet
	
Uninstall:

	msiexec /x "{CDFE42E6-4264-4A82-83AD-154DD995A2C3}" /quiet
	
List installed instances of VS AI Companion:

		Get-WmiObject -Class Win32_Product -Filter "Name='VS AI Companion'" | Select-Object -Property Name, Version

Uninstall all instances of VS AI Companion:

		Get-WmiObject -Class Win32_Product -Filter "Name='VS AI Companion'" | ForEach-Object { $_.Uninstall() }
