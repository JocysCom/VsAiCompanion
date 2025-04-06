################################################################################
# File         : Setup_1a_Docker.ps1
# Description  : Script to install and configure Docker and WSL on Windows.
#                Includes checks for WSL, installation of Docker Desktop/Engine,
#                and verification of Docker functionality.
# Usage        : Run as Administrator if required.
################################################################################

using namespace System
using namespace System.IO

# Dot-source the necessary helper function files.
. "$PSScriptRoot\Setup_0_Core.ps1"
. "$PSScriptRoot\Setup_0_ContainerEngine.ps1"
. "$PSScriptRoot\Setup_0_WSL.ps1"

# Ensure the script is running as Administrator and set the working directory.
Ensure-Elevated
Set-ScriptLocation

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

#############################################
# Define Options for Docker Installation Only
#############################################

$dockerStaticUrl = "https://download.docker.com/win/static/stable/x86_64/docker-27.5.1.zip"

#==============================================================================
# Function: Invoke-DockerEngineDownload
#==============================================================================
<#
.SYNOPSIS
	Downloads the Docker Engine static binary zip archive.
.DESCRIPTION
	Downloads the Docker Engine zip file from the specified URL to a local 'downloads' folder.
	Skips the download if the zip file already exists in the target folder.
.PARAMETER dockerStaticUrl
	The URL to download the Docker static binary zip file from.
.PARAMETER downloadFolder
	The local folder where the downloaded zip file should be saved. Defaults to '.\downloads'.
.OUTPUTS
	[string] The full path to the downloaded zip file. Exits script on download failure.
.EXAMPLE
	$zip = Invoke-DockerEngineDownload -dockerStaticUrl "https://download.docker.com/..."
.NOTES
	Uses Start-BitsTransfer for downloading.
	Creates the download folder if it doesn't exist.
#>
function Invoke-DockerEngineDownload {
	param(
		[string]$dockerStaticUrl,
		[string]$downloadFolder = ".\downloads"
	)
	if (-not (Test-Path $downloadFolder)) {
		Write-Output "Creating downloads folder at $downloadFolder..."
		New-Item -ItemType Directory -Force -Path $downloadFolder | Out-Null
	}
	$zipPath = Join-Path $downloadFolder "docker.zip"
	if (Test-Path $zipPath) {
		Write-Output "Docker archive already exists at $zipPath. Skipping download."
	}
	else {
		Write-Output "Downloading Docker static binary archive from $dockerStaticUrl..."
		try {
			# Use Start-BitsTransfer for faster download with progress.
			Start-BitsTransfer -Source $dockerStaticUrl -Destination $zipPath
		}
		catch {
			Write-Error "Failed to download Docker static binary archive. Please check your internet connection or URL. Error details: $_"
			exit 1
		}
	}
	return $zipPath
}

#==============================================================================
# Function: Install-DockerEngine
#==============================================================================
<#
.SYNOPSIS
	Extracts and installs the Docker Engine from the downloaded zip archive.
.DESCRIPTION
	Extracts the contents of the provided Docker zip archive to a temporary location.
	Handles cases where the zip contains an inner 'docker' folder.
	Moves the extracted contents to the final destination path.
.PARAMETER zipPath
	The path to the downloaded Docker zip archive.
.PARAMETER destinationPath
	The final directory where the Docker Engine files should be installed. Defaults to '.\docker'.
.OUTPUTS
	[string] The full path to the installation directory. Exits script on extraction failure.
.EXAMPLE
	$installPath = Install-DockerEngine -zipPath ".\downloads\docker.zip" -destinationPath "C:\Program Files\DockerEngine"
.NOTES
	Removes the destination path if it already exists before extraction.
	Cleans up the temporary extraction folder.
#>
function Install-DockerEngine {
	param(
		[string]$zipPath,
		[string]$destinationPath = ".\docker"
	)
	if (Test-Path $destinationPath) {
		Write-Output "Destination folder $destinationPath already exists. Skipping extraction and installation."
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
	Write-Output "Processing extracted files..."
	$innerFolder = Join-Path $tempDestination "docker"
	if (Test-Path $innerFolder) {
		Write-Output "Detected inner 'docker' folder. Moving its contents to $destinationPath..."
		New-Item -ItemType Directory -Path $destinationPath | Out-Null
		Get-ChildItem -Path $innerFolder | ForEach-Object {
			Copy-Item -Path $_.FullName -Destination $destinationPath -Recurse -Force
		}
	}
	else {
		Write-Output "No inner folder detected. Renaming extraction folder to $destinationPath..."
		Rename-Item -Path $tempDestination -NewName "docker"
		return $destinationPath
	}
	Remove-Item -Recurse -Force $tempDestination
	return $destinationPath
}

#==============================================================================
# Function: Register-DockerEngineService
#==============================================================================
<#
.SYNOPSIS
	Registers and starts the Docker Engine (dockerd) as a Windows service.
.DESCRIPTION
	Checks if 'dockerd.exe' exists in the specified installation path.
	Checks if the 'docker' service is already registered using Get-Service.
	If not registered, it executes 'dockerd.exe --register-service'.
	If the service is registered but not running, it starts the service using Start-Service.
	Waits briefly after starting and adds the Docker installation path to the current session's PATH.
.PARAMETER destinationPath
	The directory where Docker Engine was installed. Defaults to '.\docker'.
.EXAMPLE
	Register-DockerEngineService -destinationPath "C:\Program Files\DockerEngine"
.NOTES
	Requires administrative privileges to register or start services.
	Exits script if dockerd.exe is not found or if service registration/start fails.
	Uses Start-Sleep to allow the service time to initialize.
#>
function Register-DockerEngineService {
	param(
		[string]$destinationPath = ".\docker"
	)
	$dockerdPath = Join-Path $destinationPath "dockerd.exe"
	if (-not (Test-Path $dockerdPath)) {
		Write-Error "dockerd.exe not found in $destinationPath. Installation may have failed."
		exit 1
	}
	Write-Output "Checking if Docker Engine service is already registered..."
	$dockerService = Get-Service -Name docker -ErrorAction SilentlyContinue
	if ($null -eq $dockerService) {
		Write-Output "Registering Docker Engine as a service..."
		& "$dockerdPath" --register-service
		if ($LASTEXITCODE -ne 0) {
			Write-Error "Failed to register Docker service."
			exit 1
		}
		$dockerService = Get-Service -Name docker -ErrorAction SilentlyContinue
	}
	else {
		Write-Output "Docker service is already registered."
	}

	if ($dockerService.Status -ne 'Running') {
		Write-Output "Starting Docker service..."
		Start-Service docker
		$dockerService = Get-Service -Name docker -ErrorAction SilentlyContinue
		if ($dockerService.Status -ne 'Running') {
			Write-Error "Failed to start Docker service."
			exit 1
		}
	}
	else {
		Write-Output "Docker service is already running."
	}
	Start-Sleep -Seconds 10
	$env:Path = "$(Resolve-Path $destinationPath);$env:Path"
}

#==============================================================================
# Function: Test-DockerWorking
#==============================================================================
<#
.SYNOPSIS
	Verifies that the installed Docker Engine is working by running the hello-world container.
.DESCRIPTION
	Determines the path to the 'docker.exe' command (either from PATH or the specified installation directory).
	Sets the DOCKER_HOST environment variable for the npipe connection.
	Checks if the 'hello-world' image exists locally, pulling it if necessary.
	Checks if a container named 'hello-world-test' exists, running a new one if necessary.
	Reports success or failure based on the container run command's exit code.
.PARAMETER destinationPath
	The directory where Docker Engine was installed. Used to find docker.exe if not in PATH. Defaults to '.\docker'.
.EXAMPLE
	Test-DockerWorking -destinationPath "C:\Program Files\DockerEngine"
.NOTES
	Exits script if the hello-world container fails to run.
	Uses Write-Output for status messages.
#>
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
	Write-Output "Verifying Docker installation with hello-world image..."
	$env:DOCKER_HOST = "npipe:////./pipe/docker_engine"

	$existingHelloWorld = &$dockerExe images --filter "reference=hello-world" --format "{{.Repository}}"
	if (-not $existingHelloWorld) {
		Write-Output "hello-world image not found locally. Pulling hello-world image..."
		&$dockerExe pull hello-world | Out-Null
	}

	$helloWorldContainerName = "hello-world-test"
	$existingContainer = &$dockerExe ps -a --filter "name=^$helloWorldContainerName$" --format "{{.ID}}"

	if (-not $existingContainer) {
		Write-Output "No existing hello-world container found. Running a new one..."
		# docker run [options] IMAGE [COMMAND [ARG...]]
		# run         Run a command in a new container.
		# --name      Assign a name to the container.
		# --platform  Specify the platform for image selection.
		$output = &$dockerExe run --name $helloWorldContainerName --platform linux/amd64 hello-world 2>&1
		if ($LASTEXITCODE -ne 0) {
			Write-Error "Docker Engine installation verification failed. Output:`n$output"
			exit 1
		}
	}
	else {
		Write-Output "hello-world container already exists. Skipping container creation."
	}

	Write-Output "Docker Engine is working successfully."
}

#==============================================================================
# Function: Test-DockerInstallation
#==============================================================================
<#
.SYNOPSIS
	Checks if Docker is installed and provides options to install Docker Desktop or Docker Engine if not found.
.DESCRIPTION
	Checks if the 'docker' command is available using Get-Command.
	If not found, prompts the user to choose between installing Docker Desktop (via winget) or Docker Engine (static binary).
	If Docker Desktop is chosen, it attempts installation using winget.
	If Docker Engine is chosen, it calls Invoke-DockerEngineDownload, Install-DockerEngine, and Register-DockerEngineService.
	If Docker is already found, it proceeds directly to verification.
	Finally, calls Test-DockerWorking to verify the installation.
.EXAMPLE
	Test-DockerInstallation
.NOTES
	Requires administrative privileges for installation.
	Requires winget if Docker Desktop installation is chosen.
	Exits script on installation failure or invalid user selection.
	Uses Write-Output/Write-Information for status messages.
#>
function Test-DockerInstallation {
	if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
		Write-Information "Docker is not installed." # Changed to Information

		# Define Menu Title and Items
		$menuTitle = "Select Docker installation method"
		$menuItems = [ordered]@{
			"1" = "Install Docker Desktop (requires winget)"
			"2" = "Install Docker Engine (static binary installation)"
			# "0" = "Exit" # Implicit exit choice
		}

		# Define Menu Actions
		$menuActions = @{
			"1" = {
				if (Get-Command winget -ErrorAction SilentlyContinue) {
					Write-Information "Installing Docker Desktop using winget..." # Changed to Information
					winget install --id Docker.DockerDesktop -e --accept-package-agreements --accept-source-agreements
					Start-Sleep -Seconds 60
					if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
						Write-Error "Docker Desktop installation via winget failed. Please install Docker manually."
						exit 1 # Exit script on error
					}
					Write-Information "Docker Desktop installed successfully." # Changed to Information
					# Let the main script continue after successful install
				}
				else {
					Write-Error "winget is not available for Docker Desktop installation. Please install Docker manually."
					exit 1 # Exit script on error
				}
			}
			"2" = {
				$zipPath = Invoke-DockerEngineDownload -dockerStaticUrl $dockerStaticUrl
				$destinationPath = Install-DockerEngine -zipPath $zipPath -destinationPath ".\docker"
				Register-DockerEngineService -destinationPath $destinationPath
				# Let the main script continue after successful install
			}
			# "0" action will exit the loop, and the script will then exit because Docker wasn't found.
		}

		# Invoke the Menu Loop
		# We don't add 'exit' here because if the user chooses 0, we want the script to exit naturally
		# because Docker wasn't found/installed. If they choose 1 or 2, the action completes,
		# the loop exits, and the rest of Test-DockerInstallation runs.
		Invoke-MenuLoop -MenuTitle $menuTitle -MenuItems $menuItems -ActionMap $menuActions -ExitChoice "0"

		# Re-check if Docker is available after the loop (in case user chose 0 or installation failed implicitly)
		if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
			Write-Error "Docker installation was not completed or was skipped. Exiting."
			exit 1
		}
	}
	else {
		Write-Information "Docker command found in PATH. Using existing Docker installation." # Changed to Information
	}
	Test-DockerWorking -destinationPath ".\docker"
}

#==============================================================================
# Function: Test-DockerDaemonStatus
#==============================================================================
<#
.SYNOPSIS
	Checks if the Docker daemon is running and reachable.
.DESCRIPTION
	Sets the DOCKER_HOST environment variable for the npipe connection.
	Runs 'docker info' and checks the exit code.
	Reports success or failure based on the command's exit code.
.EXAMPLE
	Test-DockerDaemonStatus
.NOTES
	Exits script if the daemon is not reachable.
	Uses Write-Output for status messages.
#>
function Test-DockerDaemonStatus {
	Write-Output "Ensuring Docker daemon is reachable..."
	$env:DOCKER_HOST = "npipe:////./pipe/docker_engine"
	$dockerInfo = docker info 2>&1
	if ($LASTEXITCODE -ne 0) {
		Write-Error "Docker daemon is not reachable. Please ensure Docker is running. Error details: $dockerInfo"
		exit 1
	}
	Write-Output "Docker daemon is reachable."
}

#############################################
# Main Script Logic for Docker Setup
#############################################

Test-WSLStatus
Test-DockerInstallation
Test-DockerDaemonStatus

Write-Output "Docker installation and verification completed successfully."
