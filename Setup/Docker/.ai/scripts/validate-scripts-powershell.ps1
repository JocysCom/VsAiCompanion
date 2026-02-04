<#
.SYNOPSIS
	Validates PowerShell scripts (*.ps1) using PSScriptAnalyzer.
.DESCRIPTION
	Runs Invoke-ScriptAnalyzer on PowerShell scripts, excluding project-specific rules
	(PSAvoidGlobalVars, PSReviewUnusedParameter, PSAvoidUsingWriteHost, PSUseSingularNouns).
	Also auto-fixes formatting issues before validation.
.PARAMETER Path
	Optional. Directory path to validate scripts in. Defaults to the Docker setup directory
	(parent of .ai folder).
.PARAMETER FilePattern
	Optional. File pattern to match. Defaults to '*.ps1'.
.EXAMPLE
	.\.ai\scripts\validate-scripts-powershell.ps1
	Validates all *.ps1 files in the Docker setup directory.
.EXAMPLE
	.\.ai\scripts\validate-scripts-powershell.ps1 -FilePattern "Setup_App_*.ps1"
	Validates only Setup_App_*.ps1 files.
.EXAMPLE
	.\.ai\scripts\validate-scripts-powershell.ps1 -Path "." -FilePattern "Setup_Core_1_WSL2.ps1"
	Validates a specific script file.
.NOTES
	Ensure PSScriptAnalyzer module is installed: Install-Module -Name PSScriptAnalyzer -Scope CurrentUser
#>

param(
	[Parameter(Mandatory = $false)]
	[string]$Path,

	[Parameter(Mandatory = $false)]
	[string]$FilePattern = '*.ps1'
)

# Get the directory where the script is located
$scriptDir = $PSScriptRoot

# Default to the Docker setup directory (two levels up from .ai/scripts/)
if (-not $Path) {
	$Path = (Resolve-Path (Join-Path $scriptDir "..\..")).Path
}

# Resolve to absolute path if relative
if (-not [System.IO.Path]::IsPathRooted($Path)) {
	$Path = (Resolve-Path $Path).Path
}

Write-Host "Starting script validation in directory: $Path (Pattern: $FilePattern)"

# Get all PowerShell script files in the directory matching pattern
$scriptFiles = Get-ChildItem -Path $Path -Filter $FilePattern -File

if (-not $scriptFiles) {
	Write-Warning "No PowerShell script files found in $Path matching '$FilePattern'."
	exit 0
}

Write-Host "Found $($scriptFiles.Count) script(s) to validate."

# Define the rules to exclude
$excludedRules = @(
	'PSAvoidGlobalVars',
	'PSReviewUnusedParameter',
	'PSAvoidUsingWriteHost',
	'PSUseSingularNouns'
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
