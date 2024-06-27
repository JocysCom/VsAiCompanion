# Add stronger signature.
# Get the path of the directory where the script is located
$scriptDirectory = $PSScriptRoot
PowerShell -NoProfile -ExecutionPolicy Bypass -File "..\Engine\Sign.ps1" "$scriptDirectory\Microsoft.Data.Connection\net48\Microsoft.Data.ConnectionUI.dll"
PowerShell -NoProfile -ExecutionPolicy Bypass -File "..\Engine\Sign.ps1" "$scriptDirectory\Microsoft.Data.Connection\net48\Microsoft.Data.ConnectionUI.Dialog.dll"