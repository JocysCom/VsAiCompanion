# Add line to Release post-build event:
# PowerShell -NoProfile -ExecutionPolicy Bypass -File "$(ProjectDir)\Sign.ps1" "$(TargetPath)"

param (
    [Parameter(Mandatory = $true, Position = 0)]
    [string] $TargetPath
)

$modulePath = "d:\_Backup\Configuration\SSL\Tools\app_signModule.ps1"
if (!(Test-Path $modulePath)) {
	return 0
}

function Main {
	Import-Module $modulePath -Force
	[string]$appName = "Jocys.com VS AI Companion Plugins Core"
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
	Main
}
finally {
    # Release the mutex so that other instances can proceed.
    $mutex.ReleaseMutex()
    $mutex.Dispose()
}
#==============================================================
