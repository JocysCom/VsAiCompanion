<#
.SYNOPSIS
    Computes the Qdrant collection name and Project Alias.
    Provides an interactive menu to set the alias in Qdrant.

.DESCRIPTION
    1. Computes Qdrant collection name (SHA256 of workspace path).
    2. Extracts Project Alias from the path.
    3. Displays this information.
    4. Optionally (or interactively) sets the alias in the Qdrant instance.

.PARAMETER Path
    The workspace path to process. Defaults to current directory.

.PARAMETER SetAlias
    If specified, attempts to set the alias immediately (non-interactive).

.PARAMETER QdrantUrl
    The URL of the Qdrant instance. Defaults to "http://localhost:6333".

.PARAMETER ApiKey
    The API Key for Qdrant authentication.

.PARAMETER NoMenu
    If specified, suppresses the interactive menu prompt at the end.

.EXAMPLE
    .\Get-QdrantInfo.ps1
    Shows info, then prompts to set alias.

.EXAMPLE
    .\Get-QdrantInfo.ps1 -SetAlias -ApiKey "xyz"
    Sets alias immediately without prompt.

.EXAMPLE
    .\Get-QdrantInfo.ps1 -NoMenu
    Just shows info and returns object (for automation).
#>

param (
    [string]$Path = ".",
    [switch]$SetAlias,
    [string]$QdrantUrl = "http://localhost:6333",
    [string]$ApiKey = "",
    [switch]$NoMenu
)

$ErrorActionPreference = "Stop"

# --- 1. Compute Info ---

# Resolve absolute path
try {
    $item = Get-Item -Path $Path
    $absPath = $item.FullName
}
catch {
    Write-Error "Path not found: $Path"
    exit 1
}

# Collection Name: SHA256 -> hex -> first 16 chars -> prefix "ws-"
# IMPORTANT: Roo Code often uses lowercase drive letters (e.g. "c:\Projects...")
# We must ensure the drive letter is lowercase for the hash to match.
if ($absPath -match "^([a-zA-Z]):(.*)$") {
    $drive = $matches[1].ToLower()
    $rest = $matches[2]
    $hashPath = "$($drive):$($rest)"
} else {
    $hashPath = $absPath
}

$encoding = [System.Text.Encoding]::UTF8
$bytes = $encoding.GetBytes($hashPath)
$sha256 = [System.Security.Cryptography.SHA256]::Create()
$hashBytes = $sha256.ComputeHash($bytes)
$hashHex = [System.BitConverter]::ToString($hashBytes).Replace("-", "").ToLower()
$collectionName = "ws-" + $hashHex.Substring(0, 16)

# Alias: Extract from C:\Projects\{Org}\{Proj}\{Repo}
$alias = $absPath
if ($absPath -match "^[a-zA-Z]:[\\/]Projects[\\/](.+)$") {
    $alias = $matches[1]
}

# Result Object
$result = [PSCustomObject]@{
    WorkspacePath   = $absPath
    CollectionName  = $collectionName
    Alias           = $alias
}

# --- 2. Display Info ---
Write-Host "========================================" -ForegroundColor Magenta
Write-Host "       Qdrant Workspace Info            " -ForegroundColor Magenta
Write-Host "========================================" -ForegroundColor Magenta
Write-Host "Workspace Path:  " -NoNewline; Write-Host $result.WorkspacePath -ForegroundColor Cyan
Write-Host "Collection Name: " -NoNewline; Write-Host $result.CollectionName -ForegroundColor Green
Write-Host "Alias:           " -NoNewline; Write-Host $result.Alias -ForegroundColor Yellow
Write-Host "Qdrant URL:      " -NoNewline; Write-Host $QdrantUrl -ForegroundColor Gray
Write-Host "========================================" -ForegroundColor Magenta
Write-Host ""

# Helper Function to Set Alias
function Set-QdrantAliasInternal {
    param ($Url, $Key, $ColName, $AliasName)

    Write-Host "Connecting to $Url..." -ForegroundColor Gray
    
    $headers = @{}
    if (-not [string]::IsNullOrEmpty($Key)) {
        $headers["api-key"] = $Key
    }

    # Check Collection
    $collectionUrl = "$Url/collections/$ColName"
    try {
        $null = Invoke-RestMethod -Uri $collectionUrl -Method Get -Headers $headers -ErrorAction Stop
        Write-Host "Verified: Collection '$ColName' exists." -ForegroundColor Green
    }
    catch {
        Write-Warning "Collection '$ColName' NOT found."
        Write-Warning "Ensure Roo Code has indexed this workspace."
        return
    }

    # Set Alias
    $aliasUrl = "$Url/collections/aliases"
    $body = @{
        actions = @(
            @{
                create_alias = @{
                    collection_name = $ColName
                    alias_name      = $AliasName
                }
            }
        )
    } | ConvertTo-Json -Depth 4

    try {
        $response = Invoke-RestMethod -Uri $aliasUrl -Method Post -Body $body -ContentType "application/json" -Headers $headers -ErrorAction Stop
        if ($response.result -eq $true) {
            Write-Host "SUCCESS: Alias '$AliasName' set!" -ForegroundColor Cyan
        }
        else {
            Write-Error "FAILED: $($response | ConvertTo-Json -Depth 2)"
        }
    }
    catch {
        Write-Error "Error setting alias: $_"
    }
}

# --- 3. Handle Actions ---

# Case A: -SetAlias switch used (Non-interactive / Immediate)
if ($SetAlias) {
    Set-QdrantAliasInternal -Url $QdrantUrl -Key $ApiKey -ColName $collectionName -AliasName $alias
}
# Case B: Interactive Menu (Default, unless -NoMenu)
elseif (-not $NoMenu) {
    Write-Host "Actions:" -ForegroundColor White
    Write-Host "  [S] Set Alias (Connects to Qdrant)" -ForegroundColor Cyan
    Write-Host "  [Q] Quit" -ForegroundColor Gray
    
    $choice = Read-Host "`nSelect option"
    
    if ($choice -eq "S" -or $choice -eq "s") {
        # Ask for API Key if not provided param
        $inputKey = $ApiKey
        if ([string]::IsNullOrEmpty($inputKey)) {
            Write-Host "Enter Qdrant API Key (Leave empty if none): " -ForegroundColor Yellow -NoNewline
            $inputKey = Read-Host
        }

        Set-QdrantAliasInternal -Url $QdrantUrl -Key $inputKey -ColName $collectionName -AliasName $alias
    }
}

# Return result for pipeline (if needed)
return $result