#--------------------------------------------------------------
# Install Required modules.
#--------------------------------------------------------------

function Ensure-Module {
	param ([string]$name)
	# Install Required PowerShell Module
	if (-not (Get-Module -ListAvailable -Name $name)) {
		Write-Output "'$name' module not found. Installing..."
		Install-Module -Name $name -AllowClobber -Scope CurrentUser -Force
	}
	# Import Required PowerShell Module
	if (-not (Get-Module -Name $name)) {
		Write-Output "Loading '$name' module into the current PowerShell session..."
		Import-Module $name
	}
}

Ensure-Module "Microsoft.Graph.Intune"

# Connect to Microsoft Graph 
# Requires the Microsoft.Graph.Intune module to be installed
Connect-MSGraph -ForceInteractive

# Get all Apps and their id
$Apps = Get-DeviceAppManagement_MobileApps 
$Apps | select displayName, id

# Get Apps, their size in MB, and their id. Filter on App Name
$Apps = Get-DeviceAppManagement_MobileApps -Filter "contains(displayName, '100 MB Single File')"
$Apps | select displayName, @{Label="Size in MB";Expression={[math]::Round(($_.size/1MB),2)}}, id