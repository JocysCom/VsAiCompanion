# Add line to Release post-build event:
# PowerShell -NoProfile -ExecutionPolicy Bypass -File "$(ProjectDir)\Sign.ps1" "$(TargetPath)"

param (
    [Parameter(Mandatory = $true, Position = 0)]
    [string] $TargetPath
)

$modulePath = "d:\_Backup\Configuration\SSL\Tools\app_signModule.ps1"
if (!(Test-Path $modulePath)) {
	return
}

# Get all Plug and Play devices that match the search criteria
$tokenPresent = Get-PnpDevice | Where-Object {$_.FriendlyName -like "*SafeNet eToken*"} | Select-Object -First 1
# Check if the USB token was found
if (!($tokenPresent)) {
    Write-Host "The SafeNet eToken is NOT detected."
	return
}

function Main {
	Import-Module $modulePath -Force
	[string]$appName = "Jocys.com VS AI Companion Engine"
	[string]$appLink = "https://www.jocys.com"
	ProcessFile $appName $appLink $TargetPath
}

#==============================================================
# Ensure that only one instance of this script can run.
# Other instances wait for the previous one to complete.
#--------------------------------------------------------------
# Use the full script name with path as the lock name.
$scriptName = $MyInvocation.MyCommand.Name
$mutexName = "Global\$scriptName"
$mutexCreated = $false
$mutex = New-Object System.Threading.Mutex($true, $mutexName, [ref] $mutexCreated)
if (-not $mutexCreated) {
       
    Write-Host "Another $scriptName instance is running. Waiting..."
    $mutex.WaitOne() > $null  # Wait indefinitely for the mutex
}
try {
    # Main script logic goes here...
    Start-Sleep -Seconds 2
	Main
    Start-Sleep -Seconds 2
}
finally {
    # Release the mutex so that other instances can proceed.
    $mutex.ReleaseMutex()
    $mutex.Dispose()
}
#==============================================================
