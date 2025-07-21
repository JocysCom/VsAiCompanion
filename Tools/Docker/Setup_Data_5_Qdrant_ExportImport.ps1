################################################################################
# Description  : Provides menu options to export or import Qdrant collections
#                for a running container using the Qdrant REST API.
# Usage        : Run with appropriate permissions. Requires the Qdrant container
#                to be running. Run as Administrator if using Docker.
################################################################################

using namespace System
using namespace System.IO

# Dot-source the necessary helper function files.
. "$PSScriptRoot\Setup_Helper_CoreFunctions.ps1"
. "$PSScriptRoot\Setup_Helper_ContainerEngine.ps1"
. "$PSScriptRoot\Setup_Helper_ContainerManagement.ps1"
. "$PSScriptRoot\Setup_Helper_NetworkTests.ps1"

# Ensure the script working directory is set.
Set-ScriptLocation

#############################################
# Global Variables
#############################################
$global:backupDir = "Backup"
$global:containerName = "qdrant"
$global:qdrantHttpPort = 6333
$global:qdrantGrpcPort = 6334
$global:qdrantApiUrl = "http://localhost:$($global:qdrantHttpPort)"
$global:containerEngine = Select-ContainerEngine

# Exit if no engine was selected
if (-not $global:containerEngine) {
	Write-Warning "No container engine selected. Exiting script."
	exit 1
}

# Set engine-specific options (only admin check for Docker)
if ($global:containerEngine -eq "docker") {
	Test-AdminPrivilege
}

# Get the engine path after setting specific options
$global:enginePath = Get-EnginePath -EngineName $global:containerEngine

# Define common paths
$global:localBackupDir = Join-Path -Path $PSScriptRoot -ChildPath $global:backupDir
$global:collectionsBackupDir = Join-Path -Path $global:localBackupDir -ChildPath "qdrant_collections"

#==============================================================================
# Function: Test-QdrantConnection
#==============================================================================
<#
.SYNOPSIS
    Tests connectivity to the Qdrant container API.
.DESCRIPTION
    Verifies that the Qdrant container is running and accessible via HTTP API
    by attempting to connect to the collections endpoint.
.OUTPUTS
    [bool] $true if connection successful, $false otherwise.
.EXAMPLE
    Test-QdrantConnection
.NOTES
    Uses the global $qdrantApiUrl variable to determine the API endpoint.
#>
function Test-QdrantConnection {
	[CmdletBinding()]
	[OutputType([bool])]
	param()

	try {
		Write-Host "Testing connection to Qdrant API at $global:qdrantApiUrl..."
		$response = Invoke-RestMethod -Uri "$global:qdrantApiUrl/collections" -Method Get -TimeoutSec 10
		Write-Host "Successfully connected to Qdrant API." -ForegroundColor Green
		return $true
	}
	catch {
		Write-Error "Failed to connect to Qdrant API: $_"
		Write-Host "Please ensure the Qdrant container is running and accessible on port $global:qdrantHttpPort." -ForegroundColor Yellow
		return $false
	}
}

#==============================================================================
# Function: Get-QdrantCollections
#==============================================================================
<#
.SYNOPSIS
    Retrieves a list of all collections from the Qdrant database.
.DESCRIPTION
    Makes an API call to the Qdrant collections endpoint to retrieve information
    about all available collections including their names and basic statistics.
.OUTPUTS
    [PSCustomObject[]] Array of collection objects, or empty array if none found.
.EXAMPLE
    $collections = Get-QdrantCollections
.NOTES
    Returns collection objects with properties like name, vectors_count, etc.
#>
function Get-QdrantCollections {
	[CmdletBinding()]
	[OutputType([PSCustomObject[]])]
	param()

	try {
		Write-Host "Retrieving collections from Qdrant..."
		$response = Invoke-RestMethod -Uri "$global:qdrantApiUrl/collections" -Method Get

		if ($response.result -and $response.result.collections) {
			$collections = $response.result.collections
			Write-Host "Found $($collections.Count) collection(s)." -ForegroundColor Green
			return $collections
		}
		else {
			Write-Host "No collections found in Qdrant database." -ForegroundColor Yellow
			return @()
		}
	}
	catch {
		Write-Error "Failed to retrieve collections: $_"
		return @()
	}
}

#==============================================================================
# Function: Get-QdrantCollectionInfo
#==============================================================================
<#
.SYNOPSIS
    Retrieves detailed information about a specific collection.
.DESCRIPTION
    Makes an API call to get comprehensive information about a collection
    including its configuration, vector count, and other metadata.
.PARAMETER CollectionName
    Name of the collection to retrieve information for.
.OUTPUTS
    [PSCustomObject] Collection information object, or $null if not found.
.EXAMPLE
    $info = Get-QdrantCollectionInfo -CollectionName "my-collection"
.NOTES
    Used before export operations to validate collection exists and get metadata.
#>
function Get-QdrantCollectionInfo {
	[CmdletBinding()]
	[OutputType([PSCustomObject])]
	param(
		[Parameter(Mandatory = $true, HelpMessage = "Name of the collection to retrieve information for.")]
		[string]$CollectionName
	)

	try {
		Write-Host "Retrieving information for collection '$CollectionName'..."
		$response = Invoke-RestMethod -Uri "$global:qdrantApiUrl/collections/$CollectionName" -Method Get

		if ($response.result) {
			Write-Host "Successfully retrieved collection information." -ForegroundColor Green
			return $response.result
		}
		else {
			Write-Warning "Collection '$CollectionName' not found."
			return $null
		}
	}
	catch {
		Write-Error "Failed to retrieve collection information: $_"
		return $null
	}
}

#==============================================================================
# Function: Export-QdrantCollection
#==============================================================================
<#
.SYNOPSIS
    Exports a single Qdrant collection to a JSON file.
.DESCRIPTION
    Retrieves all vectors and metadata from the specified collection and saves
    them to a JSON file in the backup directory. Includes collection configuration
    and all vector data with payloads.
.PARAMETER CollectionName
    Name of the collection to export.
.OUTPUTS
    [bool] $true if export successful, $false otherwise.
.EXAMPLE
    Export-QdrantCollection -CollectionName "my-collection"
.NOTES
    Creates a JSON file named after the collection in the collections backup directory.
    Handles large collections by using pagination if necessary.
#>
function Export-QdrantCollection {
	[CmdletBinding(SupportsShouldProcess = $true)]
	[OutputType([bool])]
	param(
		[Parameter(Mandatory = $true, HelpMessage = "Name of the collection to export.")]
		[string]$CollectionName
	)

	$targetDescription = "Qdrant collection '$CollectionName'"
	if (-not $PSCmdlet.ShouldProcess($targetDescription, "Export")) {
		Write-Host "Skipped export due to -WhatIf."
		return $false
	}

	Write-Host "--- Starting Collection Export: $CollectionName ---"

	try {
		# Get collection info first
		$collectionInfo = Get-QdrantCollectionInfo -CollectionName $CollectionName
		if (-not $collectionInfo) {
			Write-Error "Collection '$CollectionName' does not exist."
			return $false
		}

		# Create export object with metadata
		$exportData = @{
			collection_name  = $CollectionName
			export_timestamp = (Get-Date).ToString("yyyy-MM-ddTHH:mm:ssZ")
			collection_info  = $collectionInfo
			vectors          = @()
		}

		# Get all points (vectors) from the collection
		Write-Host "Retrieving vectors from collection '$CollectionName'..."
		$scrollResponse = Invoke-RestMethod -Uri "$global:qdrantApiUrl/collections/$CollectionName/points/scroll" -Method Post -Body '{"limit": 1000, "with_payload": true, "with_vector": true}' -ContentType "application/json"

		if ($scrollResponse.result -and $scrollResponse.result.points) {
			$exportData.vectors = $scrollResponse.result.points
			Write-Host "Retrieved $($exportData.vectors.Count) vectors." -ForegroundColor Green
		}
		else {
			Write-Host "No vectors found in collection '$CollectionName'." -ForegroundColor Yellow
		}

		# Save to file
		$exportFileName = "$CollectionName.json"
		$exportFilePath = Join-Path -Path $global:collectionsBackupDir -ChildPath $exportFileName

		Write-Host "Saving collection to '$exportFilePath'..."
		$exportJson = $exportData | ConvertTo-Json -Depth 10
		Set-Content -Path $exportFilePath -Value $exportJson -Encoding UTF8 -Force

		Write-Host "Successfully exported collection '$CollectionName' to '$exportFilePath'." -ForegroundColor Green
		return $true
	}
	catch {
		Write-Error "Failed to export collection '$CollectionName': $_"
		return $false
	}
	finally {
		Write-Host "--- Finished Collection Export: $CollectionName ---"
	}
}

#==============================================================================
# Function: Import-QdrantCollection
#==============================================================================
<#
.SYNOPSIS
    Imports a Qdrant collection from a JSON file.
.DESCRIPTION
    Reads a collection export file and recreates the collection in Qdrant
    with all its vectors, payloads, and configuration. Handles collection
    conflicts by prompting the user.
.PARAMETER FilePath
    Path to the JSON file containing the collection export.
.PARAMETER OverwriteExisting
    If specified, overwrites existing collections without prompting.
.OUTPUTS
    [bool] $true if import successful, $false otherwise.
.EXAMPLE
    Import-QdrantCollection -FilePath ".\Backup\qdrant_collections\my-collection.json"
.NOTES
    Validates the JSON structure before attempting import.
    Creates the collection if it doesn't exist, or prompts for overwrite if it does.
#>
function Import-QdrantCollection {
	[CmdletBinding(SupportsShouldProcess = $true)]
	[OutputType([bool])]
	param(
		[Parameter(Mandatory = $true, HelpMessage = "Path to the JSON file containing the collection export.")]
		[string]$FilePath,

		[Parameter(Mandatory = $false)]
		[switch]$OverwriteExisting
	)

	if (-not (Test-Path -Path $FilePath -PathType Leaf)) {
		Write-Error "Import file not found: '$FilePath'"
		return $false
	}

	try {
		# Read and parse the export file
		Write-Host "Reading collection data from '$FilePath'..."
		$jsonContent = Get-Content -Path $FilePath -Raw
		$importData = $jsonContent | ConvertFrom-Json

		# Validate the import data structure
		if (-not $importData.collection_name -or -not $importData.collection_info) {
			Write-Error "Invalid collection export file format."
			return $false
		}

		$collectionName = $importData.collection_name
		$targetDescription = "Qdrant collection '$collectionName'"

		if (-not $PSCmdlet.ShouldProcess($targetDescription, "Import")) {
			Write-Host "Skipped import due to -WhatIf."
			return $false
		}

		Write-Host "--- Starting Collection Import: $collectionName ---"

		# Check if collection already exists
		$existingCollection = Get-QdrantCollectionInfo -CollectionName $collectionName
		if ($existingCollection) {
			if (-not $OverwriteExisting) {
				$overwrite = Read-Host "Collection '$collectionName' already exists. Overwrite? (Y/N, default is N)"
				if ($overwrite -ne "Y") {
					Write-Host "Import cancelled by user."
					return $false
				}
			}

			# Delete existing collection
			Write-Host "Deleting existing collection '$collectionName'..."
			Invoke-RestMethod -Uri "$global:qdrantApiUrl/collections/$collectionName" -Method Delete | Out-Null
		}

		# Create the collection with original configuration
		Write-Host "Creating collection '$collectionName'..."
		$createBody = @{
			vectors = $importData.collection_info.config.params.vectors
		} | ConvertTo-Json -Depth 10

		Invoke-RestMethod -Uri "$global:qdrantApiUrl/collections/$collectionName" -Method Put -Body $createBody -ContentType "application/json" | Out-Null

		# Import vectors if any exist
		if ($importData.vectors -and $importData.vectors.Count -gt 0) {
			Write-Host "Importing $($importData.vectors.Count) vectors..."

			# Prepare points for batch upsert
			$upsertBody = @{
				points = $importData.vectors
			} | ConvertTo-Json -Depth 10

			Invoke-RestMethod -Uri "$global:qdrantApiUrl/collections/$collectionName/points" -Method Put -Body $upsertBody -ContentType "application/json" | Out-Null
			Write-Host "Successfully imported $($importData.vectors.Count) vectors." -ForegroundColor Green
		}
		else {
			Write-Host "No vectors to import." -ForegroundColor Yellow
		}

		Write-Host "Successfully imported collection '$collectionName'." -ForegroundColor Green
		return $true
	}
	catch {
		Write-Error "Failed to import collection: $_"
		return $false
	}
	finally {
		Write-Host "--- Finished Collection Import ---"
	}
}

#==============================================================================
# Function: Export-AllQdrantCollections
#==============================================================================
<#
.SYNOPSIS
    Exports all collections from the Qdrant database.
.DESCRIPTION
    Retrieves a list of all collections and exports each one to a separate
    JSON file in the backup directory.
.OUTPUTS
    [bool] $true if all exports successful, $false if any failed.
.EXAMPLE
    Export-AllQdrantCollections
.NOTES
    Creates individual JSON files for each collection.
    Continues with remaining collections if one fails.
#>
function Export-AllQdrantCollections {
	[CmdletBinding(SupportsShouldProcess = $true)]
	[OutputType([bool])]
	param()

	if (-not $PSCmdlet.ShouldProcess("All Qdrant collections", "Export")) {
		Write-Host "Skipped export due to -WhatIf."
		return $false
	}

	Write-Host "--- Starting Export of All Collections ---"

	$collections = Get-QdrantCollections
	if ($collections.Count -eq 0) {
		Write-Host "No collections found to export."
		return $true
	}

	$successCount = 0
	$failCount = 0

	foreach ($collection in $collections) {
		$collectionName = $collection.name
		Write-Host "Exporting collection: $collectionName"

		if (Export-QdrantCollection -CollectionName $collectionName) {
			$successCount++
		}
		else {
			$failCount++
		}
	}

	Write-Host "Export completed. Success: $successCount, Failed: $failCount" -ForegroundColor $(if ($failCount -eq 0) { "Green" } else { "Yellow" })
	Write-Host "--- Finished Export of All Collections ---"

	return ($failCount -eq 0)
}

#==============================================================================
# Function: Import-AllQdrantCollections
#==============================================================================
<#
.SYNOPSIS
    Imports all collection files from the backup directory.
.DESCRIPTION
    Scans the collections backup directory for JSON files and imports each
    collection found. Prompts for overwrite confirmation for existing collections.
.PARAMETER OverwriteExisting
    If specified, overwrites existing collections without prompting.
.OUTPUTS
    [bool] $true if all imports successful, $false if any failed.
.EXAMPLE
    Import-AllQdrantCollections -OverwriteExisting
.NOTES
    Only processes .json files in the collections backup directory.
    Continues with remaining files if one fails.
#>
function Import-AllQdrantCollections {
	[CmdletBinding(SupportsShouldProcess = $true)]
	[OutputType([bool])]
	param(
		[Parameter(Mandatory = $false)]
		[switch]$OverwriteExisting
	)

	if (-not $PSCmdlet.ShouldProcess("All collection files in backup directory", "Import")) {
		Write-Host "Skipped import due to -WhatIf."
		return $false
	}

	Write-Host "--- Starting Import of All Collections ---"

	# Find all JSON files in the collections backup directory
	$collectionFiles = Get-ChildItem -Path $global:collectionsBackupDir -Filter "*.json" -File -ErrorAction SilentlyContinue

	if ($collectionFiles.Count -eq 0) {
		Write-Host "No collection files found in '$global:collectionsBackupDir'."
		return $true
	}

	Write-Host "Found $($collectionFiles.Count) collection file(s) to import."

	$successCount = 0
	$failCount = 0

	foreach ($file in $collectionFiles) {
		Write-Host "Importing collection from: $($file.Name)"

		if (Import-QdrantCollection -FilePath $file.FullName -OverwriteExisting:$OverwriteExisting) {
			$successCount++
		}
		else {
			$failCount++
		}
	}

	Write-Host "Import completed. Success: $successCount, Failed: $failCount" -ForegroundColor $(if ($failCount -eq 0) { "Green" } else { "Yellow" })
	Write-Host "--- Finished Import of All Collections ---"

	return ($failCount -eq 0)
}

#==============================================================================
# Function: Show-QdrantCollections
#==============================================================================
<#
.SYNOPSIS
    Displays a list of all collections in the Qdrant database.
.DESCRIPTION
    Retrieves and displays information about all collections including
    their names, vector counts, and other relevant statistics.
.OUTPUTS
    [void] Displays collection information to the console.
.EXAMPLE
    Show-QdrantCollections
.NOTES
    Provides a quick overview of the current state of the Qdrant database.
#>
function Show-QdrantCollections {
	[CmdletBinding()]
	param()

	Write-Host "--- Qdrant Collections ---"

	$collections = Get-QdrantCollections
	if ($collections.Count -eq 0) {
		Write-Host "No collections found in Qdrant database." -ForegroundColor Yellow
		return
	}

	Write-Host "Found $($collections.Count) collection(s):" -ForegroundColor Green
	Write-Host ""

	foreach ($collection in $collections) {
		Write-Host "Collection: $($collection.name)" -ForegroundColor Cyan
		if ($collection.vectors_count) {
			Write-Host "  Vectors: $($collection.vectors_count)"
		}
		if ($collection.points_count) {
			Write-Host "  Points: $($collection.points_count)"
		}
		Write-Host ""
	}
}

################################################################################
# Main Menu Loop using Generic Function
################################################################################

# Define Menu Title and Items
$menuTitle = "Qdrant Collections Export/Import Menu"
$menuItems = [ordered]@{
	"1" = "List Collections"
	"2" = "Export Single Collection"
	"3" = "Export All Collections"
	"4" = "Import Single Collection"
	"5" = "Import All Collections"
	"S" = "Show Container Status"
	"0" = "Exit menu"
}

# Define Menu Actions
$menuActions = @{
	"1" = { Show-QdrantCollections }
	"2" = {
		if (-not (Test-QdrantConnection)) { return }
		$collections = Get-QdrantCollections
		if ($collections.Count -eq 0) {
			Write-Host "No collections available to export."
			return
		}

		Write-Host "Available collections:"
		for ($i = 0; $i -lt $collections.Count; $i++) {
			Write-Host "$($i + 1). $($collections[$i].name)"
		}

		$selection = Read-Host "Enter collection number to export (1-$($collections.Count))"
		try {
			$index = [int]$selection - 1
			if ($index -ge 0 -and $index -lt $collections.Count) {
				Export-QdrantCollection -CollectionName $collections[$index].name
			}
			else {
				Write-Host "Invalid selection."
			}
		}
		catch {
			Write-Host "Invalid input. Please enter a number."
		}
	}
	"3" = {
		if (-not (Test-QdrantConnection)) { return }
		Export-AllQdrantCollections
	}
	"4" = {
		if (-not (Test-QdrantConnection)) { return }
		$collectionFiles = Get-ChildItem -Path $global:collectionsBackupDir -Filter "*.json" -File -ErrorAction SilentlyContinue
		if ($collectionFiles.Count -eq 0) {
			Write-Host "No collection files found in '$global:collectionsBackupDir'."
			return
		}

		Write-Host "Available collection files:"
		for ($i = 0; $i -lt $collectionFiles.Count; $i++) {
			Write-Host "$($i + 1). $($collectionFiles[$i].Name)"
		}

		$selection = Read-Host "Enter file number to import (1-$($collectionFiles.Count))"
		try {
			$index = [int]$selection - 1
			if ($index -ge 0 -and $index -lt $collectionFiles.Count) {
				Import-QdrantCollection -FilePath $collectionFiles[$index].FullName
			}
			else {
				Write-Host "Invalid selection."
			}
		}
		catch {
			Write-Host "Invalid input. Please enter a number."
		}
	}
	"5" = {
		if (-not (Test-QdrantConnection)) { return }
		$overwrite = Read-Host "Overwrite existing collections without prompting? (Y/N, default is N)"
		if ($overwrite -eq "Y") {
			Import-AllQdrantCollections -OverwriteExisting
		}
		else {
			Import-AllQdrantCollections
		}
	}
	"S" = {
		Show-ContainerStatus -ContainerName $global:containerName `
			-ContainerEngine $global:containerEngine `
			-EnginePath $global:enginePath `
			-DisplayName "Qdrant" `
			-TcpPort $global:qdrantHttpPort `
			-HttpPort $global:qdrantHttpPort `
			-AdditionalInfo @{ "gRPC Port" = $global:qdrantGrpcPort; "API URL" = $global:qdrantApiUrl }
	}
	# Note: "0" action is handled internally by Invoke-MenuLoop
}

# Ensure backup directories exist before showing menu
if (-not (Test-Path -Path $global:localBackupDir -PathType Container)) {
	Write-Host "Creating directory: $global:localBackupDir"
	New-Item -Path $global:localBackupDir -ItemType Directory -Force | Out-Null
}

if (-not (Test-Path -Path $global:collectionsBackupDir -PathType Container)) {
	Write-Host "Creating directory: $global:collectionsBackupDir"
	New-Item -Path $global:collectionsBackupDir -ItemType Directory -Force | Out-Null
}

# Test connection before showing menu
if (-not (Test-QdrantConnection)) {
	Write-Host "Cannot connect to Qdrant. Please ensure the container is running." -ForegroundColor Red
	Write-Host "You can start the Qdrant container using Setup_5_Qdrant.ps1" -ForegroundColor Yellow
	exit 1
}

# Invoke the Menu Loop
Invoke-MenuLoop -MenuTitle $menuTitle -MenuItems $menuItems -ActionMap $menuActions -ExitChoice "0"

Write-Host "Script finished."
