using namespace System
using namespace System.IO
# ----------------------------------------------------------------------------
# Get current command path.
[string]$current = $MyInvocation.MyCommand.Path;
# Get calling command path.
[string]$calling = @(Get-PSCallStack)[1].InvocationInfo.MyCommand.Path;
# If executed directly then...
if ($calling -ne "") {
    $current = $calling;
}
# ----------------------------------------------------------------------------
[FileInfo]$file = New-Object FileInfo($current);
# Set public parameters.
$global:scriptName = $file.Basename;
$global:scriptPath = $file.Directory.FullName;
# Change current directory.
Write-Host "Script Path:    $scriptPath";
[Environment]::CurrentDirectory = $scriptPath;
Set-Location $scriptPath;
#------------------------------------------------------------------------------
# List of files to sign.
$files = @(
    "..\App\bin\Release\publish\JocysCom.VS.AiCompanion.App.exe",
    "..\Extension\bin\Release\JocysCom.VS.AiCompanion.Extension.vsix"
)
#------------------------------------------------------------------------------
function Sign-File {
    param (
        [string]$FilePath,
        [string]$PfxPath = "D:\_Backup\Configuration\SSL\Code Sign - Evaldas Jocys\2020\Evaldas_Jocys.pfx",
        [string]$Description = "Jocys.com VS AI Companion",
        [string]$DescriptionUrl = "https://www.jocys.com",
        [string]$TimestampUrl = "http://timestamp.comodoca.com"
    )
    if (-not [File]::Exists($FilePath)) {
        Write-Host "File '$FilePath' not exist. Skipping."
        return
    }
    Write-Host $signToolPath
    Write-Host
    $arguments = @(
        "sign",
        "/f", "`"$PfxPath`"",
        "/d", "`"$Description`"",
        "/du", "`"$DescriptionUrl`"",
        "/fd", "sha256",
        "/td", "sha256",
        "/tr", "`"$TimestampUrl`"",
        "/v",
        "`"$FilePath`""
    )
    & $signToolPath $arguments
    #/sha1 <h>   Specify the SHA1 hash of the signing cert.
    #/sha1 cc747560b7f9d3641f2211a03c698e820f06efd9 
}
#------------------------------------------------------------------------------
function GetSignToolExePath() {
    # Paths to look for executable.
    $ps = @(
        "Tools\signtool.exe"
        "${env:ProgramFiles(x86)}\Windows Kits\10\App Certification Kit\signtool.exe"
        "${env:ProgramFiles(x86)}\Microsoft SDKs\ClickOnce\SignTool\signtool.exe"
        "${env:ProgramFiles(x86)}\Windows Kits\10\bin\x86\signtool.exe",
        "${env:ProgramFiles}\Windows Kits\10\bin\x86\signtool.exe"
    );
    return GetExistingPath $ps;
}
#------------------------------------------------------------------------------
function GetVsixSignToolExePath() {
    # Paths to look for executable.
    $ps = @(
        "Tools\vsixsigntool.exe"
    );
    return GetExistingPath $ps;
}
#------------------------------------------------------------------------------
function GetExistingPath() {
    param([string[]]$ps);
    foreach ($p in $ps) {
        if ([File]::Exists($p)) {
            $path = $p;
            break;
        }
    }
    # Fix dot notations.
    $combined = [Path]::GetFullPath($path);
    return $combined;
}
#------------------------------------------------------------------------------
function Remove-SignatureIfExists {
    param ([string]$FilePath)
    $verifyArguments = @("verify", "/pa", "`"$FilePath`"")
    try {
        $result = & $signToolPath $verifyArguments 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Host "Signature found, removing..."
            $removeArguments = @("remove", "/s", "`"$FilePath`"")
            & $signToolPath $removeArguments
            Write-Host "Signature removed."
        }
        else {
            #Write-Host "No signature found."
        }
    }
    catch {
        Write-Host "Error occurred while verifying the signature: $_"
    }
}
#------------------------------------------------------------------------------
foreach ($source in $files) {
    $name = [Path]::GetFileName($source);
    $source = [Path]::GetFullPath("$scriptPath\$source")
    $target = "Files\$name";
    Write-Host "Signing file: $source"
    $ext = [Path]::GetExtension($source);
    [File]::Copy($source, $target, $true)
    if ($ext -eq ".vsix") {
        $signToolPath = GetVsixSignToolExePath
    }
    else {
        $signToolPath = GetSignToolExePath
    }
    if (-not $signToolPath) {
        Write-Host "Sign Tool for *.$ext files not found. Please ensure it is installed."
        continue;
    }
    #Remove-SignatureIfExists -FilePath $target
    Sign-File -FilePath $destination
}
Write-Host
Pause
