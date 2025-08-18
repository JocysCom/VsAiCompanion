Param(
	[switch]$AutoApprove
)

<#
.SYNOPSIS
  Install/update required tools for Terraform + Azure on Windows.
.DESCRIPTION
  - Checks installed versions of tools (pwsh, Az PowerShell module, SqlServer module, az CLI, terraform).
  - Uses winget and PowerShellGet to determine latest available versions where possible.
  - Prompts the user to confirm install/update (unless -AutoApprove is used).
.NOTES
  Run PowerShell 7 (pwsh) as Administrator for system-wide installs when possible.
#>

Write-Host "Preparing Terraform/Azure tool installer..." -ForegroundColor Cyan

function Confirm-Action {
	param(
		[string]$toolName,
		[string]$installed,
		[string]$latest,
		[switch]$defaultNo
	)
	$message = "Install/upgrade $toolName? (Installed: $installed, Latest: $latest)"
	if ($AutoApprove) {
		Write-Host "[AutoApprove] $message -> Yes"
		return $true
	}
	$default = if ($defaultNo) { "N" } else { "Y" }
	$ans = Read-Host -Prompt "$message [Y/N] (default: $default)"
	if ([string]::IsNullOrWhiteSpace($ans)) { $ans = $default }
	return $ans.Trim().ToUpper() -eq "Y"
}

function Get-ExeVersion {
	param([string]$exe, [string]$versionArgs = "--version")
	try {
		$proc = & $exe $versionArgs 2>&1
		if (-not $proc) { return $null }

		# Convert to string if it's an array
		$output = if ($proc -is [array]) { $proc -join "`n" } else { $proc.ToString() }

		# Try to extract first semantic version found (more flexible pattern)
		if ($output -match "(\d+\.\d+\.\d+(?:\.\d+)?)") {
			return $matches[1]
		}

		# fallback to first line, cleaned up
		$firstLine = ($proc | Select-Object -First 1).ToString().Trim()
		# Try to extract version from first line if it contains one
		if ($firstLine -match "(\d+\.\d+\.\d+(?:\.\d+)?)") {
			return $matches[1]
		}

		return $firstLine
	}
	catch {
		return $null
	}
}

function Get-ModuleVersion {
	param([string]$moduleName)
	try {
		$mod = Get-InstalledModule -Name $moduleName -ErrorAction SilentlyContinue
		if ($mod) { return $mod.Version.ToString() }
		# If not installed via PowerShellGet, check Get-Module -ListAvailable
		$loaded = Get-Module -ListAvailable -Name $moduleName | Select-Object -First 1
		if ($loaded) { return $loaded.Version.ToString() }
		return $null
	}
	catch {
		return $null
	}
}

function Get-WingetLatestVersion {
	param([string]$packageId)
	try {
		# Parse the regular text output for version (older winget versions don't support --output json)
		$textOutput = winget show --id $packageId --accept-source-agreements 2>$null
		if ($textOutput) {
			# Look for "Version:" line in output
			foreach ($line in $textOutput) {
				if ($line -match "^Version:\s*(.+)$") {
					return $Matches[1].Trim()
				}
			}
		}

		return $null
	}
	catch {
		Write-Debug "Error getting winget version for $packageId : $_"
		return $null
	}
}

function ConvertTo-NormalizedVersion {
	param([string]$version)
	if ([string]::IsNullOrWhiteSpace($version)) { return $null }

	# Remove common prefixes and suffixes
	$normalized = $version.Trim()
	$normalized = $normalized -replace '^v', ''  # Remove 'v' prefix
	$normalized = $normalized -replace '^azure-cli-', ''  # Remove azure-cli prefix
	$normalized = $normalized -replace '\s.*$', ''  # Remove anything after first space

	# Extract just the semantic version part (x.y.z or x.y.z.w)
	if ($normalized -match '(\d+\.\d+\.\d+(?:\.\d+)?)') {
		$extracted = $matches[1]

		# Normalize to 4-part version to ensure consistent comparison
		# Convert x.y.z to x.y.z.0 and leave x.y.z.w as is
		$parts = $extracted.Split('.')
		if ($parts.Count -eq 3) {
			return "$extracted.0"
		}
		return $extracted
	}

	return $normalized.Trim()
}

function Compare-Version {
	param(
		[string]$v1,
		[string]$v2
	)

	# Normalize versions first
	$norm1 = ConvertTo-NormalizedVersion $v1
	$norm2 = ConvertTo-NormalizedVersion $v2

	# If either is null, handle appropriately
	if ([string]::IsNullOrWhiteSpace($norm1) -or [string]::IsNullOrWhiteSpace($norm2)) {
		if ($norm1 -eq $norm2) { return 0 }
		if ([string]::IsNullOrWhiteSpace($norm1)) { return -1 }
		if ([string]::IsNullOrWhiteSpace($norm2)) { return 1 }
	}

	try {
		[version]$a = $norm1
		[version]$b = $norm2
		if ($a -lt $b) { return -1 }
		elseif ($a -eq $b) { return 0 }
		else { return 1 }
	}
	catch {
		# fallback string compare with normalized versions
		if ($norm1 -eq $norm2) { return 0 }
		elseif ($norm1 -lt $norm2) { return -1 } else { return 1 }
	}
}

function Install-Or-Upgrade-Winget {
	param(
		[string]$packageId,
		[string]$displayName
	)
	Write-Host "Installing/upgrading $displayName via winget ($packageId)..." -ForegroundColor Yellow
	try {
		$argumentList = "install --id $packageId --accept-source-agreements --silent"
		# Use -e to match exact id when available; but leave as-is to let winget resolve.
		$startInfo = @{
			FilePath     = "winget"
			ArgumentList = $argumentList
			NoNewWindow  = $true
			Wait         = $true
		}
		Start-Process @startInfo
		return $true
	}
	catch {
		Write-Warning "winget install for $($displayName) failed: $($_)"
		return $false
	}
}

# Define tools to manage
$tools = @(
	@{ Id = "Microsoft.PowerShell"; Name = "PowerShell (pwsh)"; Type = "winget"; CheckExe = "pwsh"; VersionArgs = "--version" },
	@{ Id = "Microsoft.AzureCLI"; Name = "Azure CLI (az)"; Type = "winget"; CheckExe = "az"; VersionArgs = "version" },
	@{ Id = "HashiCorp.Terraform"; Name = "Terraform"; Type = "winget"; CheckExe = "terraform"; VersionArgs = "version" },
	@{ Id = "HashiCorp.Vault"; Name = "Vault (optional)"; Type = "winget"; CheckExe = "vault"; VersionArgs = "--version" }
)

# PowerShell modules to manage (Install-Module)
$psModules = @(
	@{ Name = "Az"; Display = "Az PowerShell Module" },
	@{ Name = "SqlServer"; Display = "SqlServer PowerShell Module" }
)

# Check admin
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")
if (-not $isAdmin) {
	Write-Warning "Not running as Administrator. Some operations may require elevation (system-wide PATH changes, winget may require admin). Consider running PowerShell as Administrator."
}

$results = @()

# Process winget-managed tools
foreach ($t in $tools) {
	$name = $t.Name
	$id = $t.Id
	$checkExe = $t.CheckExe
	$cur = Get-ExeVersion -exe $checkExe -versionArgs $t.VersionArgs
	$latest = Get-WingetLatestVersion -packageId $id
	Write-Host "`nTool: $name"
	$installedStr = if ($cur) { $cur } else { 'Not installed' }
	$latestStr = if ($latest) { $latest } else { 'Unknown' }
	Write-Host "  Installed: $installedStr"
	Write-Host "  Latest:    $latestStr"
	$needInstall = $false
	if (-not $cur) { $needInstall = $true }
	elseif ($latest) {
		$cmp = Compare-Version $cur $latest
		if ($cmp -lt 0) { $needInstall = $true }
	}
	if ($needInstall) {
		# Debug output for version comparison
		Write-Host "  Debug: Comparing '$cur' vs '$latest' (normalized: '$(ConvertTo-NormalizedVersion $cur)' vs '$(ConvertTo-NormalizedVersion $latest)')" -ForegroundColor DarkGray
		$doIt = Confirm-Action -toolName $name -installed $installedStr -latest $latestStr
		if ($doIt) {
			$ok = Install-Or-Upgrade-Winget -packageId $id -displayName $name
			Start-Sleep -Seconds 3
			$newVer = Get-ExeVersion -exe $checkExe -versionArgs $t.VersionArgs
			$results += [pscustomobject]@{ Tool = $name; InstalledBefore = $cur; InstalledAfter = $newVer; Success = $ok }
		}
		else {
			$results += [pscustomobject]@{ Tool = $name; InstalledBefore = $cur; InstalledAfter = $cur; Success = $false }
		}
	}
	else {
		Write-Host "  No action required for $name." -ForegroundColor Green
		$results += [pscustomobject]@{ Tool = $name; InstalledBefore = $cur; InstalledAfter = $cur; Success = $true }
	}
}

# Process PowerShell modules
foreach ($m in $psModules) {
	$name = $m.Name
	$display = $m.Display
	$cur = Get-ModuleVersion -moduleName $name
	Write-Host "`nPowerShell Module: $display"
	$installedStr = if ($cur) { $cur } else { 'Not installed' }
	# For modules we will attempt to get latest from PSGallery via Find-Module
	try {
		$remote = Find-Module -Name $name -Repository PSGallery -ErrorAction SilentlyContinue
		$latest = if ($remote) { $remote.Version.ToString() } else { $null }
	}
	catch {
		$latest = $null
	}
	$latestStr = if ($latest) { $latest } else { 'Unknown' }
	Write-Host "  Installed: $installedStr"
	Write-Host "  Latest (PSGallery): $latestStr"
	$need = $false
	if (-not $cur) { $need = $true }
	elseif ($latest) {
		$cmp = Compare-Version $cur $latest
		if ($cmp -lt 0) { $need = $true }
	}
	if ($need) {
		$doIt = Confirm-Action -toolName $display -installed $installedStr -latest $latestStr
		if ($doIt) {
			try {
				Write-Host "Installing/updating $display..."
				Install-Module -Name $name -Scope CurrentUser -Force -AllowClobber -ErrorAction Stop
				$new = Get-ModuleVersion -moduleName $name
				$results += [pscustomobject]@{ Tool = $display; InstalledBefore = $cur; InstalledAfter = $new; Success = $true }
			}
			catch {
				Write-Warning "Failed to install/update $($display): $($_)"
				$results += [pscustomobject]@{ Tool = $display; InstalledBefore = $cur; InstalledAfter = $cur; Success = $false }
			}
		}
		else {
			$results += [pscustomobject]@{ Tool = $display; InstalledBefore = $cur; InstalledAfter = $cur; Success = $false }
		}
	}
	else {
		Write-Host "  No action required for $display." -ForegroundColor Green
		$results += [pscustomobject]@{ Tool = $display; InstalledBefore = $cur; InstalledAfter = $cur; Success = $true }
	}
}

# Final verification summary
Write-Host "`n===== Installation Summary =====" -ForegroundColor Cyan
$results | Format-Table -AutoSize

# Quick verification commands to show versions
Write-Host "`nVerification (manual checks):" -ForegroundColor Cyan
Write-Host "  az --version => $(Get-ExeVersion -exe 'az' -versionArgs 'version')"
Write-Host "  terraform --version => $(Get-ExeVersion -exe 'terraform' -versionArgs 'version')"
Write-Host "  pwsh --version => $(Get-ExeVersion -exe 'pwsh' -versionArgs '--version')"
Write-Host "  Az module => $(Get-ModuleVersion -moduleName 'Az')"
Write-Host "  SqlServer module => $(Get-ModuleVersion -moduleName 'SqlServer')"

Write-Host "`nTool setup completed." -ForegroundColor Green
