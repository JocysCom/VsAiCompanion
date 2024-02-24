param (
    # Optional. Use comment for console.
    [Parameter(Mandatory = $false)]
    [string] $LogPrefix = ""
)

..\..\Resources\ZipFiles.ps1 "$($PSScriptRoot)\FineTuning" "$($PSScriptRoot)\FineTuning.zip" -LogPrefix $LogPrefix

