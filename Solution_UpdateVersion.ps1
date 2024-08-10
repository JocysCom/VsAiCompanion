<#
.SYNOPSIS
    Increment version of Projects.
.NOTES
    Author:     Evaldas Jocys <evaldas@jocys.com>
    File Name:  UpdateVersion.ps1
    Modified:   2022-06-06
.LINK
    http://www.jocys.com
#>

using namespace System
using namespace System.IO
using namespace System.Text
using namespace System.Text.RegularExpressions
using namespace System.Security.Principal
using namespace System.Security.Cryptography
using namespace System.Security.Cryptography.X509Certificates
using namespace System.Collections.Generic

# ----------------------------------------------------------------------------
# Get current command path.
[string]$current = $MyInvocation.MyCommand.Path
# Get calling command path.
[string]$calling = @(Get-PSCallStack)[1].InvocationInfo.MyCommand.Path
# If executed directly then...
if ("" -ne $calling) {
    $current = $calling
}
# ----------------------------------------------------------------------------
function ConfigureSettings
{
    [FileInfo]$file = New-Object FileInfo($current)
    # Set public parameters.
	$global:scriptName = $file.Basename
	$global:scriptPath = $file.Directory.FullName
    # Cange current dir.
    [Console]::WriteLine("Path: {0}", $scriptPath)
    [Environment]::CurrentDirectory = $scriptPath
	# ----------------------------------------------------------------------------
	$global:name = $scriptName
    # ----------------------------------------------------------------------------
}
# ----------------------------------------------------------------------------
class Config {
    [string]$File;
	[string[]]$VNames = @()
	[string[]]$VCodes = @()
}
# ----------------------------------------------------------------------------
function GetConfig
{
	param($file)
	$extension = [Path]::GetExtension($file)
	$c = [Config]::new()
	$c.File = $file
	$c.VCodes = @()
	if ($extension -eq ".cs"){ # AssemblyInfo.cs
		$c.VNames = @( "(?<p>[\r?\n]+\[assembly: AssemblyVersion\("")(?<v>[^""]*)(?<s>""\)])", "(?<p>[\r?\n]+\[assembly: AssemblyFileVersion\("")(?<v>[^""]*)(?<s>""\)])" )
	}
	if ($extension -eq ".csproj"){ # ProjectName.csproj
		$c.VNames = @( "(?<p><AssemblyVersion>)(?<v>[^<]*)(?<s></AssemblyVersion>)", "(?<p><FileVersion>)(?<v>[^<]*)(?<s>\</FileVersion>)" )
	}
	if ($extension -eq ".xml"){ # AndroidManifest.xml (Android)
		$c.VNames = @( "(?<p>android:versionName="")(?<v>[^""]*)(?<s>"")" )
		$c.VCodes = @( "(?<p>android:versionCode="")(?<v>[^""]*)(?<s>"")" )
	}
	if ($extension -eq "plist"){ # Info.plist (iOS)
		$c.VNames = @( "(?<p><key>CFBundleShortVersionString<\/key>[\s\v]*<string>)(?<v>[^<]*)(?<s><\/string>)" )
		$c.VCodes = @( "(?<p><key>CFBundleVersion<\/key>[\s\v]*<string>)(?<v>[^<]*)(?<s><\/string>)" )
	}
	if ($extension -eq ".appmanifest" -or $extension -eq ".vsixmanifest"){ # Package.appxmanifest (UWP) or source.extension.vsixmanifest (VSIX)
		$c.VNames = @( "(?<p><Identity[^>]*Version="")(?<v>[^""]*)(?<s>"")" )
	}
	if ($extension -eq ".vdproj"){ # Microsoft Visual Studio Installer Projects 2022 (MSI)
		$c.VNames = @( "(?<p>""ProductVersion"" = ""8:)(?<v>[^""]*)(?<s>"")")
	}
	return $c;
}
# ----------------------------------------------------------------------------
function ShowRxValues {
	param([string]$name, [string]$content, [string[]]$rxs)
	#----------------------------------------------------------
	foreach	($s in $rxs) {
		$rx = New-Object Regex($s)
		$ms = $rx.Matches($content)
		if ($ms.Count -eq 0){
			Write-Host "    $name not found!"
			break
		}
		if ($ms.Count -gt 1){
			Write-Host "    Too many $name matches found!"
			break
		}
		foreach($m in $ms) {
			$p = $m.Groups["p"].Value
			$v = $m.Groups["v"].Value
			Write-Host "    ${name}: $v"
		}
	}
}
# ----------------------------------------------------------------------------
function ShowVersions
{
	param([Config[]]$items)
	#----------------------------------------------------------
	for ($i = 0; $i -lt $items.Length; $i++) {
		$item = $items[$i]
		Write-Host
		Write-Host "  $($item.File)"
		$content = 	[File]::ReadAllText($item.File)
		# Show values.
		ShowRxValues "Version Name" $content $item.VNames
		ShowRxValues "Version Code" $content $item.VCodes
	}
}
# ----------------------------------------------------------------------------
function GetVersion
{
	param([Config]$item);
	#----------------------------------------------------------
	[string]$content = [File]::ReadAllText($item.File)
	# Get version name.
	[Regex]$rxVN = New-Object Regex($item.VNames[0])
	$vnMatch = $rxVN.Match($content);
	if (-not $vnMatch.Success) {
		Write-Host "Version Name not found. Exiting."
	}
	$versionName = $vnMatch.Groups["v"].Value
	Write-Host "    Version Name: $versionName"
	$version = new-Object Version($versionName)
	# If version revision stored separately then...
	if ($item.VCodes.Length -gt 0) {
		[Regex]$rxVC = New-Object Regex($items.VCodes[0])
		$vcMatch = $rxVC.Match($content)
		if (-not $vcMatch.Success) {
			Write-Host "Version Code not found. Exiting."
		}
		$versionCode = [int].Parse($vcMatch.Groups["v"].Value)
		Write-Host "  Version Code: $versionCode"
		$version = new-Object Version("$($version.Major).$($version.Minor).$($version.Build).$versionCode")
	}
	return $version;
}
# ----------------------------------------------------------------------------
function ReplaceRxValues {
	param([string]$name, [string]$content, [string[]]$rxs, [string]$n)
	#----------------------------------------------------------
	foreach	($v in $rxs) {
		$rx = New-Object Regex($v)
		$ms = $rx.Matches($content)
		foreach ($m in $ms) {
			$p = $m.Groups["p"].Value
			$v = $m.Groups["v"].Value
			$s = $m.Groups["s"].Value
			#Write-Host "  Original: $p$v$s"
			#Write-Host "  Replaced: $p$n$s"
			#Write-Host
			$content = $content.Replace("$p$v$s", "$p$n$s")
		}
	}
	return $content;
}
# ----------------------------------------------------------------------------
function SetVersion
{
    param([Version]$newVersion);
	#----------------------------------------------------------
	for ($i = 0; $i -lt $items.Length; $i++) {
		$item = $items[$i]
		Write-Host "  $($item.File)"
		$content = 	[File]::ReadAllText($item.File)
		$fieldCount = 3
		# If version revision stored separately then
		if ($item.VCodes.Length -gt 0) { $fieldCount = 3 }
		# Replace Values.
		$content = ReplaceRxValues "Name" $content $item.VNames $newVersion.ToString($fieldCount)
		$content = ReplaceRxValues "Code" $content $item.VCodes $newVersion.Revision
		# Save file.
		[File]::WriteAllText($item.File, $content)
	}
	Write-Host
	Write-Host "  Version updated to: $newVersion"
}
# ----------------------------------------------------------------------------
function ShowMainMenu {
	$m = "";
	do {
		# Clear screen.
		Clear-Host;
		[Config[]]$items = GetConfigurationFiles
		#$items
		Write-Host
		ShowVersions $items
		[Version]$oldVersion = GetVersion $items[0]
		[Version]$newVersionM = new-Object Version("$($oldVersion.Major).$($oldVersion.Minor).$([Math]::Max(0, $oldVersion.Build - 1)).$([Math]::Max(0, $oldVersion.Revision - 1))")
		[Version]$newVersionP = new-Object Version("$($oldVersion.Major).$($oldVersion.Minor).$($oldVersion.Build + 1).$($oldVersion.Revision + 1)")
		Write-Host
		Write-Host "Current version: $oldVersion"
		# Set certificate types.
		Write-Host
		Write-Host "  1 - Decrement  version to $newVersionM"
		Write-Host "  2 - Set        version to $oldVersion"
		Write-Host "  3 - Increment  version to $newVersionP"
		Write-Host "  4 - Set        version to custom"
		Write-Host
		$m = Read-Host -Prompt "Type option and press ENTER to continue"
		Write-Host
		# Options:
		if ("${m}" -eq "1") {  SetVersion $newVersionM; }
		if ("${m}" -eq "2") {  SetVersion $oldVersion; }
		if ("${m}" -eq "3") {  SetVersion $newVersionP; }
		if ("${m}" -eq "4") {
			Write-Host "Please enter a version number: " -NoNewLine
			$inputVersion = Read-Host
			[Version]$version = $inputVersion
			SetVersion $version;
		}
		Write-Host;
		# If option was choosen.
		IF ("$m" -ne "") {
			#pause;
		}
	} until ("$m" -eq "");
	return $m;
}
# ----------------------------------------------------------------------------
# Execute.
# ----------------------------------------------------------------------------
function GetConfigurationFiles
{
	# First record will be used to identify current version.
	[Config[]]$items = @()
	$items += (GetConfig "Shared\JocysCom.VS.AiCompanion.Shared.csproj")
	$items += (GetConfig "Plugins\Core\JocysCom.VS.AiCompanion.Plugins.Core.csproj")
	$items += (GetConfig "Data\DataClient\JocysCom.VS.AiCompanion.DataClient.csproj")
	$items += (GetConfig "Data\DataFunctions\Properties\AssemblyInfo.cs")
	$items += (GetConfig "Engine\JocysCom.VS.AiCompanion.Engine.csproj")
	$items += (GetConfig "App\JocysCom.VS.AiCompanion.App.csproj")
	$items += (GetConfig "Extension\Properties\AssemblyInfo.cs")
	$items += (GetConfig "Extension\source.extension.vsixmanifest")
	$items += (GetConfig "Setup\Setup\JocysCom.VS.AiCompanion.Setup.vdproj")
	$items += (GetConfig "Setup\CustomActions\JocysCom.VS.AiCompanion.Setup.CustomActions.csproj")
	$items += (GetConfig "Setup\CustomActions\Properties\AssemblyInfo.cs")
	return $items
}
# ----------------------------------------------------------------------------
Write-Host;
ConfigureSettings
ShowMainMenu;