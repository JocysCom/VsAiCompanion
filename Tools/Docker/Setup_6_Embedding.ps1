################################################################################
# File         : Setup_6_Embedding.ps1
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
. "$PSScriptRoot\Setup_0_Core.ps1"
. "$PSScriptRoot\Setup_0_Network.ps1" # For Test-HTTPPort, Test-TCPPort
# Note: This script specifically uses Podman, so no engine selection needed.
. "$PSScriptRoot\Setup_0_ContainerEngine.ps1" # For Get-PodmanPath
. "$PSScriptRoot\Setup_0_ContainerMgmt.ps1" # For Remove-ContainerAndVolume

# Ensure the script working directory is set.
Set-ScriptLocation

#############################################
# Global Variables and Build Context Directory
#############################################
$global:buildDir = Join-Path $PSScriptRoot "embedding_api"
$global:containerName = "embedding-api"
$global:imageTag = "embedding-api" # Use the same for image and container for simplicity
$global:volumeName = "embedding_api_data" # Define a volume name (though not used by default app)
$global:enginePath = Get-PodmanPath # Explicitly use Podman

<#
.SYNOPSIS
    Creates the build context files and builds the Embedding API container image.
.DESCRIPTION
    Populates the build context directory with the Dockerfile, requirements.txt, and embedding_api.py.
    Then builds the container image with the tag "embedding-api" using Podman.
#>
function Build-EmbeddingImage {
    # Define file contents for the Embedding API application.
    $dockerfileContent = @"
FROM python:3.9-slim

WORKDIR /app

# Install dependencies
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# Copy the API code
COPY embedding_api.py .

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
from sentence-transformers import SentenceTransformer

app = FastAPI(title='Embedding API')

# Use a robust transformer model for semantic embeddings.
MODEL_NAME = 'sentence-transformers/all-mpnet-base-v2'
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
    & $global:enginePath build --tag $global:imageTag "`"$global:buildDir`""
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to build embedding API image."
        exit 1
    }
}

<#
.SYNOPSIS
    Installs (builds and runs) the Embedding API container.
.DESCRIPTION
    Calls Build-EmbeddingImage to build the container image, removes any existing
    container, then runs the container on port 8000.
    After starting the container, the script waits for initialization and tests connectivity.
#>
function Install-EmbeddingContainer {
    Build-EmbeddingImage

    # Remove existing container if it exists
    $existingContainer = & $global:enginePath ps -a --filter "name=$global:containerName" --format "{{.ID}}"
    if ($existingContainer) {
        Write-Host "Removing existing container '$global:containerName'..."
        & $global:enginePath rm --force $global:containerName
    }

    Write-Host "Running the Embedding API container..."
    # Command: run
    #   --detach: runs the container in background.
    #   --name: assigns the container the name "embedding-api".
    #   --publish: maps host port 8000 to container port 8000.
    & $global:enginePath run --detach --name $global:containerName --publish 8000:8000 $global:imageTag
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to run embedding API container."
        exit 1
    }

    Start-Sleep -Seconds 10
    Test-HTTPPort -Uri "http://localhost:8000" -serviceName "Embedding API"
    Test-TCPPort -ComputerName "localhost" -Port 8000 -serviceName "Embedding API"
    Write-Host "Embedding API is accessible at http://localhost:8000/v1/embeddings"
}

<#
.SYNOPSIS
    Updates the Embedding API container.
.DESCRIPTION
    Rebuilds the container image using the current build context, stops and removes the existing
    container (if any), and runs a new container with the updated image.
#>
function Update-EmbeddingContainer {
    Write-Host "Updating the Embedding API container..."
    # Rebuild the container image.
    Build-EmbeddingImage

    # Stop and remove existing container
    $existingContainer = & $global:enginePath ps -a --filter "name=$global:containerName" --format "{{.ID}}"
    if ($existingContainer) {
        Write-Host "Stopping existing container..."
        & $global:enginePath stop $global:containerName 2>$null | Out-Null
        Write-Host "Removing existing container..."
        & $global:enginePath rm $global:containerName
    }

    # Run the updated container
    Write-Host "Running the updated Embedding API container..."
    # Command: run
    #   --detach: runs the container in background.
    #   --name: assigns the container the name "embedding-api".
    #   --publish: maps host port 8000 to container port 8000.
    & $global:enginePath run --detach --name $global:containerName --publish 8000:8000 $global:imageTag
     if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to run updated embedding API container."
        exit 1
    }

    Start-Sleep -Seconds 10
    Test-HTTPPort -Uri "http://localhost:8000" -serviceName "Embedding API"
    Test-TCPPort -ComputerName "localhost" -Port 8000 -serviceName "Embedding API"
    Write-Host "Embedding API container updated and accessible at http://localhost:8000/v1/embeddings"
}

<#
.SYNOPSIS
    Uninstalls the Embedding API container and optionally the data volume.
.DESCRIPTION
    Uses the generic Remove-ContainerAndVolume function.
#>
function Uninstall-EmbeddingContainer {
    # Note: This app doesn't use a named volume by default, so VolumeName is set to container name
    # for potential future use or if user manually created one.
    Remove-ContainerAndVolume -Engine $global:enginePath -ContainerName $global:containerName -VolumeName $global:containerName
}

<#
.SYNOPSIS
    Displays the main menu for Embedding API container management.
.DESCRIPTION
    Presents available options for installing, updating, and uninstalling the container.
    The exit option ("0") terminates the menu loop.
#>
function Show-ContainerMenu {
    Write-Host "==========================================="
    Write-Host "Embedding API Container Menu (Podman Only)"
    Write-Host "==========================================="
    Write-Host "1. Install/Rebuild container"
    Write-Host "2. Update container (Rebuild & Run)"
    Write-Host "3. Uninstall container"
    Write-Host "0. Exit menu"
}

################################################################################
# Main Menu Loop using Generic Function
################################################################################
$menuActions = @{
    "1" = { Install-EmbeddingContainer }
    "2" = { Update-EmbeddingContainer }
    "3" = { Uninstall-EmbeddingContainer }
}

Invoke-MenuLoop -ShowMenuScriptBlock ${function:Show-ContainerMenu} -ActionMap $menuActions -ExitChoice "0"
