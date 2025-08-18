# Generic Terraform plan script for multiple projects
# This script shows what Terraform will create/modify/destroy without making changes

param(
	[string]$ProjectPath,
	[string]$Environment,
	[switch]$SavePlan,
	[switch]$DetailedOutput
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
Write-Host "=== Terraform $ProjectName Deployment Plan ===" -ForegroundColor Cyan

# Ask for environment if not provided
if (-not $Environment) {
	Write-Host "`nWhich environment do you want to plan for?" -ForegroundColor Yellow
	Write-Host "1. dev (Development)" -ForegroundColor White
	Write-Host "2. prod (Production)" -ForegroundColor White

	do {
		$choice = Read-Host "`nEnter your choice (1-2)"
		switch ($choice) {
			"1" {
				$Environment = "dev"
				break
			}
			"2" {
				$Environment = "prod"
				break
			}
			default {
				Write-Host "Invalid choice. Please enter 1 or 2." -ForegroundColor Red
			}
		}
	} while ($choice -notin @("1", "2"))
}

Write-Host "`nPlanning deployment for environment: $Environment" -ForegroundColor Green

Write-Host "`n⚠️  Authentication Required" -ForegroundColor Yellow
Write-Host "The following environment variables are required for Terraform planning:" -ForegroundColor White

# Check required TF_VAR_* environment variables locally (no shared auth helpers)
$requiredVars = @("TF_VAR_ARM_TENANT_ID", "TF_VAR_ARM_SUBSCRIPTION_ID", "TF_VAR_ARM_CLIENT_ID", "TF_VAR_ARM_CLIENT_SECRET")
$missingVars = @()
foreach ($v in $requiredVars) {
	if (-not (Get-Item "env:$v" -ErrorAction SilentlyContinue)) {
		$missingVars += $v
	}
}

if ($missingVars.Count -gt 0) {
	foreach ($var in $missingVars) { Write-Host "  • $var" -ForegroundColor Gray }

	# Discover candidate setup scripts (development convenience)
	. .\setup-terraform-tools.ps1
	$setupScripts = Discover-SetupScripts -ProjectPath $ProjectPath
	$projectSetupPath = Join-Path $ProjectPath "setup.ps1"

	if ($setupScripts.Count -gt 0) {
		Write-Host ""
		if ($setupScripts.Count -eq 1 -and $setupScripts[0] -eq $projectSetupPath) {
			Write-Host "🔧 Found setup script for ${ProjectName}:" -ForegroundColor Cyan
			Write-Host "  $($setupScripts[0])" -ForegroundColor White
		}
		else {
			Write-Host "🔧 Available Setup Scripts:" -ForegroundColor Cyan
			for ($i = 0; $i -lt $setupScripts.Count; $i++) {
				Write-Host "  $($i + 1). $($setupScripts[$i])" -ForegroundColor White
			}
		}

		Write-Host "`nWould you like to run the setup script to configure authentication? (y/n): " -ForegroundColor Yellow -NoNewline
		$runSetup = Read-Host

		if ($runSetup -match '^[yY]$') {
			if ($setupScripts.Count -eq 1) {
				$selectedScript = $setupScripts[0]
			}
			else {
				do {
					$choice = Read-Host "`nSelect setup script (1-$($setupScripts.Count))"
					$choiceIndex = try { [int]$choice - 1 } catch { -1 }
					if ($choiceIndex -ge 0 -and $choiceIndex -lt $setupScripts.Count) {
						$selectedScript = $setupScripts[$choiceIndex]
						break
					}
					else {
						Write-Host "Invalid choice. Please enter a number between 1 and $($setupScripts.Count)." -ForegroundColor Red
					}
				} while ($true)
			}

			Write-Host "`nRunning setup script: $selectedScript" -ForegroundColor Green
			try {
				Run-SetupScriptInDir -ScriptPath $selectedScript
				Write-Host "`nSetup completed. Re-run this script to continue with planning." -ForegroundColor Green
				exit 0
			}
			catch {
				Write-Warning "Setup script failed: $_"
				Write-Host "Continuing with interactive authentication..." -ForegroundColor Yellow
			}
		}
	}

	Write-Host "`n📋 Manual Setup Options:" -ForegroundColor Cyan
	Write-Host "1. Set environment variables manually" -ForegroundColor White
	Write-Host "2. Continue with interactive authentication (Terraform will prompt)" -ForegroundColor White

	Write-Host "`n🔒 Security Note:" -ForegroundColor Magenta
	Write-Host "Service principal secrets should be obtained securely and never stored in scripts." -ForegroundColor White
	Write-Host "Interactive prompts allow secure credential entry." -ForegroundColor White

	Write-Host "`nContinuing with interactive authentication..." -ForegroundColor Yellow
}
else {
	Write-Host "✓ Authentication environment variables detected" -ForegroundColor Green
}
else {
	Write-Host "✓ Authentication environment variables detected" -ForegroundColor Green
}

# Change to project directory
Push-Location $ProjectPath

try {
	# Check if Terraform is installed
	if (-not (Get-Command terraform -ErrorAction SilentlyContinue)) {
		Write-Error "Terraform not found. Please install Terraform first."
		exit 1
	}

	# Check if we're in a valid Terraform directory
	if (-not (Test-Path "main.tf")) {
		Write-Error "main.tf not found in '$ProjectPath'. Not a valid Terraform project directory."
		exit 1
	}

	# Check if Terraform is properly initialized for planning (needs backend)
	$needsInit = $false
	$initMessage = ""

	if (-not (Test-Path ".terraform") -or -not (Test-Path ".terraform/providers")) {
		$needsInit = $true
		$initMessage = "Terraform not initialized."
	}
 elseif (-not (Test-Path ".terraform/terraform.tfstate")) {
		# Check if it was initialized in local mode (backend=false)
		$needsInit = $true
		$initMessage = "Terraform initialized in local mode only. Planning requires backend initialization."
	}

	if ($needsInit) {
		Write-Warning $initMessage
		Write-Host "`nPlanning requires full Terraform initialization with backend access." -ForegroundColor Yellow
		Write-Host "This will attempt to connect to the Azure storage account backend." -ForegroundColor Yellow
		Write-Host "`nWould you like to initialize now? (y/n): " -ForegroundColor Yellow -NoNewline
		$runInit = Read-Host

		if ($runInit -eq "y" -or $runInit -eq "Y") {
			# Determine which backend file to use
			$backendFile = "backend.$Environment.tfvars"

			if ($backendFile) {
				Write-Host "Initializing with backend config: $backendFile..." -ForegroundColor Yellow
				terraform init -reconfigure -backend-config="$backendFile"
			}
			else {
				Write-Host "Initializing without specific backend config..." -ForegroundColor Yellow
				terraform init -reconfigure
			}

			if ($LASTEXITCODE -ne 0) {
				Write-Error "Terraform initialization with backend failed."
				Write-Host "`nThis usually means you need storage account permissions." -ForegroundColor Yellow
				Write-Host "Contact your IT team for 'Storage Blob Data Contributor' access to:" -ForegroundColor White
				Write-Host "• staicompdevuks001 (for dev environment)" -ForegroundColor Gray
				Write-Host "• staicompproduks001 (for prod environment)" -ForegroundColor Gray
				Write-Host "`nAlternative: Use local validation to test configuration syntax:" -ForegroundColor Yellow
				Write-Host "  .\validate-scripts.ps1 -LocalValidationOnly" -ForegroundColor White
				exit 1
			}
			Write-Host "✓ Terraform initialized successfully with backend" -ForegroundColor Green
		}
		else {
			Write-Error "Cannot proceed with planning without backend initialization."
			Write-Host "`nAlternative options:" -ForegroundColor Yellow
			Write-Host "1. Run syntax validation only: .\validate-scripts.ps1 -LocalValidationOnly" -ForegroundColor White
			Write-Host "2. Get storage account permissions and try again" -ForegroundColor White
			exit 1
		}
	}
 else {
		Write-Host "✓ Terraform already initialized with backend" -ForegroundColor Green
	}

	# Determine which variable file to use
	$varFile = "variables.$Environment.tfvars"

	# Check if variable file exists
	if (-not (Test-Path $varFile)) {
		Write-Error "Variable file '$varFile' not found."
		exit 1
	}

	Write-Host "`nUsing variable file: $varFile" -ForegroundColor Green


	# Prepare plan command
	$planArgs = @("-var-file=$varFile")

	if ($DetailedOutput) {
		$planArgs += "-detailed-exitcode"
	}

	if ($SavePlan) {
		$planFile = "tfplan-$Environment-$(Get-Date -Format 'yyyyMMdd-HHmmss').out"
		$planArgs += "-out=$planFile"
		Write-Host "Plan will be saved to: $planFile" -ForegroundColor Yellow
	}

	Write-Host "`n=== Terraform Plan Output ===" -ForegroundColor Cyan
	Write-Host "Analyzing what changes will be made..." -ForegroundColor Yellow
	Write-Host "Legend: + create, ~ modify, - destroy" -ForegroundColor White
	Write-Host ""

	# Run terraform plan
	$planCommand = "terraform plan " + ($planArgs -join " ")
	Write-Host "Command: $planCommand" -ForegroundColor Gray
	Write-Host ""

	& terraform plan @planArgs
	$planExitCode = $LASTEXITCODE

	Write-Host "`n=== Plan Analysis Complete ===" -ForegroundColor Cyan

	# Interpret exit codes
	switch ($planExitCode) {
		0 {
			Write-Host "✓ No changes required - infrastructure matches configuration" -ForegroundColor Green
		}
		1 {
			Write-Host "✗ Plan failed - please check errors above" -ForegroundColor Red
			exit 1
		}
		2 {
			if ($DetailedOutput) {
				Write-Host "✓ Plan succeeded - changes are required" -ForegroundColor Yellow
			}
			else {
				Write-Host "✓ Plan completed successfully" -ForegroundColor Green
			}
		}
		default {
			Write-Host "? Unexpected exit code: $planExitCode" -ForegroundColor Magenta
		}
	}

	# Generic summary
	Write-Host "`nPlanned Resources for $ProjectName ($Environment):" -ForegroundColor Yellow
	Write-Host "• See plan output above for detailed resource list" -ForegroundColor White
	Write-Host "• Resources will be created/modified/destroyed as shown" -ForegroundColor White

	if ($SavePlan) {
		Write-Host "`nPlan saved to file: $planFile" -ForegroundColor Green
		Write-Host "To apply this exact plan later, use:" -ForegroundColor Yellow
		Write-Host "  terraform apply $planFile" -ForegroundColor White
	}

	Write-Host "`nNext steps:" -ForegroundColor Yellow
	if ($planExitCode -eq 0) {
		Write-Host "• No changes needed - infrastructure is up to date" -ForegroundColor White
	}
 else {
 	Write-Host "• Review the changes above carefully" -ForegroundColor White
 	Write-Host "• If changes look correct, run: terraform apply -var-file=$varFile" -ForegroundColor White

 	# Check for post-deployment scripts
 	if (Test-Path "..\Manage-AzureAppPermissions.ps1") {
 		Write-Host "• After deployment, configure permissions: ..\Manage-AzureAppPermissions.ps1" -ForegroundColor White
 	}
 	if (Test-Path ".\setup.ps1") {
 		Write-Host "• Post-deployment configuration: .\setup.ps1" -ForegroundColor White
 	}
 }

	Write-Host "`nSafety Notes:" -ForegroundColor Magenta
	Write-Host "• This plan does NOT make any changes" -ForegroundColor White
	Write-Host "• Only 'terraform apply' will actually create/modify resources" -ForegroundColor White
	Write-Host "• Always review plans carefully before applying" -ForegroundColor White
	Write-Host "• Backend access is required for planning and deployment" -ForegroundColor White

}
finally {
	# Return to original directory
	Pop-Location
}