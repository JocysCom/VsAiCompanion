using namespace System.IO
# ----------------------------------------------------------------------------
[string]$current = $MyInvocation.MyCommand.Path
# Get calling command path.
[string]$calling = @(Get-PSCallStack)[1].InvocationInfo.MyCommand.Path
# If executed directly then...
if ($calling -ne "") {
	$current = $calling
}
# ----------------------------------------------------------------------------
[FileInfo]$file = New-Object FileInfo($current)
# Set public parameters.
$global:scriptName = $file.Basename
$global:scriptPath = $file.Directory.FullName
# Change current directory.
[Console]::WriteLine("Script Path: {0}", $scriptPath)
[Environment]::CurrentDirectory = $scriptPath
Set-Location $scriptPath
# ----------------------------------------------------------------------------
# Functions
# ----------------------------------------------------------------------------
function DownloadFile {
	param($sourcePath, $targetPath)
	$tpFi = New-Object FileInfo($targetPath)
	if (-not $tpFi.Directory.Exists) {
		$tpFi.Directory.Create()
	}
	Invoke-WebRequest -Uri $sourcePath -OutFile $targetPath
}
# ----------------------------------------------------------------------------
# Show menu
# ----------------------------------------------------------------------------
function ShowOptionsMenu {
	param($items, $title)
	#----------------------------------------------------------
	# Get local configurations.
	$keys = "1234567890ABCDEFGHIJKLMNOPQRSTUVWXYZ"
	$dic = @{}
	if ("$title" -eq "") { $title = "Options:" }
	Write-Host $title
	Write-Host
	[int]$i = 0
	foreach ($item in $items) {
		if ("$item" -eq "") { 
			Write-Host
			continue
		}
		$key = $keys[$i] 
		$dic["$key"] = $item
		Write-Host "	$key - $item"
		$i++
	}
	Write-Host
	$m = Read-Host -Prompt "Type option and press ENTER to continue"
	$m = $m.ToUpper()
	return $dic[$m.ToUpper()]
}
# ----------------------------------------------------------------------------
# Download and install Python
# ----------------------------------------------------------------------------
# Download Python: https://www.python.org/downloads/
$pythonLink = "https://www.python.org/ftp/python/3.11.6/python-3.11.6-amd64.exe"
$pythonFile = ".\Data\Downloads\python-3.11.6-amd64.exe"
function DownloadPython {
	DownloadFile $pythonLink $pythonFile
}
function InstallPython {
	Start-Process -FilePath $pythonFile -ArgumentList "/quiet InstallAllUsers=1 PrependPath=1" -Wait
}
# ----------------------------------------------------------------------------
# Install Git
# ----------------------------------------------------------------------------
$gitLink = "https://github.com/git-for-windows/git/releases/download/v2.38.0.windows.1/Git-2.38.0-64-bit.exe"
$gitFile = ".\Data\Downloads\Git-2.38.0-64-bit.exe"
function DownloadGit {
	DownloadFile $gitLink $gitFile
}
function InstallGit {
	Start-Process -FilePath $gitFile -ArgumentList "/VERYSILENT" -Wait
}
# ----------------------------------------------------------------------------
# Install Git Large File Storage (LFS) Extension
# ----------------------------------------------------------------------------
# https://git-lfs.com/
$gitLfsLink = "https://github.com/git-lfs/git-lfs/releases/download/v3.4.0/git-lfs-windows-v3.4.0.exe"
$gitLfsFile = ".\Data\Downloads\git-lfs-windows-v3.4.0.exe"
function DownloadGitLfs {
	DownloadFile $gitLfsLink $gitLfsFile
}
function InstallGitLfs {
	Start-Process -FilePath $gitLfsFile -ArgumentList "/VERYSILENT" -Wait
}
# ----------------------------------------------------------------------------
# Install CUDA (optional)
# ----------------------------------------------------------------------------
# https://developer.nvidia.com/cuda-downloads
$cudaLink = "https://developer.download.nvidia.com/compute/cuda/12.3.1/local_installers/cuda_12.3.1_546.12_windows.exe"
$cudaFile = ".\Data\Downloads\cuda_12.3.1_546.12_windows.exe"
function DownloadCuda {
	DownloadFile $cudaLink $cudaFile
}
function InstallCuda {
	Start-Process -FilePath $cudaFile -ArgumentList "/s" -Wait
}
# ----------------------------------------------------------------------------
# Install Python Libraries
# ----------------------------------------------------------------------------
function InstallPythonTorch {
	python -m pip install torch torchvision torchaudio
}
function InstallPythonTransformers {
	python -m pip install transformers
}
# ----------------------------------------------------------------------------
# Clone AI Model Repository
# ----------------------------------------------------------------------------
function CloneModelRepository {
	# if you want to clone without large files – just their pointers
	# prepend your git clone with the following env var:
	#$env:GIT_LFS_SKIP_SMUDGE = "1"
	$repoUrl = "https://huggingface.co/mistralai/Mistral-7B-v0.1"
	git lfs install
	git clone $repoUrl ".\Data\Model"
}
# ----------------------------------------------------------------------------
# Download and Install: LM Studio
# ----------------------------------------------------------------------------
# https://lmstudio.ai/
$lmStudioLink = "https://s3.amazonaws.com/releases.lmstudio.ai/0.2.8/LM+Studio-0.2.8+Setup.exe"
$lmStudioFile = ".\Data\Downloads\LM+Studio-0.2.8+Setup.exe"
function DownloadCuda {
	DownloadFile $lmStudioLink $lmStudioLink
}
function InstallCuda {
	Start-Process -FilePath $lmStudioFile
}
# ----------------------------------------------------------------------------

# Show menus.
$mdp = "Download Python"
$mip = "Install  Python"
$mdg = "Download Git"
$mig = "Install  Git"
$mdgl = "Download Git LFS"
$migl = "Install  Git LFS"
$mdc = "Download CUDA"
$mic = "Install  CUDA"
$mip1 = "Install Python Torch"
$mip2 = "Install Python Transformers"
$mcr = "Clone Model Repository"
$mdlms = "Download LM Studio"
$milms = "Install  LM Studio"
$menuItems = @( $mdp, $mip, $mdg, $mig, $mdgl, $migl, $mdc, $mic, $mip1, $mip2, $mcr, $mdlms, $milms )
$option = ShowOptionsMenu $menuItems  "Select Option:"
if ("$option" -eq "") {
	return
}
Write-Host "Selected: $option"

switch ($option) {
	$mdp { DownloadPython }
	$mip { InstallPython }
	$mdg { DownloadGit }
	$mig { InstallGit }
	$mdgl { DownloadGitLfs }
	$migl { InstallGitLfs }
	$mdc { DownloadCuda }
	$mic { InstallCuda }
	$mip1 { InstallPythonTorch }
	$mip2 { InstallPythonTransformers }
	$mcr { CloneModelRepository }
	$mdlms { DownloadLmStudio }
	$milms { InstallLmStudio }
}
