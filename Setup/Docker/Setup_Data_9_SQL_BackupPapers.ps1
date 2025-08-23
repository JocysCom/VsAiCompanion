<#
.SYNOPSIS
    Export Summary and Document columns to HTML and Markdown files and
    download the PDF document.

.NOTES
    Works with .NET only (System.Data.SqlClient); no Invoke-Sqlcmd,
    therefore no truncation at 8 kB.
#>

param(
    [string]$ServerInstance,
    [string]$Database,
    [string]$SchemaName     = "Research",   #   schema
    [string]$TableName      = "Paper",      #   table
    [string]$OutputFolder   = "$PSScriptRoot\Backup\Papers"    #   where files are written
)

# Prompt for ServerInstance if not provided via command line
if ([string]::IsNullOrWhiteSpace($ServerInstance)) {
    $ServerInstanceInput = Read-Host "Enter SQL Server instance name (default: localhost)"
    # If the user presses Enter without typing anything, use the default
    $ServerInstance = if ([string]::IsNullOrWhiteSpace($ServerInstanceInput)) { "localhost" } else { $ServerInstanceInput }
}

# Prompt for Database if not provided via command line
if ([string]::IsNullOrWhiteSpace($Database)) {
    $DatabaseInput = Read-Host "Enter Database name (default: n8n)"
    # If the user presses Enter without typing anything, use the default
    $Database = if ([string]::IsNullOrWhiteSpace($DatabaseInput)) { "n8n" } else { $DatabaseInput }
}

#──────────────────────────────────────────────────────────────────────────
# 0. Prompt for SQL login
#──────────────────────────────────────────────────────────────────────────
Write-Host "Enter SQL login for instance $ServerInstance"
$UserName = Read-Host "User name"
$Password = Read-Host "Password" -AsSecureString

# Build a plain connection string
$connectionString = "Server=$ServerInstance;" +
                    "Database=$Database;"    +
                    "User ID=$UserName;" +
                    "Password=$([System.Net.NetworkCredential]::new('', $Password).Password);" +
                    "TrustServerCertificate=True;"     # removes TLS warning in test setups

#──────────────────────────────────────────────────────────────────────────
# 2. Fetch all rows with .NET – NO length restriction
#──────────────────────────────────────────────────────────────────────────
$query = @"
SELECT  *
FROM    [$SchemaName].[$TableName]
WHERE   ISNULL([HtmlSummary], '') <> '' AND ISNULL([HtmlDocument], '') <> ''
"@

#──────────────────────────────────────────────────────────────────────────
# 1. Make sure the output directory exists
#──────────────────────────────────────────────────────────────────────────
if (-not (Test-Path $OutputFolder)) {
    New-Item -Path $OutputFolder -ItemType Directory -Force | Out-Null
}

Add-Type -AssemblyName System.Data

$connection = New-Object System.Data.SqlClient.SqlConnection $connectionString
$command    = $connection.CreateCommand()
$command.CommandText = $query

try {
    $connection.Open()
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter $command
    $table   = New-Object System.Data.DataTable
    $null    = $adapter.Fill($table)        # DataTable now contains all rows
    $connection.Close()
}
catch {
    Write-Error "SQL query failed: $($_.Exception.Message)"
    if ($connection.State -eq 'Open') { $connection.Close() }
    exit 1
}

#──────────────────────────────────────────────────────────────────────────
# 3. Helper – sanitise a string so it is a valid Windows file name
#──────────────────────────────────────────────────────────────────────────
function Get-SafeFileName([string]$name) {
    ([regex]::Replace($name,'[\\\/:\*\?"<>|]', '_')).Trim()
}

# UTF-8 with BOM (= browsers recognise it as Unicode)
$utf8Bom = New-Object System.Text.UTF8Encoding $true

#──────────────────────────────────────────────────────────────────────────
# 4. Process each row
#──────────────────────────────────────────────────────────────────────────
$total = $table.Rows.Count
$idx   = 0

foreach ($row in $table.Rows) {
    $idx++

    # ----- create base name ---------------------------------------------
    $topic = if ([string]::IsNullOrWhiteSpace($row.OriginalTopic)) {
                 $row.Id.ToString()
             }
             else { $row.OriginalTopic }

    $base  = Get-SafeFileName $topic

    # ----- write HTML summary -------------------------------------------------
    if ($row.HtmlSummary -ne [DBNull]::Value) {
        $path = Join-Path $OutputFolder "$base.Summary.html"
        [System.IO.File]::WriteAllText($path, $row.HtmlSummary, $utf8Bom)
    }
    # ----- write Markdown summary -------------------------------------------------
    if ($row.HtmlSummary -ne [DBNull]::Value) {
        $path = Join-Path $OutputFolder "$base.Summary.md"
        [System.IO.File]::WriteAllText($path, $row.MarkdownSummary, $utf8Bom)
    }

    # ----- write HTML document ------------------------------------------------
    if ($row.HtmlDocument -ne [DBNull]::Value) {
        $path = Join-Path $OutputFolder "$base.Document.html"
        [System.IO.File]::WriteAllText($path, $row.HtmlDocument, $utf8Bom)
    }
    # ----- write Markdown document ------------------------------------------------
    if ($row.HtmlDocument -ne [DBNull]::Value) {
        $path = Join-Path $OutputFolder "$base.Document.md"
        [System.IO.File]::WriteAllText($path, $row.MarkdownDocument, $utf8Bom)
    }

    # ----- download PDF --------------------------------------------------
    if ($row.PdfUrl -ne [DBNull]::Value -and $row.PdfUrl.Trim()) {
        $pdfPath = Join-Path $OutputFolder "$base.pdf"
        try {
            Invoke-WebRequest -Uri $row.PdfUrl -OutFile $pdfPath -ErrorAction Stop
        }
        catch {
            Write-Warning "[$base] PDF download failed: $($_.Exception.Message)"
        }
    }

    Write-Host ("{0}/{1}  {2}" -f $idx, $total, $base)
}

Write-Host "`nFinished - files are in $OutputFolder"
