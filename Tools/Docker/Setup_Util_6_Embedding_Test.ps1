################################################################################
# File         : Setup_6_Embedding_Test.ps1
# Description  : Script to test the Embedding API.
#                Calls the API with sample text lines, decodes the base64 embeddings,
#                compares repeated lines for high similarity, and checks that distinct
#                lines show lower similarity. Outputs "TEST PASS" or "TEST FAIL".
# Usage        : Run after the Embedding API container is running.
################################################################################

# Dot-source the necessary helper function files.
. "$PSScriptRoot\Setup_Helper_NetworkTests.ps1" # For Test-TCPPort

##############################################################################
# Purpose:
#   This script calls the local Embedding API multiple times with several lines
#   of text, decodes the base64-encoded embeddings, compares repeated lines for
#   consistency, checks that distinct lines exhibit lower similarity, and then
#   prints "TEST PASS" or "TEST FAIL" accordingly.
##############################################################################

# Model selection menu
$modelConfigs = @(
	@{ ModelName = 'sentence-transformers/all-mpnet-base-v2'; Port = 8000 },
	@{ ModelName = 'Snowflake/snowflake-arctic-embed-l-v2.0'; Port = 8001 }
)
Write-Host 'Select embedding model to test:'
for ($i = 0; $i -lt $modelConfigs.Count; $i++) {
	$cfg = $modelConfigs[$i]
	Write-Host "[$($i+1)] $($cfg.ModelName) on port $($cfg.Port)"
}
$selection = Read-Host 'Enter choice number'
if (($selection -as [int]) -lt 1 -or ($selection -as [int]) -gt $modelConfigs.Count) {
	Write-Error 'Invalid selection'
	exit 1
}
$selectedConfig = $modelConfigs[$selection - 1]
$global:ModelName = $selectedConfig.ModelName
$global:Port = $selectedConfig.Port

Write-Host "Testing model: $global:ModelName on port: $global:Port"
Write-Host ""

# Overall test flag.
$testPass = $true

#==============================================================================
# Function: ConvertFrom-Embedding
#==============================================================================
<#
.SYNOPSIS
	Decodes a base64 encoded string representing an array of single-precision floats.
.DESCRIPTION
	Takes a base64 string, decodes it into a byte array, and then interprets
	every 4 bytes as a single-precision float (System.Single), returning a list of floats.
.PARAMETER EmbeddingBase64
	The base64 encoded string containing the float data. Mandatory.
.OUTPUTS
	[System.Collections.Generic.List[float]] A list of floats decoded from the base64 string,
											 or $null if decoding fails.
.EXAMPLE
	$floatList = ConvertFrom-Embedding -EmbeddingBase64 "AAAAAAAAAAA=" # Example base64
.NOTES
	Uses System.Convert::FromBase64String and System.BitConverter::ToSingle.
#>
Function ConvertFrom-Embedding {
	param(
		[Parameter(Mandatory)] [string] $EmbeddingBase64
	)
	$bytes = [System.Convert]::FromBase64String($EmbeddingBase64)
	if (-not $bytes) {
		Write-Error "Failed to decode base64 string."
		return $null
	}
	$floats = New-Object System.Collections.Generic.List[float]
	for ($i = 0; $i -lt $bytes.Length; $i += 4) {
		[single]$val = [System.BitConverter]::ToSingle($bytes, $i)
		$floats.Add($val)
	}
	return $floats
}

#==============================================================================
# Function: Get-CosineSimilarity
#==============================================================================
<#
.SYNOPSIS
	Calculates the cosine similarity between two vectors (lists of floats).
.DESCRIPTION
	Computes the dot product of the two input vectors and divides it by the product
	of their magnitudes to determine the cosine similarity. Handles zero magnitudes.
.PARAMETER vecA
	The first vector as a list of floats. Mandatory.
.PARAMETER vecB
	The second vector as a list of floats. Mandatory.
.OUTPUTS
	[double] The cosine similarity value between -1 and 1. Returns 0 if either vector has zero magnitude.
.EXAMPLE
	$similarity = Get-CosineSimilarity -vecA $list1 -vecB $list2
.NOTES
	Throws an error if the input vectors have different lengths.
#>
Function Get-CosineSimilarity {
	param(
		[Parameter(Mandatory)] [System.Collections.Generic.List[float]] $vecA,
		[Parameter(Mandatory)] [System.Collections.Generic.List[float]] $vecB
	)
	if ($vecA.Count -ne $vecB.Count) {
		throw "Vectors differ in length."
	}
	$dot = 0.0
	$magA = 0.0
	$magB = 0.0
	for ($i = 0; $i -lt $vecA.Count; $i++) {
		$dot += $vecA[$i] * $vecB[$i]
		$magA += $vecA[$i] * $vecA[$i]
		$magB += $vecB[$i] * $vecB[$i]
	}
	if ($magA -eq 0 -or $magB -eq 0) {
		return 0
	}
	return $dot / ([Math]::Sqrt($magA) * [Math]::Sqrt($magB))
}

#==============================================================================
# Function: Get-EmbeddingFromAPI
#==============================================================================
<#
.SYNOPSIS
	Requests an embedding for a given text line from the local Embedding API.
.DESCRIPTION
	Sends a POST request to the configured embedding API endpoint with a JSON body
	containing the specified model name and input text line. Parses the response,
	extracts the base64 encoded embedding, decodes it using ConvertFrom-Embedding,
	and returns the resulting list of floats.
.PARAMETER textLine
	The string of text to get an embedding for. Mandatory.
.PARAMETER modelName
	The name of the embedding model to use (passed in the API request). Mandatory.
.PARAMETER port
	The port number where the API is running. Mandatory.
.OUTPUTS
	[System.Collections.Generic.List[float]] A list of floats representing the embedding,
											 or $null if the API call or decoding fails.
.EXAMPLE
	$embedding = Get-EmbeddingFromAPI -textLine "Example sentence" -modelName "model-name" -port 8000
.NOTES
	Uses Invoke-RestMethod for the API call and ConvertFrom-Embedding for decoding.
	Includes basic validation of the API response structure.
#>
Function Get-EmbeddingFromAPI {
	param(
		[Parameter(Mandatory)] [string] $textLine,
		[Parameter(Mandatory)] [string] $modelName,
		[Parameter(Mandatory)] [int] $port
	)

	$requestJson = @{
		model = $modelName
		input = $textLine
	} | ConvertTo-Json

	# Send POST request to the API.
	# -Method: specifies the HTTP method (POST).
	# -Uri: the API endpoint.
	# -Headers: includes the Content-Type header.
	# -Body: contains the JSON payload.
	$headers = @{ "Content-Type" = "application/json" }
	$apiUrl = "http://localhost:$port/v1/embeddings"
	$response = Invoke-RestMethod -Method Post -Uri $apiUrl -Body $requestJson -Headers $headers

	# Validate basic structure.
	if ($response.object -ne "list") {
		Write-Error "Response 'object' was not 'list' for text: $textLine"
		return $null
	}
	if (-not $response.data) {
		Write-Error "Missing 'data' array for text: $textLine"
		return $null
	}
	if (-not $response.data[0].embedding) {
		Write-Error "Missing 'embedding' for text: $textLine"
		return $null
	}

	$floats = ConvertFrom-Embedding $response.data[0].embedding
	if (-not $floats) {
		Write-Error "Decoded embedding is empty for text: $textLine"
		return $null
	}
	return $floats
}

try {

	############################################################################
	# 1) Check TCP connectivity and API readiness.
	############################################################################
	Write-Host "Checking TCP connectivity on port $global:Port..."
	$tcpOk = Test-TCPPort -ComputerName "localhost" -Port $global:Port -serviceName "Embedding API"
	if (-not $tcpOk) {
		Write-Error "TCP connectivity check failed."
		$testPass = $false
		throw "Connection test failed."
	}

	Write-Host "Waiting for model to initialize (checking every 10 seconds)..."

	$maxAttempts = 20
	$attempt = 1

	while ($attempt -le $maxAttempts -and -not $apiReady) {
		Write-Host "Attempt $attempt/$maxAttempts - Checking if API is ready..."
		try {
			$response = Invoke-WebRequest -Uri "http://localhost:$global:Port/v1/models" -Method GET -TimeoutSec 5 -ErrorAction Stop
			if ($response.StatusCode -eq 200) {
				$apiReady = $true
				Write-Host "API is ready!"
			}
		}
		catch {
			$attempt++
		}
		if (-not $apiReady) {
			Start-Sleep -Seconds 10
		}
	}

	if (-not $apiReady) {
		Write-Warning "API did not become ready within 10 minutes. You may need to wait longer or check container logs with: podman logs $global:containerName"
	}


	############################################################################
	# 2) Define test lines (including repetitions).
	############################################################################
	$testLines = @(
		"Hello world",
		"Hello",
		"Hello world", # repeated line
		"I love coding",
		"What is code?",
		"Hello world"   # another repeat
	)

	# Hashtable to store embeddings keyed by "text|index" to differentiate duplicates.
	$embeddings = @{}

	############################################################################
	# 3) Request embeddings for each test line.
	############################################################################
	for ($i = 0; $i -lt $testLines.Count; $i++) {
		$text = $testLines[$i]
		Write-Host "`n[$($i+1)/$($testLines.Count)] Requesting embedding for: '$text'"
		$floats = Get-EmbeddingFromAPI -textLine $text -modelName $global:ModelName -port $global:Port
		if (-not $floats) {
			Write-Error "Failed to obtain embedding for '$text'."
			$testPass = $false
			continue
		}
		if ($floats.Count -lt 1) {
			Write-Error "Embedding for '$text' is empty."
			$testPass = $false
		}
		$embeddings["$text|$i"] = $floats
	}

	############################################################################
	# 4) Compare repeated lines for high similarity (threshold = 0.9).
	############################################################################
	Write-Host "`nComparing repeated lines for consistency..."
	$repeatIndices = @(0, 2, 5)
	for ($j = 0; $j -lt $repeatIndices.Count; $j++) {
		for ($k = $j + 1; $k -lt $repeatIndices.Count; $k++) {
			$idxA = $repeatIndices[$j]
			$idxB = $repeatIndices[$k]
			$keyA = "Hello world|$idxA"
			$keyB = "Hello world|$idxB"
			if ($embeddings.ContainsKey($keyA) -and $embeddings.ContainsKey($keyB)) {
				$sim = Get-CosineSimilarity -vecA $embeddings[$keyA] -vecB $embeddings[$keyB]
				Write-Host (" - Cosine similarity for 'Hello world' (index $idxA) vs. (index $idxB): " + [Math]::Round($sim, 4))
				if ($sim -lt 0.9) {
					Write-Error ("Cosine similarity between repeated lines is below 0.9 (got $sim)")
					$testPass = $false
				}
			}
		}
	}

	############################################################################
	# 5) Compare distinct lines for lower similarity (expect < 0.7).
	############################################################################
	$helloIndex = 1  # "Hello"
	$codingIndex = 3 # "I love coding"
	$keyHello = "Hello|$helloIndex"
	$keyCoding = "I love coding|$codingIndex"
	if ($embeddings.ContainsKey($keyHello) -and $embeddings.ContainsKey($keyCoding)) {
		$cosSim = Get-CosineSimilarity -vecA $embeddings[$keyHello] -vecB $embeddings[$keyCoding]
		Write-Host "`nCosine similarity between 'Hello' and 'I love coding': $cosSim"
		if ($cosSim -ge 0.7) {
			Write-Error "Expected similarity between 'Hello' and 'I love coding' to be below 0.7."
			$testPass = $false
		}
	}

}
catch {
	Write-Error "`nAn exception occurred: $_"
	$testPass = $false
}
finally {
	Write-Host ""
	if ($testPass) {
		Write-Host "===== TEST PASS ====="
		exit 0
	}
	else {
		Write-Error "===== TEST FAIL ====="
		exit 1
	}
}
