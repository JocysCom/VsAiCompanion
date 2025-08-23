<#
.SYNOPSIS
	Validates all PowerShell scripts (*.ps1) in the current directory using PSScriptAnalyzer.
.DESCRIPTION
	Retrieves all .ps1 files in the script's directory and runs Invoke-ScriptAnalyzer
	on each file, excluding the PSAvoidGlobalVars and PSReviewUnusedParameter rules
	as per project conventions.
	The Information (6) and Error (2) streams are redirected to the Success (1) output stream, making the messages visible to the AI agent.
.NOTES
	Ensure PSScriptAnalyzer module is installed: Install-Module -Name PSScriptAnalyzer -Scope CurrentUser
#>

param(
	[Parameter(Mandatory = $false)]
	[string]$FilePattern = '*.ps1'
)

# Set Information Preference (commented out as Write-Host is used now)
# $InformationPreference = 'Continue'

# Get the directory where the script is located
$scriptDir = $PSScriptRoot

Write-Host "Starting script validation in directory: $scriptDir (Pattern: $FilePattern)"

# Get all PowerShell script files in the directory matching pattern
$scriptFiles = Get-ChildItem -Path $scriptDir -Filter $FilePattern -File

if (-not $scriptFiles) {
	Write-Warning "No PowerShell script files found in $scriptDir."
	exit 0
}

Write-Host "Found $($scriptFiles.Count) script(s) to validate."

# Define the rules to exclude
$excludedRules = @(
	'PSAvoidGlobalVars',
	'PSReviewUnusedParameter',
	'PSAvoidUsingWriteHost'
)

$formattRules = @(
	'PSAvoidTrailingWhitespace',
	'PSUseConsistentWhitespace',
	'PSUseConsistentIndentation',
	'PSPlaceOpenBrace',
	'PSPlaceCloseBrace',
	'AlignAssignmentStatement'
)

# Variable to track if any errors were found
$anyErrorsFound = $false

# Loop through each script file and run the analyzer
foreach ($file in $scriptFiles) {
	Write-Host "--------------------------------------------------"
	Write-Host "Validating: $($file.FullName)"
	Write-Host "--------------------------------------------------"
	try {
		# Fix code formatting first.
		$results = Invoke-ScriptAnalyzer -Path $file.FullName -IncludeRule $formattRules -Fix -ErrorAction Stop 6>&1 3>&1
		# Redirect streams 3(Warn) to 1(Success) for capture (Info stream 6 removed)
		$results = Invoke-ScriptAnalyzer -Path $file.FullName -ExcludeRule $excludedRules -ErrorAction Stop 6>&1 3>&1
		if ($results) {
			Write-Warning "Issues found in $($file.Name):" 3>&1 # Redirect warning
			$results | Format-Table -AutoSize # This already goes to success stream
			$anyErrorsFound = $true
		}
		else {
			Write-Host "No issues found in $($file.Name)." # Redirect info removed
		}
	}
	catch {
		# Redirect error message to success stream as well for capture
		Write-Error "Failed to analyze $($file.Name): $_" 2>&1
		$anyErrorsFound = $true
	}
	Write-Host "" # Add a blank line for readability (redirected removed)
}

Write-Host "=================================================="
if ($anyErrorsFound) {
	Write-Warning "Validation complete. Some issues were found." 3>&1
}
else {
	Write-Host "Validation complete. No issues found."
}
Write-Host "=================================================="

# Optional: Pause at the end if running interactively
# if ($Host.Name -eq 'ConsoleHost') {
#     Read-Host -Prompt "Press Enter to exit"
# }
