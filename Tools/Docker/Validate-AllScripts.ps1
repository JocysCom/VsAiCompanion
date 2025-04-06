<#
.SYNOPSIS
    Validates all PowerShell scripts (*.ps1) in the current directory using PSScriptAnalyzer.
.DESCRIPTION
    Retrieves all .ps1 files in the script's directory and runs Invoke-ScriptAnalyzer
    on each file, excluding the PSAvoidGlobalVars and PSReviewUnusedParameter rules
    as per project conventions.
.NOTES
    Ensure PSScriptAnalyzer module is installed: Install-Module -Name PSScriptAnalyzer -Scope CurrentUser
.EXAMPLE
    .\Validate-AllScripts.ps1
#>

# Set Information Preference to show Write-Information messages by default
$InformationPreference = 'Continue'

# Get the directory where the script is located
$scriptDir = $PSScriptRoot

Write-Information "Starting script validation in directory: $scriptDir"

# Get all PowerShell script files in the directory
$scriptFiles = Get-ChildItem -Path $scriptDir -Filter *.ps1 -File

if (-not $scriptFiles) {
    Write-Warning "No PowerShell script files found in $scriptDir."
    exit 0
}

Write-Information "Found $($scriptFiles.Count) script(s) to validate."

# Define the rules to exclude
$excludedRules = @(
    'PSAvoidGlobalVars',
    'PSReviewUnusedParameter'
)

# Variable to track if any errors were found
$anyErrorsFound = $false

# Loop through each script file and run the analyzer
foreach ($file in $scriptFiles) {
    Write-Information "--------------------------------------------------"
    Write-Information "Validating: $($file.FullName)"
    Write-Information "--------------------------------------------------"
    try {
        $results = Invoke-ScriptAnalyzer -Path $file.FullName -ExcludeRule $excludedRules -ErrorAction Stop
        if ($results) {
            Write-Warning "Issues found in $($file.Name):"
            $results | Format-Table -AutoSize
            $anyErrorsFound = $true
        } else {
            Write-Information "No issues found in $($file.Name)."
        }
    } catch {
        Write-Error "Failed to analyze $($file.Name): $_"
        $anyErrorsFound = $true
    }
    Write-Information "" # Add a blank line for readability
}

Write-Information "=================================================="
if ($anyErrorsFound) {
    Write-Warning "Validation complete. Some issues were found."
} else {
    Write-Information "Validation complete. No issues found."
}
Write-Information "=================================================="

# Optional: Pause at the end if running interactively
# if ($Host.Name -eq 'ConsoleHost') {
#     Read-Host -Prompt "Press Enter to exit"
# }
