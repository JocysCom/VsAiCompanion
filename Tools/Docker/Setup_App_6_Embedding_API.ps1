################################################################################
# Description  : Script to build, run, and update the Embedding API container using Podman.
#                Sets up the build context (Dockerfile, requirements, and API code),
#                builds the container image, runs it on port 8000, tests connectivity,
#                and provides an update function that rebuilds the image and replaces
#                the running container.
# Usage        : Run the script to manage the Embedding API container.
################################################################################

using namespace System
using namespace System.IO

# Dot-source the necessary helper function files.
. "$PSScriptRoot\Setup_Helper_CoreFunctions.ps1"
. "$PSScriptRoot\Setup_Helper_NetworkTests.ps1" # For Test-HTTPPort, Test-TCPPort
# Note: This script specifically uses Podman, so no engine selection needed.
. "$PSScriptRoot\Setup_Helper_ContainerEngine.ps1" # For Get-PodmanPath
. "$PSScriptRoot\Setup_Helper_ContainerManagement.ps1" # For Remove-ContainerAndVolume

# Ensure the script working directory is set.
Set-ScriptLocation

# Model selection menu
$modelConfigs = @(
	@{ ModelName = 'sentence-transformers/all-mpnet-base-v2'; Port = 8000 },
	@{ ModelName = 'Snowflake/snowflake-arctic-embed-l-v2.0'; Port = 8001 }
)
Write-Host 'Select embedding model to install:'
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
# Derive suffix and configuration
$suffix = $selectedConfig.ModelName.Split('/')[-1].ToLower()
$global:ModelName = $selectedConfig.ModelName
$global:imageName = "embedding-$suffix"
$global:containerName = $global:imageName
$global:volumeName = $global:containerName
$global:Port = $selectedConfig.Port
# Ensure downloads root directory exists
$downloadRoot = Join-Path $PSScriptRoot "downloads"
New-Item -ItemType Directory -Path $downloadRoot -Force | Out-Null
$global:buildDir = Join-Path $downloadRoot $global:containerName

# Prompt whether to pre-download the model into the image
$preDownloadAnswer = Read-Host 'Pre-download model during build? (Y/N)'
$global:PreDownload = if ($preDownloadAnswer -match '^[Yy]') { 'true' } else { 'false' }

#############################################
# Global Variables and Build Context Directory
#############################################
# Removed static defaults; using values from menu selection above

# --- Engine Selection (Hardcoded to Podman) ---
$global:containerEngine = "podman"
$global:enginePath = Get-EnginePath -EngineName $global:containerEngine

#==============================================================================
# Function: Invoke-EmbeddingImageBuild
#==============================================================================
<#
.SYNOPSIS
	Creates the build context files (Dockerfile, requirements.txt, embedding_api.py)
	and builds the Embedding API container image using Podman.
.DESCRIPTION
	Defines the content for the Dockerfile, requirements.txt, and the Python FastAPI application code.
	Creates the build context directory ($global:buildDir) if it doesn't exist.
	Writes the defined content to the respective files within the build context directory.
	Builds the container image using 'podman build' with the specified tag ($global:imageTag)
	from the build context directory.
.OUTPUTS
	Exits the script if the image build fails.
.EXAMPLE
	Invoke-EmbeddingImageBuild
.NOTES
	Relies on global variables $global:buildDir, $global:imageTag, $global:enginePath.
	Uses Write-Host for status messages.
#>
function Invoke-EmbeddingImageBuild {
	# Define file contents for the Embedding API application.
	$dockerfileContent = @"
ARG MODEL_NAME
ARG PRE_DOWNLOAD=false
FROM python:3.9-slim

WORKDIR /app

# Install dependencies
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# Copy the API code
COPY embedding_api.py .

# Set model environment variable
ENV MODEL_NAME=$MODEL_NAME

# Pre-download model if requested
RUN if [ "$PRE_DOWNLOAD" = "true" ]; then python -c "import os; from sentence_transformers import SentenceTransformer; SentenceTransformer(os.getenv('MODEL_NAME'))"; fi

EXPOSE 8000

CMD ["uvicorn", "embedding_api:app", "--host", "0.0.0.0", "--port", "8000"]
"@

	$requirementsTxtContent = @"
fastapi
uvicorn[standard]
sentence-transformers
torch
pydantic
"@

	$embeddingApiContent = @"
from fastapi import FastAPI, HTTPException, Response
from pydantic import BaseModel
from typing import List, Union, Optional
import torch
import base64
import struct
from sentence_transformers import SentenceTransformer
import os

app = FastAPI(title='Embedding API')

# Use MODEL_NAME from environment or fallback to default
MODEL_NAME = os.getenv('MODEL_NAME', 'sentence-transformers/all-mpnet-base-v2')
model = SentenceTransformer(MODEL_NAME)

class EmbeddingRequest(BaseModel):
	# Optionally override the model (default is our MODEL_NAME).
	model: Optional[str] = MODEL_NAME
	# 'input' can be a string or a list of strings.
	input: Union[str, List[str]]

@app.post('/v1/embeddings')
async def get_embeddings(request: EmbeddingRequest):
	if not request.input:
		raise HTTPException(status_code=400, detail='No input provided')

	# Ensure we always operate on a list.
	inputs = request.input if isinstance(request.input, list) else [request.input]

	# Get embeddings as a tensor; the model returns 768-d vectors.
	embeddings = model.encode(inputs, convert_to_tensor=True, show_progress_bar=False)

	data = []
	total_tokens = 0  # (Token count not computed here)
	for i, emb in enumerate(embeddings):
		# Convert tensor to a list of 32-bit floats.
		emb_np = emb.cpu().numpy().astype('float32').tolist()
		# Pack the float list into bytes (little-endian 32-bit floats)
		emb_bytes = struct.pack(f'<{len(emb_np)}f', *emb_np)
		# Encode the byte array in base64 so that clients expecting this format work.
		emb_b64 = base64.b64encode(emb_bytes).decode('ascii')
		data.append({
			'object': 'embedding',
			'embedding': emb_b64,
			'index': i
		})

	return {
		'object': 'list',
		'data': data,
		'model': request.model,
		'usage': { 'prompt_tokens': total_tokens, 'total_tokens': total_tokens }
	}

@app.get('/v1/models')
async def list_models():
	return {
		'object': 'list',
		'data': [
			{
				'id': MODEL_NAME,
				'object': 'model',
				'owned_by': 'community',
				'permission': []
			}
		]
	}

@app.get('/v1/models/{model_id}')
async def retrieve_model(model_id: str):
	if model_id != MODEL_NAME:
		raise HTTPException(status_code=404, detail=f'Model {model_id} not found.')
	return {
		'id': model_id,
		'object': 'model',
		'owned_by': 'community',
		'permission': []
	}

@app.options('/{path:path}')
async def options_handler(path: str):
	return Response(status_code=204)
"@

	# Create the build context directory if it does not exist.
	New-Item -ItemType Directory -Path $global:buildDir -Force | Out-Null

	# Create the build context files.
	Set-Content -Path (Join-Path $global:buildDir "Dockerfile") -Value $dockerfileContent
	Set-Content -Path (Join-Path $global:buildDir "requirements.txt") -Value $requirementsTxtContent
	Set-Content -Path (Join-Path $global:buildDir "embedding_api.py") -Value $embeddingApiContent

	Write-Host "Building the Embedding API container image..."
	# Build the container image using Podman.
	& $global:enginePath build --build-arg MODEL_NAME=$global:ModelName --build-arg PRE_DOWNLOAD=$global:PreDownload --tag $global:imageName "`"$global:buildDir`"" # Use imageName
	if ($LASTEXITCODE -ne 0) {
		Write-Error "Failed to build embedding API image."
		exit 1
	}
}

#==============================================================================
# Function: Install-EmbeddingContainer
#==============================================================================
<#
.SYNOPSIS
	Installs the Embedding API container by building the image and running it.
.DESCRIPTION
	Calls Invoke-EmbeddingImageBuild to ensure the image is built.
	Removes any existing container with the same name using Remove-ContainerAndVolume.
	Runs the container using 'podman run', mapping port 8000.
	Waits 10 seconds and performs HTTP/TCP connectivity tests.
.EXAMPLE
	Install-EmbeddingContainer
.NOTES
	Relies on Invoke-EmbeddingImageBuild, Remove-ContainerAndVolume, Test-HTTPPort, Test-TCPPort helper functions.
	Uses global variables for names, paths, etc.
	Uses Write-Host for status messages.
#>
function Install-EmbeddingContainer {
	Invoke-EmbeddingImageBuild

	# Remove existing container (and potentially volume if user created one with the same name)
	# Pass container name as volume name; Remove-ContainerAndVolume will prompt if volume exists.
	Remove-ContainerAndVolume -Engine $global:enginePath -ContainerName $global:containerName -VolumeName $global:containerName

	Write-Host "Running the Embedding API container..."
	# Command: run
	#   --detach: runs the container in background.
	#   --name: assigns the container the name "embedding-api".
	#   --publish: maps host port 8000 to container port 8000.
	& $global:enginePath run --detach --name $global:containerName --publish "$($global:Port):8000" --env MODEL_NAME=$global:ModelName $global:imageName # Use imageName
	if ($LASTEXITCODE -ne 0) {
		Write-Error "Failed to run embedding API container."
		exit 1
	}

	# Try API first, only wait if it fails
	$apiReady = $false
	try {
		$response = Invoke-WebRequest -Uri "http://localhost:$global:Port/v1/models" -Method GET -TimeoutSec 5 -ErrorAction Stop
		if ($response.StatusCode -eq 200) {
			$apiReady = $true
			Write-Host "API is ready!"
		}
	}
	catch {
		Write-Host "API not ready immediately. Checking if this is a large model that needs time to load..."
		Write-Host "Waiting for model to initialize (checking every 10 seconds)..."
			
		$maxAttempts = 20
		$attempt = 1
		
		while ($attempt -le $maxAttempts -and -not $apiReady) {
			Write-Host "Attempt $attempt/$maxAttempts - Checking if API is ready..."
			Start-Sleep -Seconds 10
			
			try {
				$response = Invoke-WebRequest -Uri "http://localhost:$global:Port/v1/models" -Method GET -TimeoutSec 5 -ErrorAction Stop
				if ($response.StatusCode -eq 200) {
					$apiReady = $true
					Write-Host "API is ready!"
				}
			}
			catch {
				Write-Host "API not ready yet. Model still loading..."
				$attempt++
			}
		}
		
		if (-not $apiReady) {
			Write-Warning "API did not become ready. You may need to wait longer or check container logs with: podman logs $global:containerName"
		}
	}
	
	Test-HTTPPort -Uri "http://localhost:$global:Port/v1/models" -serviceName "Embedding API"
	Test-TCPPort -ComputerName "localhost" -Port $global:Port -serviceName "Embedding API"
	Write-Host "Embedding API is accessible at http://localhost:$global:Port/v1/embeddings"
}

#==============================================================================
# Function: Update-EmbeddingContainer
#==============================================================================
<#
.SYNOPSIS
	Updates the Embedding API container by rebuilding the image and restarting the container.
.DESCRIPTION
	Rebuilds the container image by calling Invoke-EmbeddingImageBuild.
	Removes the existing container using Remove-ContainerAndVolume.
	Runs a new container using the updated image, mapping port 8000.
	Waits 10 seconds and performs HTTP/TCP connectivity tests.
	Supports -WhatIf for the image build and container run steps.
.EXAMPLE
	Update-EmbeddingContainer -WhatIf
.NOTES
	Relies on Invoke-EmbeddingImageBuild, Remove-ContainerAndVolume, Test-HTTPPort, Test-TCPPort helper functions.
	Uses global variables for names, paths, etc.
	Uses Write-Host for status messages.
#>
function Update-EmbeddingContainer {
	[CmdletBinding(SupportsShouldProcess = $true)]
	param()

	Write-Host "Updating the Embedding API container..."

	# Rebuild the container image.
	if ($PSCmdlet.ShouldProcess($global:imageName, "Build Image")) {
		# Use imageName
		Invoke-EmbeddingImageBuild
	}
	else {
		Write-Warning "Skipping image build due to -WhatIf."
		# Decide if update should proceed without build? For now, let's abort.
		Write-Warning "Update cannot proceed without building the image."
		return
	}

	# Remove existing container (and potentially volume) using shared function
	# This handles ShouldProcess internally for removal.
	Remove-ContainerAndVolume -Engine $global:enginePath -ContainerName $global:containerName -VolumeName $global:containerName

	# Run the updated container
	if ($PSCmdlet.ShouldProcess($global:containerName, "Run Updated Container")) {
		Write-Host "Running the updated Embedding API container..."
		# Command: run
		#   --detach: runs the container in background.
		#   --name: assigns the container the name "embedding-api".
		#   --publish: maps host port 8000 to container port 8000.
		& $global:enginePath run --detach --name $global:containerName --publish "$($global:Port):8000" --env MODEL_NAME=$global:ModelName $global:imageName # Use imageName
		if ($LASTEXITCODE -ne 0) {
			Write-Error "Failed to run updated embedding API container."
			exit 1
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
	
		Test-HTTPPort -Uri "http://localhost:$global:Port/v1/models" -serviceName "Embedding API"
		Test-TCPPort -ComputerName "localhost" -Port $global:Port -serviceName "Embedding API"
		Write-Host "Embedding API container updated and accessible at http://localhost:$global:Port/v1/embeddings"
	}
}

# Note: Uninstall-EmbeddingContainer function removed. Shared function called directly from menu.

################################################################################
# Main Menu Loop using Generic Function
################################################################################

# Define Menu Title and Items
$menuTitle = "Embedding API Container Menu (Podman Only)"
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
	"0" = "Exit menu"
}

# Define Menu Actions
$menuActions = @{
	"1" = {
		Show-ContainerStatus -ContainerName $global:containerName `
			-ContainerEngine $global:containerEngine ` # This script hardcodes podman
		-EnginePath $global:enginePath `
			-DisplayName "Embedding API" `
			-TcpPort $global:Port `
			-HttpPort $global:Port `
			-HttpPath "/v1/models" `
			-AdditionalInfo @{ "Build Dir" = $global:buildDir }
	}
	"2" = { Install-EmbeddingContainer }
	"3" = { Remove-ContainerAndVolume -Engine $global:enginePath -ContainerName $global:containerName -VolumeName $global:containerName } # Call shared function directly
	"4" = { Backup-ContainerImage -Engine $global:enginePath -ContainerName $global:containerName } # Call shared function directly
	"5" = {
		Restore-ContainerImage -Engine $global:enginePath -ContainerName $global:containerName # Call shared function directly
		Write-Warning "Container image restored from backup. A rebuild (option 2 or 8) might be needed if source code changed."
	}
	"6" = { Update-EmbeddingContainer }
	"7" = { $null = Backup-ContainerVolume -EngineType $global:containerEngine -VolumeName $global:volumeName } # Call shared function directly
	"8" = {
		$null = Restore-ContainerVolume -EngineType $global:containerEngine -VolumeName $global:volumeName
		& $global:enginePath restart $global:containerName
	}
	"9" = { Test-ImageUpdateAvailable -Engine $global:enginePath -ImageName $global:imageName }
	# Note: "0" action is handled internally by Invoke-MenuLoop
}

# Invoke the Menu Loop
Invoke-MenuLoop -MenuTitle $menuTitle -MenuItems $menuItems -ActionMap $menuActions -ExitChoice "0"
