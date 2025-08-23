# Terraform Tools Manager

# --- PS7 Check ---
if ($PSVersionTable.PSEdition -ne 'Core' -or $PSVersionTable.PSVersion -lt [Version]'7.0') {
    Write-Warning "This script requires PowerShell 7+ (pwsh) to work properly."
	Write-Warning "You're running PowerShell $($PSVersionTable.PSEdition) $($PSVersionTable.PSVersion)."
	pause
 }

Write-Host "=== Terraform Tools Status ===" -ForegroundColor Cyan

# Ensure WinGet PowerShell module is available
if (-not (Get-Module Microsoft.WinGet.Client -ListAvailable)) {
	Write-Host "Installing Microsoft.WinGet.Client module..." -ForegroundColor Yellow
	Install-Module -Scope CurrentUser Microsoft.WinGet.Client -Force
}
Import-Module Microsoft.WinGet.Client

# Define tools with exact names for WinGet
$tools = @(
	@{ Name = "PowerShell"; WinGetName = "PowerShell"; Id = "Microsoft.PowerShell" }
	@{ Name = "Azure CLI"; WinGetName = "Microsoft Azure CLI"; Id = "Microsoft.AzureCLI" }
	@{ Name = "Terraform"; WinGetName = "HashiCorp Terraform"; Id = "Hashicorp.Terraform" }
)

# Display status
for ($i = 0; $i -lt $tools.Count; $i++) {
	$tool = $tools[$i]

	# Get installed version using exact name match
	$installed = Get-WinGetPackage -Id $tool.Id -MatchOption 'Equals' -ErrorAction SilentlyContinue
	$local = if ($installed) { $installed.InstalledVersion } else { "Not installed" }

	# Get available version
	$available = Find-WinGetPackage -Id $tool.Id -MatchOption 'Equals' -ErrorAction SilentlyContinue
	$remote = if ($available) { $available.Version } else { "Unknown" }

	Write-Host "$($i+1). $($tool.Name): Local=$local | Remote=$remote"

}

$modules = @(
	@{ Name = "Az" }
	@{ Name = "SqlServer" }
)

function GetModuleScope {
	param ($p)
	if (-not $p) { return '' }
	if ($p -like "$env:USERPROFILE\Documents\PowerShell\Modules*" -or
		$p -like "$env:USERPROFILE\Documents\WindowsPowerShell\Modules*") { return 'CurrentUser' }
	elseif ($p -like "$env:ProgramFiles\PowerShell\Modules*" -or
		$p -like "$env:ProgramFiles\WindowsPowerShell\Modules*") { return 'AllUsers' }
	elseif ($p -like "$PSHOME\Modules*") { return 'PSHome' }
	return 'Custom'
}


# Display status
for ($i = 0; $i -lt $modules.Count; $i++) {
	$module = $modules[$i]

	# Get installed version.
	$installed = Get-Module $module.Name -ListAvailable | Select-Object -First 1
	$moduleScope = GetModuleScope($installed.ModuleBase)
	$local = if ($installed) { $installed.Version } else { "Not installed" }

	# Get available version
	$available = Find-Module -Name $module.Name -Repository PSGallery
	$remote = if ($available) { $available  | Select-Object -ExpandProperty Version } else { "Unknown" }

	Write-Host "$($i+1+$tools.Count). $($module.Name): Local=$local | Remote=$remote | Scope=$moduleScope"
}

function UninstallAzModules {
	# uninstall modules installed via PowerShellGet
	$installed = Get-InstalledModule -Name 'Az.*' -ErrorAction SilentlyContinue
	foreach ($module in $installed) {
		Write-Output "Uninstalling $($module.Name) $($module.Version)..."
		try {
			Uninstall-Module -Name $module.Name -RequiredVersion $module.Version -Force -ErrorAction Stop
			Write-Output "Uninstalled $($module.Name) $($module.Version)."
		} catch {
			Write-Warning "Failed to uninstall $($module.Name) $($module.Version): $_"
		}
	}
	# remove leftover module folders from all PSModulePath locations
	#$paths = $env:PSModulePath -split ';'
	#foreach ($p in $paths) {
	#	if (-not (Test-Path $p)) { continue }
	#	Get-ChildItem -Path $p -Directory -Filter 'Az.*' -ErrorAction SilentlyContinue | ForEach-Object {
	#		Write-Output "Removing folder $($_.FullName)..."
	#		try {
	#			Remove-Item -LiteralPath $_.FullName -Recurse -Force -ErrorAction Stop
	#			Write-Output "Removed $($_.FullName)."
	#		} catch {
	#			Write-Warning "Failed to remove $($_.FullName): $_"
	#		}
	#	}
	#}
}
	


# Menu
Write-Host "`n=== Actions ===" -ForegroundColor Yellow
Write-Host "1. Reinstall PowerShell"
Write-Host "2. Reinstall Azure CLI"
Write-Host "3. Reinstall Terraform"
Write-Host "4. Reinstall Az Module (Current User)"
Write-Host "5. Reinstall SqlServer Module (Current User)"
Write-Host ""
Write-Host "6. Uninstall Az.* Modules"
Write-Host "0. Quit"

$choice = Read-Host "Choice"

switch ($choice) {
	"1" { winget install Microsoft.PowerShell }
	"2" { winget install Microsoft.AzureCLI }
	"3" { winget install HashiCorp.Terraform }
	"4" {
			Set-PSRepository -Name 'PSGallery' -InstallationPolicy Trusted
			Install-Module PowerShellGet -Force -AllowClobber -Scope CurrentUser
			Install-Module Az -AllowClobber -Scope CurrentUser -Force
		}
	"5" {
			Set-PSRepository -Name 'PSGallery' -InstallationPolicy Trusted
			Install-Module PowerShellGet -Force -AllowClobber -Scope CurrentUser
			Install-Module SqlServer -AllowClobber -Scope CurrentUser -Force
		}
	"6" {
			UninstallAzModules
		}
	"0" { exit }
	default { Write-Host "Invalid choice" -ForegroundColor Red }
}

Write-Host "Done!" -ForegroundColor Green
Write-Warning "Close all IDE tools and open it again for the changes to come into effect."
