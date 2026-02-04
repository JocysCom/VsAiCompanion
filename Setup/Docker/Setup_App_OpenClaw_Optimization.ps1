################################################################################
# File         : Setup_App_OpenClaw_Optimization.ps1
# Description  : Interactive wizard to apply token optimization recommendations
#                to OpenClaw to reduce AI costs by up to 97%.
#                Based on the OpenClaw Token Optimization Guide by Matt Ganzak.
#
# Optimizations:
#   1. Session Initialization - Load only essential context (8KB vs 50KB)
#   2. Model Routing - Haiku default, Sonnet/Opus for complex tasks only
#   3. Heartbeat - Free local LLM (Ollama) or cheapest API model
#   4. Prompt Caching - 90% token discount on reused content
#   5. Rate Limits - Prevent runaway automation from burning tokens
#   6. Workspace Templates - Lean SOUL.md, USER.md files
#
# Expected Savings:
#   - Daily: $2-3 → $0.10
#   - Monthly: $70-90 → $3-5
#   - Yearly: $800+ → $40-60
#
# Usage        : Run after Setup_App_OpenClaw.ps1 to optimize costs.
################################################################################

using namespace System
using namespace System.IO

# Dot-source the necessary helper function files.
. "$PSScriptRoot\Setup_Helper_CoreFunctions.ps1"
. "$PSScriptRoot\Setup_Helper_WSLFunctions.ps1"

# Ensure the script working directory is set.
Set-ScriptLocation

#==============================================================================
# Global Configuration
#==============================================================================

$global:appName = "OpenClaw"
$global:wslDistroName = "OpenClaw-WSL"
$global:openclawConfigPath = "~/.openclaw"
$global:openclawConfigFile = "~/.openclaw/openclaw.json"
$global:workspacePath = "~/workspace"

# Ollama configuration
$global:ollamaModel = "llama3.2:3b"
$global:ollamaEndpoint = "http://localhost:11434"

# Model configuration
$global:defaultModel = "anthropic/claude-haiku-4-5"
$global:complexModel = "anthropic/claude-sonnet-4-5"
$global:criticalModel = "anthropic/claude-opus-4"

# Heartbeat configuration
$global:heartbeatInterval = "1h"
$global:heartbeatPrompt = "Check: Any blockers, opportunities, or progress updates needed?"

# Rate limits
$global:apiCallDelay = 5
$global:webSearchDelay = 10
$global:maxSearchesPerBatch = 5
$global:batchBreakMinutes = 2
$global:dailyBudget = 5
$global:monthlyBudget = 200
$global:budgetWarningPercent = 75

# Wizard state - tracks user selections
$global:wizardConfig = @{
    UseHaikuDefault        = $false
    HeartbeatMode          = "unchanged"
    EnableCaching          = $false
    EnableRateLimits       = $false
    CreateWorkspaceFiles   = $false
    InstallOllama          = $false
}

#==============================================================================
# Function: Test-WSLDistroExists
#==============================================================================
<#
.SYNOPSIS
    Checks if a WSL distro exists by name.
.DESCRIPTION
    Uses 'wsl --list' to check if the specified distro is registered.
.PARAMETER DistroName
    Name of the WSL distro to check.
.OUTPUTS
    [bool] True if distro exists, false otherwise.
#>
function Test-WSLDistroExists {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory = $true)]
        [string]$DistroName
    )

    try {
        $distros = wsl --list --quiet 2>&1
        if ($LASTEXITCODE -ne 0) {
            return $false
        }

        foreach ($distro in $distros) {
            $cleanName = $distro -replace '\x00', '' -replace '^\s+|\s+$', ''
            if ($cleanName -eq $DistroName) {
                return $true
            }
        }
        return $false
    }
    catch {
        return $false
    }
}

#==============================================================================
# Function: Invoke-WSLCommand
#==============================================================================
<#
.SYNOPSIS
    Executes a command inside a WSL distro.
.DESCRIPTION
    Runs a bash command inside the specified WSL distro and returns the output.
.PARAMETER DistroName
    Name of the WSL distro to run the command in.
.PARAMETER Command
    Bash command to execute.
.PARAMETER AsRoot
    Run command as root user.
.OUTPUTS
    [string] Command output.
#>
function Invoke-WSLCommand {
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory = $true)]
        [string]$DistroName,

        [Parameter(Mandatory = $true)]
        [string]$Command,

        [Parameter(Mandatory = $false)]
        [switch]$AsRoot
    )

    $wslArgs = @("--distribution", $DistroName)
    if ($AsRoot) {
        $wslArgs += @("--user", "root")
    }
    $wslArgs += @("--", "bash", "-c", $Command)

    Write-Host "Executing: $Command" -ForegroundColor DarkGray
    $output = & wsl @wslArgs 2>&1

    return $output
}

#==============================================================================
# Function: Test-OllamaInstalled
#==============================================================================
<#
.SYNOPSIS
    Checks if Ollama is installed in the WSL distro.
.DESCRIPTION
    Returns true if Ollama is installed and accessible.
.OUTPUTS
    [bool] True if Ollama is installed, false otherwise.
#>
function Test-OllamaInstalled {
    [CmdletBinding()]
    [OutputType([bool])]
    param()

    $ollamaVersion = Invoke-WSLCommand -DistroName $global:wslDistroName -Command "ollama --version 2>/dev/null || echo 'not-installed'"

    return ($ollamaVersion -notmatch "not-installed")
}

#==============================================================================
# Function: Test-OllamaRunning
#==============================================================================
<#
.SYNOPSIS
    Checks if Ollama service is running and responsive.
.DESCRIPTION
    Tests both the service status and API responsiveness.
.OUTPUTS
    [bool] True if Ollama is running and responsive, false otherwise.
#>
function Test-OllamaRunning {
    [CmdletBinding()]
    [OutputType([bool])]
    param()

    $testResult = Invoke-WSLCommand -DistroName $global:wslDistroName -Command "curl -s http://localhost:11434/api/tags 2>/dev/null | grep -q 'models' && echo 'running' || echo 'stopped'"

    return ($testResult -match "running")
}

#==============================================================================
# Function: Test-OllamaModelAvailable
#==============================================================================
<#
.SYNOPSIS
    Checks if the required Ollama model is available.
.DESCRIPTION
    Checks if the specified model has been pulled.
.OUTPUTS
    [bool] True if model is available, false otherwise.
#>
function Test-OllamaModelAvailable {
    [CmdletBinding()]
    [OutputType([bool])]
    param()

    $modelCheck = Invoke-WSLCommand -DistroName $global:wslDistroName -Command "ollama list 2>/dev/null | grep '$($global:ollamaModel)' || echo 'not-found'"

    return ($modelCheck -notmatch "not-found")
}

#==============================================================================
# Function: Show-WizardHeader
#==============================================================================
<#
.SYNOPSIS
    Displays the wizard header with cost savings summary.
.DESCRIPTION
    Shows a formatted header explaining the optimization wizard.
.OUTPUTS
    [void]
#>
function Show-WizardHeader {
    [CmdletBinding()]
    param()

    Clear-Host
    Write-Host ""
    Write-Host "########################################################" -ForegroundColor Cyan
    Write-Host "#                                                      #" -ForegroundColor Cyan
    Write-Host "#     OpenClaw Cost Optimization Wizard                #" -ForegroundColor Cyan
    Write-Host "#                                                      #" -ForegroundColor Cyan
    Write-Host "#     Reduce your AI costs by up to 97%                #" -ForegroundColor Cyan
    Write-Host "#                                                      #" -ForegroundColor Cyan
    Write-Host "########################################################" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "This wizard will guide you through optimizations that can" -ForegroundColor White
    Write-Host "dramatically reduce your OpenClaw API costs:" -ForegroundColor White
    Write-Host ""
    Write-Host "  BEFORE: `$2-3/day,  `$70-90/month,  `$800+/year" -ForegroundColor Red
    Write-Host "  AFTER:  `$0.10/day, `$3-5/month,    `$40-60/year" -ForegroundColor Green
    Write-Host ""
    Write-Host "You will be asked about each optimization separately." -ForegroundColor DarkGray
    Write-Host "Press Enter to continue or Ctrl+C to exit." -ForegroundColor DarkGray
    Read-Host
}

#==============================================================================
# Function: Show-StepHeader
#==============================================================================
<#
.SYNOPSIS
    Displays a formatted step header.
.DESCRIPTION
    Shows a numbered step header with title.
.PARAMETER StepNumber
    The step number.
.PARAMETER TotalSteps
    Total number of steps.
.PARAMETER Title
    Step title.
.OUTPUTS
    [void]
#>
function Show-StepHeader {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [int]$StepNumber,

        [Parameter(Mandatory = $true)]
        [int]$TotalSteps,

        [Parameter(Mandatory = $true)]
        [string]$Title
    )

    Write-Host ""
    Write-Host "========================================================" -ForegroundColor Yellow
    Write-Host "Step $StepNumber of $TotalSteps : $Title" -ForegroundColor White
    Write-Host "========================================================" -ForegroundColor Yellow
    Write-Host ""
}

#==============================================================================
# Function: Get-UserChoice
#==============================================================================
<#
.SYNOPSIS
    Prompts user to select from a list of options.
.DESCRIPTION
    Displays numbered options and returns the selected choice.
.PARAMETER Options
    Array of option strings.
.PARAMETER DefaultChoice
    Default choice number (1-based).
.OUTPUTS
    [int] Selected option number (1-based).
#>
function Get-UserChoice {
    [CmdletBinding()]
    [OutputType([int])]
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Options,

        [Parameter(Mandatory = $false)]
        [int]$DefaultChoice = 1
    )

    Write-Host ""
    for ($i = 0; $i -lt $Options.Count; $i++) {
        $marker = if ($i + 1 -eq $DefaultChoice) { " (default)" } else { "" }
        Write-Host "  [$($i + 1)] $($Options[$i])$marker" -ForegroundColor Cyan
    }
    Write-Host ""

    do {
        $userInput = Read-Host "Enter your choice (1-$($Options.Count)) [default: $DefaultChoice]"
        if ([string]::IsNullOrWhiteSpace($userInput)) {
            return $DefaultChoice
        }
        $choice = 0
        if ([int]::TryParse($userInput, [ref]$choice) -and $choice -ge 1 -and $choice -le $Options.Count) {
            return $choice
        }
        Write-Host "Invalid choice. Please enter a number between 1 and $($Options.Count)." -ForegroundColor Red
    } while ($true)
}

#==============================================================================
# Function: Invoke-Step1ModelRouting
#==============================================================================
<#
.SYNOPSIS
    Wizard step for model routing configuration.
.DESCRIPTION
    Asks user about switching default model from Sonnet to Haiku.
.OUTPUTS
    [void]
#>
function Invoke-Step1ModelRouting {
    [CmdletBinding()]
    param()

    Show-StepHeader -StepNumber 1 -TotalSteps 4 -Title "Default Model Selection"

    Write-Host "COST SAVINGS: 90% reduction on routine tasks" -ForegroundColor Green
    Write-Host ""
    Write-Host "By default, OpenClaw uses Claude Sonnet for ALL tasks." -ForegroundColor White
    Write-Host "However, most routine tasks (file operations, simple queries," -ForegroundColor White
    Write-Host "organizing data) work perfectly with the cheaper Haiku model." -ForegroundColor White
    Write-Host ""
    Write-Host "Cost comparison per 1,000 tokens:" -ForegroundColor Cyan
    Write-Host "  - Sonnet: `$0.003  (current default)" -ForegroundColor Yellow
    Write-Host "  - Haiku:  `$0.00025 (12x cheaper!)" -ForegroundColor Green
    Write-Host ""
    Write-Host "You can still use 'use sonnet' or 'use opus' in prompts when" -ForegroundColor DarkGray
    Write-Host "you need more reasoning power for complex tasks." -ForegroundColor DarkGray

    $options = @(
        "Yes - Set Haiku as default (recommended, 90% savings)"
        "No - Keep Sonnet as default (no change)"
    )

    $choice = Get-UserChoice -Options $options -DefaultChoice 1

    $global:wizardConfig.UseHaikuDefault = ($choice -eq 1)

    if ($global:wizardConfig.UseHaikuDefault) {
        Write-Host ""
        Write-Host "[OK] Haiku will be set as the default model." -ForegroundColor Green
    }
    else {
        Write-Host ""
        Write-Host "[OK] Keeping Sonnet as default model." -ForegroundColor Yellow
    }
}

#==============================================================================
# Function: Invoke-Step2Heartbeat
#==============================================================================
<#
.SYNOPSIS
    Wizard step for heartbeat configuration.
.DESCRIPTION
    Asks user about heartbeat optimization with Ollama or Haiku fallback.
.OUTPUTS
    [void]
#>
function Invoke-Step2Heartbeat {
    [CmdletBinding()]
    param()

    Show-StepHeader -StepNumber 2 -TotalSteps 4 -Title "Heartbeat Optimization"

    Write-Host "COST SAVINGS: `$5-15/month → `$0-1/month" -ForegroundColor Green
    Write-Host ""
    Write-Host "OpenClaw sends periodic heartbeat checks to keep your agent" -ForegroundColor White
    Write-Host "responsive. By default, these use your paid Anthropic API." -ForegroundColor White
    Write-Host ""
    Write-Host "If heartbeats run every 30 minutes with full context," -ForegroundColor White
    Write-Host "you could spend `$5-15/month just keeping the agent 'alive'!" -ForegroundColor Yellow
    Write-Host ""

    # Check Ollama status
    $ollamaInstalled = Test-OllamaInstalled
    $ollamaRunning = $false
    $ollamaModelReady = $false

    if ($ollamaInstalled) {
        $ollamaRunning = Test-OllamaRunning
        if ($ollamaRunning) {
            $ollamaModelReady = Test-OllamaModelAvailable
        }
    }

    Write-Host "Current Ollama Status:" -ForegroundColor Cyan
    if ($ollamaInstalled) {
        Write-Host "  - Ollama installed: Yes" -ForegroundColor Green
        if ($ollamaRunning) {
            Write-Host "  - Ollama running: Yes" -ForegroundColor Green
            if ($ollamaModelReady) {
                Write-Host "  - Model $($global:ollamaModel) ready: Yes" -ForegroundColor Green
            }
            else {
                Write-Host "  - Model $($global:ollamaModel) ready: No (will need to pull)" -ForegroundColor Yellow
            }
        }
        else {
            Write-Host "  - Ollama running: No (will need to start)" -ForegroundColor Yellow
        }
    }
    else {
        Write-Host "  - Ollama installed: No" -ForegroundColor Yellow
    }
    Write-Host ""

    Write-Host "Options:" -ForegroundColor White

    if ($ollamaInstalled -and $ollamaRunning -and $ollamaModelReady) {
        # Ollama fully ready
        $options = @(
            "Use Ollama for heartbeats (FREE - recommended)"
            "Use Haiku for heartbeats (cheapest API option, ~`$1/month)"
            "Keep current heartbeat settings (no change)"
        )
        $defaultChoice = 1
    }
    elseif ($ollamaInstalled) {
        # Ollama installed but not running or model not ready
        $options = @(
            "Setup Ollama for heartbeats (start service + pull model, then FREE)"
            "Use Haiku for heartbeats (cheapest API option, ~`$1/month)"
            "Keep current heartbeat settings (no change)"
        )
        $defaultChoice = 1
    }
    else {
        # Ollama not installed
        $options = @(
            "Install Ollama for heartbeats (download ~2GB, then FREE forever)"
            "Use Haiku for heartbeats (cheapest API option, ~`$1/month)"
            "Keep current heartbeat settings (no change)"
        )
        $defaultChoice = 2
    }

    $choice = Get-UserChoice -Options $options -DefaultChoice $defaultChoice

    switch ($choice) {
        1 {
            if (-not $ollamaInstalled) {
                $global:wizardConfig.InstallOllama = $true
            }
            $global:wizardConfig.HeartbeatMode = "ollama"
            Write-Host ""
            Write-Host "[OK] Heartbeats will use Ollama (free local LLM)." -ForegroundColor Green
        }
        2 {
            $global:wizardConfig.HeartbeatMode = "haiku"
            Write-Host ""
            Write-Host "[OK] Heartbeats will use Haiku (cheapest API option)." -ForegroundColor Green
        }
        3 {
            $global:wizardConfig.HeartbeatMode = "unchanged"
            Write-Host ""
            Write-Host "[OK] Keeping current heartbeat settings." -ForegroundColor Yellow
        }
    }
}

#==============================================================================
# Function: Invoke-Step3CachingInfo
#==============================================================================
<#
.SYNOPSIS
    Wizard step showing prompt caching information.
.DESCRIPTION
    Explains that prompt caching is automatic with Anthropic API.
    No OpenClaw configuration needed - just informational.
.OUTPUTS
    [void]
#>
function Invoke-Step3CachingInfo {
    [CmdletBinding()]
    param()

    Show-StepHeader -StepNumber 3 -TotalSteps 4 -Title "Prompt Caching (Info)"

    Write-Host "AUTOMATIC SAVINGS: 90% discount on repeated content" -ForegroundColor Green
    Write-Host ""
    Write-Host "Good news! Prompt caching is AUTOMATIC with Anthropic's API" -ForegroundColor White
    Write-Host "when using Claude 3.5+ models. No configuration needed!" -ForegroundColor White
    Write-Host ""
    Write-Host "How it works:" -ForegroundColor Cyan
    Write-Host "  - Your system prompt (SOUL.md, USER.md, etc.) gets cached" -ForegroundColor DarkGray
    Write-Host "  - First request: Full price" -ForegroundColor DarkGray
    Write-Host "  - Subsequent requests (within 5 min): 90% discount" -ForegroundColor DarkGray
    Write-Host ""
    Write-Host "Tips to maximize cache hits:" -ForegroundColor Cyan
    Write-Host "  - Keep system prompts stable (don't edit mid-session)" -ForegroundColor DarkGray
    Write-Host "  - Batch requests within 5-minute windows" -ForegroundColor DarkGray
    Write-Host "  - Use lean workspace files (SOUL.md, USER.md)" -ForegroundColor DarkGray
    Write-Host ""
    Write-Host "Example savings:" -ForegroundColor Cyan
    Write-Host "  - 5KB system prompt x 100 calls/week" -ForegroundColor DarkGray
    Write-Host "  - Without batching: `$0.30/week" -ForegroundColor Yellow
    Write-Host "  - With batching:    `$0.03/week (90% savings)" -ForegroundColor Green
    Write-Host ""

    Write-Host "[INFO] Prompt caching is automatic - no action needed!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Press Enter to continue..." -ForegroundColor DarkGray
    Read-Host
}

#==============================================================================
# Function: Invoke-Step4RateLimits
#==============================================================================
<#
.SYNOPSIS
    Wizard step for rate limits configuration.
.DESCRIPTION
    Asks user about adding rate limits to prevent runaway costs.
.OUTPUTS
    [void]
#>
function Invoke-Step4RateLimits {
    [CmdletBinding()]
    param()

    Show-StepHeader -StepNumber 3 -TotalSteps 4 -Title "Rate Limits & Budget Controls"

    Write-Host "COST SAVINGS: Prevents `$100+ overnight disasters" -ForegroundColor Green
    Write-Host ""
    Write-Host "Without rate limits, an autonomous agent can:" -ForegroundColor White
    Write-Host "  - Make 100+ API calls in rapid loops" -ForegroundColor Red
    Write-Host "  - Run expensive web searches continuously" -ForegroundColor Red
    Write-Host "  - Burn `$500+ overnight while you sleep" -ForegroundColor Red
    Write-Host ""
    Write-Host "Rate limits add guardrails:" -ForegroundColor Cyan
    Write-Host "  - 5 seconds minimum between API calls" -ForegroundColor DarkGray
    Write-Host "  - 10 seconds between web searches" -ForegroundColor DarkGray
    Write-Host "  - Max 5 searches per batch, then 2-minute break" -ForegroundColor DarkGray
    Write-Host "  - Daily budget: `$$($global:dailyBudget) (warning at $($global:budgetWarningPercent)%)" -ForegroundColor DarkGray
    Write-Host "  - Monthly budget: `$$($global:monthlyBudget) (warning at $($global:budgetWarningPercent)%)" -ForegroundColor DarkGray

    $options = @(
        "Yes - Add rate limits and budget controls (recommended)"
        "No - Do not add rate limits"
    )

    $choice = Get-UserChoice -Options $options -DefaultChoice 1

    $global:wizardConfig.EnableRateLimits = ($choice -eq 1)

    if ($global:wizardConfig.EnableRateLimits) {
        Write-Host ""
        Write-Host "[OK] Rate limits and budget controls will be added." -ForegroundColor Green
    }
    else {
        Write-Host ""
        Write-Host "[OK] Rate limits will not be added." -ForegroundColor Yellow
    }
}

#==============================================================================
# Function: Invoke-Step5WorkspaceFiles
#==============================================================================
<#
.SYNOPSIS
    Wizard step for workspace template files.
.DESCRIPTION
    Asks user about creating lean workspace files for session initialization.
.OUTPUTS
    [void]
#>
function Invoke-Step5WorkspaceFiles {
    [CmdletBinding()]
    param()

    Show-StepHeader -StepNumber 4 -TotalSteps 4 -Title "Lean Workspace Templates"

    Write-Host "COST SAVINGS: 80% context reduction (50KB → 8KB)" -ForegroundColor Green
    Write-Host ""
    Write-Host "By default, OpenClaw loads your entire history and all" -ForegroundColor White
    Write-Host "context files on EVERY message - that's 50KB+ of tokens!" -ForegroundColor White
    Write-Host ""
    Write-Host "Optimized workspace files include:" -ForegroundColor Cyan
    Write-Host "  - SOUL.md: Core principles with model routing rules" -ForegroundColor DarkGray
    Write-Host "  - USER.md: Your info (you customize this)" -ForegroundColor DarkGray
    Write-Host "  - OPTIMIZATION.md: Session initialization rules" -ForegroundColor DarkGray
    Write-Host "  - memory/YYYY-MM-DD.md: Daily notes template" -ForegroundColor DarkGray
    Write-Host ""
    Write-Host "These files tell your agent to:" -ForegroundColor White
    Write-Host "  - Load only essential files at session start" -ForegroundColor DarkGray
    Write-Host "  - NOT auto-load MEMORY.md or session history" -ForegroundColor DarkGray
    Write-Host "  - Use memory_search() only when needed" -ForegroundColor DarkGray

    $options = @(
        "Yes - Create optimized workspace templates (recommended)"
        "No - Do not create workspace files"
    )

    $choice = Get-UserChoice -Options $options -DefaultChoice 1

    $global:wizardConfig.CreateWorkspaceFiles = ($choice -eq 1)

    if ($global:wizardConfig.CreateWorkspaceFiles) {
        Write-Host ""
        Write-Host "[OK] Optimized workspace templates will be created." -ForegroundColor Green
    }
    else {
        Write-Host ""
        Write-Host "[OK] Workspace files will not be created." -ForegroundColor Yellow
    }
}

#==============================================================================
# Function: Show-WizardSummary
#==============================================================================
<#
.SYNOPSIS
    Shows a summary of all wizard selections.
.DESCRIPTION
    Displays what will be applied and asks for confirmation.
.OUTPUTS
    [bool] True if user confirms, false otherwise.
#>
function Show-WizardSummary {
    [CmdletBinding()]
    [OutputType([bool])]
    param()

    Write-Host ""
    Write-Host "========================================================" -ForegroundColor Green
    Write-Host "Configuration Summary" -ForegroundColor White
    Write-Host "========================================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "The following optimizations will be applied:" -ForegroundColor White
    Write-Host ""

    $changeCount = 0

    # Model routing
    if ($global:wizardConfig.UseHaikuDefault) {
        Write-Host "  [X] Default model: Haiku (90% token cost savings)" -ForegroundColor Green
        $changeCount++
    }
    else {
        Write-Host "  [ ] Default model: Unchanged (Sonnet)" -ForegroundColor DarkGray
    }

    # Heartbeat
    switch ($global:wizardConfig.HeartbeatMode) {
        "ollama" {
            if ($global:wizardConfig.InstallOllama) {
                Write-Host "  [X] Heartbeat: Install Ollama + use for heartbeats (FREE)" -ForegroundColor Green
            }
            else {
                Write-Host "  [X] Heartbeat: Use existing Ollama (FREE)" -ForegroundColor Green
            }
            $changeCount++
        }
        "haiku" {
            Write-Host "  [X] Heartbeat: Use Haiku (~`$1/month)" -ForegroundColor Green
            $changeCount++
        }
        default {
            Write-Host "  [ ] Heartbeat: Unchanged" -ForegroundColor DarkGray
        }
    }

    # Caching (always show as info since it's automatic)
    Write-Host "  [i] Prompt caching: Automatic with Anthropic API" -ForegroundColor DarkGray

    # Rate limits
    if ($global:wizardConfig.EnableRateLimits) {
        Write-Host "  [X] Rate limits: Enabled (prevents runaway costs)" -ForegroundColor Green
        $changeCount++
    }
    else {
        Write-Host "  [ ] Rate limits: Not enabled" -ForegroundColor DarkGray
    }

    # Workspace files
    if ($global:wizardConfig.CreateWorkspaceFiles) {
        Write-Host "  [X] Workspace templates: Will be created (80% context savings)" -ForegroundColor Green
        $changeCount++
    }
    else {
        Write-Host "  [ ] Workspace templates: Not creating" -ForegroundColor DarkGray
    }

    Write-Host ""

    if ($changeCount -eq 0) {
        Write-Host "No optimizations selected. Nothing to apply." -ForegroundColor Yellow
        Write-Host ""
        return $false
    }

    Write-Host "Total optimizations to apply: $changeCount" -ForegroundColor Cyan
    Write-Host ""

    $options = @(
        "Yes - Apply these optimizations now"
        "No - Cancel and exit"
    )

    $choice = Get-UserChoice -Options $options -DefaultChoice 1

    return ($choice -eq 1)
}

#==============================================================================
# Function: Install-Ollama
#==============================================================================
<#
.SYNOPSIS
    Installs Ollama for free local LLM heartbeats.
.DESCRIPTION
    Installs Ollama in the WSL distro and pulls the lightweight model.
.OUTPUTS
    [bool] True if successful, false otherwise.
#>
function Install-Ollama {
    [CmdletBinding(SupportsShouldProcess = $true)]
    [OutputType([bool])]
    param()

    if (-not $PSCmdlet.ShouldProcess($global:wslDistroName, "Install Ollama")) {
        return $false
    }

    Write-Host ""
    Write-Host "Installing Ollama..." -ForegroundColor Yellow

    # Check if already installed
    if (Test-OllamaInstalled) {
        Write-Host "Ollama already installed." -ForegroundColor Green
    }
    else {
        Write-Host "Downloading and installing Ollama (this may take a few minutes)..." -ForegroundColor Cyan
        Invoke-WSLCommand -DistroName $global:wslDistroName -AsRoot -Command "curl -fsSL https://ollama.ai/install.sh | sh"

        if (-not (Test-OllamaInstalled)) {
            Write-Error "Failed to install Ollama."
            return $false
        }

        Write-Host "Ollama installed successfully." -ForegroundColor Green
    }

    # Start Ollama service
    Write-Host "Starting Ollama service..." -ForegroundColor Cyan
    Invoke-WSLCommand -DistroName $global:wslDistroName -Command "nohup ollama serve > /dev/null 2>&1 &"
    Start-Sleep -Seconds 3

    # Pull the model
    if (-not (Test-OllamaModelAvailable)) {
        Write-Host "Pulling model $($global:ollamaModel) (approx 2GB download)..." -ForegroundColor Yellow
        & wsl --distribution $global:wslDistroName -- ollama pull $global:ollamaModel

        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Model pull may have issues. Heartbeats may not work correctly."
            return $false
        }
    }

    Write-Host "Ollama setup complete." -ForegroundColor Green
    return $true
}

#==============================================================================
# Function: Install-OllamaService
#==============================================================================
<#
.SYNOPSIS
    Configures Ollama as a systemd service.
.DESCRIPTION
    Creates a systemd user service for automatic Ollama startup.
.OUTPUTS
    [bool] True if successful, false otherwise.
#>
function Install-OllamaService {
    [CmdletBinding(SupportsShouldProcess = $true)]
    [OutputType([bool])]
    param()

    if (-not $PSCmdlet.ShouldProcess($global:wslDistroName, "Install Ollama Service")) {
        return $false
    }

    Write-Host "Configuring Ollama systemd service..." -ForegroundColor Cyan

    $serviceContent = @"
[Unit]
Description=Ollama Local LLM Service
After=network.target

[Service]
Type=simple
ExecStart=/usr/local/bin/ollama serve
Restart=on-failure
RestartSec=5
Environment=OLLAMA_HOST=0.0.0.0

[Install]
WantedBy=default.target
"@

    $escapedContent = $serviceContent -replace '"', '\"' -replace '\$', '\$'

    Invoke-WSLCommand -DistroName $global:wslDistroName -Command "mkdir -p ~/.config/systemd/user"
    Invoke-WSLCommand -DistroName $global:wslDistroName -Command "echo `"$escapedContent`" > ~/.config/systemd/user/ollama.service"
    Invoke-WSLCommand -DistroName $global:wslDistroName -Command "systemctl --user daemon-reload"
    Invoke-WSLCommand -DistroName $global:wslDistroName -Command "systemctl --user enable ollama 2>/dev/null || true"
    Invoke-WSLCommand -DistroName $global:wslDistroName -Command "systemctl --user start ollama 2>/dev/null || true"

    Write-Host "Ollama service configured." -ForegroundColor Green
    return $true
}

#==============================================================================
# Function: Set-OpenClawConfig
#==============================================================================
<#
.SYNOPSIS
    Applies the OpenClaw configuration based on wizard selections.
.DESCRIPTION
    Generates and writes the openclaw.json config file.
.OUTPUTS
    [bool] True if successful, false otherwise.
#>
function Set-OpenClawConfig {
    [CmdletBinding(SupportsShouldProcess = $true)]
    [OutputType([bool])]
    param()

    if (-not $PSCmdlet.ShouldProcess($global:wslDistroName, "Set OpenClaw Config")) {
        return $false
    }

    Write-Host ""
    Write-Host "Applying OpenClaw configuration..." -ForegroundColor Yellow

    # Backup existing config
    Write-Host "Backing up existing config..." -ForegroundColor Cyan
    Invoke-WSLCommand -DistroName $global:wslDistroName -Command "cp $($global:openclawConfigFile) $($global:openclawConfigFile).backup 2>/dev/null || true"

    # Determine primary model
    $primaryModel = if ($global:wizardConfig.UseHaikuDefault) { $global:defaultModel } else { $global:complexModel }

    # Determine heartbeat model
    $heartbeatModel = switch ($global:wizardConfig.HeartbeatMode) {
        "ollama" { "ollama/$($global:ollamaModel)" }
        "haiku" { $global:defaultModel }
        default { $null }
    }

    # Build heartbeat config (inside agents.defaults)
    $heartbeatConfig = ""
    if ($heartbeatModel) {
        $heartbeatConfig = @"
      "heartbeat": {
        "every": "$($global:heartbeatInterval)",
        "model": "$heartbeatModel",
        "session": "main",
        "prompt": "$($global:heartbeatPrompt)"
      },
"@
    }

    # Build full config
    # Note: OpenClaw schema:
    #   - models only support "alias" and "params", NOT "cache"
    #   - heartbeat goes under agents.defaults, NOT at root
    #   - cache config is not supported at agents.defaults level
    $configJson = @"
{
  "agents": {
    "defaults": {
      "model": {
        "primary": "$primaryModel"
      },
$heartbeatConfig
      "models": {
        "$($global:complexModel)": {
          "alias": "sonnet"
        },
        "$($global:defaultModel)": {
          "alias": "haiku"
        },
        "$($global:criticalModel)": {
          "alias": "opus"
        }
      }
    }
  }
}
"@

    # Write config
    Write-Host "Writing configuration..." -ForegroundColor Cyan
    $escapedJson = $configJson -replace '"', '\"' -replace '\$', '\$'
    Invoke-WSLCommand -DistroName $global:wslDistroName -Command "mkdir -p $($global:openclawConfigPath)"
    Invoke-WSLCommand -DistroName $global:wslDistroName -Command "echo `"$escapedJson`" > $($global:openclawConfigFile)"

    Write-Host "OpenClaw configuration applied." -ForegroundColor Green
    return $true
}

#==============================================================================
# Function: New-WorkspaceTemplates
#==============================================================================
<#
.SYNOPSIS
    Creates lean workspace template files.
.DESCRIPTION
    Generates SOUL.md, USER.md, OPTIMIZATION.md files.
.OUTPUTS
    [bool] True if successful, false otherwise.
#>
function New-WorkspaceTemplates {
    [CmdletBinding(SupportsShouldProcess = $true)]
    [OutputType([bool])]
    param()

    if (-not $PSCmdlet.ShouldProcess($global:wslDistroName, "Create Workspace Templates")) {
        return $false
    }

    Write-Host ""
    Write-Host "Creating workspace templates..." -ForegroundColor Yellow

    # Create workspace directory
    Invoke-WSLCommand -DistroName $global:wslDistroName -Command "mkdir -p $($global:workspacePath)/memory"

    # SOUL.md with rate limits if enabled
    $rateLimitsSection = ""
    if ($global:wizardConfig.EnableRateLimits) {
        $rateLimitsSection = @"

## Rate Limits

- 5 seconds minimum between API calls
- 10 seconds between web searches
- Max 5 searches per batch, then 2-minute break
- Batch similar work (one request for 10 items, not 10 requests)
- If 429 error: STOP, wait 5 minutes, retry

## Budget Awareness

- Daily budget: `$$($global:dailyBudget) (warning at $($global:budgetWarningPercent)%)
- Monthly budget: `$$($global:monthlyBudget) (warning at $($global:budgetWarningPercent)%)
- Report estimated token cost before large tasks
- Report actual token usage after task completion
"@
    }

    $soulMd = @"
# SOUL.md

## Core Principles

- Optimize for efficiency and low token usage
- Use the cheapest model that can accomplish the task
- Batch similar work together
- Cache and reuse context when possible

## Model Selection

Default: Haiku
Switch to Sonnet ONLY for:
- Architecture decisions
- Production code review
- Security analysis
- Complex debugging/reasoning

Switch to Opus ONLY for:
- Strategic multi-project decisions
- Critical system architecture

When in doubt: Try Haiku first.
$rateLimitsSection
"@

    $userMd = @"
# USER.md

- **Name:** [YOUR NAME]
- **Timezone:** [YOUR TIMEZONE]
- **Mission:** [WHAT YOU'RE BUILDING]

## Success Metrics

- Low token usage (optimize for efficiency)
- Task completion quality
- [ADD YOUR METRICS]

## Preferences

- [ADD YOUR PREFERENCES]
"@

    $optimizationMd = @"
# OPTIMIZATION.md

## Session Initialization Rule

On every session start:

1. Load ONLY these files:
   - SOUL.md
   - USER.md
   - IDENTITY.md (if exists)
   - memory/YYYY-MM-DD.md (if it exists)

2. DO NOT auto-load:
   - MEMORY.md
   - Session history
   - Prior messages
   - Previous tool outputs

3. When user asks about prior context:
   - Use memory_search() on demand
   - Pull only the relevant snippet with memory_get()
   - Don't load the whole file

4. Update memory/YYYY-MM-DD.md at end of session with:
   - What you worked on
   - Decisions made
   - Leads generated
   - Blockers
   - Next steps

This saves 80% on context overhead.
"@

    # Write files using heredoc
    Write-Host "Creating SOUL.md..." -ForegroundColor Cyan
    Invoke-WSLCommand -DistroName $global:wslDistroName -Command "cat > $($global:workspacePath)/SOUL.md << 'EOFMARKER'
$soulMd
EOFMARKER"

    Write-Host "Creating USER.md..." -ForegroundColor Cyan
    Invoke-WSLCommand -DistroName $global:wslDistroName -Command "cat > $($global:workspacePath)/USER.md << 'EOFMARKER'
$userMd
EOFMARKER"

    Write-Host "Creating OPTIMIZATION.md..." -ForegroundColor Cyan
    Invoke-WSLCommand -DistroName $global:wslDistroName -Command "cat > $($global:workspacePath)/OPTIMIZATION.md << 'EOFMARKER'
$optimizationMd
EOFMARKER"

    # Create daily memory file
    $today = Get-Date -Format "yyyy-MM-dd"
    $dailyMemoryMd = @"
# Daily Memory - $today

## What I Worked On

-

## Decisions Made

-

## Blockers

-

## Next Steps

-
"@

    Write-Host "Creating daily memory template..." -ForegroundColor Cyan
    Invoke-WSLCommand -DistroName $global:wslDistroName -Command "cat > $($global:workspacePath)/memory/$today.md << 'EOFMARKER'
$dailyMemoryMd
EOFMARKER"

    Write-Host "Workspace templates created." -ForegroundColor Green
    Write-Host ""
    Write-Host "Important: Edit $($global:workspacePath)/USER.md with your information!" -ForegroundColor Yellow

    return $true
}

#==============================================================================
# Function: Invoke-ApplyOptimizations
#==============================================================================
<#
.SYNOPSIS
    Applies all selected optimizations.
.DESCRIPTION
    Executes the installation and configuration based on wizard selections.
.OUTPUTS
    [void]
#>
function Invoke-ApplyOptimizations {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param()

    if (-not $PSCmdlet.ShouldProcess("OpenClaw", "Apply Optimizations")) {
        return
    }

    Write-Host ""
    Write-Host "========================================================" -ForegroundColor Yellow
    Write-Host "Applying Optimizations" -ForegroundColor White
    Write-Host "========================================================" -ForegroundColor Yellow

    # Step 1: Install Ollama if needed
    if ($global:wizardConfig.InstallOllama) {
        if (-not (Install-Ollama)) {
            Write-Warning "Ollama installation had issues. Heartbeat will fall back to API."
            $global:wizardConfig.HeartbeatMode = "haiku"
        }
        else {
            Install-OllamaService | Out-Null
        }
    }
    elseif ($global:wizardConfig.HeartbeatMode -eq "ollama") {
        # Ollama already installed, just make sure it's running
        $ollamaRunning = Test-OllamaRunning
        if (-not $ollamaRunning) {
            Write-Host "Starting Ollama service..." -ForegroundColor Cyan
            Invoke-WSLCommand -DistroName $global:wslDistroName -Command "nohup ollama serve > /dev/null 2>&1 &"
            Start-Sleep -Seconds 3
        }

        # Pull model if needed
        if (-not (Test-OllamaModelAvailable)) {
            Write-Host "Pulling model $($global:ollamaModel)..." -ForegroundColor Yellow
            & wsl --distribution $global:wslDistroName -- ollama pull $global:ollamaModel
        }

        Install-OllamaService | Out-Null
    }

    # Step 2: Apply OpenClaw config
    if ($global:wizardConfig.UseHaikuDefault -or
        $global:wizardConfig.HeartbeatMode -ne "unchanged") {
        Set-OpenClawConfig | Out-Null
    }

    # Step 3: Create workspace files
    if ($global:wizardConfig.CreateWorkspaceFiles) {
        New-WorkspaceTemplates | Out-Null
    }

    # Show completion
    Write-Host ""
    Write-Host "========================================================" -ForegroundColor Green
    Write-Host "Optimization Complete!" -ForegroundColor Green
    Write-Host "========================================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Cyan
    Write-Host "  1. Restart OpenClaw service to apply changes" -ForegroundColor DarkGray
    Write-Host "  2. Edit ~/workspace/USER.md with your information" -ForegroundColor DarkGray
    Write-Host "  3. Monitor token usage on Anthropic dashboard" -ForegroundColor DarkGray
    Write-Host ""
    Write-Host "Expected savings:" -ForegroundColor Cyan
    Write-Host "  - Before: `$2-3/day, `$70-90/month" -ForegroundColor Red
    Write-Host "  - After:  `$0.10/day, `$3-5/month" -ForegroundColor Green
    Write-Host ""
}

#==============================================================================
# Function: Start-OptimizationWizard
#==============================================================================
<#
.SYNOPSIS
    Runs the complete optimization wizard.
.DESCRIPTION
    Guides user through all optimization steps and applies selections.
.OUTPUTS
    [void]
#>
function Start-OptimizationWizard {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param()

    if (-not $PSCmdlet.ShouldProcess("OpenClaw", "Run Optimization Wizard")) {
        return
    }

    # Check prerequisites
    if (-not (Test-WSLDistroExists -DistroName $global:wslDistroName)) {
        Write-Host ""
        Write-Host "ERROR: WSL distro '$($global:wslDistroName)' not found." -ForegroundColor Red
        Write-Host "Please run Setup_Core_WSL_OpenClaw.ps1 first." -ForegroundColor Yellow
        Write-Host ""
        return
    }

    # Reset wizard state
    $global:wizardConfig = @{
        UseHaikuDefault        = $false
        HeartbeatMode          = "unchanged"
        EnableCaching          = $false
        EnableRateLimits       = $false
        CreateWorkspaceFiles   = $false
        InstallOllama          = $false
    }

    # Show header
    Show-WizardHeader

    # Run steps (4 steps - caching is automatic with Anthropic API)
    Invoke-Step1ModelRouting
    Invoke-Step2Heartbeat
    Invoke-Step3CachingInfo
    Invoke-Step4RateLimits
    Invoke-Step5WorkspaceFiles

    # Show summary and confirm
    $confirmed = Show-WizardSummary

    if ($confirmed) {
        Invoke-ApplyOptimizations
    }
    else {
        Write-Host ""
        Write-Host "Wizard cancelled. No changes were made." -ForegroundColor Yellow
        Write-Host ""
    }
}

#==============================================================================
# Function: Test-OptimizationStatus
#==============================================================================
<#
.SYNOPSIS
    Shows current optimization status.
.DESCRIPTION
    Checks and displays the current state of all optimizations.
.OUTPUTS
    [void]
#>
function Test-OptimizationStatus {
    [CmdletBinding()]
    param()

    Write-Host ""
    Write-Host "========================================================" -ForegroundColor Yellow
    Write-Host "OpenClaw Optimization Status" -ForegroundColor White
    Write-Host "========================================================" -ForegroundColor Yellow
    Write-Host ""

    # Check WSL distro
    if (-not (Test-WSLDistroExists -DistroName $global:wslDistroName)) {
        Write-Host "WSL distro '$($global:wslDistroName)' not found." -ForegroundColor Red
        Write-Host "Run Setup_Core_WSL_OpenClaw.ps1 first." -ForegroundColor Yellow
        return
    }

    Write-Host "1. WSL Distro: " -NoNewline -ForegroundColor White
    Write-Host "$($global:wslDistroName) OK" -ForegroundColor Green

    # Check config
    $configExists = Invoke-WSLCommand -DistroName $global:wslDistroName -Command "test -f $($global:openclawConfigFile) && echo 'exists' || echo 'missing'"

    Write-Host "2. Config File: " -NoNewline -ForegroundColor White
    if ($configExists -match "exists") {
        Write-Host "Exists" -ForegroundColor Green

        $configContent = Invoke-WSLCommand -DistroName $global:wslDistroName -Command "cat $($global:openclawConfigFile) 2>/dev/null"

        # Check default model
        Write-Host "   - Default Model: " -NoNewline -ForegroundColor DarkGray
        if ($configContent -match "claude-haiku") {
            Write-Host "Haiku (optimized)" -ForegroundColor Green
        }
        else {
            Write-Host "Sonnet (not optimized)" -ForegroundColor Yellow
        }

        # Check heartbeat
        Write-Host "   - Heartbeat: " -NoNewline -ForegroundColor DarkGray
        if ($configContent -match "ollama") {
            Write-Host "Ollama (free)" -ForegroundColor Green
        }
        elseif ($configContent -match "haiku.*heartbeat|heartbeat.*haiku") {
            Write-Host "Haiku (cheap)" -ForegroundColor Green
        }
        else {
            Write-Host "Default (may be expensive)" -ForegroundColor Yellow
        }

        # Check caching
        Write-Host "   - Caching: " -NoNewline -ForegroundColor DarkGray
        if ($configContent -match '"cache".*"enabled".*true') {
            Write-Host "Enabled" -ForegroundColor Green
        }
        else {
            Write-Host "Not enabled" -ForegroundColor Yellow
        }
    }
    else {
        Write-Host "Not found" -ForegroundColor Yellow
    }

    # Check Ollama
    Write-Host "3. Ollama: " -NoNewline -ForegroundColor White
    if (Test-OllamaInstalled) {
        Write-Host "Installed" -ForegroundColor Green

        Write-Host "   - Service: " -NoNewline -ForegroundColor DarkGray
        if (Test-OllamaRunning) {
            Write-Host "Running" -ForegroundColor Green
        }
        else {
            Write-Host "Stopped" -ForegroundColor Yellow
        }

        Write-Host "   - Model: " -NoNewline -ForegroundColor DarkGray
        if (Test-OllamaModelAvailable) {
            Write-Host "$($global:ollamaModel) ready" -ForegroundColor Green
        }
        else {
            Write-Host "Not pulled" -ForegroundColor Yellow
        }
    }
    else {
        Write-Host "Not installed" -ForegroundColor Yellow
    }

    # Check workspace files
    Write-Host "4. Workspace Files: " -ForegroundColor White
    $soulExists = Invoke-WSLCommand -DistroName $global:wslDistroName -Command "test -f $($global:workspacePath)/SOUL.md && echo 'exists' || echo 'missing'"
    $userExists = Invoke-WSLCommand -DistroName $global:wslDistroName -Command "test -f $($global:workspacePath)/USER.md && echo 'exists' || echo 'missing'"
    $optExists = Invoke-WSLCommand -DistroName $global:wslDistroName -Command "test -f $($global:workspacePath)/OPTIMIZATION.md && echo 'exists' || echo 'missing'"

    Write-Host "   - SOUL.md: " -NoNewline -ForegroundColor DarkGray
    Write-Host $(if ($soulExists -match "exists") { "OK" } else { "Missing" }) -ForegroundColor $(if ($soulExists -match "exists") { "Green" } else { "Yellow" })

    Write-Host "   - USER.md: " -NoNewline -ForegroundColor DarkGray
    Write-Host $(if ($userExists -match "exists") { "OK" } else { "Missing" }) -ForegroundColor $(if ($userExists -match "exists") { "Green" } else { "Yellow" })

    Write-Host "   - OPTIMIZATION.md: " -NoNewline -ForegroundColor DarkGray
    Write-Host $(if ($optExists -match "exists") { "OK" } else { "Missing" }) -ForegroundColor $(if ($optExists -match "exists") { "Green" } else { "Yellow" })

    Write-Host ""
}

#==============================================================================
# Function: Show-OptimizationGuide
#==============================================================================
<#
.SYNOPSIS
    Displays the quick reference optimization guide.
.DESCRIPTION
    Shows key optimization tips and expected savings.
.OUTPUTS
    [void]
#>
function Show-OptimizationGuide {
    [CmdletBinding()]
    param()

    Write-Host ""
    Write-Host "========================================================" -ForegroundColor Cyan
    Write-Host "OpenClaw Token Optimization Guide" -ForegroundColor White
    Write-Host "========================================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "OPTIMIZATION 1: Model Routing" -ForegroundColor Yellow
    Write-Host "  Problem:  Using Sonnet/Opus for all tasks" -ForegroundColor DarkGray
    Write-Host "  Solution: Haiku default, Sonnet only for complex tasks" -ForegroundColor DarkGray
    Write-Host "  Savings:  90% token cost reduction" -ForegroundColor Green
    Write-Host ""
    Write-Host "OPTIMIZATION 2: Free Heartbeats" -ForegroundColor Yellow
    Write-Host "  Problem:  Heartbeats use paid API calls" -ForegroundColor DarkGray
    Write-Host "  Solution: Use Ollama (free) or Haiku (cheap)" -ForegroundColor DarkGray
    Write-Host "  Savings:  `$5-15/month → `$0-1/month" -ForegroundColor Green
    Write-Host ""
    Write-Host "OPTIMIZATION 3: Prompt Caching" -ForegroundColor Yellow
    Write-Host "  Problem:  System prompt sent with every request" -ForegroundColor DarkGray
    Write-Host "  Solution: Enable cache, batch within 5-min windows" -ForegroundColor DarkGray
    Write-Host "  Savings:  90% discount on cached tokens" -ForegroundColor Green
    Write-Host ""
    Write-Host "OPTIMIZATION 4: Rate Limits" -ForegroundColor Yellow
    Write-Host "  Problem:  Runaway automation burns tokens" -ForegroundColor DarkGray
    Write-Host "  Solution: 5s API delay, 5 search max, budget caps" -ForegroundColor DarkGray
    Write-Host "  Savings:  Prevents `$500 overnight disasters" -ForegroundColor Green
    Write-Host ""
    Write-Host "OPTIMIZATION 5: Session Initialization" -ForegroundColor Yellow
    Write-Host "  Problem:  Loading 50KB context on every message" -ForegroundColor DarkGray
    Write-Host "  Solution: Lean workspace files, load on demand" -ForegroundColor DarkGray
    Write-Host "  Savings:  80% context reduction" -ForegroundColor Green
    Write-Host ""
    Write-Host "========================================================" -ForegroundColor Cyan
    Write-Host "TOTAL EXPECTED SAVINGS: 97%" -ForegroundColor White
    Write-Host "========================================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "  Before: `$2-3/day,  `$70-90/month,  `$800+/year" -ForegroundColor Red
    Write-Host "  After:  `$0.10/day, `$3-5/month,    `$40-60/year" -ForegroundColor Green
    Write-Host ""
}

################################################################################
# Main Menu Loop
################################################################################

$menuTitle = "OpenClaw Optimization Menu"
$menuItems = [ordered]@{
    "1" = "Run Optimization Wizard (recommended)"
    "2" = "Show Current Optimization Status"
    "3" = "Show Optimization Guide"
    "0" = "Exit menu"
}

$menuActions = @{
    "1" = { Start-OptimizationWizard }
    "2" = { Test-OptimizationStatus }
    "3" = { Show-OptimizationGuide }
}

Invoke-MenuLoop -MenuTitle $menuTitle -MenuItems $menuItems -ActionMap $menuActions -ExitChoice "0"
