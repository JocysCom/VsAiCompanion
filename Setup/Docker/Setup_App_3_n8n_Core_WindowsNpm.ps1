################################################################################
# Description  : Installs n8n globally via npm (if needed) and launches n8n on Windows
#                with environment variables equivalent to the Podman/Docker manifest.
# Usage        : Run in PowerShell (not CMD). Then open http://localhost:5678
################################################################################

using namespace System
using namespace System.IO

# Ensure script runs from its own directory
Set-Location -Path $PSScriptRoot

# Hardcoded settings (match Files/Aspire/manifest.json -> resources.n8n.properties.environment)
$envVars = @{
    GENERIC_TIMEZONE                        = "Europe/London"
    TZ                                      = "Europe/London"
    N8N_COMMUNITY_PACKAGES_ENABLED          = "true"
    N8N_COMMUNITY_PACKAGES_ALLOW_TOOL_USAGE = "true"
    N8N_RUNNERS_ENABLED                     = "true"
    N8N_ENFORCE_SETTINGS_FILE_PERMISSIONS   = "true"
    N8N_TRUST_HOST_HEADERS                  = "true"
    N8N_LOG_LEVEL                           = "info"
    NODE_OPTIONS                            = "--max-old-space-size=12288"
    NODES_EXCLUDE                           = "[]"
}

function Assert-CommandExists {
    param(
        [Parameter(Mandatory = $true)]
        [string]$CommandName
    )

    $cmd = Get-Command $CommandName -ErrorAction SilentlyContinue
    if (-not $cmd) {
        throw "Required command '$CommandName' was not found in PATH. Install Node.js (includes npm) and restart your shell."
    }
}

function Install-n8nIfMissing {
    Assert-CommandExists -CommandName "npm"

    $n8nCmd = Get-Command "n8n" -ErrorAction SilentlyContinue
    if ($n8nCmd) {
        Write-Host "n8n is already installed: $($n8nCmd.Source)"
        return
    }

    Write-Host "Installing n8n globally..."
    npm install -g n8n
    if ($LASTEXITCODE -ne 0) {
        throw "npm install -g n8n failed with exit code $LASTEXITCODE"
    }
}

function Start-n8nWithEnv {
    Assert-CommandExists -CommandName "n8n"

    Write-Host "Launching n8n with the following environment variables:"
    foreach ($k in $envVars.Keys) {
        Write-Host "  $k=$($envVars[$k])"
    }

    # Set environment for this process only
    foreach ($k in $envVars.Keys) {
        Set-Item -Path ("Env:{0}" -f $k) -Value $envVars[$k]
    }

    Write-Host "Starting n8n..."
    Write-Host "Open: http://localhost:5678"

    # Run in the foreground so you can stop with Ctrl+C
    n8n
}

Install-n8nIfMissing
Start-n8nWithEnv
