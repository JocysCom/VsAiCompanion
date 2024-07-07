az logout
az login

# Ask for the environment abbreviation
$env = Read-Host -Prompt "Enter the name of the environment (dev, test, prod, ...)"

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
$armSubscriptionId = $backend["subscription_id"]
$resourceGroupName = $backend["resource_group_name"]
$storageAccountName = $backend["storage_account_name"]
$containerName = $backend["container_name"]

$variables = GetConfig "variables.${env}.tfvars"
$org = $variables["org"]
$app = $variables["app"]
$env = $variables["env"]
$location = $variables["location"]

$sqlServerName = "sqlsrv-${org}-${app}-${env}"
$sqlDatabaseName = "sqldb-${org}-${app}-${env}"
$logAnalyticsWorkspaceName = "kv-logging-${org}-${app}-${env}"
$keyVaultName = "kv-${org}-${app}-${env}"

# Service Principal Name (user/application). It is a client that will is owner of resource group.
$spName = "sp-${org}-${app}-${env}-001"

#--------------------------------------------------------------
# Set active subscription
#--------------------------------------------------------------

# By verifying the subscription and explicitly setting it as active before performing any operations,
# you ensure that the context is correct and avoid issues related to subscription not being found.

Write-Host "Verifying subscription exists..."
$subscriptionExists = az account list --query "[?id=='$armSubscriptionId'] | length(@)"

if ($subscriptionExists -eq 0) {
    Write-Host "Error: Subscription '$armSubscriptionId' was not found."
    exit 1
} else {
    Write-Host "Subscription '$armSubscriptionId' exists."
}

Write-Host "Setting active subscription '$armSubscriptionId'..."
az account set --subscription $armSubscriptionId

#--------------------------------------------------------------
# Create `Azure Service Principals` group
#--------------------------------------------------------------

$spGroupName = "Azure Service Principals"
$spGroupMail = "AzureServicePrincipals"

$spGroup = az ad group list --display-name $spGroupName --query "[0]" | ConvertFrom-Json
$userInput = ""
if ($spGroup -eq $null) {
    $userInput = Read-Host -Prompt "Do you want to create group '$spGroupName'? [y/N]"
}

# Check the user's answer, ignoring case
if ($userInput.Trim().ToUpper() -eq "Y") {
    Write-Host "Creating group '$spGroupName'."
    az ad group create --display-name $spGroupName --mail-nickname $spGroupMail
    # Get details.
    $sp = az ad sp list --display-name $spName --query '[0]' | ConvertFrom-Json
    $spGroup = az ad group list --display-name $spGroupName --query "[0]" | ConvertFrom-Json
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

$sp = az ad sp list --display-name $spName --query '[0]' | ConvertFrom-Json
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
    $sp = az ad sp list --display-name $spName --query '[0]' | ConvertFrom-Json
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
    $apiSp = az ad sp list --display-name $apiName --query '[0]' | ConvertFrom-Json
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
    $delegatedPermissions = az ad sp show --id $apiAppId --query "oauth2PermissionScopes" | ConvertFrom-Json
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
    $existingPermissions = az ad app permission list --id $appId | ConvertFrom-Json
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
$adPermissionId = GetDelegatedPermissionId $adApiName $adApiAppId $adPermissionName
AssignAndGrantPermission $spAppId $adApiAppId $adPermissionId $adPermissionName

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

$resourceGroupExists = az group exists --name $resourceGroupName --subscription $armSubscriptionId
$userInput = ""
if ($resourceGroupExists -ne $true) {
    $userInput = Read-Host -Prompt "Do you want to create resource group? '$resourceGroupName'? [y/N]"
}

# Check the user's answer, ignoring case
if ($userInput.Trim().ToUpper() -eq "Y") {

    $spResourceRole = "Owner"

    Write-Host "Create resource group '$resourceGroupName'"
    az group create --name $resourceGroupName --location $location --subscription $armSubscriptionId

    Write-Host "Get '$resourceGroupName' resource group Id"
    $resourceGroup = az group show --name $resourceGroupName --subscription $armSubscriptionId | ConvertFrom-Json
    $resourceGroupId = $resourceGroup.id

    Write-Host "Assign '$spResourceRole' role for the resource group '$resourceGroupName' to the service principal"
    az role assignment create --assignee $spAppId --role $spResourceRole --scope $resourceGroupId
}

#--------------------------------------------------------------
# Create storage account
#--------------------------------------------------------------

$storageAccountExists = az storage account list --resource-group $resourceGroupName --query "[?name=='$storageAccountName'] | length(@)" --subscription $armSubscriptionId
$userInput = ""
if ($storageAccountExists -eq 0) {
    $userInput = Read-Host -Prompt "Do you want to create storage account? '$storageAccountName'? [y/N]"
}

# Check the user's answer, ignoring case
if ($userInput.Trim().ToUpper() -eq "Y") {

    Write-Host "Checking if Microsoft.Storage resource provider is registered..."
    $resourceProviderState = az provider show --namespace Microsoft.Storage --subscription $armSubscriptionId --query "registrationState" --output tsv
    if ($resourceProviderState -ne "Registered") {
        Write-Host "Registering Microsoft.Storage resource provider..."
        az provider register --namespace Microsoft.Storage --subscription $armSubscriptionId
        # wait 20 second for resource to register
        Write-Host "Please wait 20 seconds..."
        Start-Sleep -Seconds 20
    }
    
    Write-Host "Creating storage account '$storageAccountName'..."
    az storage account create --name $storageAccountName --resource-group $resourceGroupName --location $location --sku Standard_LRS --subscription $armSubscriptionId

    Write-Host "Creating storage container '$storageAccountName'..."
    az storage container create --name $containerName --account-name $storageAccountName

    Write-Host "Assigning 'Storage Account Key Operator Service Role' to the service principal for the storage account '$storageAccountName'..."
    $storageAccountScope = "/subscriptions/$armSubscriptionId/resourceGroups/$resourceGroupName/providers/Microsoft.Storage/storageAccounts/$storageAccountName"
    #az role assignment create --assignee $spAppId --role "Storage Account Key Operator Service Role" --scope $storageAccountScope

    Start-Sleep -Seconds 20

}

#--------------------------------------------------------------
# Setup service principal
#--------------------------------------------------------------

$armClientSecretSecure = Read-Host -Prompt "Enter password for service principal '$spName'" -AsSecureString
$armClientSecret = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($armClientSecretSecure))

# Logout from Azure to ensure a clean login.
az logout

# Login to Azure with principal that will have owner permissions on azure resource where everything will be created.
az login --service-principal -u $spAppId -p $armClientSecret --tenant $spTenantId
az account set --subscription $armSubscriptionId

# Check if can access groups. Requires "Directory Readers" role assingned on Service Principal or group tha service principal is member of.
az ad group list --filter "displayName eq 'AI_RiskLevel_Low'" --verbose

# Init environment.

Write-Host "Get Azure resource management access key."
$env:ARM_ACCESS_KEY = (az storage account keys list --resource-group $resourceGroupName --account-name $storageAccountName --query '[0].value' -o tsv)
Write-Host "ARM_ACCESS_KEY = $env:ARM_ACCESS_KEY"

Write-Host "Get Azure resource management database access token."
$env:ARM_DATABASE_ACCESS_TOKEN = (az account get-access-token --resource https://database.windows.net/ --output json | ConvertFrom-Json).accessToken
Write-Host "ARM_DATABASE_ACCESS_TOKEN = $env:ARM_DATABASE_ACCESS_TOKEN"

# Set environment variables. Use `TF_VAR_` prefix to make recognized by Terraform.
$env:TF_VAR_ARM_TENANT_ID = $spTenantId
$env:TF_VAR_ARM_SUBSCRIPTION_ID = $armSubscriptionId
$env:TF_VAR_ARM_CLIENT_ID = $spAppId
$env:TF_VAR_ARM_CLIENT_SECRET = $armClientSecret 

$env:ARM_TENANT_ID = $spTenantId
$env:ARM_SUBSCRIPTION_ID = $armSubscriptionId
$env:ARM_CLIENT_ID = $spAppId
$env:ARM_CLIENT_SECRET = $armClientSecret 

Write-Output "TF_VAR_ARM_TENANT_ID: $($env:TF_VAR_ARM_TENANT_ID)"
Write-Output "TF_VAR_ARM_SUBSCRIPTION_ID: $($env:TF_VAR_ARM_SUBSCRIPTION_ID)"
Write-Output "TF_VAR_ARM_CLIENT_ID: $($env:TF_VAR_ARM_CLIENT_ID)"

#--------------------------------------------------------------
# Begin Terraform Setup (requries normal user login)
#--------------------------------------------------------------

# Logout from Azure to ensure a clean login
az logout
az login

# Register providers
#az provider register --namespace Microsoft.Security
#az provider register --namespace Microsoft.Logic

$userInput = Read-Host -Prompt "Do you want to initialize Terraform? [y/N]"
# Check the user's answer, ignoring case
if ($userInput.Trim().ToUpper() -eq "Y") {
    terraform init -backend-config="backend.${env}.tfvars" -var-file="variables.${env}.tfvars"
}

$kvs_openai_value = Read-Host -Prompt "Enter OpenAI API Key"
$vs_speech_value = Read-Host -Prompt "Enter Microsoft Speech Service API Key"

# Run terraform init with the correct backend config and reconfigure
terraform init -reconfigure -backend-config="backend.${env}.tfvars"

# Refresh the state without making changes
terraform apply -refresh-only -var-file="variables.${env}.tfvars"

# Plan the changes for the new environment
terraform plan -var-file="variables.${env}.tfvars"
