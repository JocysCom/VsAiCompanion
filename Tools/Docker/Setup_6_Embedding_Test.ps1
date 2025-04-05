################################################################################
# File         : Setup_6_Embedding_Test.ps1
# Description  : Script to test the Embedding API.
#                Calls the API with sample text lines, decodes the base64 embeddings,
#                compares repeated lines for high similarity, and checks that distinct
#                lines show lower similarity. Outputs "TEST PASS" or "TEST FAIL".
# Usage        : Run after the Embedding API container is running.
################################################################################

##############################################################################
# Purpose:
#   This script calls the local Embedding API multiple times with several lines
#   of text, decodes the base64-encoded embeddings, compares repeated lines for
#   consistency, checks that distinct lines exhibit lower similarity, and then
#   prints "TEST PASS" or "TEST FAIL" accordingly.
##############################################################################

# Dot-source any common helper script if available.
. "$PSScriptRoot\Setup_0.ps1" 2>$null | Out-Null

# Overall test flag.
$testPass = $true

try {
    ############################################################################
    # 1) Helper Functions: Decode embedding and compute cosine similarity.
    ############################################################################
    Function ConvertFrom-Embedding { # Renamed function
        param(
            [Parameter(Mandatory)] [string] $EmbeddingBase64
        )
        $bytes = [System.Convert]::FromBase64String($EmbeddingBase64)
        if (-not $bytes) {
            Write-Error "Failed to decode base64 string."
            return $null
        }
        # $floatCount = $bytes.Length / 4 # Removed unused variable
        $floats = New-Object System.Collections.Generic.List[float]
        for ($i = 0; $i -lt $bytes.Length; $i += 4) {
            [single]$val = [System.BitConverter]::ToSingle($bytes, $i)
            $floats.Add($val)
        }
        return $floats
    }

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

    Function Get-EmbeddingFromAPI {
        param(
            [Parameter(Mandatory)] [string] $textLine,
            [Parameter(Mandatory)] [string] $modelName
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
        $response = Invoke-RestMethod -Method Post -Uri "http://localhost:8000/v1/embeddings" -Body $requestJson -Headers $headers

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

        $floats = ConvertFrom-Embedding $response.data[0].embedding # Use renamed function
        if (-not $floats) {
            Write-Error "Decoded embedding is empty for text: $textLine"
            return $null
        }
        return $floats
    }

    ############################################################################
    # 2) Check TCP connectivity.
    ############################################################################
    Write-Output "Checking TCP connectivity on port 8000..." # Replaced Write-Host
    $tcpOk = Test-TCPPort -ComputerName "localhost" -Port 8000 -serviceName "Embedding API"
    if (-not $tcpOk) {
        Write-Error "TCP connectivity check failed."
        $testPass = $false
        throw "Connection test failed."
    }

    ############################################################################
    # 3) Define test lines (including repetitions).
    ############################################################################
    # The API model is 'sentence-transformers/all-mpnet-base-v2'.
    $modelName = "sentence-transformers/all-mpnet-base-v2"
    $testLines = @(
        "Hello world",
        "Hello",
        "Hello world",  # repeated line
        "I love coding",
        "What is code?",
        "Hello world"   # another repeat
    )

    # Hashtable to store embeddings keyed by "text|index" to differentiate duplicates.
    $embeddings = @{}

    ############################################################################
    # 4) Request embeddings for each test line.
    ############################################################################
    for ($i = 0; $i -lt $testLines.Count; $i++) {
        $text = $testLines[$i]
        Write-Output "`n[$($i+1)/$($testLines.Count)] Requesting embedding for: '$text'" # Replaced Write-Host
        $floats = Get-EmbeddingFromAPI -textLine $text -modelName $modelName
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
    # 5) Compare repeated lines for high similarity (threshold ≥ 0.9).
    ############################################################################
    Write-Output "`nComparing repeated lines for consistency..." # Replaced Write-Host
    $repeatIndices = @(0, 2, 5)
    for ($j = 0; $j -lt $repeatIndices.Count; $j++) {
        for ($k = $j + 1; $k -lt $repeatIndices.Count; $k++) {
            $idxA = $repeatIndices[$j]
            $idxB = $repeatIndices[$k]
            $keyA = "Hello world|$idxA"
            $keyB = "Hello world|$idxB"
            if ($embeddings.ContainsKey($keyA) -and $embeddings.ContainsKey($keyB)) {
                $sim = Get-CosineSimilarity -vecA $embeddings[$keyA] -vecB $embeddings[$keyB]
                Write-Output (" - Cosine similarity for 'Hello world' (index $idxA) vs. (index $idxB): " + [Math]::Round($sim, 4)) # Replaced Write-Host
                if ($sim -lt 0.9) {
                    Write-Error ("Cosine similarity between repeated lines is below 0.9 (got $sim)")
                    $testPass = $false
                }
            }
        }
    }

    ############################################################################
    # 6) Compare distinct lines for lower similarity (expect < 0.7).
    ############################################################################
    $helloIndex = 1  # "Hello"
    $codingIndex = 3 # "I love coding"
    $keyHello = "Hello|$helloIndex"
    $keyCoding = "I love coding|$codingIndex"
    if ($embeddings.ContainsKey($keyHello) -and $embeddings.ContainsKey($keyCoding)) {
        $cosSim = Get-CosineSimilarity -vecA $embeddings[$keyHello] -vecB $embeddings[$keyCoding]
        Write-Output "`nCosine similarity between 'Hello' and 'I love coding': $cosSim" # Replaced Write-Host
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
    Write-Output "" # Replaced Write-Host
    if ($testPass) {
        Write-Output "===== TEST PASS =====" # Replaced Write-Host
        exit 0
    }
    else {
        Write-Error "===== TEST FAIL ====="
        exit 1
    }
}
