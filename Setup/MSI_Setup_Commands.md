### JocysCom.VS.AiCompanion.Setup

Platform: 64-bit
Company Name: Jocys.com
Product Name: VS AI Companion
Product Code: {CDFE42E6-4264-4A82-83AD-154DD995A2C3} // The code is different for each installer version.
Upgrade Code: {8EAC34EB-107A-44B2-B4AF-067C6A4DBF80}

### Commands Require elevated properties.

Returns True if VS AI Companion is installed (Use Product Code / Identifying Number):

	Test-Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{CDFE42E6-4264-4A82-83AD-154DD995A2C3}"

Install:

	msiexec /i "JocysCom.VS.AiCompanion.Setup.msi" /quiet
	
Uninstall (Use Product Code / Identifying Number):

	msiexec /x "{CDFE42E6-4264-4A82-83AD-154DD995A2C3}" /quiet
	
List installed instances of VS AI Companion:

	PowerShell:		Get-WmiObject -Class Win32_Product -Filter "Name='VS AI Companion'" | Select-Object -Property Name, Version

	Command Prompt:	wmic product where "name='VS AI Companion'" get Name,Version,InstallDate,IdentifyingNumber


Uninstall all instances of VS AI Companion:

	PowerShell:		Get-WmiObject -Class Win32_Product -Filter "Name='VS AI Companion'" | ForEach-Object { $_.Uninstall() }

	Command Prompt: wmic product where name="VS AI Companion" call uninstall /nointeractive
