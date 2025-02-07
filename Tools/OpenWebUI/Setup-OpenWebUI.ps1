using namespace System
using namespace System.IO

#############################################
# Docker and WSL Troubleshooting Guide
#############################################
# 1. Run Docker as Administrator:
#    Make sure to launch Docker Desktop with elevated privileges.
#
# 2. Check and Repair System Health:
#    Uncomment the commands below if needed.
# dism /online /cleanup-image /restorehealth
# sfc /scannow
#
# 3. Restart WSL Services:
#    e.g. wsl --shutdown ; wsl --update --web-download ; wsl -l -v
#
# 4. Enable Virtualization & Required Windows Features:
#    dism.exe /online /enable-feature /featurename:VirtualMachinePlatform /all /norestart
#    dism.exe /online /enable-feature /featurename:Microsoft-Windows-Subsystem-Linux /all /norestart
#
# 5. Ensure WSL Manager Service is Running:
#    sc.exe config LxssManager start=auto; sc.exe start LxssManager
#
# 6. Switch Docker to use the WSL 2 Backend:
#    "C:\Program Files\Docker\Docker\DockerCli.exe" -SwitchDaemon
#############################################
# Define Options
#############################################

$imageName = "ghcr.io/open-webui/open-webui:main"
$containerName = "open-webui"
$dockerStaticUrl  = "https://download.docker.com/win/static/stable/x86_64/docker-27.5.1.zip"

# Define Docker network for container intercommunication.
$networkName = "my-docker-network"

# Pipelines container settings.
$pipelineContainerName = "pipelines"
# Default official pipelines image (will be overwritten if local custom pipelines are used)
$pipelineImage = "ghcr.io/open-webui/pipelines:main"

# Optional: Set recreate pipelines flag if "--recreate-pipelines" is passed as an argument.
$recreatePipelines = $true
if ($args.Length -ge 4 -and $args[3] -eq "--recreate-pipelines") {
    $recreatePipelines = $true
}

#############################################
# Run in Elevated Mode and Set Location
#############################################

If (-NOT ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    $argumentList = "& '" + $MyInvocation.MyCommand.Path + "' '$($env:USERNAME)' '$($env:USERPROFILE)' '$($env:LOCALAPPDATA)'"
    Start-Process PowerShell.exe -Verb Runas -ArgumentList $argumentList
    return
}

$userNames = @( $args[0])
if ($args[0] -ne $env:USERNAME) { $userNames += $env:USERNAME }

$userProfilePaths = @( $args[1])
if ($args[1] -ne $env:USERPROFILE) { $userProfilePaths += $env:USERPROFILE }

$localAppDataPaths = @( $args[2])
if ($args[2] -ne $env:LOCALAPPDATA) { $localAppDataPaths += $env:LOCALAPPDATA }

[string]$current = $MyInvocation.MyCommand.Path
[string]$calling = @(Get-PSCallStack)[1].InvocationInfo.MyCommand.Path
if ($calling -ne "") {
    $current = $calling
}
[FileInfo]$file = New-Object FileInfo($current)
$global:scriptName = $file.Basename
$global:scriptPath = $file.Directory.FullName
Write-Host "Script Path:    $scriptPath"
[Environment]::CurrentDirectory = $scriptPath
Set-Location $scriptPath

#############################################
# Helper Functions
#############################################

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    Write-Host "Git command not found in PATH. Attempting to locate Git via common installation paths..."
    $possibleGitPath = "C:\Program Files\Microsoft Visual Studio\2022\Professional\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Git\cmd"
    if (Test-Path $possibleGitPath) {
        $env:Path += ";" + $possibleGitPath
        Write-Host "Added Git path: $possibleGitPath"
    }
}
if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    Write-Error "Git command not found. Please install Git and ensure it's in your PATH."
    exit 1
}

function Get-DockerPath {
    $dockerCmd = Get-Command docker -ErrorAction SilentlyContinue
    if ($dockerCmd) {
        return $dockerCmd.Source
    }
    else {
        $dockerPath = Join-Path (Resolve-Path ".\docker") "docker.exe"
        if (Test-Path $dockerPath) {
            return $dockerPath
        }
        else {
            Write-Error "Docker executable not found."
            exit 1
        }
    }
}

#############################################
# WSL and Service Health Functions
#############################################

function Check-WSLStatus {
    Write-Host "Verifying WSL installation and required service status..."
    if (!(Get-Command wsl -ErrorAction SilentlyContinue)) {
        Write-Error "WSL (wsl.exe) is not available. Please install Windows Subsystem for Linux."
        exit 1
    }
    $wslVersionInfo = wsl --version 2>&1
    Write-Host "WSL Version Info:`n$wslVersionInfo"
    
    $lxssManager = Get-Service -Name LxssManager -ErrorAction SilentlyContinue
    if (!$lxssManager) {
        Write-Error "LxssManager service not found. Please ensure WSL is installed properly."
        exit 1
    }
    if ($lxssManager.Status -ne 'Running') {
        Write-Host "LxssManager service is not running. Attempting to start LxssManager..."
        try {
            Start-Service LxssManager
            Start-Sleep -Seconds 5
            $lxssManager = Get-Service -Name LxssManager
            if ($lxssManager.Status -ne 'Running') {
                Write-Error "Failed to start LxssManager service. Please start it manually."
                exit 1
            }
        }
        catch {
            Write-Error "Error starting LxssManager service: $_"
            exit 1
        }
    }
    Write-Host "LxssManager service is running."
    
    $wslFeature = Get-WindowsOptionalFeature -Online -FeatureName Microsoft-Windows-Subsystem-Linux
    if ($wslFeature.State -ne "Enabled") {
        Write-Error "The Microsoft-Windows-Subsystem-Linux feature is not enabled. Please enable it via DISM or Windows Features."
        exit 1
    }
    $vmFeature = Get-WindowsOptionalFeature -Online -FeatureName VirtualMachinePlatform
    if ($vmFeature.State -ne "Enabled") {
        Write-Error "The VirtualMachinePlatform feature is not enabled. Please enable it via DISM or Windows Features."
        exit 1
    }
    Write-Host "WSL and required Windows features are enabled."
}

#############################################
# Docker Installation Functions
#############################################

function Download-DockerEngine {
    param(
        [string]$dockerStaticUrl,
        [string]$downloadFolder = ".\downloads"
    )
    if (-not (Test-Path $downloadFolder)) {
        Write-Host "Creating downloads folder at $downloadFolder..."
        New-Item -ItemType Directory -Force -Path $downloadFolder | Out-Null
    }
    $zipPath = Join-Path $downloadFolder "docker.zip"
    if (Test-Path $zipPath) {
        Write-Host "Docker archive already exists at $zipPath. Skipping download."
    }
    else {
        Write-Host "Downloading Docker static binary archive from $dockerStaticUrl..."
        try {
            Invoke-WebRequest -Uri $dockerStaticUrl -OutFile $zipPath -UseBasicParsing
        }
        catch {
            Write-Error "Failed to download Docker static binary archive. Please check your internet connection or URL."
            exit 1
        }
    }
    return $zipPath
}

function Install-DockerEngine {
    param(
        [string]$zipPath,
        [string]$destinationPath = ".\docker"
    )
    if (Test-Path $destinationPath) {
        Write-Host "Destination folder $destinationPath already exists. Skipping extraction and installation."
        return $destinationPath
    }
    
    $tempDestination = ".\docker_temp"
    if (Test-Path $tempDestination) {
        Remove-Item -Recurse -Force $tempDestination
    }
    try {
        Expand-Archive -Path $zipPath -DestinationPath $tempDestination
    }
    catch {
        Write-Error "Failed to extract Docker archive."
        exit 1
    }
    Write-Host "Processing extracted files..."
    $innerFolder = Join-Path $tempDestination "docker"
    if (Test-Path $innerFolder) {
        Write-Host "Detected inner 'docker' folder. Moving its contents to $destinationPath..."
        New-Item -ItemType Directory -Path $destinationPath | Out-Null
        Get-ChildItem -Path $innerFolder | ForEach-Object {
            Copy-Item -Path $_.FullName -Destination $destinationPath -Recurse -Force
        }
    }
    else {
        Write-Host "No inner folder detected. Renaming extraction folder to $destinationPath..."
        Rename-Item -Path $tempDestination -NewName "docker"
        return $destinationPath
    }
    Remove-Item -Recurse -Force $tempDestination
    return $destinationPath
}

function RegisterAndStart-DockerEngine {
    param(
        [string]$destinationPath = ".\docker"
    )
    $dockerdPath = Join-Path $destinationPath "dockerd.exe"
    if (-not (Test-Path $dockerdPath)) {
        Write-Error "dockerd.exe not found in $destinationPath. Installation may have failed."
        exit 1
    }
    Write-Host "Checking if Docker Engine service is already registered..."
    $dockerService = Get-Service -Name docker -ErrorAction SilentlyContinue
    if ($dockerService -eq $null) {
        Write-Host "Registering Docker Engine as a service..."
        & "$dockerdPath" --register-service
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Failed to register Docker service."
            exit 1
        }
        $dockerService = Get-Service -Name docker -ErrorAction SilentlyContinue
    }
    else {
        Write-Host "Docker service is already registered."
    }

    if ($dockerService.Status -ne 'Running') {
        Write-Host "Starting Docker service..."
        Start-Service docker
        $dockerService = Get-Service -Name docker -ErrorAction SilentlyContinue
        if ($dockerService.Status -ne 'Running') {
            Write-Error "Failed to start Docker service."
            exit 1
        }
    }
    else {
        Write-Host "Docker service is already running."
    }
    Start-Sleep -Seconds 10
    $env:Path = "$(Resolve-Path $destinationPath);$env:Path"
}

function Test-DockerWorking {
    param(
        [string]$destinationPath = ".\docker"
    )
    if (Get-Command docker -ErrorAction SilentlyContinue) {
        $dockerExe = "docker"
    }
    else {
        $dockerExe = Join-Path $destinationPath "docker.exe"
    }
    Write-Host "Verifying Docker installation with hello-world image..."
    $env:DOCKER_HOST = "npipe:////./pipe/docker_engine"

    # Use existing hello-world image if present.
    $existingHelloWorld = &$dockerExe images --filter "reference=hello-world" --format "{{.Repository}}"
    if (-not $existingHelloWorld) {
        Write-Host "hello-world image not found locally. Pulling hello-world image..."
        &$dockerExe pull hello-world | Out-Null
    }
    $output = &$dockerExe run --platform linux/amd64 hello-world 2>&1
    if ($LASTEXITCODE -ne 0) {
        if ($output -match "The request is not supported") {
            Write-Host "Encountered 'The request is not supported'. Docker service appears to be running, but the container test is not supported in this environment."
            Write-Host "Skipping container test verification."
            return
        }
        else {
            Write-Error "Docker Engine installation verification failed. Output:`n$output"
            exit 1
        }
    }
    Write-Host "Docker Engine is working successfully."
}

function Ensure-DockerInstalledAndWorking {
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
        Write-Host "Docker is not installed."
        Write-Host "Select Docker installation method:"
        Write-Host "1) Install Docker Desktop (requires winget)"
        Write-Host "2) Install Docker Engine (static binary installation)"
        $installMethod = Read-Host "Enter your choice (1 or 2), default is 1"
        if ([string]::IsNullOrWhiteSpace($installMethod)) {
            $installMethod = "1"
        }
        if ($installMethod -eq "1") {
            if (Get-Command winget -ErrorAction SilentlyContinue) {
                Write-Host "Installing Docker Desktop using winget..."
                winget install --id Docker.DockerDesktop -e --accept-package-agreements --accept-source-agreements
                Start-Sleep -Seconds 60
                if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
                    Write-Error "Docker Desktop installation via winget failed. Please install Docker manually."
                    exit 1
                }
                Write-Host "Docker Desktop installed successfully."
            }
            else {
                Write-Error "winget is not available for Docker Desktop installation. Please install Docker manually."
                exit 1
            }
        }
        elseif ($installMethod -eq "2") {
            $zipPath = Download-DockerEngine -dockerStaticUrl $dockerStaticUrl
            $destinationPath = Install-DockerEngine -zipPath $zipPath -destinationPath ".\docker"
            RegisterAndStart-DockerEngine -destinationPath $destinationPath
        }
        else {
            Write-Error "Invalid selection for Docker installation method."
            exit 1
        }
    }
    else {
        Write-Host "Docker command found in PATH. Using existing Docker Desktop installation."
    }
    Test-DockerWorking -destinationPath ".\docker"
}

function Ensure-DockerDaemonRunning {
    Write-Host "Ensuring Docker daemon is reachable..."
    $env:DOCKER_HOST = "npipe:////./pipe/docker_engine"
    $dockerInfo = docker info 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Docker daemon is not reachable. Please ensure Docker is running. Error details: $dockerInfo"
        exit 1
    }
    Write-Host "Docker daemon is reachable."
}

#############################################
# Main Script Logic
#############################################

Check-WSLStatus
Ensure-DockerInstalledAndWorking
Ensure-DockerDaemonRunning

$dockerPath = Get-DockerPath

#############################################
# Setup Docker Network
#############################################

$existingNetwork = &$dockerPath network ls --filter "name=$networkName" --format "{{.Name}}"
if (-not $existingNetwork) {
    Write-Host "Creating Docker network '$networkName'..."
    &$dockerPath network create $networkName | Out-Null
    if ($LASTEXITCODE -ne 0) {
         Write-Error "Failed to create Docker network '$networkName'."
         exit 1
    }
}
else {
    Write-Host "Docker network '$networkName' already exists."
}

#############################################
# Setup Custom Pipelines Container
#############################################

# Check if local pipelines folder exists; if missing, clone the repository with LF line endings.
$pipelinesFolder = ".\pipelines"
if (-not (Test-Path $pipelinesFolder)) {
    Write-Host "Pipelines folder not found. Cloning official pipelines repository with LF line endings..."
    git -c core.autocrlf=false clone https://github.com/open-webui/pipelines.git $pipelinesFolder
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Git clone failed for pipelines repository. Exiting..."
        exit 1
    }
}

# Modify the Dockerfile to override MINIMUM_BUILD to skip torch installation.
$dockerfilePath = Join-Path $pipelinesFolder "Dockerfile"
if (Test-Path $dockerfilePath) {
    Write-Host "Found Dockerfile in pipelines folder. Applying modifications..."
    $content = Get-Content $dockerfilePath -Raw
    $modified = $false

    if ($content -notmatch "ENV MINIMUM_BUILD") {
         # Insert 'ENV MINIMUM_BUILD true' right after the FROM line.
         $content = $content -replace "(FROM\s+\S+)", "`$1`nENV MINIMUM_BUILD true"
         $modified = $true
         Write-Host "Inserted 'ENV MINIMUM_BUILD true' into Dockerfile."
    }
    # Optionally modify PyTorch pip install commands to include --trusted-host.
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

# Build a custom pipelines Docker image from the local repository.
$customPipelineImageTag = "open-webui/pipelines:custom"
Write-Host "Building custom pipelines Docker image from $pipelinesFolder..."
& $dockerPath build -t $customPipelineImageTag $pipelinesFolder
if ($LASTEXITCODE -ne 0) {
    Write-Error "Docker build failed for custom pipelines image."
    exit 1
}
# Use the custom-built image.
$pipelineImage = $customPipelineImageTag

# Manage the pipelines container.
$existingPipelineContainer = &$dockerPath ps -a --filter "name=$pipelineContainerName" --format "{{.ID}}"
if ($recreatePipelines -and $existingPipelineContainer) {
    Write-Host "Recreate pipelines flag set. Removing existing pipelines container..."
    &$dockerPath rm -f $pipelineContainerName
    $existingPipelineContainer = $null
}

if (-not $existingPipelineContainer) {
    Write-Host "Running custom pipelines container using image '$pipelineImage'..."
    &$dockerPath run -d --add-host=host.docker.internal:host-gateway -v pipelines:/app/pipelines --restart always --network $networkName --name $pipelineContainerName -p 9099:9099 $pipelineImage
    if ($LASTEXITCODE -ne 0) {
         Write-Error "Failed to run the pipelines container."
         exit 1
    }
}
else {
    Write-Host "Pipelines container already exists. Starting container if necessary..."
    &$dockerPath start $pipelineContainerName
}

#############################################
# Pull and Run Open WebUI Container
#############################################

Write-Host "Pulling Open WebUI Docker image '$imageName'..."
& $dockerPath pull --platform linux/amd64 $imageName
if ($LASTEXITCODE -ne 0) {
    Write-Error "Docker pull failed. Please check your internet connection or the image URL."
    exit 1
}

$existingContainer = & $dockerPath ps -a --filter "name=$containerName" --format "{{.ID}}"
if ($existingContainer) {
    Write-Host "Removing existing container '$containerName'..."
    & $dockerPath rm -f $containerName
}

Write-Host "Running Docker container '$containerName'..."
& $dockerPath run --platform linux/amd64 -d -p 3000:8080 -v open-webui:/app/backend/data --network $networkName --name $containerName $imageName
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to run the Docker container."
    exit 1
}

Write-Host "Waiting 20 seconds for the container to fully start..."
Start-Sleep -Seconds 20

#############################################
# Testing if the Open WebUI Website is Running
#############################################
$siteTestPassed = $false
Write-Host "Testing if website is accessible at http://localhost:3000..."
try {
    $response = Invoke-WebRequest -Uri "http://localhost:3000" -UseBasicParsing -TimeoutSec 15
    if ($response.StatusCode -eq 200) {
        Write-Host "Success: Website is up and running! (HTTP 200)"
        $siteTestPassed = $true
    }
    else {
        Write-Error "Error: Website returned status code $($response.StatusCode)."
        Write-Host "Response Content: $($response.Content)"
    }
}
catch {
    Write-Error "Failed to reach http://localhost:3000. Error details: $_"
    Write-Host "Please check if the Docker container is running properly and that port 3000 is not blocked."
}

Write-Host "Performing additional network connection test on port 3000..."
$portTest = Test-NetConnection -ComputerName "localhost" -Port 3000
if ($portTest.TcpTestSucceeded) {
    Write-Host "Port 3000 is confirmed open."
    if (-not $siteTestPassed) { $siteTestPassed = $true }
} 
else {
    Write-Error "Port 3000 is not reachable. Check firewall settings or Docker container configuration."
}

if ($siteTestPassed) {
    Write-Host "Open WebUI is now running and accessible at http://localhost:3000"
    Write-Host "Reminder: In Open WebUI settings, set the OpenAI API URL to 'http://pipelines:9099' and API key to '0p3n-w3bu!' to activate your pipeline."
    Write-Host "If accessing from the host (and not from within another Docker container), you might need to use 'http://localhost:9099' instead."
}
else {
    Write-Error "Open WebUI does not seem to be running properly. Please check container logs (docker logs $containerName) for more details."
}