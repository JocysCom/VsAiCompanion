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
.EXAMPLE
    .\Validate-AllScripts.ps1
#>

# Set Information Preference to show Write-Information messages by default
$InformationPreference = 'Continue'

# Get the directory where the script is located
$scriptDir = $PSScriptRoot

Write-Information "Starting script validation in directory: $scriptDir" 6>&1

# Get all PowerShell script files in the directory
$scriptFiles = Get-ChildItem -Path $scriptDir -Filter *.ps1 -File

if (-not $scriptFiles) {
    Write-Warning "No PowerShell script files found in $scriptDir."
    exit 0
}

Write-Information "Found $($scriptFiles.Count) script(s) to validate." 6>&1

# Define the rules to exclude
$excludedRules = @(
    'PSAvoidGlobalVars',
    'PSReviewUnusedParameter'
)

# Variable to track if any errors were found
$anyErrorsFound = $false

# Loop through each script file and run the analyzer
foreach ($file in $scriptFiles) {
    Write-Information "--------------------------------------------------" 6>&1
    Write-Information "Validating: $($file.FullName)" 6>&1
    Write-Information "--------------------------------------------------" 6>&1
    try {
        # Redirect streams 6(Info), 3(Warn) to 1(Success) for capture
        $results = Invoke-ScriptAnalyzer -Path $file.FullName -ExcludeRule $excludedRules -ErrorAction Stop 6>&1 3>&1
        if ($results) {
            Write-Warning "Issues found in $($file.Name):" 3>&1 # Redirect warning
            $results | Format-Table -AutoSize # This already goes to success stream
            $anyErrorsFound = $true
        } else {
            Write-Information "No issues found in $($file.Name)." 6>&1 # Redirect info
        }
    } catch {
        # Redirect error message to success stream as well for capture
        Write-Error "Failed to analyze $($file.Name): $_" 2>&1
        $anyErrorsFound = $true
    }
    Write-Information "" 6>&1 # Add a blank line for readability (redirected)
}

Write-Information "==================================================" 6>&1
if ($anyErrorsFound) {
    Write-Warning "Validation complete. Some issues were found." 3>&1
} else {
    Write-Information "Validation complete. No issues found." 6>&1
}
Write-Information "==================================================" 6>&1

# Optional: Pause at the end if running interactively
# if ($Host.Name -eq 'ConsoleHost') {
#     Read-Host -Prompt "Press Enter to exit"
# }
