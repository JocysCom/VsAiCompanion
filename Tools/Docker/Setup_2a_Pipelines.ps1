using namespace System
using namespace System.IO

# Dot-source the common functions file.
. "$PSScriptRoot\Setup_0.ps1"

# Ensure the script is running as Administrator and set the working directory.
Set-ScriptLocation

#############################################
# Setup Pipelines Container Script - Docker/Podman Support
#############################################

# Prompt for container engine selection.
$containerEngine = Select-ContainerEngine
if ($containerEngine -eq "docker") {
	Ensure-Elevated
    $enginePath = Get-DockerPath
} else {
    $enginePath = Get-PodmanPath
}

# Git Check using Setup_0.ps1's Check-Git function.
Check-Git

#############################################
# Setup Pipelines Container
#############################################

# Clone the pipelines repository if the folder does not exist.
$pipelinesFolder = ".\pipelines"
if (-not (Test-Path $pipelinesFolder)) {
    Write-Host "Pipelines folder not found. Cloning official pipelines repository with LF line endings..."
    git -c core.autocrlf=false clone https://github.com/open-webui/pipelines.git $pipelinesFolder
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Git clone failed for pipelines repository. Exiting..."
        exit 1
    }
}

# Modify Dockerfile (if found) to insert "ENV MINIMUM_BUILD true".
$dockerfilePath = Join-Path $pipelinesFolder "Dockerfile"
if (Test-Path $dockerfilePath) {
    Write-Host "Found Dockerfile in pipelines folder. Applying modifications..."
    $content = Get-Content $dockerfilePath -Raw
    $modified = $false
    if ($content -notmatch "ENV MINIMUM_BUILD") {
         $content = $content -replace "(FROM\s+\S+)", "`$1`nENV MINIMUM_BUILD true"
         $modified = $true
         Write-Host "Inserted 'ENV MINIMUM_BUILD true' into Dockerfile."
    }
    if ($content -match '--index-url https://download.pytorch.org/whl/cpu') {
         $content = $content -replace '--index-url https://download.pytorch.org/whl/cpu', '--index-url https://download.pytorch.org/whl/cpu --trusted-host download.pytorch.org'
         $modified = $true
         Write-Host "Modified CPU torch install command with --trusted-host."
    }
    if ($content -match '--index-url https://download.pytorch.org/whl/\$USE_CUDA_DOCKER_VER') {
         $content = $content -replace '--index-url https://download.pytorch.org/whl/\$USE_CUDA_DOCKER_VER', '--index-url https://download.pytorch.org/whl/$USE_CUDA_DOCKER_VER --trusted-host download.pytorch.org'
         $modified = $true
         Write-Host "Modified CUDA torch install command with --trusted-host."
    }
    if ($modified) {
         $content | Out-File -FilePath $dockerfilePath -Encoding utf8
    }
}
else {
    Write-Host "Dockerfile not found in pipelines folder. Creating a default Dockerfile..."
    $dockerfileContent = @"
FROM python:3.11-slim
WORKDIR /app/pipelines
COPY . /app/pipelines
ENV MINIMUM_BUILD true
RUN pip install --no-cache-dir -r requirements.txt
EXPOSE 9099
CMD ["sh", "./start.sh"]
"@
    $dockerfileContent | Out-File -FilePath $dockerfilePath -Encoding utf8
}

# Build a custom pipelines image from the local repository.
$customPipelineImageTag = "open-webui/pipelines:custom"
Write-Host "Building custom pipelines image from $pipelinesFolder..."
& $enginePath build -t $customPipelineImageTag $pipelinesFolder
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed for custom pipelines image."
    exit 1
}

# Remove any existing pipelines container.
$existingPipelineContainer = & $enginePath ps -a --filter "name=pipelines" --format "{{.ID}}"
if ($existingPipelineContainer) {
    Write-Host "Pipelines container already exists. Removing it..."
    & $enginePath rm -f pipelines
}

# Run the pipelines container with the custom image built.
Write-Host "Running pipelines container using image '$customPipelineImageTag'..."
& $enginePath run -d --add-host=host.docker.internal:host-gateway -v pipelines:/app/pipelines --restart always --name pipelines -p 9099:9099 $customPipelineImageTag
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to run the pipelines container."
    exit 1
}
Write-Host "Pipelines container is now running."

#############################################
# Test Pipelines Container Connectivity
#############################################

Write-Host "Waiting 20 seconds for the pipelines container to fully start..."
Start-Sleep -Seconds 20

Test-TCPPort -ComputerName "localhost" -Port 9099 -serviceName "Pipelines"
Test-HTTPPort -Uri "http://localhost:9099" -serviceName "Pipelines"