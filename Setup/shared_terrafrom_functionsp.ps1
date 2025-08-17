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
		if ($line -match '^(\w+)\s*=\s*"?([^"]*)"?$') {
			$key = $matches[1].Trim()
			$value = $matches[2].Trim() -replace '^"(.*)"$', '$1'
			$config[$key] = $value
		}
	}
	return $config
}

function Ensure-AzModule {
	if (-not (Get-Module -ListAvailable -Name "Az")) {
		Write-Host "Az PowerShell module not found. Please run setup_terraform_tools.ps1 first." -ForegroundColor Red
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
	if ($roleAssignmentId -eq $null) {
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
		if ($sqlSpId -ne $null) {
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
	if ($resourceId -eq $null) {
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
	if ($resourceId -eq $null) {
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
	if ($resourceId -eq $null) {
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
	if ($resourceId -eq $null) {
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
