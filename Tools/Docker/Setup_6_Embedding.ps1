# Dot-source the common functions file.
. "$PSScriptRoot\Setup_0.ps1"

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
from sentence_transformers import SentenceTransformer

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

# Create a temporary directory to store the build files.
$tempDir = Join-Path $PSScriptRoot "embedding_api"
New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

Set-Content -Path (Join-Path $tempDir "Dockerfile") -Value $dockerfileContent
Set-Content -Path (Join-Path $tempDir "requirements.txt") -Value $requirementsTxtContent
Set-Content -Path (Join-Path $tempDir "embedding_api.py") -Value $embeddingApiContent

# Build the container image using Podman.
Write-Host "Building the Embedding API container image..."
Invoke-Expression "podman build -t embedding-api `"$tempDir`""

# Run the container on port 8000.
Write-Host "Running the Embedding API container..."
Invoke-Expression "podman run -d --name embedding-api -p 8000:8000 embedding-api"

# Allow some time for the container to initialize.
Start-Sleep -Seconds 10

# Validate container accessibility.
Test-HTTPPort -Uri "http://localhost:8000" -serviceName "Embedding API"
Test-TCPPort -ComputerName "localhost" -Port 8000 -serviceName "Embedding API"

# Test the API with a sample request.
$headers = @{ "Content-Type" = "application/json" }
$body = '{"input": "Hello, world!"}'
$response = Invoke-RestMethod -Method Post -Uri "http://localhost:8000/v1/embeddings" -Headers $headers -Body $body
Write-Host "Embedding API response:"
$response

Write-Host "Embedding API is accessible at http://localhost:8000/v1/embeddings"