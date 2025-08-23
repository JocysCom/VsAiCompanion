# Shared functions for Terraform projects
# Contains reusable functions that can be used across multiple terraform setups

function GetConfig {
	param ([Parameter(Mandatory = $true)][string]$configFile)
	$config = @{}
	if (-not (Test-Path -Path $configFile)) {
		Write-Error "Configuration file '$configFile' does not exist."
		return $null
	}
	$content = Get-Content -Path $configFile -Raw
	$lines = $content -split "`n" | Where-Object { $_.Trim() -ne "" -and -not $_.Trim().StartsWith("#") }
	foreach ($line in $lines) {
		# Updated regex to handle comments after quoted values
		if ($line -match '^(\w+)\s*=\s*"?([^"#]*?)"?\s*(?:#.*)?$') {
			$key = $matches[1].Trim()
			$value = $matches[2].Trim()
			$config[$key] = $value
		}
	}
	return $config
}


function Ensure-AzModule {
	if (-not (Get-Module -ListAvailable -Name "Az")) {
		Write-Host "Az PowerShell module not found. Ensure tools are installed properly." -ForegroundColor Red
		return $false
	}
	if (-not (Get-Module -Name "Az.Accounts")) {
		Import-Module Az.Accounts -Force
	}
	return $true
}

function GetAppId {
	param ([Parameter(Mandatory = $true)][string]$apiName)
	Write-Host "API '$apiName':"
	$apiSp = az ad sp list --display-name $apiName --query '[0]'  | Out-String | ConvertFrom-Json
	$apiAppId = $apiSp.appId
	Write-Host "  App Id: $apiAppId"
	return $apiAppId
}

function GetDelegatedPermissionId {
	param (
		[Parameter(Mandatory = $true)][string]$apiName,
		[Parameter(Mandatory = $true)][string]$apiAppId,
		[Parameter(Mandatory = $true)][string]$permissionName
	)
	Write-Host "Permission '$permissionName' of '$apiName':"
	$delegatedPermissions = az ad sp show --id $apiAppId --query "oauth2PermissionScopes"  | Out-String | ConvertFrom-Json
	$permission = $delegatedPermissions | Where-Object { $_.value -eq $permissionName }
	$permissionId = $permission.id
	Write-Host "  Id: $permissionId"
	return $permissionId
}

function AssignAndGrantPermission {
	param (
		[Parameter(Mandatory = $true)][string]$appId,
		[Parameter(Mandatory = $true)][string]$apiAppId,
		[Parameter(Mandatory = $true)][string]$apiPermissionId,
		[Parameter(Mandatory = $true)][string]$apiPermissionName
	)
	$existingPermissions = az ad app permission list --id $appId | Out-String | ConvertFrom-Json
	$permissionExists = $existingPermissions | Where-Object { $_.resourceAppId -eq $apiAppId -and $_.resourceAccess.id -eq $apiPermissionId }
	if ($permissionExists) {
		Write-Host "Permission '$apiPermissionName' already exists. No need to add."
		return
	}
	Write-Host "Add '$apiPermissionName' permission to the service principal"
	$null = az ad app permission add --id $appId --api $apiAppId --api-permissions "$apiPermissionId=Scope"
	Start-Sleep -Seconds 4
	Write-Host "  Grant permissions admin consent for the service principal"
	$null = az ad app permission grant --id $appId --api $apiAppId --scope $apiPermissionName
	Start-Sleep -Seconds 4
	Write-Host "  Grant permission admin consent for entire application"
	$null = az ad app permission admin-consent --id $appId
}

function CreateRoleAssignment {
	param (
		[string] $scope,
		[string] $principalName,
		[string] $roleDefinitionName
	)
	$roleAssignmentId = az role assignment list --scope $scope --query "[?principalName=='$principalName' && roleDefinitionName=='$roleDefinitionName'] | [0].id" | ConvertFrom-Json
	if ($null -eq $roleAssignmentId) {
		Write-Host "Assign '$roleDefinitionName' role for the resource group to the service principal"
		az role assignment create --assignee $principalName --role $roleDefinitionName --scope $scope
	}
	else {
		Write-Host "Role '$roleDefinitionName' already assigned to the service principal"
	}
}

function SignInAsPrincipalUsingAzCli {
	param (
		[Parameter(Mandatory = $true)][string]$spAppId,
		[Parameter(Mandatory = $true)][string]$armClientSecret,
		[Parameter(Mandatory = $true)][string]$spTenantId,
		[Parameter(Mandatory = $true)][string]$subscriptionId
	)
	az login --service-principal -u $spAppId -p $armClientSecret --tenant $spTenantId
	Write-Host "Select Azure CLI Subscription: $subscriptionId"
	az account set --subscription $subscriptionId
}

function SignInAsPrincipalUsingAzPowerShell {
	param (
		[Parameter(Mandatory = $true)][string]$spAppId,
		[Parameter(Mandatory = $true)][string]$armClientSecret,
		[Parameter(Mandatory = $true)][string]$spTenantId,
		[Parameter(Mandatory = $true)][string]$subscriptionId
	)
	$securePassword = ConvertTo-SecureString $armClientSecret -AsPlainText -Force
	$creds = New-Object System.Management.Automation.PSCredential ($spAppId, $securePassword)
	Connect-AzAccount -ServicePrincipal -Tenant $spTenantId -Credential $creds
	Write-Host "Select Az Subscription: $subscriptionId"
	Select-AzSubscription -Subscription $subscriptionId
}

function SignOut {
	az account clear
	Disconnect-AzAccount
}

function DeleteResources {
	param (
		[Parameter(Mandatory = $true)][string]$resourceGroupName,
		[Parameter(Mandatory = $true)][string]$sqlDatabaseName,
		[Parameter(Mandatory = $true)][string]$sqlServerName,
		[Parameter(Mandatory = $true)][string]$sqlIdentity,
		[Parameter(Mandatory = $true)][string]$keyVaultName,
		[Parameter(Mandatory = $true)][string]$logAnalyticsWorkspaceName,
		[Parameter(Mandatory = $true)][string]$containerName,
		[Parameter(Mandatory = $true)][string]$storageAccountName
	)
	$lockName = "ResourceLock"
	$lockLevel = "CanNotDelete"
	try {
		Write-Host "Removing lock: $lockName from resource group: $resourceGroupName"
		Remove-AzResourceLock -LockName $lockName -ResourceGroupName $resourceGroupName -Force
	}
	catch {
		Write-Host "Lock does not exist or could not be removed. Continuing..."
	}

	$userInput = Read-Host -Prompt "Do you want to delete '$sqlDatabaseName' SQL database? [y/N]"
	if ($userInput.Trim().ToUpper() -eq "Y") {
		Start-Sleep -Seconds 4
		Write-Host "Deleting SQL database: $sqlDatabaseName"
		az sql db delete --resource-group $resourceGroupName --server $sqlServerName --name $sqlDatabaseName --yes
	}

	$userInput = Read-Host -Prompt "Do you want to delete '$sqlServerName' SQL server? [y/N]"
	if ($userInput.Trim().ToUpper() -eq "Y") {
		Start-Sleep -Seconds 2
		Write-Host "Deleting SQL server: $sqlServerName"
		az sql server delete --resource-group $resourceGroupName --name $sqlServerName --yes
		Start-Sleep -Seconds 2
		Write-Host "Deleting Service Principal of SQL Identity: $sqlIdentity"
		$sqlSpId = az ad sp list --display-name $sqlIdentity --query "[0].id" -o tsv
		if ($null -ne $sqlSpId) {
			az ad sp delete --id $sqlSpId
		}
		Write-Host "Deleting SQL Identity : $sqlIdentity"
		az identity delete --resource-group $resourceGroupName --name $sqlIdentity
	}

	$userInput = Read-Host -Prompt "Do you want to delete '$keyVaultName' key vault? [y/N]"
	if ($userInput.Trim().ToUpper() -eq "Y") {
		Start-Sleep -Seconds 2
		Write-Host "Deleting Log Analytics workspace: $logAnalyticsWorkspaceName"
		az monitor log-analytics workspace delete --resource-group $resourceGroupName --workspace-name $logAnalyticsWorkspaceName --yes
	}

	$userInput = Read-Host -Prompt "Do you want to delete '$containerName' terraform container? [y/N]"
	if ($userInput.Trim().ToUpper() -eq "Y") {
		az storage container delete --account-name $storageAccountName --name $containerName
	}

	Start-Sleep -Seconds 2
	New-AzResourceLock -LockName $lockName -ResourceGroupName $resourceGroupName -LockLevel $lockLevel -Force
}

function IsResourceManagedByTerraform {
	param (
		[string] $resourceType,
		[string] $resourceName
	)
	$stateList = terraform state list
	foreach ($resource in $stateList) {
		if ($resource -eq "$resourceType.$resourceName") {
			Write-Host "Resource '$resourceType.$resourceName' already managed by terraform."
			return $true
		}
	}
	return $false
}

function ImportResource {
	param (
		[string] $resourceType,
		[string] $resourceName,
		[string] $resourceId,
		[string] $terraformResourceId = "",
		[string] $env
	)
	if (IsResourceManagedByTerraform $resourceType $resourceName) {
		return
	}
	$resource = & az resource show --ids $resourceId --query "id" --output tsv 2> $null
	if (-not $resource) {
		"'$resourceId' not found on Azure."
		return
	}

	$userInput = Read-Host -Prompt "Do you want to import '$resourceType.$resourceName'? [y/N]"
	if ($userInput.Trim().ToUpper() -eq "Y") {
		if (-not $terraformResourceId) {
			$terraformResourceId = $resourceId
		}
		terraform import  -var-file="variables.${env}.tfvars" "$resourceType.$resourceName" $terraformResourceId
	}
}

function ImportRoleResource {
	param (
		[string] $resourceType,
		[string] $resourceName,
		[string] $scope,
		[string] $principalName,
		[string] $roleDefinitionName,
		[string] $env
	)
	if (IsResourceManagedByTerraform $resourceType $resourceName) {
		return
	}
	$resourceId = & az role assignment list --scope $scope --query "[?principalName=='$principalName' && roleDefinitionName=='$roleDefinitionName'] | [0].id" | ConvertFrom-Json
	if ($null -eq $resourceId) {
		"'$resourceType.$resourceName' not found on Azure."
		return
	}
	ImportResource $resourceType $resourceName $resourceId "" $env
}

function ImportIdentity {
	param (
		[string] $resourceType,
		[string] $resourceName,
		[string] $identityName,
		[string] $resourceGroupName,
		[string] $env
	)
	if (IsResourceManagedByTerraform $resourceType $resourceName) {
		return
	}
	$resourceId = az identity show --resource-group $resourceGroupName --name $identityName --query "id" -o tsv
	if ($null -eq $resourceId) {
		"'$resourceType.$resourceName' not found on Azure."
		return
	}
	ImportResource $resourceType $resourceName $resourceId "" $env
}

function ImportServicePrincipal {
	param (
		[string] $resourceType,
		[string] $resourceName,
		[string] $displayname,
		[string] $env
	)
	if (IsResourceManagedByTerraform $resourceType $resourceName) {
		return
	}
	$resourceId = az ad sp list --display-name $displayname --query "[0].id" -o tsv
	if ($null -eq $resourceId) {
		"'$resourceType.$resourceName' not found on Azure."
		return
	}
	ImportResource $resourceType $resourceName $resourceId "" $env
}

function ImportSecretResource {
	param (
		[string] $resourceType,
		[string] $resourceName,
		[string] $vaultName,
		[string] $secretName,
		[string] $rgPath,
		[string] $env
	)
	if (IsResourceManagedByTerraform $resourceType $resourceName) {
		return
	}
	$resourceId = az keyvault secret show --vault-name $vaultName --name $secretName --query 'id' -o tsv
	if ($null -eq $resourceId) {
		"'$resourceType.$resourceName' not found on Azure."
		return
	}
	"'$resourceType.$resourceName' found on Azure."
	$allVersionsResourceId = "$rgPath/providers/Microsoft.KeyVault/vaults/$vaultName/secrets/$secretName"
	ImportResource $resourceType $resourceName $allVersionsResourceId $resourceId $env
}

function RegisterProvider {
	param ([Parameter(Mandatory = $true)][string]$providerName, [Parameter(Mandatory = $true)][string]$subscriptionId)
	Write-Host "Checking if '$providerName' resource provider is registered..."
	$resourceProviderState = az provider show --namespace $providerName --subscription $subscriptionId --query "registrationState" --output tsv
	if ($resourceProviderState -ne "Registered") {
		Write-Host "Registering '$providerName' resource provider..."
		az provider register --namespace $providerName --subscription $subscriptionId
		Write-Host "Please wait 20 seconds..."
		Start-Sleep -Seconds 20
	}
}

# Additional generic functions for common patterns

function Get-UserConfirmation {
	param (
		[Parameter(Mandatory = $true)][string]$Message,
		[string]$DefaultResponse = "N"
	)
	$response = Read-Host -Prompt "$Message [y/N]"
	return $response.Trim().ToUpper() -eq "Y"
}

function Test-SubscriptionExists {
	param (
		[Parameter(Mandatory = $true)][string]$SubscriptionId
	)
	Write-Host "Verifying subscription exists..."
	$subscriptionExists = az account list --query "[?id=='$SubscriptionId'] | length(@)"
	if ($subscriptionExists -eq 0) {
		Write-Host "Error: Subscription '$SubscriptionId' was not found." -ForegroundColor Red
		return $false
	}
	Write-Host "Subscription '$SubscriptionId' exists."
	return $true
}

function Set-ActiveSubscription {
	param (
		[Parameter(Mandatory = $true)][string]$SubscriptionId
	)
	Write-Host "Setting active subscription '$SubscriptionId'..."
	az account set --subscription $SubscriptionId

	# Get the subscription details
	$subscription = az account list --query "[?id=='$SubscriptionId']" --output json | ConvertFrom-Json
	$subscriptionName = $subscription[0].name
	Write-Host "Active subscription set to: $subscriptionName"
	return $subscriptionName
}

function New-AzureADGroupIfNotExists {
	param (
		[Parameter(Mandatory = $true)][string]$GroupName,
		[Parameter(Mandatory = $true)][string]$MailNickname,
		[bool]$PromptUser = $true
	)
	$group = az ad group list --display-name $GroupName --query "[0]" | Out-String | ConvertFrom-Json

	if ($group -eq $null) {
		$shouldCreate = $true
		if ($PromptUser) {
			$shouldCreate = Get-UserConfirmation "Do you want to create group '$GroupName'?"
		}

		if ($shouldCreate) {
			Write-Host "Creating group '$GroupName'."
			az ad group create --display-name $GroupName --mail-nickname $MailNickname
			$group = az ad group list --display-name $GroupName --query "[0]" | Out-String | ConvertFrom-Json
		}
	}

	if ($group) {
		Write-Host "Group '$GroupName':"
		Write-Host "  Id: $($group.id)"
		return $group
	}
	return $null
}

function New-ServicePrincipalIfNotExists {
	param (
		[Parameter(Mandatory = $true)][string]$ServicePrincipalName,
		[bool]$PromptUser = $true
	)
	$sp = az ad sp list --display-name $ServicePrincipalName --query '[0]' | Out-String | ConvertFrom-Json

	if ($sp -eq $null) {
		$shouldCreate = $true
		if ($PromptUser) {
			$shouldCreate = Get-UserConfirmation "Do you want to create service principal '$ServicePrincipalName'?"
		}

		if ($shouldCreate) {
			az ad sp create-for-rbac --name $ServicePrincipalName
			Write-Host "IMPORTANT: Safely store information above. Press any key to continue."
			pause
			$sp = az ad sp list --display-name $ServicePrincipalName --query '[0]' | Out-String | ConvertFrom-Json
		}
	}

	if ($sp) {
		Write-Host "Service principal '$ServicePrincipalName':"
		Write-Host "  Id: $($sp.id)"
		Write-Host "  App Id: $($sp.appId)"
		return $sp
	}
	return $null
}

function Add-ServicePrincipalToGroup {
	param (
		[Parameter(Mandatory = $true)][string]$GroupId,
		[Parameter(Mandatory = $true)][string]$ServicePrincipalId,
		[Parameter(Mandatory = $true)][string]$GroupName,
		[Parameter(Mandatory = $true)][string]$ServicePrincipalName
	)
	$memberCheck = az ad group member check --group $GroupId --member-id $ServicePrincipalId --query "value"
	if ($memberCheck -eq $false) {
		Write-Host "Adding service principal '$ServicePrincipalName' to group '$GroupName'."
		az ad group member add --group $GroupId --member-id $ServicePrincipalId
	}
 else {
		Write-Host "Service principal '$ServicePrincipalName' is already a member of the group '$GroupName'."
	}
}

function New-ResourceGroupIfNotExists {
	param (
		[Parameter(Mandatory = $true)][string]$ResourceGroupName,
		[Parameter(Mandatory = $true)][string]$Location,
		[Parameter(Mandatory = $true)][string]$SubscriptionId,
		[bool]$PromptUser = $true
	)
	$resourceGroupExists = az group exists --name $ResourceGroupName --subscription $SubscriptionId

	if ($resourceGroupExists -ne $true) {
		$shouldCreate = $true
		if ($PromptUser) {
			$shouldCreate = Get-UserConfirmation "Do you want to create resource group '$ResourceGroupName'?"
		}

		if ($shouldCreate) {
			Write-Host "Create resource group '$ResourceGroupName'"
			az group create --name $ResourceGroupName --location $Location --subscription $SubscriptionId
		}
	}

	$resourceGroup = az group show --name $ResourceGroupName --subscription $SubscriptionId | Out-String | ConvertFrom-Json
	return $resourceGroup
}

function New-StorageAccountIfNotExists {
	param (
		[Parameter(Mandatory = $true)][string]$StorageAccountName,
		[Parameter(Mandatory = $true)][string]$ResourceGroupName,
		[Parameter(Mandatory = $true)][string]$Location,
		[Parameter(Mandatory = $true)][string]$ContainerName,
		[Parameter(Mandatory = $true)][string]$SubscriptionId,
		[bool]$PromptUser = $true
	)
	$storageAccountExists = az storage account list --resource-group $ResourceGroupName --query "[?name=='$StorageAccountName'] | length(@)" --subscription $SubscriptionId

	if ($storageAccountExists -eq 0) {
		$shouldCreate = $true
		if ($PromptUser) {
			$shouldCreate = Get-UserConfirmation "Do you want to create storage account '$StorageAccountName'?"
		}

		if ($shouldCreate) {
			Write-Host "Creating storage account '$StorageAccountName'..."
			az storage account create --name $StorageAccountName --resource-group $ResourceGroupName --location $Location --sku Standard_LRS --subscription $SubscriptionId

			Write-Host "Creating storage container '$ContainerName'..."
			az storage container create --name $ContainerName --account-name $StorageAccountName

			Start-Sleep -Seconds 20
			Write-Host "Storage account and container created successfully."
		}
	}
 else {
		Write-Host "Storage account '$StorageAccountName' already exists."
	}
}

function Set-TerraformEnvironmentVariables {
	param (
		[Parameter(Mandatory = $true)][string]$TenantId,
		[Parameter(Mandatory = $true)][string]$SubscriptionId,
		[Parameter(Mandatory = $true)][string]$ClientId,
		[Parameter(Mandatory = $true)][string]$ClientSecret,
		[string]$AccessKey = $null,
		[string]$DatabaseAccessToken = $null
	)

	# Set Terraform variables with TF_VAR_ prefix
	$env:TF_VAR_ARM_TENANT_ID = $TenantId
	$env:TF_VAR_ARM_SUBSCRIPTION_ID = $SubscriptionId
	$env:TF_VAR_ARM_CLIENT_ID = $ClientId
	$env:TF_VAR_ARM_CLIENT_SECRET = $ClientSecret

	# Set ARM provider variables
	$env:ARM_TENANT_ID = $TenantId
	$env:ARM_SUBSCRIPTION_ID = $SubscriptionId
	$env:ARM_CLIENT_ID = $ClientId
	$env:ARM_CLIENT_SECRET = $ClientSecret

	if ($AccessKey) {
		$env:ARM_ACCESS_KEY = $AccessKey
		Write-Host "ARM_ACCESS_KEY = $env:ARM_ACCESS_KEY"
	}

	if ($DatabaseAccessToken) {
		$env:ARM_DATABASE_ACCESS_TOKEN = $DatabaseAccessToken
		Write-Host "ARM_DATABASE_ACCESS_TOKEN = ********"
	}

	Write-Output "TF_VAR_ARM_TENANT_ID: $($env:TF_VAR_ARM_TENANT_ID)"
	Write-Output "TF_VAR_ARM_SUBSCRIPTION_ID: $($env:TF_VAR_ARM_SUBSCRIPTION_ID)"
	Write-Output "TF_VAR_ARM_CLIENT_ID: $($env:TF_VAR_ARM_CLIENT_ID)"
	Write-Host "Environment variables set successfully."
}

function Grant-ServicePrincipalPermissions {
	param (
		[Parameter(Mandatory = $true)][string]$ServicePrincipalAppId,
		[bool]$PromptUser = $true
	)

	$shouldGrant = $true
	if ($PromptUser) {
		$shouldGrant = Get-UserConfirmation "Do you want to grant permissions to the service principal?"
	}

	if ($shouldGrant) {
		# Microsoft Graph permissions
		$mgApiName = "Microsoft Graph"
		$mgApiAppId = GetAppId $mgApiName
		$mgPermissionName = "Directory.Read.All"
		$mgPermissionId = GetDelegatedPermissionId $mgApiName $mgApiAppId $mgPermissionName
		AssignAndGrantPermission $ServicePrincipalAppId $mgApiAppId $mgPermissionId $mgPermissionName

		# Azure Active Directory Graph permissions
		$adApiName = "Azure Active Directory Graph"
		$adApiAppId = "00000002-0000-0000-c000-000000000000"
		$adPermissionName = "Directory.Read.All"
		$adPermissionId = GetDelegatedPermissionId $adApiName $adApiAppId $adPermissionName
		AssignAndGrantPermission $ServicePrincipalAppId $adApiAppId $adPermissionId $adPermissionName

		Write-Host "Permissions granted successfully."
	}
}

function Register-RequiredProviders {
	param (
		[Parameter(Mandatory = $true)][string]$SubscriptionId,
		[Parameter(Mandatory = $true)][string]$SubscriptionName,
		[bool]$PromptUser = $true
	)

	$shouldRegister = $true
	if ($PromptUser) {
		$shouldRegister = Get-UserConfirmation "Register providers for '$SubscriptionName' subscription?"
	}

	if ($shouldRegister) {
		$providers = @(
			"Microsoft.Storage",
			"Microsoft.KeyVault",
			"Microsoft.Sql",
			"Microsoft.Resources",
			"Microsoft.Insights",
			"Microsoft.OperationalInsights"
		)

		foreach ($provider in $providers) {
			RegisterProvider $provider $SubscriptionId
		}
		Write-Host "All required providers registered successfully."
	}
}

# ========================================
# MENU AND PROJECT SELECTION FUNCTIONS
# ========================================

function Get-TerraformProjectSelection {
	param (
		[string]$ProjectPath,
		[string]$PatternPrefix = "Terraform-"
	)

	# Return provided path if specified
	if ($ProjectPath) {
		if (Test-Path $ProjectPath) {
			return $ProjectPath
		}
		else {
			Write-Error "Project directory '$ProjectPath' not found."
			return $null
		}
	}

	# Auto-discover Terraform projects
	$terraformProjects = Get-ChildItem -Directory -Name "$PatternPrefix*" | Sort-Object

	if ($terraformProjects.Count -eq 0) {
		Write-Error "No Terraform projects found (looking for $PatternPrefix* directories)."
		return $null
	}

	if ($terraformProjects.Count -eq 1) {
		$selectedPath = $terraformProjects[0]
		Write-Host "Auto-selected project: $selectedPath" -ForegroundColor Green
		return $selectedPath
	}
	else {
		Write-Host "`nAvailable Terraform projects:" -ForegroundColor Yellow
		$selectedIndex = Show-NumberedMenu -Items $terraformProjects -PromptText "Select project"
		if ($selectedIndex -ge 0) {
			return $terraformProjects[$selectedIndex]
		}
		return $null
	}
}

function Get-EnvironmentSelection {
	param (
		[string]$Environment,
		[switch]$IncludeLocalValidation
	)

	# Return provided environment if specified
	if ($Environment) {
		return @{ Environment = $Environment; LocalValidationOnly = $false }
	}

	# Build menu options
	$options = @(
		@{ Key = "1"; Value = "dev"; Description = "dev (Development)" },
		@{ Key = "2"; Value = "prod"; Description = "prod (Production)" }
	)

	if ($IncludeLocalValidation) {
		$options += @{ Key = "3"; Value = "local"; Description = "local (Local validation only - no backend access)" }
	}

	# Show menu
	Write-Host "`nWhich environment do you want to use?" -ForegroundColor Yellow
	foreach ($option in $options) {
		Write-Host "$($option.Key). $($option.Description)" -ForegroundColor White
	}

	$validChoices = $options | ForEach-Object { $_.Key }
	$maxChoice = $options.Count

	do {
		$choice = Read-Host "`nEnter your choice (1-$maxChoice)"
		$selectedOption = $options | Where-Object { $_.Key -eq $choice }
		if ($selectedOption) {
			if ($selectedOption.Value -eq "local") {
				return @{ Environment = "dev"; LocalValidationOnly = $true }
			}
			else {
				return @{ Environment = $selectedOption.Value; LocalValidationOnly = $false }
			}
		}
		else {
			Write-Host "Invalid choice. Please enter a number between 1 and $maxChoice." -ForegroundColor Red
		}
	} while ($choice -notin $validChoices)
}

function Show-NumberedMenu {
	param (
		[Parameter(Mandatory = $true)]
		[array]$Items,
		[string]$PromptText = "Select an option",
		[string]$ItemPrefix = ""
	)

	# Display menu items
	for ($i = 0; $i -lt $Items.Count; $i++) {
		Write-Host "$($i + 1). $ItemPrefix$($Items[$i])" -ForegroundColor White
	}

	# Get user selection
	do {
		$choice = Read-Host "`n$PromptText (1-$($Items.Count))"
		$choiceIndex = try { [int]$choice - 1 } catch { -1 }
		if ($choiceIndex -ge 0 -and $choiceIndex -lt $Items.Count) {
			return $choiceIndex
		}
		else {
			Write-Host "Invalid choice. Please enter a number between 1 and $($Items.Count)." -ForegroundColor Red
		}
	} while ($true)
}

function Test-TerraformProject {
	param (
		[Parameter(Mandatory = $true)]
		[string]$ProjectPath,
		[switch]$ReturnProjectName
	)

	# Validate project path exists
	if (-not (Test-Path $ProjectPath)) {
		Write-Error "Project directory '$ProjectPath' not found."
		return $false
	}

	# Check for main.tf file
	$mainTfPath = Join-Path $ProjectPath "main.tf"
	if (-not (Test-Path $mainTfPath)) {
		Write-Error "main.tf not found in '$ProjectPath'. Not a valid Terraform project directory."
		return $false
	}

	if ($ReturnProjectName) {
		return Split-Path $ProjectPath -Leaf
	}
	return $true
}


function Test-RequiredEnvironmentVariables {
	param (
		[Parameter(Mandatory = $true)]
		[array]$RequiredVars
	)

	$missingVars = @()
	foreach ($var in $RequiredVars) {
		if (-not (Get-Item "env:$var" -ErrorAction SilentlyContinue)) {
			$missingVars += $var
		}
	}

	if ($missingVars.Count -gt 0) {
		Write-Host "⚠️  Missing Environment Variables" -ForegroundColor Yellow
		Write-Host "The following environment variables are required:" -ForegroundColor White
		foreach ($var in $missingVars) {
			Write-Host "  • $var" -ForegroundColor Gray
		}
		return $missingVars
	}
	else {
		Write-Host "✓ Authentication environment variables detected" -ForegroundColor Green
		return @()
	}
}


# ========================================
# AUTHENTICATION AND CONTEXT FUNCTIONS
# ========================================

function Show-AuthContext {
	param([string]$Title = "Authentication Context")

	Write-Host "=== $Title ===" -ForegroundColor Cyan
	Write-Host "Environment Variables:" -ForegroundColor Yellow
	Write-Host "TF_VAR_ARM_CLIENT_ID: $env:TF_VAR_ARM_CLIENT_ID" -ForegroundColor Gray
	Write-Host "ARM_CLIENT_ID: $env:ARM_CLIENT_ID" -ForegroundColor Gray
	Write-Host ""
	Write-Host "Current Azure CLI context:" -ForegroundColor Yellow
	try {
		az account show --query "{name:name, user:user.name, type:user.type}" -o table
	} catch {
		Write-Host "Not logged in" -ForegroundColor Red
	}
	Write-Host ""
}
