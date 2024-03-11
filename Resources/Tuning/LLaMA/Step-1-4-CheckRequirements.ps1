# Purpose: Check software and tool version compatibility with Orca-2 model requirements.

# Activate "tuning" Python environment.
tuning\Scripts\activate

# Verify CUDA version compatibility with PyTorch and Orca-2 model requirements.
$requiredCudaVersion = "11.1"
$cudaVersion = (nvidia-smi | Select-String -Pattern "CUDA Version: \d+\.\d+").Matches.Value.Split(":")[1].Trim()
if ([version]$cudaVersion -lt [version]$requiredCudaVersion) {
    Write-Warning "FAIL: Installed CUDA version ($cudaVersion) is not compatible. Please install CUDA version $requiredCudaVersion or higher."
}else{
Write-Host "PASS: CUDA version $cudaVersion"
}

# Ensure Python version is compatible with the transformers version required by Orca-2 (4.33.1)
$requiredPythonVersion = "3.6"
$pythonVersion = (python --version | Select-String -Pattern "Python \d+\.\d+").Matches.Value.Split(" ")[1].Trim()
if ([version]$pythonVersion -lt [version]$requiredPythonVersion) {
    Write-Warning "FAIL: Python version ($pythonVersion) is not compatible. Please install Python version $requiredPythonVersion or higher."
}else{
	Write-Host "PASS: Python version $pythonVersion"
}

# Ensure Git version is up-to-date, especially for handling large models like Orca-2.
$requiredGitVersion = "2.20"
$gitVersion = (git --version | Select-String -Pattern "git version \d+\.\d+").Matches.Value.Split(" ")[2].Trim()
if ([version]$gitVersion -lt [version]$requiredGitVersion) {
    Write-Warning "FAIL: Git version ($gitVersion) is outdated. Please update Git to version $gitVersion or higher."
}else{
	Write-Host "PASS: Git version $gitVersion"
}

