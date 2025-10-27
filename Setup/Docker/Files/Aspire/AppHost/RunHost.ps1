param(
    [Parameter(Mandatory = $false, HelpMessage = "Optional profile overlay name (e.g., 'local' or 'prod').")]
    [string]$Profile
)

# Global configuration values (eliminate hardcoded paths)
$global:ManifestPath   = Join-Path $PSScriptRoot "..\manifest.json"
$global:AppHostProject = Join-Path $PSScriptRoot "AppHost.csproj"

#==============================================================================
# Function: Get-ManifestResourceNames
#==============================================================================
<#
.SYNOPSIS
    Load resource names from the Aspire manifest JSON file.
.DESCRIPTION
    Reads the manifest JSON located relative to the AppHost folder and returns
    the list of resource names (keys under 'resources').
.PARAMETER Path
    Optional explicit manifest path. Defaults to $global:ManifestPath.
.EXAMPLE
    PS > Get-ManifestResourceNames
    Returns an array of resource names from Files/Aspire/manifest.json.
.OUTPUTS
    [string[]]
.NOTES
    Uses ConvertFrom-Json and extracts PSCustomObject property names for 'resources'.
#>
function Get-ManifestResourceNames {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $false)]
        [string]$Path = $global:ManifestPath
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        Write-Error "Manifest file not found: $Path"
        return @()
    }

    $json = Get-Content -LiteralPath $Path -Raw
    try {
        $obj = $json | ConvertFrom-Json
    }
    catch {
        Write-Error "Failed to parse manifest JSON: $($_.Exception.Message)"
        return @()
    }

    if (-not $obj.resources) {
        Write-Error "Manifest contains no 'resources' object."
        return @()
    }

    # Extract resource names (keys)
    $names = $obj.resources.PSObject.Properties.Name
    return $names
}

#==============================================================================
# Function: Show-CheckboxMenu
#==============================================================================
<#
.SYNOPSIS
    Display an interactive checkbox menu to select items.
.DESCRIPTION
    Renders a keyboard-driven checkbox menu. Use Up/Down to navigate, Space to
    toggle selection, Enter to confirm, Esc to cancel (returns empty).
.PARAMETER Items
    The list of item names to present.
.PARAMETER Title
    Optional title to display at the top of the menu.
.EXAMPLE
    PS > $selected = Show-CheckboxMenu -Items $names -Title "Select resources"
.OUTPUTS
    [string[]]
.NOTES
    Uses System.Console for precise key handling and Write-Host for direct console output.
#>
function Show-CheckboxMenu {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Items,
        [Parameter(Mandatory = $false)]
        [string]$Title = "Select resources to start locally"
    )

    if ($Items.Count -eq 0) {
        return @()
    }

    # Selection map: item name -> bool
    $selected = @{}
    foreach ($it in $Items) { $selected[$it] = $false }

    $index = 0

    while ($true) {
        Clear-Host
        Write-Host ("{0}" -f $Title)
        Write-Host ""
        Write-Host "Controls: Up/Down = navigate, Space = toggle, Enter = confirm, Esc = cancel"
        Write-Host ""

        for ($i = 0; $i -lt $Items.Count; $i++) {
            $name = $Items[$i]
            $mark = if ($selected[$name]) { "[x]" } else { "[ ]" }
            if ($i -eq $index) {
                # Highlight current line
                Write-Host ("> {0} {1}" -f $mark, $name)
            } else {
                Write-Host ("  {0} {1}" -f $mark, $name)
            }
        }

        $key = [System.Console]::ReadKey($true)

        switch ($key.Key) {
            'UpArrow'   { if ($index -gt 0) { $index-- } else { $index = $Items.Count - 1 } }
            'DownArrow' { if ($index -lt ($Items.Count - 1)) { $index++ } else { $index = 0 } }
            'Spacebar'  {
                $curName = $Items[$index]
                $selected[$curName] = -not $selected[$curName]
            }
            'Enter' {
                $result = @()
                foreach ($name in $Items) {
                    if ($selected[$name]) { $result += $name }
                }
                return $result
            }
            'Escape' { return @() }
            default { }
        }
    }
}

#==============================================================================
# Function: Ensure-AppHostManifestLink
#==============================================================================
<#
.SYNOPSIS
    Ensure AppHost can resolve manifest.json at runtime without duplicating files.
.DESCRIPTION
    AppHost resolves "..\..\..\manifest.json" relative to its bin folder, which maps to Files/Aspire/AppHost/manifest.json.
    To preserve single source of truth (Files/Aspire/manifest.json), this function creates a temporary NTFS hard link
    (or symbolic link as fallback) at Files/Aspire/AppHost/manifest.json pointing to Files/Aspire/manifest.json.
    The caller may remove the link after execution.
.PARAMETER SourcePath
    Path to the authoritative manifest file (defaults to $global:ManifestPath).
.PARAMETER DestinationPath
    Destination path where AppHost expects manifest.json (defaults to "$PSScriptRoot\manifest.json").
.OUTPUTS
    [bool] True if a new link was created, False if destination already existed.
#>
function Ensure-AppHostManifestLink {
    [CmdletBinding(SupportsShouldProcess=$true)]
    param(
        [Parameter(Mandatory=$false)]
        [string]$SourcePath = $global:ManifestPath,
        [Parameter(Mandatory=$false)]
        [string]$DestinationPath = (Join-Path $PSScriptRoot "manifest.json")
    )

    if (-not (Test-Path -LiteralPath $SourcePath)) {
        Write-Error "Source manifest not found: $SourcePath"
        return $false
    }

    if (Test-Path -LiteralPath $DestinationPath) {
        # Destination already exists; do not modify to avoid breaking expectations.
        return $false
    }

    if ($PSCmdlet.ShouldProcess($DestinationPath, "Create link to manifest")) {
        try {
            # Prefer hard link to avoid permission issues with symlinks
            New-Item -ItemType HardLink -Path $DestinationPath -Target $SourcePath -ErrorAction Stop | Out-Null
            Write-Host "Created hard link: `"$DestinationPath`" -> `"$SourcePath`""
            return $true
        } catch {
            try {
                New-Item -ItemType SymbolicLink -Path $DestinationPath -Target $SourcePath -ErrorAction Stop | Out-Null
                Write-Host "Created symbolic link: `"$DestinationPath`" -> `"$SourcePath`""
                return $true
            } catch {
                Write-Error "Failed to create link for manifest: $($_.Exception.Message)"
                return $false
            }
        }
    }

    return $false
}

#==============================================================================
# Function: Invoke-AppHost
#==============================================================================
<#
.SYNOPSIS
    Invoke the AppHost with an optional profile and selected resources.
.DESCRIPTION
    Builds a comma-separated list from the selected resources and runs:
      dotnet run --project AppHost.csproj -- <profile> <csv>
    Also sets ASPIRE_MANIFEST_PROFILE and ASPIRE_RESOURCES environment variables.
.PARAMETER Profile
    Optional profile overlay (e.g., 'local', 'prod').
.PARAMETER SelectedResources
    Array of selected resource names; converted to CSV for AppHost arg/env.
.EXAMPLE
    PS > Invoke-AppHost -Profile local -SelectedResources @('n8n','qdrant')
.OUTPUTS
    [void]
.NOTES
    Uses Write-Host for status; relies on dotnet CLI being available on PATH.
#>
function Invoke-AppHost {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory = $false)]
        [string]$Profile,
        [Parameter(Mandatory = $true)]
        [string[]]$SelectedResources
    )

    if ($SelectedResources.Count -eq 0) {
        Write-Warning "No resources selected. Aborting."
        return
    }

    $csv = [string]::Join(",", $SelectedResources)

    # Export environment variables for AppHost
    if ($Profile) {
        $env:ASPIRE_MANIFEST_PROFILE = $Profile
    } else {
        Remove-Item Env:ASPIRE_MANIFEST_PROFILE -ErrorAction SilentlyContinue
    }

    $env:ASPIRE_RESOURCES = $csv

    # Create a temporary link where AppHost expects its manifest (no duplication)
    $destManifestPath = Join-Path $PSScriptRoot "manifest.json"
    $linkCreated = Ensure-AppHostManifestLink -SourcePath $global:ManifestPath -DestinationPath $destManifestPath

    $argsList = @()
    if ($Profile) { $argsList += $Profile } else { $argsList += "" }
    $argsList += $csv

    # Detect target framework from the project (supports TargetFramework or TargetFrameworks)
    $tfm = $null
    try {
        $projXml = [xml](Get-Content -LiteralPath $global:AppHostProject -Raw)
        $tfm = $projXml.Project.PropertyGroup.TargetFramework
        if (-not $tfm -or [string]::IsNullOrWhiteSpace($tfm)) {
            $tfm = $projXml.Project.PropertyGroup.TargetFrameworks
        }
        if ($tfm -and ($tfm -like "*;*")) {
            $tfm = ($tfm -split ';')[0]
        }
    } catch {
        # Fallback if parsing fails
        $tfm = $null
    }
    if (-not $tfm -or [string]::IsNullOrWhiteSpace($tfm)) {
        $tfm = "net8.0"
    }

    $cmd = "dotnet run --project `"$($global:AppHostProject)`" --framework $tfm -- $($argsList -join ' ')"
    if ($PSCmdlet.ShouldProcess("AppHost", "Run with resources: $csv, profile: $Profile, framework: $tfm")) {
        Write-Host "Executing:"
        Write-Host "  $cmd"
        & dotnet run --project $global:AppHostProject --framework $tfm -- @argsList
    }

    # Clean up temporary link to preserve single-source-of-truth
    if ($linkCreated -and (Test-Path -LiteralPath $destManifestPath)) {
        try { Remove-Item -LiteralPath $destManifestPath -Force } catch { }
    }
}

#==============================================================================
# Main
#==============================================================================
# Load resource names from manifest
$resourceNames = Get-ManifestResourceNames -Path $global:ManifestPath
if ($resourceNames.Count -eq 0) {
    Write-Error "No resources found in manifest. Ensure the manifest file is correct: $global:ManifestPath"
    exit 1
}

# Show selection menu
$selected = Show-CheckboxMenu -Items $resourceNames -Title "Select resources to start locally"
if ($selected.Count -eq 0) {
    Write-Warning "No selection made. Exiting."
    exit 0
}

# Run AppHost with chosen resources and optional profile
Invoke-AppHost -Profile $Profile -SelectedResources $selected