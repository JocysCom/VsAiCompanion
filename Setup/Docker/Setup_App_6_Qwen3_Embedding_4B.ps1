################################################################################
# Description  : Script to manage Qwen3-Embedding-4B using Ollama.
#                Uses ollama/ollama Docker image with qwen3-embedding:4b model.
#                Provides OpenAI-compatible API endpoint (/v1/embeddings).
#                Loads configuration from Aspire manifest (Files/Aspire/manifest.json).
# Usage        : .\Setup_App_6_Qwen3_Embedding_4B.ps1 [-Profile local|prod]
# Features     : IN (Install), UN (Uninstall), UP (Update), BA (Backup)
# Model        : qwen3-embedding:4b (2.5GB, 40K context, 2560-dim)
# Docker Image : ollama/ollama:latest
################################################################################

using namespace System
using namespace System.IO
using namespace System.Diagnostics.CodeAnalysis

param(
    [Parameter(Mandatory=$false)]
    [string]$ManifestPath,

    [Parameter(Mandatory=$false)]
    [ValidateSet("local","prod")]
    [string]$Profile,

    [Parameter(Mandatory=$false)]
    [string]$ResourceName = "qwen3-embedding-4b"
)

# Dot-source the necessary helper function files.
. "$PSScriptRoot\Setup_Helper_CoreFunctions.ps1"
. "$PSScriptRoot\Setup_Helper_NetworkTests.ps1"
. "$PSScriptRoot\Setup_Helper_ContainerEngine.ps1"
. "$PSScriptRoot\Setup_Helper_ContainerManagement.ps1"
. "$PSScriptRoot\Setup_Helper_BackupRestore.ps1"

# Ensure the script working directory is set.
Set-ScriptLocation

#############################################
# Load Configuration from Aspire Manifest
#############################################

if (-not $ManifestPath -or [string]::IsNullOrWhiteSpace($ManifestPath)) {
    $ManifestPath = Join-Path $PSScriptRoot "Files\Aspire\manifest.json"
}

$baseManifestPath = $ManifestPath
$overlayManifestPath = $null
if ($Profile) {
    $manifestDir = Split-Path -Parent $ManifestPath
    $manifestFile = Split-Path -Leaf $ManifestPath
    $manifestNameNoExt = [IO.Path]::GetFileNameWithoutExtension($manifestFile)
    $manifestExt = [IO.Path]::GetExtension($manifestFile)
    $candidate = Join-Path $manifestDir ("{0}.{1}{2}" -f $manifestNameNoExt, $Profile, $manifestExt)
    if (Test-Path -LiteralPath $candidate) {
        $overlayManifestPath = $candidate
    } else {
        Write-Warning "Profile manifest not found: $candidate. Using base manifest: $ManifestPath"
    }
}

if (-not (Test-Path -LiteralPath $baseManifestPath)) {
    Write-Error "Manifest file not found at: $baseManifestPath"
    Write-Error "The Aspire manifest is required to run this script."
    exit 1
}

# Load base manifest
$baseManifest = Get-Content -Raw $baseManifestPath | ConvertFrom-Json

# Apply overlay if exists
if ($overlayManifestPath) {
    $overlayManifest = Get-Content -Raw $overlayManifestPath | ConvertFrom-Json
    $baseRes = $baseManifest.resources.$ResourceName
    $overlayRes = $overlayManifest.resources.$ResourceName
    if ($null -eq $baseRes -and $overlayRes) {
        $baseManifest.resources.$ResourceName = $overlayRes
    }
    elseif ($overlayRes -and $overlayRes.properties) {
        $bp = $baseRes.properties
        $op = $overlayRes.properties

        if ($op.image) { $bp.image = $op.image }
        if ($op.bindings) { $bp.bindings = $op.bindings }
        if ($op.volumes) { $bp.volumes = $op.volumes }
        if ($op.environment) {
            if ($null -eq $bp.environment) { $bp.environment = [PSCustomObject]@{} }
            foreach ($p in $op.environment.PSObject.Properties) {
                $bp.environment | Add-Member -NotePropertyName $p.Name -NotePropertyValue $p.Value -Force
            }
        }
    }
}

$manifest = $baseManifest
$manifestConfig = $manifest.resources.$ResourceName

if (-not $manifestConfig) {
    Write-Error "Resource '$ResourceName' not found in manifest at: $baseManifestPath"
    Write-Error "Expected resource name: $ResourceName"
    exit 1
}

Write-Host "Loading configuration from Aspire manifest for resource: $ResourceName" -ForegroundColor Green

# Extract configuration from manifest
$global:ollamaModel = $manifestConfig.properties.environment.OLLAMA_MODEL
$global:Port = $manifestConfig.properties.bindings[0].hostPort
$global:containerPort = $manifestConfig.properties.bindings[0].containerPort
$global:imageName = $manifestConfig.properties.image
$global:containerName = $ResourceName
$global:volumeName = $manifestConfig.properties.volumes[0].name
$global:dataPath = $manifestConfig.properties.volumes[0].containerPath

Write-Host "  Ollama Model: $global:ollamaModel"
Write-Host "  Port: $global:Port"
Write-Host "  Image: $global:imageName"
Write-Host "  Container: $global:containerName"
Write-Host "  Volume: $global:volumeName"

#############################################
# Engine Selection
#############################################
$global:containerEngine = Select-ContainerEngine
# Exit if no engine was selected
if (-not $global:containerEngine) {
	Write-Warning "No container engine selected. Exiting script."
	exit 1
}
# Set engine-specific options
if ($global:containerEngine -eq "docker") {
	Test-AdminPrivilege
	$global:pullOptions = @()
}
else {
	$global:pullOptions = @("--tls-verify=false")
}
# Get the engine path after setting specific options
$global:enginePath = Get-EnginePath -EngineName $global:containerEngine

#############################################
# GPU Configuration
#############################################
$global:useGpu = $true           # Set to $false to force CPU-only mode
$global:gpuDeviceIds = "all"     # Use "all" or specific IDs like "0" or "0,1"

#==============================================================================
# Function: Test-GpuAvailability
#==============================================================================
<#
.SYNOPSIS
	Tests if GPU/CUDA is available for the container engine.
.DESCRIPTION
	Checks if NVIDIA GPU is available by attempting to run nvidia-smi
	and verifying the container engine supports GPU devices.
.OUTPUTS
	[bool] Returns $true if GPU is available and usable, $false otherwise.
.NOTES
	For Docker: Requires nvidia-docker2 runtime
	For Podman: Requires nvidia-container-toolkit
#>
function Test-GpuAvailability {
	[CmdletBinding()]
	[OutputType([bool])]
	param()

	try {
		Write-Host "Checking GPU availability..." -ForegroundColor Yellow

		# Check if nvidia-smi is available on the host
		$nvidiaSmi = Get-Command nvidia-smi -ErrorAction SilentlyContinue
		if (-not $nvidiaSmi) {
			Write-Warning "nvidia-smi not found. GPU support not available."
			return $false
		}

		# Test nvidia-smi execution
		$result = nvidia-smi --query-gpu=name --format=csv,noheader 2>&1
		if ($LASTEXITCODE -ne 0) {
			Write-Warning "nvidia-smi failed to execute. GPU may not be properly configured."
			return $false
		}

		Write-Host "  Detected GPU(s): $result" -ForegroundColor Green

		# Check container engine GPU support
		if ($global:enginePath -match "docker") {
			Write-Host "  Testing Docker GPU access..." -ForegroundColor Yellow
			$null = & $global:enginePath run --rm --gpus all nvidia/cuda:12.0.0-base-ubuntu22.04 nvidia-smi 2>&1
			if ($LASTEXITCODE -eq 0) {
				Write-Host "  ✅ Docker GPU support verified." -ForegroundColor Green
				return $true
			} else {
				Write-Warning "Docker GPU test failed."
				Write-Warning "Install nvidia-docker2: https://docs.nvidia.com/datacenter/cloud-native/container-toolkit/install-guide.html"
				return $false
			}
		} elseif ($global:enginePath -match "podman") {
			Write-Host "  ✅ Podman detected. GPU support via CDI." -ForegroundColor Green
			Write-Host "     Container will attempt to use GPU if available." -ForegroundColor Cyan
			Write-Host "     Check container logs after startup to verify GPU usage." -ForegroundColor Cyan
			return $true
		}

		return $false
	}
	catch {
		Write-Warning "Error checking GPU availability: $_"
		return $false
	}
}

#==============================================================================
# Function: Install-EmbeddingContainer
#==============================================================================
<#
.SYNOPSIS
	Installs the Qwen3-Embedding-4B container using Ollama.
.DESCRIPTION
	Pulls Ollama image, creates container, pulls the qwen3-embedding:4b model.
.EXAMPLE
	Install-EmbeddingContainer
#>
function Install-EmbeddingContainer {
	Write-Host "Pulling Ollama image: $global:imageName..." -ForegroundColor Yellow
	& $global:enginePath pull $global:imageName
	if ($LASTEXITCODE -ne 0) {
		Write-Error "Failed to pull Ollama image."
		exit 1
	}

	Remove-ContainerAndVolume -Engine $global:enginePath -ContainerName $global:containerName -VolumeName $global:volumeName

	# Check GPU availability
	$gpuAvailable = $false
	if ($global:useGpu) {
		$gpuAvailable = Test-GpuAvailability
		if (-not $gpuAvailable) {
			Write-Warning "GPU not available or not configured. Falling back to CPU mode."
			Write-Warning "Installation will be slower. For GPU support, install:"
			Write-Warning "  Docker: nvidia-docker2 (https://docs.nvidia.com/datacenter/cloud-native/container-toolkit/install-guide.html)"
			Write-Warning "  Podman: nvidia-container-toolkit"
		}
	} else {
		Write-Host "GPU support disabled by configuration. Using CPU mode." -ForegroundColor Yellow
	}

	Write-Host "Running Ollama container..." -ForegroundColor Yellow

	$runArgs = @(
		"--detach",
		"--name", $global:containerName,
		"--publish", "$($global:Port):$global:containerPort",
		"--volume", "$($global:volumeName):$global:dataPath",
		"--restart", "always"
	)

	# Add GPU support if available
	if ($gpuAvailable) {
		if ($global:enginePath -match "docker") {
			$runArgs += "--gpus"
			$runArgs += $global:gpuDeviceIds
			Write-Host "  🚀 Configuring Docker with GPU devices: $($global:gpuDeviceIds)" -ForegroundColor Green
		} elseif ($global:enginePath -match "podman") {
			$runArgs += "--device"
			$runArgs += "nvidia.com/gpu=$($global:gpuDeviceIds)"
			$runArgs += "--security-opt=label=disable"
			Write-Host "  🚀 Configuring Podman with GPU devices: $($global:gpuDeviceIds)" -ForegroundColor Green
		}
	} else {
		Write-Host "  ⚠️ Running in CPU-only mode (slower performance)" -ForegroundColor Yellow
	}

	$runArgs += $global:imageName

	& $global:enginePath run $runArgs
	if ($LASTEXITCODE -ne 0) {
		if ($global:enginePath -match "podman") {
			Write-Warning "Failed to run container. Attempting Podman CDI repair..."
			if (Repair-PodmanCDI -EnginePath $global:enginePath) {
				Write-Host "Retrying container start..." -ForegroundColor Green
				& $global:enginePath run $runArgs
			}
		}

		if ($LASTEXITCODE -ne 0) {
			Write-Error "Failed to run Ollama container."
			exit 1
		}
	}

	Write-Host "Waiting for Ollama to start..." -ForegroundColor Yellow
	Start-Sleep -Seconds 10

	Write-Host "Pulling qwen3-embedding:4b model inside Ollama..." -ForegroundColor Yellow
	& $global:enginePath exec $global:containerName ollama pull $global:ollamaModel
	if ($LASTEXITCODE -ne 0) {
		Write-Error "Failed to pull embedding model."
		exit 1
	}

	Write-Host "Testing API..." -ForegroundColor Yellow
	Start-Sleep -Seconds 5

	Test-HTTPPort -Uri "http://localhost:$global:Port/api/tags" -serviceName "Ollama"
	Test-TCPPort -ComputerName "localhost" -Port $global:Port -serviceName "Ollama"

	Write-Host "`nQwen3-Embedding-4B is ready!" -ForegroundColor Green
	Write-Host "  Ollama API: http://localhost:$global:Port" -ForegroundColor Cyan
	Write-Host "  OpenAI-compatible: http://localhost:$global:Port/v1/embeddings" -ForegroundColor Cyan
	Write-Host "  Mode: $(if ($gpuAvailable) { '🚀 GPU-accelerated' } else { '💻 CPU-only' })" -ForegroundColor $(if ($gpuAvailable) { 'Green' } else { 'Yellow' })
	Write-Host "`nExample curl command:" -ForegroundColor Yellow
	Write-Host "  curl http://localhost:$global:Port/api/embed -d '{`"model`": `"qwen3-embedding:4b`", `"input`": `"Your text`"}'" -ForegroundColor Cyan
	Write-Host "  curl http://localhost:$global:Port/v1/embeddings -d '{`"model`": `"qwen3-embedding:4b`", `"input`": `"Your text`"}'" -ForegroundColor Cyan

	Update-AspireManifest
}

#==============================================================================
# Function: Update-AspireManifest
#==============================================================================
<#
.SYNOPSIS
	Updates the Aspire manifest with current configuration.
.DESCRIPTION
	Updates the resource in Files/Aspire/manifest.json.
.EXAMPLE
	Update-AspireManifest
#>
function Update-AspireManifest {
	[CmdletBinding(SupportsShouldProcess=$true)]
	param()

	$manifestPath = Join-Path -Path $PSScriptRoot -ChildPath "Files\Aspire\manifest.json"

	if (-not (Test-Path $manifestPath)) {
		Write-Warning "Aspire manifest not found at: $manifestPath"
		return
	}

	if ($PSCmdlet.ShouldProcess($manifestPath, "Update Aspire manifest")) {
		try {
			Write-Host "Updating Aspire manifest..." -ForegroundColor Yellow
			$manifest = Get-Content -Path $manifestPath -Raw | ConvertFrom-Json

			if (-not $manifest.resources) {
				$manifest | Add-Member -MemberType NoteProperty -Name "resources" -Value ([PSCustomObject]@{}) -Force
			}

			$embeddingApiConfig = [PSCustomObject]@{
				type = "container.v0"
				properties = [PSCustomObject]@{
					image = $global:imageName
					bindings = @(
						[PSCustomObject]@{
							name = "http"
							protocol = "tcp"
							containerPort = $global:containerPort
							hostPort = $global:Port
						}
					)
					volumes = @(
						[PSCustomObject]@{
							name = $global:volumeName
							containerPath = $global:dataPath
							type = "named"
						}
					)
					environment = [PSCustomObject]@{
						OLLAMA_MODEL = $global:ollamaModel
					}
					restart = "always"
				}
			}

			$resourceName = $global:containerName
			if ($manifest.resources.PSObject.Properties.Name -contains $resourceName) {
				$manifest.resources.$resourceName = $embeddingApiConfig
			} else {
				$manifest.resources | Add-Member -MemberType NoteProperty -Name $resourceName -Value $embeddingApiConfig -Force
			}

			$manifest | ConvertTo-Json -Depth 10 | Set-Content -Path $manifestPath -Encoding UTF8
			Write-Host "Aspire manifest updated successfully" -ForegroundColor Green
		} catch {
			Write-Warning "Failed to update Aspire manifest: $_"
		}
	}
}

#==============================================================================
# Function: Update-EmbeddingContainer
#==============================================================================
<#
.SYNOPSIS
	Updates the Qwen3-Embedding-4B container.
.DESCRIPTION
	Pulls latest Ollama image and updates the embedding model.
.EXAMPLE
	Update-EmbeddingContainer
#>
function Update-EmbeddingContainer {
	[CmdletBinding(SupportsShouldProcess = $true)]
	param()

	if (-not $PSCmdlet.ShouldProcess($global:containerName, "Update Container")) {
		return
	}

	Write-Host "Updating Ollama container..." -ForegroundColor Yellow

	# Pull latest Ollama image
	& $global:enginePath pull $global:imageName
	if ($LASTEXITCODE -ne 0) {
		Write-Warning "Failed to pull latest image."
	}

	# Check GPU availability
	$gpuAvailable = $false
	if ($global:useGpu) {
		$gpuAvailable = Test-GpuAvailability
		if (-not $gpuAvailable) {
			Write-Warning "GPU not available or not configured. Falling back to CPU mode."
		}
	} else {
		Write-Host "GPU support disabled by configuration. Using CPU mode." -ForegroundColor Yellow
	}

	# Stop and remove container (keep volume)
	& $global:enginePath stop $global:containerName 2>$null
	& $global:enginePath rm $global:containerName 2>$null

	# Restart with new image
	$runArgs = @(
		"--detach",
		"--name", $global:containerName,
		"--publish", "$($global:Port):$global:containerPort",
		"--volume", "$($global:volumeName):$global:dataPath",
		"--restart", "always"
	)

	# Add GPU support if available
	if ($gpuAvailable) {
		if ($global:enginePath -match "docker") {
			$runArgs += "--gpus"
			$runArgs += $global:gpuDeviceIds
			Write-Host "  🚀 Configuring Docker with GPU devices: $($global:gpuDeviceIds)" -ForegroundColor Green
		} elseif ($global:enginePath -match "podman") {
			$runArgs += "--device"
			$runArgs += "nvidia.com/gpu=$($global:gpuDeviceIds)"
			$runArgs += "--security-opt=label=disable"
			Write-Host "  🚀 Configuring Podman with GPU devices: $($global:gpuDeviceIds)" -ForegroundColor Green
		}
	} else {
		Write-Host "  ⚠️ Running in CPU-only mode (slower performance)" -ForegroundColor Yellow
	}

	$runArgs += $global:imageName

	& $global:enginePath run $runArgs
	if ($LASTEXITCODE -ne 0) {
		if ($global:enginePath -match "podman") {
			Write-Warning "Failed to run container. Attempting Podman CDI repair..."
			if (Repair-PodmanCDI -EnginePath $global:enginePath) {
				Write-Host "Retrying container start..." -ForegroundColor Green
				& $global:enginePath run $runArgs
			}
		}

		if ($LASTEXITCODE -ne 0) {
			Write-Error "Failed to run updated container."
			exit 1
		}
	}

	Write-Host "Waiting for Ollama..." -ForegroundColor Yellow
	Start-Sleep -Seconds 10

	Write-Host "Updating qwen3-embedding:4b model..." -ForegroundColor Yellow
	& $global:enginePath exec $global:containerName ollama pull $global:ollamaModel

	Test-HTTPPort -Uri "http://localhost:$global:Port/api/tags" -serviceName "Ollama"
	Write-Host "Container updated successfully" -ForegroundColor Green

	Update-AspireManifest
}

#==============================================================================
# Function: Test-OllamaAPI
#==============================================================================
<#
.SYNOPSIS
	Tests the Ollama native API endpoint.
.DESCRIPTION
	Sends a test embedding request to the Ollama /api/embed endpoint.
.EXAMPLE
	Test-OllamaAPI
#>
function Test-OllamaAPI {
	Write-Host "`nTesting Ollama Native API..." -ForegroundColor Yellow

	$containerStatus = & $global:enginePath ps --filter "name=$global:containerName" --format "{{.Status}}"
	if (-not $containerStatus -or $containerStatus -notmatch "Up") {
		Write-Warning "Container $global:containerName is not running."
		return
	}

	$endpoint = "http://localhost:$global:Port/api/embed"
	$payload = @{
		model = $global:ollamaModel
		input = "This is a test sentence for Ollama embedding."
	} | ConvertTo-Json

	try {
		Write-Host "Endpoint: $endpoint"
		Write-Host "Model: $global:ollamaModel"

		$response = Invoke-RestMethod -Uri $endpoint -Method Post -Body $payload -ContentType "application/json" -ErrorAction Stop

		if ($response.embeddings -and $response.embeddings.Count -gt 0) {
			$embeddingLength = $response.embeddings[0].Count
			Write-Host "`n✅ Success! Ollama API working" -ForegroundColor Green
			Write-Host "  Embedding dimensions: $embeddingLength" -ForegroundColor Green
			Write-Host "  Duration: $($response.total_duration / 1000000)ms" -ForegroundColor Green
			Write-Host "  Sample (first 5 values): $($response.embeddings[0][0..4] -join ', ')" -ForegroundColor Cyan
		} else {
			Write-Warning "No embedding data in response"
		}
	} catch {
		Write-Host "`n❌ Error testing Ollama API:" -ForegroundColor Red
		Write-Host $_.Exception.Message -ForegroundColor Red
	}
}

#==============================================================================
# Function: Test-OpenAIAPI
#==============================================================================
<#
.SYNOPSIS
	Tests the OpenAI-compatible API endpoint.
.DESCRIPTION
	Sends a test embedding request to the /v1/embeddings endpoint using OpenAI format.
.EXAMPLE
	Test-OpenAIAPI
#>
function Test-OpenAIAPI {
	Write-Host "`nTesting OpenAI-Compatible API..." -ForegroundColor Yellow

	$containerStatus = & $global:enginePath ps --filter "name=$global:containerName" --format "{{.Status}}"
	if (-not $containerStatus -or $containerStatus -notmatch "Up") {
		Write-Warning "Container $global:containerName is not running."
		return
	}

	$endpoint = "http://localhost:$global:Port/v1/embeddings"
	$payload = @{
		model = $global:ollamaModel
		input = "This is a test sentence for OpenAI-compatible embedding."
	} | ConvertTo-Json

	try {
		Write-Host "Endpoint: $endpoint"
		Write-Host "Model: $global:ollamaModel"

		$response = Invoke-RestMethod -Uri $endpoint -Method Post -Body $payload -ContentType "application/json" -ErrorAction Stop

		if ($response.data -and $response.data.Count -gt 0) {
			$embeddingLength = $response.data[0].embedding.Count
			Write-Host "`n✅ Success! OpenAI-compatible API working" -ForegroundColor Green
			Write-Host "  Object type: $($response.object)" -ForegroundColor Green
			Write-Host "  Model: $($response.model)" -ForegroundColor Green
			Write-Host "  Embedding dimensions: $embeddingLength" -ForegroundColor Green
			Write-Host "  Tokens used: $($response.usage.total_tokens)" -ForegroundColor Green
			Write-Host "  Sample (first 5 values): $($response.data[0].embedding[0..4] -join ', ')" -ForegroundColor Cyan

			Write-Host "`n📝 Python SDK example:" -ForegroundColor Yellow
			Write-Host "import openai" -ForegroundColor Cyan
			Write-Host "openai.api_base = 'http://localhost:$global:Port/v1'" -ForegroundColor Cyan
			Write-Host "openai.api_key = 'not-needed'" -ForegroundColor Cyan
			Write-Host "response = openai.Embedding.create(model='$global:ollamaModel', input='Your text')" -ForegroundColor Cyan
		} else {
			Write-Warning "No embedding data in response"
		}
	} catch {
		Write-Host "`n❌ Error testing OpenAI API:" -ForegroundColor Red
		Write-Host $_.Exception.Message -ForegroundColor Red
	}
}

################################################################################
# Main Menu Loop
################################################################################

$menuTitle = "Qwen3-Embedding-4B Container Menu (Ollama/$global:containerEngine)"
$menuItems = [ordered]@{
	"1" = "Show Info & Test Connection"
	"2" = "Install container"
	"3" = "Uninstall container"
	"4" = "Save Image (App)"
	"5" = "Load Image (App)"
	"6" = "Update Image (App)"
	"7" = "Export Volume (Data)"
	"8" = "Import Volume (Data)"
	"9" = "Check for Updates"
	"T" = "Test Ollama API"
	"O" = "Test OpenAI-Compatible API"
	"0" = "Exit menu"
}

$menuActions = @{
	"1" = {
		Show-ContainerStatus -ContainerName $global:containerName `
			-ContainerEngine $global:containerEngine `
			-EnginePath $global:enginePath `
			-DisplayName "Qwen3-Embedding-4B (Ollama)" `
			-TcpPort $global:Port `
			-HttpPort $global:Port `
			-HttpPath "/api/tags" `
			-AdditionalInfo @{
				"Ollama Model" = $global:ollamaModel
				"Image" = $global:imageName
			}
	}
	"2" = { Install-EmbeddingContainer }
	"3" = { Remove-ContainerAndVolume -Engine $global:enginePath -ContainerName $global:containerName -VolumeName $global:volumeName }
	"4" = { Backup-ContainerImage -Engine $global:enginePath -ImageName $global:imageName }
	"5" = {
		Test-AndRestoreBackup -Engine $global:enginePath -ImageName $global:imageName
		Write-Warning "Image restored from backup."
	}
	"6" = { Update-EmbeddingContainer }
	"7" = { $null = Backup-ContainerVolume -EngineType $global:containerEngine -VolumeName $global:volumeName }
	"8" = {
		$null = Restore-ContainerVolume -EngineType $global:containerEngine -VolumeName $global:volumeName
		& $global:enginePath restart $global:containerName
	}
	"9" = { Test-ImageUpdateAvailable -Engine $global:enginePath -ImageName $global:imageName }
	"T" = { Test-OllamaAPI }
	"O" = { Test-OpenAIAPI }
}

Invoke-MenuLoop -MenuTitle $menuTitle -MenuItems $menuItems -ActionMap $menuActions -ExitChoice "0"