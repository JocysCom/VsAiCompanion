# Script: Update-AgentInstructions.ps1
# Location: .ai/Update-AgentInstructions.ps1
# Description: Updates AI agent instruction files from master copies in the .ai folder,
#              processing only files matching '*instructions.md'.
#              Supports both multiple-file agents (CLINE, ROO CODE) and
#              single-file agents (GitHub CoPilot, OpenAI Codex).
# Options for Mode: ALL (update all), AUTO (default; update only agents with instruction files),
#                  or specific agent names: CLINE, ROO CODE, GitHub CoPilot, OpenAI Codex.
#              Adds 'AUTO' mode (default) to update only agents with instructions present.

param(
    [Parameter(Position = 0)]
    [string]$Mode
)

# Fallback argument handling: if Mode not set, use first bare argument
if (-not $Mode -and $args.Count -gt 0) {
    $Mode = $args[0]
}

# Strict mode
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"


# Function to check if instruction files exist in a directory
function Test-HasInstructionFiles {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [string]$Filter = '*instructions.md'
    )
    
    if (Test-Path $Path -PathType Container) {
        $files = Get-ChildItem $Path -Filter $Filter -File -ErrorAction SilentlyContinue
        return ($null -ne $files -and $files.Count -gt 0)
    }
    return $false
}

# Function to pause at the end (unless -NoWait is specified)
function Invoke-Pause {
    Write-Host "Pausing for 2 seconds..."
    Start-Sleep -Seconds 2
}

# Function to compare content and write file if different
function Test-AndWriteFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$TargetPath,
        [Parameter(Mandatory = $true)]
        [string]$NewContent,
        [string]$FileDescription = "File" # Optional description for messages
    )

    try {
        $TargetDir = Split-Path -Path $TargetPath -Parent
        if (-not (Test-Path -Path $TargetDir)) {
            Write-Host "Creating directory: $TargetDir"
            New-Item -ItemType Directory -Force -Path $TargetDir | Out-Null
        }

        # Ensure the new content ends with a newline, typical for markdown files
        $ContentToWrite = $NewContent.TrimEnd() + "`r`n"

        if (Test-Path -Path $TargetPath) {
            $existingContent = Get-Content -Path $TargetPath -Raw -Encoding UTF8 -ErrorAction SilentlyContinue
            
            # Direct comparison as requested by user
            if ($existingContent -eq $ContentToWrite) {
                $relative = $TargetPath.Substring($repoRoot.Length + 1)
                $prefix = if ($FileDescription) { "$($FileDescription): " } else { "" }
                Write-Host "${prefix}Up-to-date: $relative"
                return $false
            }
            else {
                Set-Content -Path $TargetPath -Value $ContentToWrite -Encoding UTF8 -Force
                $relative = $TargetPath.Substring($repoRoot.Length + 1)
                $prefix = if ($FileDescription) { "$($FileDescription): " } else { "" }
                Write-Host "${prefix}Updated: $relative"
                return $true
            }
        }
        else {
            Set-Content -Path $TargetPath -Value $ContentToWrite -Encoding UTF8 -Force
            $relative = $TargetPath.Substring($repoRoot.Length + 1)
            $prefix = if ($FileDescription) { "$($FileDescription): " } else { "" }
            Write-Host "${prefix}Created: $relative"
            return $true
        }
    }
    catch {
        Write-Error "Error processing file '$TargetPath': $($_.Exception.Message)"
        throw # Re-throw to be caught by main try-catch
    }
}

# Function to update agents that use multiple separate instruction files
function Update-MultipleFileAgent {
    param(
        [Parameter(Mandatory = $true)]
        [string]$AgentName,
        [Parameter(Mandatory = $true)]
        [string]$TargetDirectory,
        [Parameter(Mandatory = $true)]
        [System.IO.FileSystemInfo[]]$SourceFiles,
        [Parameter(Mandatory = $true)]
        [string]$RepoRoot
    )

    Write-Host "`r`n--- Updating $AgentName Instructions ---"
    $targetDir = Join-Path $RepoRoot $TargetDirectory
    
    foreach ($sourceFile in $SourceFiles) {
        $targetFile = Join-Path $targetDir $sourceFile.Name
        $sourceContent = Get-Content $sourceFile.FullName -Raw -Encoding UTF8
        if ([string]::IsNullOrWhiteSpace($sourceContent)) {
            Write-Warning "Skipping empty file: $($sourceFile.Name)"
            continue
        }
        [void](Test-AndWriteFile -TargetPath $targetFile -NewContent $sourceContent -FileDescription "")
    }
}

# Function to update agents that use a single combined instruction file
function Update-SingleFileAgent {
    param(
        [Parameter(Mandatory = $true)]
        [string]$AgentName,
        [Parameter(Mandatory = $true)]
        [string]$TargetFilePath,
        [Parameter(Mandatory = $true)]
        [System.IO.FileSystemInfo[]]$SourceFiles,
        [Parameter(Mandatory = $true)]
        [string]$RepoRoot
    )

    Write-Host "`r`n--- Updating $AgentName Instructions ---"
    $targetFile = Join-Path $RepoRoot $TargetFilePath
    
    $allInstructionsContent = New-Object System.Text.StringBuilder
    $firstFile = $true

    foreach ($sourceFile in $SourceFiles) {
        $sourceContent = Get-Content $sourceFile.FullName -Raw -Encoding UTF8
        if ([string]::IsNullOrWhiteSpace($sourceContent)) {
            Write-Warning "Skipping empty file: $($sourceFile.Name)"
            continue
        }

        if (-not $firstFile) {
            [void]$allInstructionsContent.AppendLine("") # Add a blank line separator before the next section
        }
        
        [void]$allInstructionsContent.AppendLine("==== START OF INSTRUCTIONS FROM: $($sourceFile.Name) ====")
        [void]$allInstructionsContent.AppendLine("") # Blank line after START marker
        
        [void]$allInstructionsContent.AppendLine("# Instructions from: $($sourceFile.Name)")
        [void]$allInstructionsContent.AppendLine("") # Blank line after header
        
        [void]$allInstructionsContent.AppendLine($sourceContent.Trim())
        
        [void]$allInstructionsContent.AppendLine("") # Blank line before END marker
        [void]$allInstructionsContent.AppendLine("==== END OF INSTRUCTIONS FROM: $($sourceFile.Name) ====")
        
        $firstFile = $false # Set to false after processing the first file
    }

    # No need to remove leading newline with this new structure as each block is self-contained.
    # The first block will start directly with "==== START..."
    $finalContent = $allInstructionsContent.ToString()
    [void](Test-AndWriteFile -TargetPath $targetFile -NewContent $finalContent -FileDescription "")
}

# --- Main Script ---
Clear-Host
$scriptDir = $PSScriptRoot # Directory where the script itself is located (.ai)
$repoRoot = Join-Path -Path $scriptDir -ChildPath ".." | Resolve-Path # Absolute path to the repository root

# Discover source files matching *instructions.md in the .ai folder
[System.IO.FileSystemInfo[]]$sourceInstructionFiles = Get-ChildItem -Path $scriptDir -Filter "*instructions.md" -File
if ($null -eq $sourceInstructionFiles -or $sourceInstructionFiles.Count -eq 0) {
    Write-Warning "No '*instructions.md' files found in '$scriptDir'. Nothing to process."
    exit 0
}
Write-Host "Found the following source instruction files in '$scriptDir':"
$sourceInstructionFiles | ForEach-Object { Write-Host "- $($_.Name)" }

# Mode parameter handling: if 'ALL' or 'AUTO', skip interactive prompt
if ($Mode -eq 'ALL') {
    Write-Host "Selected: ALL (parameter mode)"
    $updateCline = $true
    $updateCopilot = $true
    $updateRooCode = $true
    $updateCodex = $true
}
elseif ($Mode -eq 'AUTO') {
    Write-Host "Selected: AUTO (parameter mode)"
    # Determine available agents based on instruction files
    $updateCline = Test-HasInstructionFiles -Path (Join-Path $repoRoot '.clinerules')
    $updateRooCode = Test-HasInstructionFiles -Path (Join-Path $repoRoot '.roo\rules')
    $updateCopilot = Test-Path (Join-Path $repoRoot '.github\copilot-instructions.md') -PathType Leaf
    $updateCodex = Test-Path (Join-Path $repoRoot 'AGENTS.md') -PathType Leaf
    Write-Host "Agents to update based on available instruction files:"
    if ($updateCline) { Write-Host "- CLINE" }
    if ($updateRooCode) { Write-Host "- ROO CODE" }
    if ($updateCopilot) { Write-Host "- GitHub CoPilot" }
    if ($updateCodex) { Write-Host "- OpenAI Codex" }
}
elseif ($Mode -and $Mode -ne '') {
    # Specific agent mode (e.g., CLINE, "ROO CODE", etc.)
    $updateCline = ($Mode -eq 'CLINE')
    $updateCopilot = ($Mode -eq 'GitHub CoPilot')
    $updateRooCode = ($Mode -eq 'ROO CODE')
    $updateCodex = ($Mode -eq 'OpenAI Codex')
    Write-Host "Selected: $Mode (parameter mode)"
}
else {
    # User prompt for agent selection
    # Detect available agents for interactive menu
    $hasCline = Test-HasInstructionFiles -Path (Join-Path $repoRoot '.clinerules')
    $hasRooCode = Test-HasInstructionFiles -Path (Join-Path $repoRoot '.roo\rules')
    $hasCopilot = Test-Path (Join-Path $repoRoot '.github\copilot-instructions.md') -PathType Leaf
    $hasCodex = Test-Path (Join-Path $repoRoot 'AGENTS.md') -PathType Leaf
    
    Write-Host "`r`nDetected AI agents with instruction files:"
    if ($hasCline) { Write-Host "- CLINE" }
    if ($hasRooCode) { Write-Host "- ROO CODE" }
    if ($hasCopilot) { Write-Host "- GitHub CoPilot" }
    if ($hasCodex) { Write-Host "- OpenAI Codex" }
    Write-Host ""
    Write-Host "=============================================================="
    Write-Host "Select Agent Instruction Set to Update"
    Write-Host "--------------------------------------------------------------"
    Write-Host "1. AUTO           - Update only agents with instruction files (default)"
    Write-Host "2. ALL            - Update instructions for all AI agents"
    Write-Host "3. CLINE          - Update instructions for CLINE"
    Write-Host "4. ROO CODE       - Update instructions for ROO CODE"
    Write-Host "5. GitHub CoPilot - Update instructions for GitHub CoPilot"
    Write-Host "6. OpenAI Codex   - Update instructions for OpenAI Codex"
    Write-Host "0. Exit"
    Write-Host "=============================================================="
    $selection = Read-Host "Enter the number of your choice (0-6)"
    
    # Initialize flags
    $updateCline = $false
    $updateCopilot = $false
    $updateRooCode = $false
    $updateCodex = $false
    
    switch ($selection) {
        '1' {
            # AUTO mode - same logic as above
            $updateCline = Test-HasInstructionFiles -Path (Join-Path $repoRoot '.clinerules')
            $updateRooCode = Test-HasInstructionFiles -Path (Join-Path $repoRoot '.roo\rules')
            $updateCopilot = Test-Path (Join-Path $repoRoot '.github\copilot-instructions.md') -PathType Leaf
            $updateCodex = Test-Path (Join-Path $repoRoot 'AGENTS.md') -PathType Leaf
            Write-Host "Selected: AUTO"
        }
        '2' {
            $updateCline = $true
            $updateCopilot = $true
            $updateRooCode = $true
            $updateCodex = $true
            Write-Host "Selected: ALL"
        }
        '3' { $updateCline = $true; Write-Host "Selected: CLINE" }
        '4' { $updateRooCode = $true; Write-Host "Selected: ROO CODE" }
        '5' { $updateCopilot = $true; Write-Host "Selected: GitHub CoPilot" }
        '6' { $updateCodex = $true; Write-Host "Selected: OpenAI Codex" }
        '0' { Write-Host "Operation cancelled by user."; exit 0 }
        default { throw "Invalid selection. Exiting." }
    }
}

# --- Multiple-File Agent Updates ---
if ($updateCline) {
    Update-MultipleFileAgent -AgentName "CLINE" -TargetDirectory ".clinerules" -SourceFiles $sourceInstructionFiles -RepoRoot $repoRoot
}

if ($updateRooCode) {
    Update-MultipleFileAgent -AgentName "ROO CODE" -TargetDirectory ".roo\rules" -SourceFiles $sourceInstructionFiles -RepoRoot $repoRoot
}

# --- Single-File Agent Updates ---
if ($updateCopilot) {
    Update-SingleFileAgent -AgentName "GitHub CoPilot" -TargetFilePath ".github\copilot-instructions.md" -SourceFiles $sourceInstructionFiles -RepoRoot $repoRoot
}

if ($updateCodex) {
    Update-SingleFileAgent -AgentName "OpenAI Codex" -TargetFilePath "AGENTS.md" -SourceFiles $sourceInstructionFiles -RepoRoot $repoRoot
}

Write-Host "`r`nAll selected operations completed successfully."

Invoke-Pause
