param (
    # Optional. Use comment for console.
    [Parameter(Mandatory = $false)]
    [string] $LogPrefix = ""
)

function ZipFiles {
	param ([string] $name)

	# Remove temp folder from source.
	$tmpPath = "$($PSScriptRoot)\$name\Temp"
	if (Test-Path $tmpPath -PathType Container) {
		# Delete the folder
		Remove-Item -Path $tmpPath -Recurse -Force
	}

	# Remove target zip file.
	$zipFile = "$($PSScriptRoot)\$name.zip"
	#if ([System.IO.File]::Exists($zipFile)) {
    # [System.IO.File]::Delete($zipFile)
	#}

	..\..\Resources\ZipFiles.ps1 "$($PSScriptRoot)\$name" $zipFile -excludePattern "Temp*" -LogPrefix $LogPrefix -IgnoreEmptyFolders $true
}

ZipFiles "Settings"
ZipFiles "Settings.CompanyName"

