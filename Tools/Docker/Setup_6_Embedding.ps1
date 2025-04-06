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
    Uses Write-Information for status messages.
#>
function Invoke-EmbeddingImageBuild {
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

    Write-Information "Building the Embedding API container image..."
    # Build the container image using Podman.
    & $global:enginePath build --tag $global:imageTag "`"$global:buildDir`""
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
    Uses Write-Information for status messages.
#>
function Install-EmbeddingContainer {
    Invoke-EmbeddingImageBuild

    # Remove existing container (and potentially volume if user created one with the same name)
    # Pass container name as volume name; Remove-ContainerAndVolume will prompt if volume exists.
    Remove-ContainerAndVolume -Engine $global:enginePath -ContainerName $global:containerName -VolumeName $global:containerName

    Write-Information "Running the Embedding API container..."
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
    Write-Information "Embedding API is accessible at http://localhost:8000/v1/embeddings"
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
    Uses Write-Information for status messages.
#>
function Update-EmbeddingContainer {
    [CmdletBinding(SupportsShouldProcess=$true)]
    param()

    Write-Information "Updating the Embedding API container..."

    # Rebuild the container image.
    if ($PSCmdlet.ShouldProcess($global:imageTag, "Build Image")) {
        Invoke-EmbeddingImageBuild
    } else {
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
        Write-Information "Running the updated Embedding API container..."
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
        Write-Information "Embedding API container updated and accessible at http://localhost:8000/v1/embeddings"
    }
}

#==============================================================================
# Function: Uninstall-EmbeddingContainer
#==============================================================================
<#
.SYNOPSIS
    Uninstalls the Embedding API container and optionally removes an associated volume if one exists with the same name.
.DESCRIPTION
    Calls the Remove-ContainerAndVolume helper function, specifying 'embedding-api' as both the
    container and volume name. This will stop/remove the container and prompt the user
    about removing the volume if a volume named 'embedding-api' exists. Supports -WhatIf.
.EXAMPLE
    Uninstall-EmbeddingContainer -Confirm:$false
.NOTES
    Relies on Remove-ContainerAndVolume helper function. The default app doesn't use a named volume,
    so the volume removal prompt is unlikely unless manually created.
#>
function Uninstall-EmbeddingContainer {
    # Note: This app doesn't use a named volume by default, so VolumeName is set to container name
    # for potential future use or if user manually created one.
    Remove-ContainerAndVolume -Engine $global:enginePath -ContainerName $global:containerName -VolumeName $global:containerName
}

#==============================================================================
# Function: Show-ContainerMenu
#==============================================================================
<#
.SYNOPSIS
    Displays the main menu options for Embedding API container management.
.DESCRIPTION
    Writes the available menu options (Show Info, Install/Rebuild, Update, Uninstall, Exit)
    to the console using Write-Output. Notes that this script uses Podman only.
.EXAMPLE
    Show-ContainerMenu
.NOTES
    Uses Write-Output for direct console display.
#>
function Show-ContainerMenu {
    Write-Output "==========================================="
    Write-Output "Embedding API Container Menu (Podman Only)"
    Write-Output "==========================================="
    Write-Output "1. Show Info & Test Connection"
    Write-Output "2. Install/Rebuild container"
    Write-Output "3. Update container (Rebuild & Run)"
    Write-Output "4. Uninstall container"
    Write-Output "0. Exit menu"
}

################################################################################
# Main Menu Loop using Generic Function
################################################################################
$menuActions = @{
    "1" = {
        Show-ContainerStatus -ContainerName $global:containerName `
                             -ContainerEngine $global:containerEngine ` # This script hardcodes podman
                             -EnginePath $global:enginePath `
                             -DisplayName "Embedding API" `
                             -TcpPort 8000 `
                             -HttpPort 8000 `
                             -HttpPath "/v1/models" `
                             -AdditionalInfo @{ "Build Dir" = $global:buildDir }
    }
    "2" = { Install-EmbeddingContainer }
    "3" = { Update-EmbeddingContainer }
    "4" = { Uninstall-EmbeddingContainer }
}

Invoke-MenuLoop -ShowMenuScriptBlock ${function:Show-ContainerMenu} -ActionMap $menuActions -ExitChoice "0"
