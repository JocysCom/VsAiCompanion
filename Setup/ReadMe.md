# VS AI Companion Installer - Update Requirements

## Installation Path and Settings
Setup installs both the application and its settings for the current user only and does not affect other user accounts on the same machine.
The installation will appear under the Add/Remove Programs list for the current user only.
The application and its settings are installed in the following directory:

`%LOCALAPPDATA%\<company>\<product>\`

For example:

`C:\Users\<UserName>\AppData\Local\Jocys.com\VS AI Companion\`

## Overview
To ensure that the Visual Studio Installer properly handles upgrades by uninstalling the previous version before installing the new one, certain properties in the installer project must be correctly configured. This guide outlines these requirements.

## Requirements

### 1. Product Version
- **Must Change**: Increment the `ProductVersion` for each new release.
  - Format: `major.minor.build`
  - Example: Change from `1.12.25` to `1.12.26` or `1.13.0`

### 2. Package Code
- **Must Change**: Generate a new and unique `PackageCode` for each build.
  - Visual Studio typically handles this automatically with each rebuild.
  - Ensure it changes between builds to identify new packages distinctly.

### 3. Product Code
- **Must Change**: Each version of the install package should have a unique `ProductCode`.
  - Ensure it is different for each new release.
  - Visual Studio generally updates this automatically during the build process.

### 4. Upgrade Code
- **Must Remain Constant**: The `UpgradeCode` should stay the same across different versions of the product.
  - This identifier allows the installer to recognize and manage the upgrade process.
  - It should not change between different releases of your application.

### 5. Remove Previous Versions
- **Set to True**: The `RemovePreviousVersions` property should be set to `True` to ensure the installer removes the older version before installing the new one.


## MSI Setup and Commands

Platform: 64-bit
Company Name: Jocys.com
Product Name: VS AI Companion
Upgrade Code: {8EAC34EB-107A-44B2-B4AF-067C6A4DBF80}

Product Code: {C280041B-D36E-45FC-B558-3E9B5238A10D} // 1.12.41
Product Code: {4995F371-C24D-49B4-8010-4E452987468F} // 1.12.52
Product Code: {96091D58-A78F-4D54-908E-0ED1163A443A} // 1.12.53

Application Path: %LOCALAPPDATA%\Jocys.com\VS AI Companion\JocysCom.VS.AiCompanion.App.exe

### PowerShell Commands

Returns True if VS AI Companion is installed (Use Product Code / Identifying Number):

	Test-Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{96091D58-A78F-4D54-908E-0ED1163A443A}"

Get the information about VS AI Companion installed:

	(Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*" | Where-Object { $_.DisplayName -eq "VS AI Companion" })

### Commands Require elevated properties.

Install:

	msiexec /i "JocysCom.VS.AiCompanion.Setup.msi" /quiet
	
Uninstall (Use Product Code / Identifying Number):

	msiexec /x "{96091D58-A78F-4D54-908E-0ED1163A443A}" /quiet
	
Uninstall all instances of VS AI Companion with Command Prompt:

	wmic product where name="VS AI Companion" call uninstall /nointeractive

Uninstall all instances of VS AI Companion with PowerShell:

	Get-WmiObject -Class Win32_Product -Filter "Name='VS AI Companion'" | ForEach-Object { $_.Uninstall() }

List installed instances of VS AI Companion with Command Prompt:

	wmic product where "name='VS AI Companion'" get Name,Version,InstallDate,IdentifyingNumber

List installed instances of VS AI Companion with PowerShell

	Get-WmiObject -Class Win32_Product -Filter "Name='VS AI Companion'" | Select-Object -Property Name, Version

### MSI Properties

MSI Properties can be viewed with LessMSI: [Table View] tab, Table: "Property".
https://github.com/activescott/lessmsi
