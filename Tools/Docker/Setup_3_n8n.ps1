using namespace System
using namespace System.IO

# Dot-source the common functions file.
. "$PSScriptRoot\Setup_0.ps1"

# Ensure the script is running with Administrator privileges and set the working directory.
Ensure-Elevated
Set-ScriptLocation

#############################################
# Setup n8n Docker Container Script
#############################################
# This script installs and runs n8n within a Docker container.
# It performs the following steps:
# 1. Uses Setup_0.ps1's functions to verify prerequisite conditions.
# 2. Creates a Docker volume named 'n8n_data' for persisting n8n data.
# 3. Pulls the latest n8n Docker image.
# 4. Removes any existing container named 'n8n'.
# 5. Runs a new n8n container exposing port 5678.
# 6. Tests container connectivity using TCP and HTTP checks.
#
# Optional: Uncomment and modify the environment variable sections to use alternate databases or to set a specific timezone.

# Retrieve Docker executable path using Setup_0.ps1's Get-DockerPath function.
$dockerPath = Get-DockerPath

# Create Docker volume 'n8n_data' to persist n8n data.
Write-Host "Creating Docker volume 'n8n_data'..."
& $dockerPath volume create n8n_data

# Pull the latest n8n Docker image.
$imageName = "docker.n8n.io/n8nio/n8n"
Write-Host "Pulling n8n Docker image '$imageName'..."
& $dockerPath pull $imageName
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to pull n8n Docker image. Exiting..."
    exit 1
}

# Remove any existing container named 'n8n'.
$existingContainer = & $dockerPath ps -a --filter "name=n8n" --format "{{.ID}}"
if ($existingContainer) {
    Write-Host "Removing existing container 'n8n'..."
    & $dockerPath rm -f n8n
}

# Build Docker run options.
# Note: Each argument (flag and its value) must be passed separately.
$runOptions = @(
    "-d",                                  # Run in detached mode.
    "-p", "5678:5678",                     # Map host port 5678 to container port 5678.
    "-v", "n8n_data:/home/node/.n8n",       # Mount volume to persist data.
    "--name", "n8n"                        # Container name.
)

# Optional: To use PostgresDB instead of SQLite, uncomment and modify the following.
<# 
$runOptions += @(
    "-e", "DB_TYPE=postgresdb",
    "-e", "DB_POSTGRESDB_DATABASE=<POSTGRES_DATABASE>",
    "-e", "DB_POSTGRESDB_HOST=<POSTGRES_HOST>",
    "-e", "DB_POSTGRESDB_PORT=<POSTGRES_PORT>",
    "-e", "DB_POSTGRESDB_USER=<POSTGRES_USER>",
    "-e", "DB_POSTGRESDB_SCHEMA=<POSTGRES_SCHEMA>",
    "-e", "DB_POSTGRESDB_PASSWORD=<POSTGRES_PASSWORD>"
)
#>

# Optional: To set the timezone, uncomment and modify the following.
<# 
$runOptions += @(
    "-e", "GENERIC_TIMEZONE=Europe/Berlin",
    "-e", "TZ=Europe/Berlin"
)
#>

# Run the n8n container.
Write-Host "Starting n8n container..."
& $dockerPath run $runOptions $imageName
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to run n8n container. Exiting..."
    exit 1
}

Write-Host "Waiting 20 seconds for the n8n container to start..."
Start-Sleep -Seconds 20

# Test connectivity to the n8n service.
Test-TCPPort -ComputerName "localhost" -Port 5678 -serviceName "n8n"
Test-HTTPPort -Uri "http://localhost:5678" -serviceName "n8n"

Write-Host "n8n is now running and accessible at http://localhost:5678"