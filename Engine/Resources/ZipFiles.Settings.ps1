param (
    # Optional. Use comment for console.
    [Parameter(Mandatory = $false)]
    [string] $LogPrefix = ""
)

function ZipFiles {
	param ([string] $name)

	# Remove temp folder from source.
	$tmpPath = "$($PSScriptRoot)\$name\Temp"
	if ([System.IO.Directory]::Exists($tmpPath)) {
		# Delete the folder
		[System.IO.Directory]::Delete($tmpPath, $true)
	}

	# Remove target zip file.
	$zipFile = "$($PSScriptRoot)\$name.zip"
	#if ([System.IO.File]::Exists($zipFile)) {
    # [System.IO.File]::Delete($zipFile)
	#}

	..\..\Resources\ZipFiles.ps1 "$($PSScriptRoot)\$name" $zipFile -LogPrefix $LogPrefix
}

ZipFiles "Settings"
ZipFiles "Settings.CompanyName"

