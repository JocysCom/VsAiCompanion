# Workaround: Exception of type 'Microsoft.Graph.AGS.Contracts.ClaimsChallengeRequiredException' was thrown.
# When set to 1: This environment variable instructs the Azure SDK to disable handling of the Conditional Access prompt,
# meaning it won't prompt users for additional credentials interactively to satisfy CA policies.
#Instead, it will fail and throw an exception if it encounters such a situation
$env:AZURE_IDENTITY_DISABLE_CP1 = "1"

az account clear
az login

# Ask for the environment abbreviation
$env = Read-Host -Prompt "Enter the name of the environment (dev, test, prod, ...)"

# Check if the backend file exists
if (-Not (Test-Path -Path "backend.${env}.tfvars")) {
    Write-Error "File 'backend.${env}.tfvars' does not exist. Exiting script."
    exit 1  # Exit with a non-zero status code to indicate an error
}

#--------------------------------------------------------------
# Install Required modules.
#--------------------------------------------------------------

function Ensure-AzModule {
	# Install Required `Az` PowerShell Module
	if (-not (Get-Module -ListAvailable -Name Az)) {
		Write-Output "Az module not found. Installing..."
		Install-Module -Name Az -AllowClobber -Scope CurrentUser -Force
	}
	# Import Required `Az` PowerShell Module
	if (-not (Get-Module -Name Az)) {
		Write-Output "Loading `Az` module into the current PowerShell session..."
		Import-Module Az
	}
}

#--------------------------------------------------------------
# Load configuration data.
#--------------------------------------------------------------

function GetConfig {
    param (
        [Parameter(Mandatory=$true)]
        [string]$FilePath
    )
    $config = @{}
    $fileContent = Get-Content $FilePath
    $fileContent | ForEach-Object {
        if ($_ -match '(\w+)\s*=\s*"(.+?)"') {
            $config[$matches[1]] = $matches[2]
            Write-Host "$($matches[1]) = $($matches[2])"
        }
    }
    return $config
}

$backend = GetConfig "backend.${env}.tfvars"
$spTenantId = $backend["tenant_id"]
$subscriptionId = $backend["subscription_id"]
$resourceGroupName = $backend["resource_group_name"]
$storageAccountName = $backend["storage_account_name"]
$containerName = $backend["container_name"]

$variables = GetConfig "variables.${env}.tfvars"
$org = $variables["org"]
$app = $variables["app"]
$env = $variables["env"]
$location = $variables["location"]

$sqlServerName = "sql-${org}-${app}-${env}"
$sqlDatabaseName = "sqldb-${org}-${app}-${env}"
$sqlIdentity = "identity-${org}-${app}-${env}-sql"
$logAnalyticsWorkspaceName = "kv-logging-${org}-${app}-${env}"
$keyVaultName = "kv-${org}-${app}-${env}"
$kvDiagnosticLogging = "kv-diagnostic-logging-${org}-${app}-${env}" 

# Service Principal Name (user/application). It is a client that will is owner of resource group.
$spName = "sp-${org}-${app}-${env}-001"

#--------------------------------------------------------------
# Set active subscription
#--------------------------------------------------------------

# By verifying the subscription and explicitly setting it as active before performing any operations,
# you ensure that the context is correct and avoid issues related to subscription not being found.

Write-Host "Verifying subscription exists..."
$subscriptionExists = az account list --query "[?id=='$subscriptionId'] | length(@)"

if ($subscriptionExists -eq 0) {
    Write-Host "Error: Subscription '$subscriptionId' was not found."
    exit 1
} else {
    Write-Host "Subscription '$subscriptionId' exists."
}

Write-Host "Setting active subscription '$subscriptionId'..."
az account set --subscription $subscriptionId

# Get the subscription details
$subscription = az account list --query "[?id=='$subscriptionId']" --output json | ConvertFrom-Json

# Extract the subscription name
$subscriptionName = $subscription[0].name

#--------------------------------------------------------------
# Create `Azure Service Principals` group
#--------------------------------------------------------------

$spGroupName = "Azure Service Principals"
$spGroupMail = "AzureServicePrincipals"

$spGroup = az ad group list --display-name $spGroupName --query "[0]" | Out-String | ConvertFrom-Json
$userInput = ""
if ($spGroup -eq $null) {
    $userInput = Read-Host -Prompt "Do you want to create group '$spGroupName'? [y/N]"
}

# Check the user's answer, ignoring case
if ($userInput.Trim().ToUpper() -eq "Y") {
    Write-Host "Creating group '$spGroupName'."
    az ad group create --display-name $spGroupName --mail-nickname $spGroupMail
    # Get details.
    $sp = az ad sp list --display-name $spName --query '[0]' | Out-String | ConvertFrom-Json
    $spGroup = az ad group list --display-name $spGroupName --query "[0]" | Out-String | ConvertFrom-Json
}

$spGroupId = $spGroup.id
Write-Host "Group '$spGroupName':"
Write-Host "  Id: $spGroupId"

# Assigns Directory Reader role to the root scope
#$roleDR = "Directory Readers"
#az role assignment create --assignee-object-id $spGroupId --role $roleDR  --scope "/"

#--------------------------------------------------------------
# Create Azure Service Principal applicaton
#--------------------------------------------------------------

$sp = az ad sp list --display-name $spName --query '[0]' | Out-String | ConvertFrom-Json
$userInput = ""
if ($sp -eq $null) {
    $userInput = Read-Host -Prompt "Do you want to create service principal '$spName'? [y/N]"
}

# Check the user's answer, ignoring case
if ($userInput.Trim().ToUpper() -eq "Y") {
    # WARNING: Unable to encode the output with cp1252 encoding. Unsupported characters are discarded.
    #az config set core.only_show_errors=true
    # or: Control Panel > Region > Administrative > Change system local -> Check the 'Beta: Use Unicode UTF-8 for worldwide language support' option
    az ad sp create-for-rbac --name $spName
    Write "IMPORTANT: Safely store inforamtion above. Press any key to continue."
    pause
    # Get details.
    $sp = az ad sp list --display-name $spName --query '[0]' | Out-String | ConvertFrom-Json
}

# Service Priciple application Id.
$spId = $sp.id
$spAppId = $sp.appId
$spTenantId = $sp.appOwnerOrganizationId

Write-Host "Service principal '$spName':"
Write-Host "  Id: $spId"
Write-Host "  App Id: $spAppId"

#--------------------------------------------------------------
# Configure Service Principal application permissions
#--------------------------------------------------------------

function GetAppId {
	param ([Parameter(Mandatory=$true)][string]$apiName)
    Write-Host "API '$apiName':"
    $apiSp = az ad sp list --display-name $apiName --query '[0]'  | Out-String | ConvertFrom-Json
    $apiAppId = $apiSp.appId
    Write-Host "  App Id: $apiAppId"
    return $apiAppId
}

function GetDelegatedPermissionId {
	param (
        [Parameter(Mandatory=$true)][string]$apiName,
        [Parameter(Mandatory=$true)][string]$apiAppId,
        [Parameter(Mandatory=$true)][string]$permissionName
    )
    Write-Host "Permission '$permissionName' of '$apiName':"
    $delegatedPermissions = az ad sp show --id $apiAppId --query "oauth2PermissionScopes"  | Out-String | ConvertFrom-Json
    $permission = $delegatedPermissions | Where-Object { $_.value -eq $permissionName }
    $permissionId = $permission.id
    Write-Host "  Id: $permissionId"
    return $permissionId
}

# Assign and grant permission to the service principal
function AssignAndGrantPermission {
	param (
        [Parameter(Mandatory=$true)][string]$appId,
        [Parameter(Mandatory=$true)][string]$apiAppId,
        [Parameter(Mandatory=$true)][string]$apiPermissionId,
        [Parameter(Mandatory=$true)][string]$apiPermissionName
    )
    # First, list existing permissions
    $existingPermissions = az ad app permission list --id $appId | Out-String | ConvertFrom-Json
    # Check if the permission already exists
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

$userInput = Read-Host -Prompt "Do you want to grant permissions to '$spName'? [y/N]"
# Check the user's answer, ignoring case
if ($userInput.Trim().ToUpper() -eq "Y") {

	# https://graph.microsoft.com/
	$mgApiName = "Microsoft Graph"
	$mgApiAppId = GetAppId $mgApiName # 00000003-0000-0000-c000-000000000000
	$mgPermissionName = "Directory.Read.All" # 06da0dbc-49e2-44d2-8312-53f166ab848a
	$mgPermissionId = GetDelegatedPermissionId $mgApiName $mgApiAppId $mgPermissionName
	AssignAndGrantPermission $spAppId $mgApiAppId $mgPermissionId $mgPermissionName

	# https://graph.windows.net/
	$adApiName = "Azure Active Directory Graph"
	#$adApiAppId = GetAppId $adApiName # 00000002-0000-0000-c000-000000000000
	$adApiAppId = "00000002-0000-0000-c000-000000000000"
	$adPermissionName = "Directory.Read.All" # 5778995a-e1bf-45b8-affa-663a9f3f4d04
	#$adPermissionName = "Directory.ReadWrite.All" # 5778995a-e1bf-45b8-affa-663a9f3f4d04
	$adPermissionId = GetDelegatedPermissionId $adApiName $adApiAppId $adPermissionName
	AssignAndGrantPermission $spAppId $adApiAppId $adPermissionId $adPermissionName
}

#--------------------------------------------------------------
# SQL Managed Identity
#--------------------------------------------------------------

# retrieve the user-assigned managed identity of an Azure SQL Database logical server:
#$sqlUserIdentity = az sql server show --resource-group $resourceGroupName --name $sqlServerName --query identity.userAssignedIdentities
# retrieve the system-assigned managed identity of an Azure SQL Database logical server:
#Write-Host "SQL Managed Identity:"
#$sqlSystemIdentity = az sql server show --resource-group $resourceGroupName --name $sqlServerName --query identity | ConvertFrom-Json
#$sqlSystemIdentityId = $sqlSystemIdentity.principalId
#Write-Host "principalId: $sqlSystemIdentityId"

#AssignAndGrantPermission $sqlSystemIdentityId $mgApiAppId $mgPermissionId $mgPermissionName


#--------------------------------------------------------------
# Assign Service Principal application to the group.
#--------------------------------------------------------------

# Check if the service principal is already a member of the group
$memberCheck = az ad group member check --group $spGroupId --member-id $spId --query "value"
if ($memberCheck -eq $false) {
    Write-Host "Adding service principal '$spName' to group '$spGroupName'."
    az ad group member add --group $spGroupId --member-id $spId
} else {
    Write-Host "Service principal '$spName' is already a member of the group '$spGroupName'."
}

#--------------------------------------------------------------
# Create resource group.
#--------------------------------------------------------------

$resourceGroupExists = az group exists --name $resourceGroupName --subscription $subscriptionId
$userInput = ""
if ($resourceGroupExists -ne $true) {
    $userInput = Read-Host -Prompt "Do you want to create resource group? '$resourceGroupName'? [y/N]"
}

# Check the user's answer, ignoring case
if ($userInput.Trim().ToUpper() -eq "Y") {

    Write-Host "Create resource group '$resourceGroupName'"
    az group create --name $resourceGroupName --location $location --subscription $subscriptionId
}

function CreateRoleAssignment {
	param (
		[string] $scope,
		[string] $principalName,
		[string] $roleDefinitionName
	)
	# Get the existing role assignment information
	$roleAssignmentId = az role assignment list --scope $scope --query "[?principalName=='$principalName' && roleDefinitionName=='$roleDefinitionName'] | [0].id" | ConvertFrom-Json
	if ($roleAssignmentId -eq $null) {
		Write-Host "Assign '$roleDefinitionName' role for the resource group '$resourceGroupName' to the service principal"
		az role assignment create --assignee $principalName --role $roleDefinitionName --scope $scope
	}else{
		Write-Host "Role '$roleDefinitionName' already assigned to the service principal"
	}
}

#Write-Host "Get '$resourceGroupName' resource group Id"
$resourceGroup = az group show --name $resourceGroupName --subscription $subscriptionId | Out-String | ConvertFrom-Json
$resourceGroupId = $resourceGroup.id

$userInput = Read-Host -Prompt "Assign user roles to '$spName'? [y/N]"
# Check the user's answer, ignoring case
if ($userInput.Trim().ToUpper() -eq "Y") {

	CreateRoleAssignment $resourceGroupId  $spAppId "Owner"
	CreateRoleAssignment $resourceGroupId  $spAppId "User Access Administrator"
	#CreateRoleAssignment $resourceGroupId  $spAppId "Role Based Access Control Administrator"
	#CreateRoleAssignment $resourceGroupId  $spAppId "App Administrator"
	#CreateRoleAssignment $resourceGroupId  $spAppId "Reader"

	#Write-Host "Assign '$spResourceRole' role for the resource group '$resourceGroupName' to the service principal"
	#az role assignment create --assignee $spAppId --role $spResourceRole --scope $resourceGroupId
}

#--------------------------------------------------------------
# Register providers
#--------------------------------------------------------------

function RegisterProvider {
    param (
       [Parameter(Mandatory=$true)][string]$providerName
    )
    Write-Host "Checking if '$providerName' resource provider is registered..."
    $resourceProviderState = az provider show --namespace $providerName --subscription $subscriptionId --query "registrationState" --output tsv
    if ($resourceProviderState -ne "Registered") {
        Write-Host "Registering '$providerName' resource provider..."
        az provider register --namespace $providerName --subscription $subscriptionId
        # wait 20 second for resource to register
        Write-Host "Please wait 20 seconds..."
        Start-Sleep -Seconds 20
    }
}

$userInput = Read-Host -Prompt "Register providers for '$subscriptionName' subscription? [y/N]"
# Check the user's answer, ignoring case
if ($userInput.Trim().ToUpper() -eq "Y") {

	RegisterProvider "Microsoft.Storage"
	RegisterProvider "Microsoft.KeyVault"
	RegisterProvider "Microsoft.Sql"
	RegisterProvider "Microsoft.Resources"

	# azurerm_log_analytics_workspace.kv_logging_workspace
	RegisterProvider "Microsoft.Insights"
	RegisterProvider "Microsoft.OperationalInsights"

}
#--------------------------------------------------------------
# Create storage account
#--------------------------------------------------------------

$storageAccountExists = az storage account list --resource-group $resourceGroupName --query "[?name=='$storageAccountName'] | length(@)" --subscription $subscriptionId
$userInput = ""
if ($storageAccountExists -eq 0) {
    $userInput = Read-Host -Prompt "Do you want to create storage account? '$storageAccountName'? [y/N]"
}

# Check the user's answer, ignoring case
if ($userInput.Trim().ToUpper() -eq "Y") {

    Write-Host "Creating storage account '$storageAccountName'..."
    az storage account create --name $storageAccountName --resource-group $resourceGroupName --location $location --sku Standard_LRS --subscription $subscriptionId

    Write-Host "Creating storage container '$storageAccountName'..."
    az storage container create --name $containerName --account-name $storageAccountName

    Write-Host "Assigning 'Storage Account Key Operator Service Role' to the service principal for the storage account '$storageAccountName'..."
    $storageAccountScope = "/subscriptions/$subscriptionId/resourceGroups/$resourceGroupName/providers/Microsoft.Storage/storageAccounts/$storageAccountName"
    #az role assignment create --assignee $spAppId --role "Storage Account Key Operator Service Role" --scope $storageAccountScope

    Start-Sleep -Seconds 20

}

#--------------------------------------------------------------
# Setup service principal
#--------------------------------------------------------------

$armClientSecretSecure = Read-Host -Prompt "Enter password for service principal '$spName'" -AsSecureString
$armClientSecret = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($armClientSecretSecure))

# Logout from Azure to ensure a clean login.
az account clear

#--------------------------------------------------------------
# Delete Resources
#--------------------------------------------------------------


function SignInAsPrincipalUsingAzCli {
	# Login to Azure with principal that will have owner permissions on azure resoure where everything will be created.
	az login --service-principal -u $spAppId -p $armClientSecret --tenant $spTenantId
	Write-Host "Select Azure CLI Subscription: $subscriptionId"
	az account set --subscription $subscriptionId
}

function SignInAsPrincipalUsingAzPowerShell {
	# Login to Azure with service principal using Az module
	$securePassword = ConvertTo-SecureString $armClientSecret -AsPlainText -Force
	$creds = New-Object System.Management.Automation.PSCredential ($spAppId, $securePassword)
	Connect-AzAccount -ServicePrincipal -Tenant $spTenantId -Credential $creds
	Write-Host "Select Az Subscription: $subscriptionId"
	Select-AzSubscription -Subscription $subscriptionId
}


function SignOut {
	# Logout from Azure CLI
	az account clear
	# Logout from Azure
	Disconnect-AzAccount
}


function DeleteResources {
	$lockName = "ResourceLock"
	$lockLevel = "CanNotDelete"
	try {
		Write-Host "Removing lock: $lockName from resource group: $resourceGroupName"
		Remove-AzResourceLock -LockName $lockName -ResourceGroupName $resourceGroupName -Force
	} catch {
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
			az ad sp delete --id $sp
		}
		Write-Host "Deleting SQL Identity : $sqlIdentity"
		az identity delete --resource-group $resourceGroupName --name $sqlIdentity
	}

	$userInput = Read-Host -Prompt "Do you want to delete '$keyVaultName' key vault? [y/N]"
	if ($userInput.Trim().ToUpper() -eq "Y") {
		Start-Sleep -Seconds 2
		Write-Host "Deleting Log Analytics workspace: $logAnalyticsWorkspaceName"
		az monitor log-analytics workspace delete --resource-group $resourceGroupName --workspace-name $logAnalyticsWorkspaceName --yes
		Start-Sleep -Seconds 2
		#Write-Host "Deleting Key Vault: $keyVaultName"
		#az keyvault delete --subscription $subscriptionId -g $resourceGroupName -n $keyVaultName
		#az keyvault recover --subscription $subscriptionId -n $keyVaultName
		Start-Sleep -Seconds 2
		#$servicePrincipalObjectId = az ad sp show --id $armClientId --query "id" -o tsv
		#$servicePrincipalName = az ad sp show --id $armClientId --query "appDisplayName" -o tsv
		#$servicePrincipalObjectId
		#$servicePrincipalName
		# Unfortunatelly but service principal can't purge key vault.
		#Write-Host "purging Key Vault: $keyVaultName"
		#az keyvault purge --subscription $subscriptionId -n $keyVaultName --location "uksouth"
	}

	$userInput = Read-Host -Prompt "Do you want to delete '$containerName' terraform container? [y/N]"
	if ($userInput.Trim().ToUpper() -eq "Y") {
		az storage container delete --account-name $storageAccountName --name $containerName
	}

	Start-Sleep -Seconds 2
	# Recreate the lock on the resource group
	New-AzResourceLock -LockName $lockName -ResourceGroupName $resourceGroupName -LockLevel $lockLevel -Force
}

$userInput = Read-Host -Prompt "Do you want to delete resources in '$resourceGroupName'? [y/N]"
# Check the user's answer, ignoring case
if ($userInput.Trim().ToUpper() -eq "Y") {
	Ensure-AzModule
	SignInAsPrincipalUsingAzCli
	SignInAsPrincipalUsingAzPowerShell
	DeleteResources
	SignOut 
	return
}

SignInAsPrincipalUsingAzCli

# Check if can access groups. Requires "Directory Readers" role assingned on Service Principal or group tha service principal is member of.
az ad group list --filter "displayName eq 'AI_RiskLevel_Low'" --verbose

# Init environment.

Write-Host "Get Azure resource management access key."
$env:ARM_ACCESS_KEY = (az storage account keys list --resource-group $resourceGroupName --account-name $storageAccountName --query '[0].value' -o tsv)
Write-Host "ARM_ACCESS_KEY = $env:ARM_ACCESS_KEY"

Write-Host "Get Azure resource management database access token."
$env:ARM_DATABASE_ACCESS_TOKEN = (az account get-access-token --resource https://database.windows.net/ --output json | Out-String | ConvertFrom-Json).accessToken
Write-Host "ARM_DATABASE_ACCESS_TOKEN = ********"

# Set environment variables. Use `TF_VAR_` prefix to make recognized by Terraform.
$env:TF_VAR_ARM_TENANT_ID = $spTenantId
$env:TF_VAR_ARM_SUBSCRIPTION_ID = $subscriptionId
$env:TF_VAR_ARM_CLIENT_ID = $spAppId
$env:TF_VAR_ARM_CLIENT_SECRET = $armClientSecret 

$env:ARM_TENANT_ID = $spTenantId
$env:ARM_SUBSCRIPTION_ID = $subscriptionId
$env:ARM_CLIENT_ID = $spAppId
$env:ARM_CLIENT_SECRET = $armClientSecret 

Write-Output "TF_VAR_ARM_TENANT_ID: $($env:TF_VAR_ARM_TENANT_ID)"
Write-Output "TF_VAR_ARM_SUBSCRIPTION_ID: $($env:TF_VAR_ARM_SUBSCRIPTION_ID)"
Write-Output "TF_VAR_ARM_CLIENT_ID: $($env:TF_VAR_ARM_CLIENT_ID)"


Write-Host "Get '$resourceGroupName' resource group Id"
$resourceGroup = az group show --name $resourceGroupName --subscription $subscriptionId | Out-String | ConvertFrom-Json
$resourceGroupId = $resourceGroup.id

CreateRoleAssignment $resourceGroupId  $spAppId "Owner"
CreateRoleAssignment $resourceGroupId  $spAppId "User Access Administrator"

#Write-Host "Assign '$spResourceRole' role for the resource group '$resourceGroupName' to the service principal"
#az role assignment create --assignee $spAppId --role $spResourceRole --scope $resourceGroupId

$KVS_OPENAI_VALUE_Secure = Read-Host -Prompt "Enter OpenAI API Key" -AsSecureString
$env:TF_VAR_KVS_OPENAI_VALUE = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($KVS_OPENAI_VALUE_Secure))

$KVS_SPEECH_VALUE_Secure = Read-Host -Prompt "Enter Microsoft Speech Service API Key" -AsSecureString
$env:TF_VAR_KVS_SPEECH_VALUE = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($KVS_SPEECH_VALUE_Secure))

#--------------------------------------------------------------
# Import existing resources into Terraform
#--------------------------------------------------------------

# Function to check if a resource is managed by Terraform
function IsResourceManagedByTerraform {
    param (
        [string] $resourceType,
        [string] $resourceName
    )
    # List all resources in the Terraform state
    $stateList = terraform state list
    # Check if the specified resource is in the state list
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
		[string] $terraformResourceId
    )
	if (IsResourceManagedByTerraform $resourceType $resourceName) {
		return
	}
	# Check if resource exists on Azure.
	$resource = & az resource show --ids $resourceId --query "id" --output tsv 2> $null
	if (-not $resource) {
		"'$resourceId' not found on Azure."
		return
	}

	$userInput = Read-Host -Prompt "Do you want to import '$resourceType.$resourceName'? [y/N]"
	if ($userInput.Trim().ToUpper() -eq "Y") {
		if (-not $terraformResourceId){
			$terraformResourceId = $resourceId
		}
		terraform import -var-file="variables.${env}.tfvars" "$resourceType.$resourceName" $terraformResourceId
	}
}

function ImportRoleResource {
	param (
        [string] $resourceType,
        [string] $resourceName,
		[string] $scope,
		[string] $principalName,
		[string] $roleDefinitionName
    )
	if (IsResourceManagedByTerraform $resourceType $resourceName){
		return
	}
	# Get the existing role assignment information
	$resourceId = & az role assignment list --scope $scope --query "[?principalName=='$principalName' && roleDefinitionName=='$roleDefinitionName'] | [0].id" | ConvertFrom-Json
	if ($resourceId -eq $null) {
		"'$resourceType.$resourceName' not found on Azure."
		return
	}
	ImportResource $resourceType $resourceName $resourceId
}

function ImportIdentity {
	param (
        [string] $resourceType,
        [string] $resourceName,
		[string] $identityName
    )
	if (IsResourceManagedByTerraform $resourceType $resourceName){
		return
	}
	$resourceId = az identity show --resource-group $resourceGroupName --name $identityName --query "id" -o tsv
	if ($resourceId -eq $null) {
		"'$resourceType.$resourceName' not found on Azure."
		return
	}
	ImportResource $resourceType $resourceName $resourceId
}

function ImportServicePrincipal {
	param (
        [string] $resourceType,
        [string] $resourceName,
		[string] $displayname
    )
	if (IsResourceManagedByTerraform $resourceType $resourceName){
		return
	}
	$resourceId = az ad sp list --display-name $displayname --query "[0].id" -o tsv
	if ($resourceId -eq $null) {
		"'$resourceType.$resourceName' not found on Azure."
		return
	}
	ImportResource $resourceType $resourceName $resourceId
}

function ImportSecretResource {
	param (
        [string] $resourceType,
        [string] $resourceName,
		[string] $vaultName,
		[string] $secretName
    )
	if (IsResourceManagedByTerraform $resourceType $resourceName){
		return
	}
	$resourceId = az keyvault secret show --vault-name $vaultName --name $secretName --query 'id' -o tsv
	if ($resourceId -eq $null) {
		"'$resourceType.$resourceName' not found on Azure."
		return
	}
	"'$resourceType.$resourceName' found on Azure."
	$allVersionsResourceId = "$rgPath/providers/Microsoft.KeyVault/vaults/$vaultName/secrets/$secretName"
	ImportResource $resourceType $resourceName $allVersionsResourceId $resourceId
}

$userInput = Read-Host -Prompt "Do you want to import resources? [y/N]"
# Check the user's answer, ignoring case
if ($userInput.Trim().ToUpper() -eq "Y") {
	$rgPath = "/subscriptions/$subscriptionId/resourceGroups/$resourceGroupName"

	$keyVaultPath = "$rgPath/providers/Microsoft.KeyVault/vaults/$keyVaultName"
	$sqlPath = "$rgPath/providers/Microsoft.Sql/servers/$sqlServerName"
	$storagePath = "$rgPath/providers/Microsoft.Storage/storageAccounts/$storageAccountName"
	#$identitiesPath = "$rgPath/providers/Microsoft.ManagedIdentity/userAssignedIdentities"

	# SQL

	ImportResource "azurerm_mssql_server" "sqlsrv" $sqlPath
    ImportResource "azurerm_mssql_server_security_alert_policy" "sqlsrv_audit_policy" "$sqlPath/securityAlertPolicies/default"
	ImportServicePrincipal "azuread_service_principal" "sql_identity_sp" $sqlIdentity
	ImportIdentity "azurerm_user_assigned_identity" "sql_identity" $sqlIdentity

	# Key Vault
	
	ImportResource "azurerm_key_vault"    "kv"     $keyVaultPath
	ImportRoleResource "azurerm_role_assignment" "key_vault_reader_low"      $keyVaultPath "AI_RiskLevel_Low"      "Key Vault Reader"
	ImportRoleResource "azurerm_role_assignment" "key_vault_reader_medium"   $keyVaultPath "AI_RiskLevel_Medium"   "Key Vault Reader"
	ImportRoleResource "azurerm_role_assignment" "key_vault_reader_high"     $keyVaultPath "AI_RiskLevel_High"     "Key Vault Reader"
	ImportRoleResource "azurerm_role_assignment" "key_vault_reader_critical"               $keyVaultPath "AI_RiskLevel_Critical" "Key Vault Reader"
	ImportRoleResource "azurerm_role_assignment" "key_vault_contributor_critical"          $keyVaultPath "AI_RiskLevel_Critical" "Key Vault Contributor"
	ImportRoleResource "azurerm_role_assignment" "key_vault_administrator_critical"        $keyVaultPath "AI_RiskLevel_Critical" "Key Vault Administrator"
	ImportRoleResource "azurerm_role_assignment" "key_vault_crypto_officer_critical"       $keyVaultPath "AI_RiskLevel_Critical" "Key Vault Crypto Officer"
	ImportRoleResource "azurerm_role_assignment" "key_vault_certificates_officer_critical" $keyVaultPath "AI_RiskLevel_Critical" "Key Vault Certificates Officer"
	ImportRoleResource "azurerm_role_assignment" "key_vault_secrets_officer_critical"      $keyVaultPath "AI_RiskLevel_Critical" "Key Vault Secrets Officer"
	
	ImportResource "azurerm_key_vault_access_policy" "key_vault_access_policy_terraform" "$keyVaultPath/objectId/3da556b7-5f1a-4df9-9e6a-bfee3b4dd096"
	ImportResource "azurerm_key_vault_access_policy" "key_vault_access_policy_terraform" "$keyVaultPath/objectId/03eb84a4-4533-4658-9426-1c5107e4de1b"

	ImportSecretResource "azurerm_key_vault_secret" "kvs_openai" $keyVaultName "openai-api-key"
    ImportSecretResource "azurerm_key_vault_secret" "kvs_speech" $keyVaultName "ms-speech-service-api-key"

	ImportResource "azurerm_monitor_diagnostic_setting" "kv_diagnostic_logging" "$keyVaultPath|$kvDiagnosticLogging"

	# Storage 

	ImportResource "azurerm_storage_management_policy" "storage_management" "$storagePath/managementPolicies/default"

	pause
}

#--------------------------------------------------------------
# Begin Terraform Setup (requries normal user login)
#--------------------------------------------------------------

# Logout from Azure to ensure a clean login
#az account clear
#az login

$userInput = Read-Host -Prompt "Do you want to initialize Terraform? [y/N]"
# Check the user's answer, ignoring case
if ($userInput.Trim().ToUpper() -eq "Y") {
    terraform init -backend-config="backend.${env}.tfvars" -var-file="variables.${env}.tfvars"
}

# Run terraform init with the correct backend config and reconfigure
terraform init -reconfigure -backend-config="backend.${env}.tfvars"

# Plan the changes for the new environment
terraform plan -var-file="variables.${env}.tfvars"

# Refresh the state without making changes
terraform apply -refresh-only -var-file="variables.${env}.tfvars"

# Apply the changes for the new environment
#terraform apply -var-file="variables.${env}.tfvars"

#az account clear
#az login
