################################################################################
# Description  : Script to install and configure NVIDIA Container Toolkit in WSL2.
#                Enables GPU acceleration for Docker, Podman, Containerd, and CRI-O.
#                Installs toolkit, configures runtime, and verifies GPU access.
# Usage        : Run as Administrator (for WSL operations)
# Prerequisites: - WSL2 with Ubuntu/Debian distribution
#                - NVIDIA GPU drivers installed on Windows
#                - Container engine (Docker, Podman, etc.) installed
################################################################################

using namespace System
using namespace System.IO

# Dot-source the necessary helper function files.
. "$PSScriptRoot\Setup_Helper_CoreFunctions.ps1"
. "$PSScriptRoot\Setup_Helper_WSLFunctions.ps1"
. "$PSScriptRoot\Setup_Helper_ContainerEngine.ps1"

# Ensure the script is running as Administrator and set the working directory.
Test-AdminPrivilege
Set-ScriptLocation

#==============================================================================
# Function: Test-NvidiaDrivers
#==============================================================================
<#
.SYNOPSIS
	Tests if NVIDIA GPU drivers are installed and working on Windows.
.DESCRIPTION
	Checks if nvidia-smi is available and can query GPU information.
.OUTPUTS
	[bool] Returns $true if NVIDIA drivers are working, $false otherwise.
.EXAMPLE
	Test-NvidiaDrivers
#>
function Test-NvidiaDrivers {
	[CmdletBinding()]
	param()

	try {
		Write-Host "Checking for NVIDIA GPU drivers..." -ForegroundColor Yellow

		# Try common nvidia-smi locations
		$nvidiaSmiPaths = @(
			"nvidia-smi",  # In PATH
			"C:\Windows\System32\nvidia-smi.exe",
			"C:\Program Files\NVIDIA Corporation\NVSMI\nvidia-smi.exe"
		)

		$nvidiaSmiPath = $null
		foreach ($path in $nvidiaSmiPaths) {
			if ($path -eq "nvidia-smi") {
				$cmd = Get-Command nvidia-smi -ErrorAction SilentlyContinue
				if ($cmd) {
					$nvidiaSmiPath = $cmd.Source
					break
				}
			}
			elseif (Test-Path $path) {
				$nvidiaSmiPath = $path
				break
			}
		}

		if (-not $nvidiaSmiPath) {
			Write-Warning "nvidia-smi not found in common locations."
			Write-Host "Checking if NVIDIA drivers are installed via device manager..." -ForegroundColor Yellow

			# Alternative check: Look for NVIDIA devices
			$nvidiaDevices = Get-CimInstance Win32_PnPEntity | Where-Object { $_.Name -match "NVIDIA" -and $_.Name -match "GeForce|Quadro|Tesla|RTX|GTX" }
			if ($nvidiaDevices) {
				Write-Host "  ⚠️ NVIDIA GPU detected but nvidia-smi not found" -ForegroundColor Yellow
				Write-Host "  GPU(s) found: $($nvidiaDevices.Name -join ', ')" -ForegroundColor Cyan
				Write-Host "  This is acceptable - WSL2 GPU access is what matters" -ForegroundColor Green
				return $true
			}

			Write-Host "Please install NVIDIA GPU drivers from: https://www.nvidia.com/Download/index.aspx" -ForegroundColor Yellow
			return $false
		}

		# Test nvidia-smi execution
		Write-Host "  Found nvidia-smi at: $nvidiaSmiPath" -ForegroundColor Cyan
		$result = & $nvidiaSmiPath --query-gpu=name,driver_version --format=csv,noheader 2>&1
		if ($LASTEXITCODE -ne 0) {
			Write-Warning "nvidia-smi failed to execute properly."
			Write-Host "  Error: $result" -ForegroundColor Red
			return $false
		}

		Write-Host "  ✅ NVIDIA drivers detected:" -ForegroundColor Green
		Write-Host "  $result" -ForegroundColor Cyan
		return $true
	}
	catch {
		Write-Warning "Error checking NVIDIA drivers: $_"
		return $false
	}
}

#==============================================================================
# Function: Test-WSL2NvidiaSupport
#==============================================================================
<#
.SYNOPSIS
	Tests if NVIDIA GPU is accessible from within WSL2.
.DESCRIPTION
	Runs nvidia-smi inside WSL2 to verify GPU access.
.OUTPUTS
	[bool] Returns $true if GPU is accessible in WSL2, $false otherwise.
.EXAMPLE
	Test-WSL2NvidiaSupport
#>
function Test-WSL2NvidiaSupport {
	[CmdletBinding()]
	param()

	try {
		Write-Host "Testing NVIDIA GPU access in WSL2..." -ForegroundColor Yellow

		# Test nvidia-smi in WSL
		$wslResult = wsl nvidia-smi 2>&1
		if ($LASTEXITCODE -ne 0) {
			Write-Warning "nvidia-smi not accessible in WSL2."
			Write-Host "This is normal if Container Toolkit is not installed yet." -ForegroundColor Yellow
			return $false
		}

		Write-Host "  ✅ GPU accessible in WSL2" -ForegroundColor Green
		return $true
	}
	catch {
		Write-Warning "Error testing WSL2 NVIDIA support: $_"
		return $false
	}
}

#==============================================================================
# Function: Get-WSLDistribution
#==============================================================================
<#
.SYNOPSIS
	Gets the default or first available WSL distribution name.
.DESCRIPTION
	Queries WSL for available distributions and returns the name.
.OUTPUTS
	[string] Returns the distribution name or empty string if none found.
.EXAMPLE
	$distro = Get-WSLDistribution
#>
function Get-WSLDistribution {
	[CmdletBinding()]
	param()

	try {
		# Get default distribution
		$defaultDistro = wsl --list --quiet 2>&1 | Select-Object -First 1
		if ($defaultDistro) {
			# Remove any non-printable characters
			$distro = $defaultDistro -replace '[^\x20-\x7E]', ''
			return $distro.Trim()
		}
		return ""
	}
	catch {
		Write-Warning "Error getting WSL distribution: $_"
		return ""
	}
}

#==============================================================================
# Function: Install-NvidiaContainerToolkit
#==============================================================================
<#
.SYNOPSIS
	Installs NVIDIA Container Toolkit in WSL2.
.DESCRIPTION
	Adds NVIDIA repository and installs the toolkit packages in WSL2.
	Supports Ubuntu/Debian-based distributions (apt).
.OUTPUTS
	[bool] Returns $true if installation succeeds, $false otherwise.
.EXAMPLE
	Install-NvidiaContainerToolkit
#>
function Install-NvidiaContainerToolkit {
	[CmdletBinding()]
	param()

	Write-Host "`n=== Installing NVIDIA Container Toolkit in WSL2 Ubuntu ===" -ForegroundColor Cyan
	Write-Host "⚠️ WARNING: This installs in your Ubuntu WSL2 distro." -ForegroundColor Yellow
	Write-Host "⚠️ If you're using Windows Podman, use Option 5 instead!" -ForegroundColor Yellow
	Write-Host "⚠️ This option is ONLY for Docker/Podman running directly in WSL2." -ForegroundColor Yellow

	$continue = Read-Host "`nAre you using Docker or Podman inside WSL2 Ubuntu? (Y/N, default N)"
	if ($continue -ne "Y") {
		Write-Host "Installation cancelled. For Windows Podman, use Option 5." -ForegroundColor Yellow
		return $false
	}

	# Verify prerequisites
	if (-not (Test-NvidiaDrivers)) {
		Write-Error "NVIDIA drivers not found. Please install GPU drivers first."
		return $false
	}

	# Quick WSL check without verbose output
	Write-Host "Checking WSL2..." -ForegroundColor Yellow
	$wslVersion = wsl --status 2>&1 | Out-String
	if ($wslVersion -notmatch "version 2" -and $wslVersion -notmatch "Default Version: 2") {
		Write-Warning "WSL2 may not be enabled. Checking distributions..."
	}

	$distro = Get-WSLDistribution
	if (-not $distro) {
		Write-Error "No WSL distribution found. Please install a Linux distribution first."
		Write-Host "Install Ubuntu: wsl --install -d Ubuntu" -ForegroundColor Yellow
		return $false
	}

	Write-Host "  ✅ Using WSL distribution: $distro" -ForegroundColor Green

	# Check Linux distribution type
	Write-Host "Detecting Linux distribution type..." -ForegroundColor Yellow
	$osRelease = wsl cat /etc/os-release 2>&1

	if ($osRelease -match 'ubuntu|debian') {
		Write-Host "  Detected Debian/Ubuntu-based distribution" -ForegroundColor Green

		# Install prerequisites
		Write-Host "Installing prerequisites..." -ForegroundColor Yellow
		wsl sudo apt-get update
		wsl sudo apt-get install -y curl gnupg2

		# Configure NVIDIA repository
		Write-Host "Configuring NVIDIA Container Toolkit repository..." -ForegroundColor Yellow
		$repoCommands = @"
curl -fsSL https://nvidia.github.io/libnvidia-container/gpgkey | sudo gpg --dearmor -o /usr/share/keyrings/nvidia-container-toolkit-keyring.gpg
curl -s -L https://nvidia.github.io/libnvidia-container/stable/deb/nvidia-container-toolkit.list | sed 's#deb https://#deb [signed-by=/usr/share/keyrings/nvidia-container-toolkit-keyring.gpg] https://#g' | sudo tee /etc/apt/sources.list.d/nvidia-container-toolkit.list
sudo apt-get update
"@
		wsl bash -c $repoCommands

		# Install toolkit
		Write-Host "Installing NVIDIA Container Toolkit packages..." -ForegroundColor Yellow
		wsl sudo apt-get install -y nvidia-container-toolkit

		if ($LASTEXITCODE -ne 0) {
			Write-Error "Failed to install NVIDIA Container Toolkit."
			return $false
		}

		Write-Host "  ✅ NVIDIA Container Toolkit installed successfully" -ForegroundColor Green

		# Enable and start CDI refresh service (v1.18.0+)
		Write-Host "Enabling CDI specification auto-refresh service..." -ForegroundColor Yellow
		wsl sudo systemctl enable nvidia-cdi-refresh.path 2>&1 | Out-Null
		wsl sudo systemctl enable nvidia-cdi-refresh.service 2>&1 | Out-Null
		wsl sudo systemctl start nvidia-cdi-refresh.path 2>&1 | Out-Null
		wsl sudo systemctl restart nvidia-cdi-refresh.service 2>&1 | Out-Null

		Write-Host "  ✅ CDI auto-refresh service enabled" -ForegroundColor Green

		# Generate initial CDI specification
		Write-Host "Generating CDI specification..." -ForegroundColor Yellow
		wsl sudo nvidia-ctk cdi generate --output=/var/run/cdi/nvidia.yaml

		if ($LASTEXITCODE -eq 0) {
			Write-Host "  ✅ CDI specification generated at /var/run/cdi/nvidia.yaml" -ForegroundColor Green

			# List available CDI devices
			Write-Host "`nAvailable CDI devices:" -ForegroundColor Cyan
			wsl nvidia-ctk cdi list
		}

		return $true
	}
	elseif ($osRelease -match 'rhel|centos|fedora|amazon') {
		Write-Host "  Detected RHEL/CentOS/Fedora-based distribution" -ForegroundColor Green

		# Install prerequisites
		Write-Host "Installing prerequisites..." -ForegroundColor Yellow
		wsl sudo dnf install -y curl

		# Configure NVIDIA repository
		Write-Host "Configuring NVIDIA Container Toolkit repository..." -ForegroundColor Yellow
		wsl bash -c "curl -s -L https://nvidia.github.io/libnvidia-container/stable/rpm/nvidia-container-toolkit.repo | sudo tee /etc/yum.repos.d/nvidia-container-toolkit.repo"

		# Install toolkit
		Write-Host "Installing NVIDIA Container Toolkit packages..." -ForegroundColor Yellow
		wsl sudo dnf install -y nvidia-container-toolkit

		if ($LASTEXITCODE -ne 0) {
			Write-Error "Failed to install NVIDIA Container Toolkit."
			return $false
		}

		Write-Host "  ✅ NVIDIA Container Toolkit installed successfully" -ForegroundColor Green
		return $true
	}
	else {
		Write-Error "Unsupported Linux distribution. This script supports Ubuntu/Debian and RHEL/CentOS/Fedora."
		Write-Host "Distribution info:" -ForegroundColor Yellow
		Write-Host $osRelease
		return $false
	}
}

#==============================================================================
# Function: Configure-ContainerRuntime
#==============================================================================
<#
.SYNOPSIS
	Configures the container runtime to use NVIDIA Container Runtime.
.DESCRIPTION
	Uses nvidia-ctk to configure Docker, Podman, or other container runtimes.
.PARAMETER Runtime
	The container runtime to configure (docker, podman, containerd, crio).
.OUTPUTS
	[bool] Returns $true if configuration succeeds, $false otherwise.
.EXAMPLE
	Configure-ContainerRuntime -Runtime "docker"
#>
function Configure-ContainerRuntime {
	[CmdletBinding()]
	param(
		[Parameter(Mandatory=$true)]
		[ValidateSet("docker", "podman", "containerd", "crio")]
		[string]$Runtime
	)

	Write-Host "`n=== Configuring $Runtime Runtime ===" -ForegroundColor Cyan

	# Configure runtime
	Write-Host "Running nvidia-ctk runtime configure..." -ForegroundColor Yellow
	wsl sudo nvidia-ctk runtime configure --runtime=$Runtime

	if ($LASTEXITCODE -ne 0) {
		Write-Error "Failed to configure $Runtime runtime."
		return $false
	}

	# Restart service if needed
	if ($Runtime -eq "docker") {
		Write-Host "Restarting Docker service..." -ForegroundColor Yellow
		wsl sudo systemctl restart docker 2>&1
		Start-Sleep -Seconds 5
	}
	elseif ($Runtime -eq "containerd") {
		Write-Host "Restarting containerd service..." -ForegroundColor Yellow
		wsl sudo systemctl restart containerd 2>&1
		Start-Sleep -Seconds 5
	}
	elseif ($Runtime -eq "crio") {
		Write-Host "Restarting CRI-O service..." -ForegroundColor Yellow
		wsl sudo systemctl restart crio 2>&1
		Start-Sleep -Seconds 5
	}
	elseif ($Runtime -eq "podman") {
		Write-Host "Note: Podman uses CDI natively - no runtime config needed." -ForegroundColor Green
		Write-Host "      Devices are accessed via: --device nvidia.com/gpu=all" -ForegroundColor Cyan

		# Ensure CDI specification is generated
		Write-Host "Generating CDI specification for Podman..." -ForegroundColor Yellow
		wsl sudo nvidia-ctk cdi generate --output=/var/run/cdi/nvidia.yaml

		if ($LASTEXITCODE -eq 0) {
			Write-Host "  ✅ CDI specification ready for Podman" -ForegroundColor Green
			Write-Host "`nAvailable CDI devices:" -ForegroundColor Cyan
			wsl nvidia-ctk cdi list
		}
	}

	Write-Host "  ✅ $Runtime runtime configured successfully" -ForegroundColor Green
	return $true
}

#==============================================================================
# Function: Test-ContainerGPUAccess
#==============================================================================
<#
.SYNOPSIS
	Tests GPU access from within a container.
.DESCRIPTION
	Runs a test container with GPU access to verify the setup.
.PARAMETER Runtime
	The container runtime to test (docker or podman).
.OUTPUTS
	[bool] Returns $true if GPU is accessible in containers, $false otherwise.
.EXAMPLE
	Test-ContainerGPUAccess -Runtime "docker"
#>
function Test-ContainerGPUAccess {
	[CmdletBinding()]
	param(
		[Parameter(Mandatory=$false)]
		[ValidateSet("docker", "podman")]
		[string]$Runtime = "docker"
	)

	Write-Host "`n=== Testing GPU Access in Containers ===" -ForegroundColor Cyan

	try {
		if ($Runtime -eq "docker") {
			Write-Host "Running test container with Docker..." -ForegroundColor Yellow
			Write-Host "Command: docker run --rm --gpus all nvidia/cuda:12.0.0-base-ubuntu22.04 nvidia-smi" -ForegroundColor Cyan
			$testResult = wsl docker run --rm --gpus all nvidia/cuda:12.0.0-base-ubuntu22.04 nvidia-smi 2>&1
		}
		else {
			Write-Host "Running test container with Podman (CDI)..." -ForegroundColor Yellow
			Write-Host "Command: podman run --rm --device nvidia.com/gpu=all --security-opt=label=disable nvidia/cuda:12.0.0-base-ubuntu22.04 nvidia-smi" -ForegroundColor Cyan
			$testResult = wsl podman run --rm --device nvidia.com/gpu=all --security-opt=label=disable nvidia/cuda:12.0.0-base-ubuntu22.04 nvidia-smi 2>&1
		}

		if ($LASTEXITCODE -eq 0) {
			Write-Host "  ✅ GPU successfully accessible in containers!" -ForegroundColor Green
			Write-Host "`nTest output:" -ForegroundColor Cyan
			Write-Host $testResult
			return $true
		}
		else {
			Write-Warning "GPU test failed."
			Write-Host "Error output:" -ForegroundColor Red
			Write-Host $testResult
			return $false
		}
	}
	catch {
		Write-Warning "Error testing container GPU access: $_"
		return $false
	}
}

#==============================================================================
# Function: Show-NvidiaToolkitStatus
#==============================================================================
<#
.SYNOPSIS
	Displays the installation status of NVIDIA components.
.DESCRIPTION
	Shows status of NVIDIA drivers, WSL2 GPU access, and Container Toolkit.
.EXAMPLE
	Show-NvidiaToolkitStatus
#>
function Show-NvidiaToolkitStatus {
	[CmdletBinding()]
	param()

	Write-Host "`n==================== NVIDIA CONTAINER TOOLKIT STATUS ====================" -ForegroundColor Cyan

	# Check Windows NVIDIA Drivers
	if (Test-NvidiaDrivers) {
		Write-Host "Windows NVIDIA Drivers: ✅ INSTALLED" -ForegroundColor Green
	}
	else {
		Write-Host "Windows NVIDIA Drivers: ❌ NOT INSTALLED" -ForegroundColor Red
		Write-Host "  Install from: https://www.nvidia.com/Download/index.aspx" -ForegroundColor Yellow
	}

	# Check WSL2 GPU Access
	if (Test-WSL2NvidiaSupport) {
		Write-Host "WSL2 GPU Access: ✅ AVAILABLE" -ForegroundColor Green
	}
	else {
		Write-Host "WSL2 GPU Access: ❌ NOT AVAILABLE" -ForegroundColor Red
	}

	# Auto-detect setup
	Write-Host "`n--- Container Engine Detection ---" -ForegroundColor Cyan

	$podmanWindows = Get-Command podman -ErrorAction SilentlyContinue
	$dockerWSL = wsl command -v docker 2>&1
	$podmanWSL = wsl command -v podman 2>&1

	$detectedSetup = ""

	if ($podmanWindows) {
		Write-Host "✅ Podman Desktop (Windows)" -ForegroundColor Green
		$machineList = podman machine list --format json 2>&1 | ConvertFrom-Json
		if ($machineList) {
			Write-Host "  Machine: $($machineList[0].Name)" -ForegroundColor Cyan
			$detectedSetup = "Podman Desktop"
		}
	}

	if ($LASTEXITCODE -eq 0 -and $dockerWSL) {
		Write-Host "✅ Docker in WSL2" -ForegroundColor Green
		if (-not $detectedSetup) { $detectedSetup = "WSL2 Docker" }
	}

	if ($LASTEXITCODE -eq 0 -and $podmanWSL) {
		Write-Host "✅ Podman in WSL2" -ForegroundColor Green
		if (-not $detectedSetup) { $detectedSetup = "WSL2 Podman" }
	}

	if ($detectedSetup) {
		Write-Host "`n💡 DETECTED SETUP: $detectedSetup" -ForegroundColor Yellow
		Write-Host "💡 RECOMMENDED: Use Option 2 (Auto-Install)" -ForegroundColor Green
	}

	# Check Container Toolkit Installation in WSL2 Ubuntu
	$toolkitWSL = wsl dpkg -l 2>&1 | Select-String "nvidia-container-toolkit"
	if ($toolkitWSL) {
		Write-Host "`nNVIDIA Toolkit in WSL2 Ubuntu: ✅ INSTALLED" -ForegroundColor Green
		$version = wsl nvidia-ctk --version 2>&1
		Write-Host "  Version: $version" -ForegroundColor Cyan
	}
	else {
		Write-Host "`nNVIDIA Toolkit in WSL2 Ubuntu: ❌ NOT INSTALLED" -ForegroundColor Gray
	}

	# Check toolkit in Podman machine
	if ($podmanWindows -and $machineList) {
		$machineName = $machineList[0].Name
		$toolkitPodman = podman machine ssh $machineName rpm -q nvidia-container-toolkit 2>&1
		if ($toolkitPodman -notmatch "not installed") {
			Write-Host "NVIDIA Toolkit in Podman Machine: ✅ INSTALLED" -ForegroundColor Green
			$versionPodman = podman machine ssh $machineName nvidia-ctk --version 2>&1
			Write-Host "  Version: $versionPodman" -ForegroundColor Cyan
		}
		else {
			Write-Host "NVIDIA Toolkit in Podman Machine: ❌ NOT INSTALLED" -ForegroundColor Red
		}
	}

	Write-Host "========================================================================`n" -ForegroundColor Cyan
}

#==============================================================================
# Function: Configure-PodmanMachineCDI
#==============================================================================
<#
.SYNOPSIS
	Configures CDI for Windows Podman machine.
.DESCRIPTION
	Installs NVIDIA Container Toolkit inside Podman's WSL2 machine and generates CDI specs.
.EXAMPLE
	Configure-PodmanMachineCDI
#>
function Configure-PodmanMachineCDI {
	[CmdletBinding()]
	param()

	Write-Host "`n=== Configuring CDI for Windows Podman Machine ===" -ForegroundColor Cyan

	# Check if Podman is available on Windows
	$podmanCmd = Get-Command podman -ErrorAction SilentlyContinue
	if (-not $podmanCmd) {
		Write-Error "Podman not found on Windows. Please install Podman Desktop first."
		return $false
	}

	# Get Podman machine name
	Write-Host "Detecting Podman machine..." -ForegroundColor Yellow
	$machineList = podman machine list --format json 2>&1 | ConvertFrom-Json
	if (-not $machineList) {
		Write-Error "No Podman machine found. Please initialize a Podman machine first."
		return $false
	}

	$machineName = $machineList[0].Name
	Write-Host "  Using Podman machine: $machineName" -ForegroundColor Green

	# Detect OS in Podman machine
	Write-Host "Detecting Podman machine OS..." -ForegroundColor Yellow
	$machineOS = podman machine ssh $machineName cat /etc/os-release 2>&1

	if ($machineOS -match 'fedora-coreos|fedora') {
		Write-Host "  Detected Fedora CoreOS (Podman's default)" -ForegroundColor Green

		# Fedora CoreOS uses rpm-ostree, install via toolbox or direct rpm
		Write-Host "`nInstalling NVIDIA Container Toolkit in Fedora CoreOS..." -ForegroundColor Yellow

		$installCommands = @"
# Add NVIDIA repository
curl -s -L https://nvidia.github.io/libnvidia-container/stable/rpm/nvidia-container-toolkit.repo | sudo tee /etc/yum.repos.d/nvidia-container-toolkit.repo

# Install toolkit packages (Fedora uses dnf)
sudo dnf install -y nvidia-container-toolkit

# Generate CDI specifications
sudo nvidia-ctk cdi generate --output=/etc/cdi/nvidia.yaml
sudo nvidia-ctk cdi generate --output=/var/run/cdi/nvidia.yaml

# Verify installation
nvidia-ctk --version
nvidia-ctk cdi list
"@
	}
	elseif ($machineOS -match 'ubuntu|debian') {
		Write-Host "  Detected Ubuntu/Debian-based machine" -ForegroundColor Green

		$installCommands = @"
# Update package lists
sudo apt-get update

# Install prerequisites
sudo apt-get install -y curl gnupg2

# Add NVIDIA repository
curl -fsSL https://nvidia.github.io/libnvidia-container/gpgkey | sudo gpg --dearmor -o /usr/share/keyrings/nvidia-container-toolkit-keyring.gpg
curl -s -L https://nvidia.github.io/libnvidia-container/stable/deb/nvidia-container-toolkit.list | sed 's#deb https://#deb [signed-by=/usr/share/keyrings/nvidia-container-toolkit-keyring.gpg] https://#g' | sudo tee /etc/apt/sources.list.d/nvidia-container-toolkit.list

# Update and install
sudo apt-get update
sudo apt-get install -y nvidia-container-toolkit

# Generate CDI specifications
sudo nvidia-ctk cdi generate --output=/etc/cdi/nvidia.yaml
sudo nvidia-ctk cdi generate --output=/var/run/cdi/nvidia.yaml

# Verify installation
nvidia-ctk --version
nvidia-ctk cdi list
"@
	}
	else {
		Write-Error "Unsupported OS in Podman machine. Expected Fedora CoreOS, Ubuntu, or Debian."
		Write-Host "OS info:" -ForegroundColor Yellow
		Write-Host $machineOS
		return $false
	}

	Write-Host "Running installation commands..." -ForegroundColor Yellow
	podman machine ssh $machineName $installCommands

	if ($LASTEXITCODE -eq 0) {
		Write-Host "`n  ✅ CDI configured successfully for Podman machine" -ForegroundColor Green
		Write-Host "`nAvailable CDI devices:" -ForegroundColor Cyan
		podman machine ssh $machineName nvidia-ctk cdi list

		Write-Host "`n  ℹ️ You can now use GPU with: podman run --device nvidia.com/gpu=all ..." -ForegroundColor Cyan
		return $true
	} else {
		Write-Error "Failed to configure CDI in Podman machine."
		return $false
	}
}

#==============================================================================
# Function: Install-FullStack
#==============================================================================
<#
.SYNOPSIS
	Performs complete installation and configuration.
.DESCRIPTION
	Installs toolkit and configures all available container runtimes.
.EXAMPLE
	Install-FullStack
#>
function Install-FullStack {
	[CmdletBinding()]
	param()

	# Install toolkit
	if (-not (Install-NvidiaContainerToolkit)) {
		Write-Error "Installation failed. Please check the errors above."
		return
	}

	# Configure Docker if available
	$dockerAvailable = wsl command -v docker 2>&1
	if ($LASTEXITCODE -eq 0) {
		Configure-ContainerRuntime -Runtime "docker"
		Test-ContainerGPUAccess -Runtime "docker"
	}
	else {
		Write-Host "Docker not found in WSL2. Skipping Docker configuration." -ForegroundColor Yellow
	}

	# Configure Podman if available
	$podmanAvailable = wsl command -v podman 2>&1
	if ($LASTEXITCODE -eq 0) {
		Configure-ContainerRuntime -Runtime "podman"
		Test-ContainerGPUAccess -Runtime "podman"
	}
	else {
		Write-Host "Podman not found in WSL2. Skipping Podman configuration." -ForegroundColor Yellow
	}

	Write-Host "`n✅ NVIDIA Container Toolkit setup complete!" -ForegroundColor Green
	Write-Host "You can now use GPU acceleration in your containers." -ForegroundColor Green
}

#############################################
# Main Menu Loop
#############################################

#==============================================================================
# Function: Install-AutoDetect
#==============================================================================
<#
.SYNOPSIS
	Auto-detects container engine and installs NVIDIA toolkit in correct location.
.DESCRIPTION
	Detects Podman Desktop or WSL2 Docker/Podman and installs toolkit automatically.
.EXAMPLE
	Install-AutoDetect
#>
function Install-AutoDetect {
	[CmdletBinding()]
	param()

	Write-Host "`n=== Auto-Detecting Container Engine Setup ===" -ForegroundColor Cyan

	# Check for Podman Desktop (Windows)
	$podmanWindows = Get-Command podman -ErrorAction SilentlyContinue
	if ($podmanWindows) {
		Write-Host "✅ Detected: Podman Desktop (Windows)" -ForegroundColor Green
		Write-Host "   Installing NVIDIA Container Toolkit in Podman machine..." -ForegroundColor Yellow
		return Configure-PodmanMachineCDI
	}

	# Check for WSL2 Docker
	$dockerWSL = wsl command -v docker 2>&1
	if ($LASTEXITCODE -eq 0) {
		Write-Host "✅ Detected: Docker in WSL2" -ForegroundColor Green
		Write-Host "   Installing NVIDIA Container Toolkit in WSL2..." -ForegroundColor Yellow
		if (Install-NvidiaContainerToolkit) {
			return Configure-ContainerRuntime -Runtime "docker"
		}
		return $false
	}

	# Check for WSL2 Podman
	$podmanWSL = wsl command -v podman 2>&1
	if ($LASTEXITCODE -eq 0) {
		Write-Host "✅ Detected: Podman in WSL2" -ForegroundColor Green
		Write-Host "   Installing NVIDIA Container Toolkit in WSL2..." -ForegroundColor Yellow
		if (Install-NvidiaContainerToolkit) {
			return Configure-ContainerRuntime -Runtime "podman"
		}
		return $false
	}

	Write-Error "No container engine detected. Please install Docker or Podman first."
	return $false
}

$menuTitle = "NVIDIA Container Toolkit Setup"
$menuItems = [ordered]@{
	"1" = "Show Status"
	"2" = "Auto-Install (Detects your setup automatically)"
	"3" = "Test GPU Access"
	"0" = "Exit"
}

$menuActions = @{
	"1" = { Show-NvidiaToolkitStatus }
	"2" = { Install-AutoDetect }
	"3" = {
		# Auto-detect for testing too
		$podmanWindows = Get-Command podman -ErrorAction SilentlyContinue
		if ($podmanWindows) {
			Write-Host "Testing Podman Desktop GPU access..." -ForegroundColor Yellow
			podman run --rm --device nvidia.com/gpu=all --security-opt=label=disable nvidia/cuda:12.0.0-base-ubuntu22.04 nvidia-smi
		}
		else {
			$dockerWSL = wsl command -v docker 2>&1
			if ($LASTEXITCODE -eq 0) {
				Test-ContainerGPUAccess -Runtime "docker"
			}
			else {
				Test-ContainerGPUAccess -Runtime "podman"
			}
		}
	}
}

Invoke-MenuLoop -MenuTitle $menuTitle -MenuItems $menuItems -ActionMap $menuActions -ExitChoice "0"