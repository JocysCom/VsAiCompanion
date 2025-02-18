################################################################################
# File         : Setup_2a_Pipelines.ps1
# Description  : Script to set up, back up, restore, and uninstall the Pipelines
#                container using Docker/Podman. All installation steps have been
#                moved into functions and a menu is presented with options.
# Usage        : Run as Administrator if using Docker.
################################################################################

using namespace System
using namespace System.IO

# Dot-source the common functions file.
. "$PSScriptRoot\Setup_0.ps1"

# Ensure the working directory is set.
Set-ScriptLocation

# Global variables used across functions.
$global:containerName   = "pipelines"
$global:pipelinesFolder = ".\pipelines"
$global:enginePath      = $null
$global:containerEngine = $null

################################################################################
# Function: Install-PipelinesContainer
# Description : Installs (or reinstalls) the Pipelines container.
#               • Prompts for the container engine (Docker or Podman)
#               • Verifies Git and clones the pipelines repository if not present
#               • Modifies the Dockerfile as required
#               • Builds the custom image (or uses a restored backup if available)
#               • Removes any existing container and runs a new one
#               • Tests container connectivity via TCP and HTTP ports
################################################################################
function Install-PipelinesContainer {
    # Verify Git is available.
    Check-Git

    # Clone the pipelines repository if the folder does not exist.
    if (-not (Test-Path $global:pipelinesFolder)) {
        Write-Host "Pipelines folder not found. Cloning official pipelines repository with LF line endings..."
        git -c core.autocrlf=false clone https://github.com/open-webui/pipelines.git $global:pipelinesFolder
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Git clone failed for pipelines repository. Aborting installation."
            return
        }
    } else {
        Write-Host "Pipelines folder found at $global:pipelinesFolder."
    }

    # Modify the Dockerfile in the pipelines folder.
    $dockerfilePath = Join-Path $global:pipelinesFolder "Dockerfile"
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

    # Define the custom image tag.
    $customPipelineImageTag = "open-webui/pipelines:custom"

    # Try to restore a backup. If no backup is restored, build the image.
    if (-not (Check-AndRestoreBackup -Engine $global:enginePath -ImageName $customPipelineImageTag)) {
        Write-Host "No backup restored. Building custom pipelines image from $global:pipelinesFolder..."
        & $global:enginePath build -t $customPipelineImageTag $global:pipelinesFolder
        if ($LASTEXITCODE -ne 0) {
             Write-Error "Build failed for custom pipelines image. Installation aborted."
             return
        }
    } else {
        Write-Host "Using restored backup image '$customPipelineImageTag'."
    }

    # Remove any existing pipelines container.
    $existingPipelineContainer = & $global:enginePath ps -a --filter "name=$global:containerName" --format "{{.ID}}"
    if ($existingPipelineContainer) {
        Write-Host "Pipelines container already exists. Removing it..."
        & $global:enginePath rm -f $global:containerName
    }

    # Run the pipelines container.
    Write-Host "Running pipelines container using image '$customPipelineImageTag'..."
    & $global:enginePath run -d --add-host=host.docker.internal:host-gateway -v pipelines:/app/pipelines --restart always --name $global:containerName -p 9099:9099 $customPipelineImageTag
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to run the pipelines container."
        return
    }
    Write-Host "Pipelines container is now running."

    # Wait for the container to start and test connectivity.
    Start-Sleep -Seconds 20
    Test-TCPPort -ComputerName "localhost" -Port 9099 -serviceName $global:containerName
    Test-HTTPPort -Uri "http://localhost:9099" -serviceName $global:containerName
}

################################################################################
# Function: Backup-PipelinesContainer
# Description : Backs up the live pipelines container.
################################################################################
function Backup-PipelinesContainer {
    if (-not $global:enginePath) {
        Write-Error "Engine path not set. Please install the pipelines container first."
        return
    }
    Backup-ContainerState -Engine $global:enginePath -ContainerName $global:containerName
}

################################################################################
# Function: Restore-PipelinesContainer
# Description : Restores the pipelines container from a backup.
################################################################################
function Restore-PipelinesContainer {
    if (-not $global:enginePath) {
        Write-Error "Engine path not set. Please install the pipelines container first."
        return
    }
    Restore-ContainerState -Engine $global:enginePath -ContainerName $global:containerName
}

################################################################################
# Function: Uninstall-PipelinesContainer
# Description : Uninstalls (removes) the pipelines container.
################################################################################
function Uninstall-PipelinesContainer {
    if (-not $global:enginePath) {
        Write-Error "Engine path not set. Nothing to uninstall."
        return
    }
    $existingPipelineContainer = & $global:enginePath ps -a --filter "name=$global:containerName" --format "{{.ID}}"
    if ($existingPipelineContainer) {
        Write-Host "Removing pipelines container '$global:containerName'..."
        & $global:enginePath rm -f $global:containerName
        if ($LASTEXITCODE -eq 0) {
            Write-Host "Pipelines container removed successfully."
        } else {
            Write-Error "Failed to remove pipelines container."
        }
    }
    else {
        Write-Host "No pipelines container found to remove."
    }
}

################################################################################
# Pick Container Engine BEFORE showing container menu
################################################################################
$global:containerEngine = Select-ContainerEngine
if ($global:containerEngine -eq "docker") {
    Ensure-Elevated
    $global:enginePath = Get-DockerPath
}
else {
    $global:enginePath = Get-PodmanPath
}

################################################################################
# Function: Show-ContainerMenu
# Description : Displays the main menu for Pipelines container options.
################################################################################
function Show-ContainerMenu {
    Write-Host "==========================================="
    Write-Host "Pipelines Container Menu"
    Write-Host "==========================================="
    Write-Host "1) Install Container"
    Write-Host "2) Backup live container"
    Write-Host "3) Restore container from backup"
    Write-Host "4) Uninstall Container"
    Write-Host "5) Exit menu"
}

################################################################################
# Main Script Execution - Menu Option Loop
################################################################################
do {
    Show-ContainerMenu
    $choice = Read-Host "Enter your choice (1, 2, 3, 4, or 5)"
    switch ($choice) {
        "1" { Install-PipelinesContainer }
        "2" { Backup-PipelinesContainer }
        "3" { Restore-PipelinesContainer }
        "4" { Uninstall-PipelinesContainer }
        "5" { Write-Host "Exiting menu." }
        default { Write-Host "Invalid selection. Please enter 1, 2, 3, 4, or 5." }
    }
    if ($choice -ne "5") {
         Write-Host "`nPress any key to continue..."
         $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
         Clear-Host
    }
} while ($choice -ne "5")