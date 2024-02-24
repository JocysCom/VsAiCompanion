param (
    # Optional. Use comment for console.
    [Parameter(Mandatory = $false)]
    [string] $LogPrefix = ""
)

..\..\Resources\ZipFiles.ps1 "$($PSScriptRoot)\Settings" "$($PSScriptRoot)\Settings.zip" -LogPrefix $LogPrefix

