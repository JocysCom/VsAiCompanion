# Generic Terraform validation script for multiple projects
# This script validates Terraform configuration and formatting for any project directory

param(
	[string]$ProjectPath,
	[string]$Environment,
	[switch]$LocalValidationOnly
)

# Auto-discover Terraform projects if not specified
if (-not $ProjectPath) {
	$terraformProjects = Get-ChildItem -Directory -Name "Terraform-*" | Sort-Object

	if ($terraformProjects.Count -eq 0) {
		Write-Error "No Terraform projects found (looking for Terraform-* directories)."
		exit 1
	}

	if ($terraformProjects.Count -eq 1) {
		$ProjectPath = $terraformProjects[0]
		Write-Host "Auto-selected project: $ProjectPath" -ForegroundColor Green
	}
 else {
		Write-Host "`nAvailable Terraform projects:" -ForegroundColor Yellow
		for ($i = 0; $i -lt $terraformProjects.Count; $i++) {
			Write-Host "$($i + 1). $($terraformProjects[$i])" -ForegroundColor White
		}

		do {
			$choice = Read-Host "`nSelect project (1-$($terraformProjects.Count))"
			$choiceIndex = try { [int]$choice - 1 } catch { -1 }
			if ($choiceIndex -ge 0 -and $choiceIndex -lt $terraformProjects.Count) {
				$ProjectPath = $terraformProjects[$choiceIndex]
				break
			}
			else {
				Write-Host "Invalid choice. Please enter a number between 1 and $($terraformProjects.Count)." -ForegroundColor Red
			}
		} while ($true)
	}
}

# Validate project path
if (-not (Test-Path $ProjectPath)) {
	Write-Error "Project directory '$ProjectPath' not found."
	exit 1
}

$ProjectName = Split-Path $ProjectPath -Leaf
Write-Host "=== Terraform $ProjectName Configuration Validation ===" -ForegroundColor Cyan

# Ask for environment if not provided
if (-not $Environment) {
	Write-Host "`nWhich environment do you want to validate?" -ForegroundColor Yellow
	Write-Host "1. dev (Development)" -ForegroundColor White
	Write-Host "2. prod (Production)" -ForegroundColor White
	Write-Host "3. local (Local validation only - no backend access)" -ForegroundColor White

	do {
		$choice = Read-Host "`nEnter your choice (1-3)"
		switch ($choice) {
			"1" {
				$Environment = "dev"
				break
			}
			"2" {
				$Environment = "prod"
				break
			}
			"3" {
				$Environment = "dev"  # Default for local validation
				$LocalValidationOnly = $true
				break
			}
			default {
				Write-Host "Invalid choice. Please enter 1, 2, or 3." -ForegroundColor Red
			}
		}
	} while ($choice -notin @("1", "2", "3"))
}

Write-Host "`nValidating for environment: $Environment" -ForegroundColor Green

if ($LocalValidationOnly) {
	Write-Host "Running in LOCAL VALIDATION MODE (no backend access required)" -ForegroundColor Magenta
}

# Change to project directory
Push-Location $ProjectPath

try {
	# Check if Terraform is installed
	if (-not (Get-Command terraform -ErrorAction SilentlyContinue)) {
		Write-Error "Terraform not found. Please install Terraform first."
		exit 1
	}

	Write-Host "✓ Terraform found: $(terraform version -json | ConvertFrom-Json | Select-Object -ExpandProperty terraform_version)" -ForegroundColor Green

	# Check if we're in a valid Terraform directory
	if (-not (Test-Path "main.tf")) {
		Write-Error "main.tf not found in '$ProjectPath'. Not a valid Terraform project directory."
		exit 1
	}

	Write-Host "✓ Found main.tf file" -ForegroundColor Green

	# Format check
	Write-Host "`nChecking Terraform formatting..." -ForegroundColor Yellow
	terraform fmt -check -diff
	if ($LASTEXITCODE -ne 0) {
		Write-Warning "Terraform formatting issues found. Running 'terraform fmt' to fix..."
		terraform fmt
		Write-Host "✓ Formatting fixed" -ForegroundColor Green
	}
	else {
		Write-Host "✓ Terraform formatting is correct" -ForegroundColor Green
	}

	# Check if Terraform is initialized
	Write-Host "`nChecking Terraform initialization..." -ForegroundColor Yellow

	if ($LocalValidationOnly) {
		Write-Host "Local validation mode - downloading providers only..." -ForegroundColor Yellow

		# For local validation, just init without backend
		if (-not (Test-Path ".terraform") -or -not (Test-Path ".terraform/providers")) {
			terraform init -backend=false
			if ($LASTEXITCODE -ne 0) {
				Write-Error "Failed to download providers. Please check the errors above."
				exit 1
			}
			Write-Host "✓ Providers downloaded successfully" -ForegroundColor Green
		}
		else {
			Write-Host "✓ Providers already available" -ForegroundColor Green
		}
	}
 else {
		# Full initialization with backend
		if (-not (Test-Path ".terraform") -or -not (Test-Path ".terraform/providers")) {
			# Determine which backend file to use
			$backendFile = "backend.$Environment.tfvars"

			if ($backendFile) {
				Write-Host "Running 'terraform init' with backend config: $backendFile..." -ForegroundColor Yellow
				terraform init -backend-config="$backendFile"
			}
			else {
				Write-Host "Running 'terraform init' (no backend config found)..." -ForegroundColor Yellow
				terraform init
			}

			if ($LASTEXITCODE -ne 0) {
				Write-Warning "Terraform initialization with backend failed."
				Write-Host "This is usually due to storage account permissions." -ForegroundColor Yellow
				Write-Host "Falling back to local validation mode..." -ForegroundColor Yellow
				terraform init -backend=false
				if ($LASTEXITCODE -ne 0) {
					Write-Error "Failed to download providers. Please check the errors above."
					exit 1
				}
				Write-Host "✓ Providers downloaded for local validation" -ForegroundColor Green
			}
			else {
				Write-Host "✓ Terraform initialized successfully with backend" -ForegroundColor Green
			}
		}
		else {
			Write-Host "✓ Terraform already initialized" -ForegroundColor Green
		}
	}

	# Validation check
	Write-Host "`nValidating Terraform configuration..." -ForegroundColor Yellow
	terraform validate
	if ($LASTEXITCODE -ne 0) {
		Write-Error "Terraform validation failed. Please fix the errors above."
		exit 1
	}

	Write-Host "✓ Terraform configuration is valid" -ForegroundColor Green

	# Check for common Terraform files
	$commonFiles = @("main.tf", "variables.tf")
	$foundFiles = @()
	$missingFiles = @()

	foreach ($file in $commonFiles) {
		if (Test-Path $file) {
			$foundFiles += $file
		}
		else {
			$missingFiles += $file
		}
	}

	if ($foundFiles.Count -gt 0) {
		Write-Host "✓ Common Terraform files found: $($foundFiles -join ', ')" -ForegroundColor Green
	}

	if ($missingFiles.Count -gt 0) {
		Write-Warning "Optional files not found: $($missingFiles -join ', ')"
	}

	# Count total .tf files
	$tfFiles = Get-ChildItem -Filter "*.tf" | Measure-Object
	Write-Host "✓ Total Terraform files: $($tfFiles.Count)" -ForegroundColor Green

	# Check for backend configuration files
	$backendFiles = @("backend.dev.tfvars", "backend.prod.tfvars")

	# Also check for legacy naming
	$legacyBackendFiles = @("backend.sandbox.tfvars", "backend.production.tfvars")

	$foundBackends = @()
	foreach ($file in ($backendFiles + $legacyBackendFiles)) {
		if (Test-Path $file) {
			$foundBackends += $file
		}
	}

	if ($foundBackends.Count -gt 0) {
		Write-Host "✓ Backend configuration files found: $($foundBackends -join ', ')" -ForegroundColor Green

		# Warn about legacy naming
		$legacyFound = $foundBackends | Where-Object { $_ -in $legacyBackendFiles }
		if ($legacyFound.Count -gt 0) {
			Write-Warning "Legacy backend files found: $($legacyFound -join ', '). Consider renaming to dev/prod convention."
		}
	}
	else {
		Write-Warning "No backend configuration files found. Consider creating backend.{env}.tfvars files."
	}

	# Check for variable files
	$varFiles = @("variables.dev.tfvars", "variables.prod.tfvars")

	$foundVars = @()
	foreach ($file in ($varFiles)) {
		if (Test-Path $file) {
			$foundVars += $file
		}
	}

	if ($foundVars.Count -gt 0) {
		Write-Host "✓ Variable files found: $($foundVars -join ', ')" -ForegroundColor Green
	}

	Write-Host "`n=== Validation Complete ===" -ForegroundColor Cyan
	Write-Host "Your $ProjectName Terraform configuration is ready!" -ForegroundColor Green

	# Display next steps with appropriate file names
	Write-Host "`nNext steps:" -ForegroundColor Yellow

	if ($LocalValidationOnly) {
		Write-Host "For actual deployment, you'll need:" -ForegroundColor White
		Write-Host "1. Storage account permissions for backend access" -ForegroundColor White
		Write-Host "2. Azure authentication (az login or service principal)" -ForegroundColor White
		Write-Host "3. Run validation again selecting dev/prod environment" -ForegroundColor White
		Write-Host "4. Use validate-plan script: .\validate-plan.ps1" -ForegroundColor White
	}
 else {
		# Determine which backend and variable files to use
		$backendFile = "backend.$Environment.tfvars"

		$varFile = "variables.$Environment.tfvars"

		Write-Host "1. Show deployment plan: .\validate-plan.ps1" -ForegroundColor White
		Write-Host "2. Plan deployment: terraform plan -var-file=`"$varFile`"" -ForegroundColor White
		Write-Host "3. Apply configuration: terraform apply -var-file=`"$varFile`"" -ForegroundColor White
		Write-Host "`nNote: If you see permission errors, you may need to run with local validation mode first." -ForegroundColor Yellow
	}

}
finally {
	# Return to original directory
	Pop-Location
}