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

$variablesConfig = GetConfig "variables.${env}.tfvars"
$org = $variablesConfig["org"]
$app = $variablesConfig["app"]
$env = $variablesConfig["env"]

$location = $variablesConfig["location"]
$resourceGroupName = $variablesConfig["resource_group_name"]
$storageAccountName = $variablesConfig["storage_account_name"]
$armSubscriptionId = $variablesConfig["subscription_id"]
$armTenantId = $variablesConfig["tenant_id"]

$sqlServerName = "sqlsrv-${org}-${app}-${env}"
$sqlDatabaseName = "sqldb-${org}-${app}-${env}"
$logAnalyticsWorkspaceName = "kv-logging-${org}-${app}-${env}"
$keyVaultName = "kv-${org}-${app}-${env}"

# Service Principal Name (user/application). It is a client that will is owner of resource group.
$armClientName = "sp-${org}-${app}-${env}-001"

#--------------------------------------------------------------
# Create Azure Service Principal Applicaton (User)
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
# Create Azure Service Principal Applicaton (User)
#--------------------------------------------------------------

$servicePrincipalExists = az ad sp list --display-name $armClientName --query "[0]" | Out-String
if ($servicePrincipalExists -eq $null) {
    $userInput = Read-Host -Prompt "Do you want to create service principal '$armClientName'? [y/N]"
}else{
    $userInput = ""
    Write-Host "Service principal $armClientName' found."
}

# Check the user's answer, ignoring case
if ($userInput.Trim().ToUpper() -eq "Y") {
    
    # WARNING: Unable to encode the output with cp1252 encoding. Unsupported characters are discarded.
    #az config set core.only_show_errors=true
    # or: Control Panel > Region > Administrative > Change system local -> Check the 'Beta: Use Unicode UTF-8 for worldwide language support' option

    az ad sp create-for-rbac --name $armClientName

    Write "IMPORTANT: Safely store inforamtion above. Press any key to continue."
    pause

    $scopeName = "Microsoft Graph"
    $permissionName = "Directory.Read.All"

    # Get the service principal details.
    $sp = az ad sp list --display-name $armClientName --query '[0]' | Out-String | ConvertFrom-Json
    $armClientId = $sp.appId

    Write-Host "Retrieve the $scopeName API's service principal"
    $graphApiSp = az ad sp list --display-name $scopeName --query '[0]' | Out-String | ConvertFrom-Json
    $graphApiAppId = $graphApiSp.appId # "00000003-0000-0000-c000-000000000000"

    Write-Host "'$scopeName' application Id: '$armClientName'"

    Write-Host "Retrieve the '$permissionName' permission Id for  $scopeName"
    $delegatedPermissions = az ad sp show --id $graphApiAppId --query "oauth2PermissionScopes" | Out-String | ConvertFrom-Json
    $directoryReadAllPermission = $delegatedPermissions | Where-Object { $_.value -eq $permissionName }

    Write-Host "'$permissionName' permission Id: $($directoryReadAllPermission.id)"

    Write-Host "Assign '$permissionName' permission to the service principal"
    az ad app permission add --id $armClientId --api $graphApiAppId --api-permissions "$($directoryReadAllPermission.id)=Scope"

    Start-Sleep -Seconds 4

    Write-Host "Grant admin consent for the added permissions for the service principal"
    az ad app permission grant --id $armClientId --api $graphApiAppId --scope "https://graph.microsoft.com/.default"

    Start-Sleep -Seconds 4

    Write-Host "Admin consent granting"
    az ad app permission admin-consent --id $armClientId

    Write-Output "Service principal is granted '$permissionName' permission."

}

#--------------------------------------------------------------
# Get Azure service principal details.
#--------------------------------------------------------------

# Get the service principal details.
$sp = az ad sp list --display-name $armClientName --query '[0]' | ConvertFrom-Json
# Service Priciple application Id.
$armClientId = $sp.appId
$armTenantId = $sp.appOwnerOrganizationId

#--------------------------------------------------------------
# Create resource group.
#--------------------------------------------------------------

$resourceGroupExists = az group exists --name $resourceGroupName --subscription $armSubscriptionId
if ($resourceGroupExists -ne $true) {
    $userInput = Read-Host -Prompt "Do you want to create resource group? '$resourceGroupName'? [y/N]"
}else{
    $userInput = ""
    Write-Host "Resource group $resourceGroupName' found."
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
    az role assignment create --assignee $armClientId --role $spResourceRole --scope $resourceGroupId
}

#--------------------------------------------------------------
# Create storage account
#--------------------------------------------------------------

$storageAccountExists = az storage account list --resource-group $resourceGroupName --query "[?name=='$storageAccountName'] | length(@)" --subscription $armSubscriptionId
if ($storageAccountExists -eq 0) {
    $userInput = Read-Host -Prompt "Do you want to create storage account? '$storageAccountName'? [y/N]"
}else{
    $userInput = ""
    Write-Host "Storage account '$storageAccountName' found."
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

    Start-Sleep -Seconds 20

}

    Write-Host "Assigning 'Storage Account Key Operator Service Role' to the service principal for the storage account '$storageAccountName'..."
    $storageAccountScope = "/subscriptions/$armSubscriptionId/resourceGroups/$resourceGroupName/providers/Microsoft.Storage/storageAccounts/$storageAccountName"
    #az role assignment create --assignee $armClientId --role "Storage Account Key Operator Service Role" --scope $storageAccountScope

#--------------------------------------------------------------
# Begin Terraform Setup
#--------------------------------------------------------------

$armClientSecretSecure = Read-Host -Prompt "Enter password for service principal '$armClientName'" -AsSecureString
$armClientSecret = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($armClientSecretSecure))

# Logout from Azure to ensure a clean login.
az logout

# Login to Azure with principal that will have owner permissions on azure resource where everything will be created.
az login --service-principal -u $armClientId -p $armClientSecret --tenant $armTenantId
az account set --subscription $armSubscriptionId

#--------------------------------------------------------------
# Init environment.
#--------------------------------------------------------------

$userInput = Read-Host -Prompt "Do you want to initialize Terraform? [y/N]"
# Check the user's answer, ignoring case
if ($userInput.Trim().ToUpper() -eq "Y") {
    terraform init -backend-config="backend.${env}.tfvars" -var-file="variables.${env}.tfvars"
}

$kvs_openai_value = Read-Host -Prompt "Enter OpenAI API Key"
$vs_speech_value = Read-Host -Prompt "Enter Microsoft Speech Service API Key"

# Set environment variables. Use `TF_VAR_` prefix to make recognized by Terraform.
$env:TF_VAR_ARM_TENANT_ID = $armTenantId
$env:TF_VAR_ARM_SUBSCRIPTION_ID = $armSubscriptionId
$env:TF_VAR_ARM_CLIENT_ID = $armClientId
$env:TF_VAR_ARM_CLIENT_SECRET = $armClientSecret 

$env:ARM_TENANT_ID = $armTenantId
$env:ARM_SUBSCRIPTION_ID = $armSubscriptionId
$env:ARM_CLIENT_ID = $armClientId
$env:ARM_CLIENT_SECRET = $armClientSecret 

Write-Output "TF_VAR_ARM_TENANT_ID: $($env:TF_VAR_ARM_TENANT_ID)"
Write-Output "TF_VAR_ARM_SUBSCRIPTION_ID: $($env:TF_VAR_ARM_SUBSCRIPTION_ID)"
Write-Output "TF_VAR_ARM_CLIENT_ID: $($env:TF_VAR_ARM_CLIENT_ID)"

#--------------------------------------------------------------
# Setup service principal
#--------------------------------------------------------------

pause

Write-Host "Get Azure resource management access key."
$env:ARM_ACCESS_KEY = (az storage account keys list --resource-group $resourceGroupName --account-name $storageAccountName --query '[0].value' -o tsv)
Write-Host "ARM_ACCESS_KEY = $env:ARM_ACCESS_KEY"

Write-Host "Get Azure resource management database access token."
$env:ARM_DATABASE_ACCESS_TOKEN = (az account get-access-token --resource https://database.windows.net/ --output json | ConvertFrom-Json).accessToken
Write-Host "ARM_DATABASE_ACCESS_TOKEN = $env:ARM_DATABASE_ACCESS_TOKEN"

# Logout from Azure to ensure a clean login
az logout

# Run terraform init with the correct backend config and reconfigure
terraform init -reconfigure -backend-config="backend.${env}.tfvars"

# Refresh the state without making changes
terraform apply -refresh-only -var-file="variables.${env}.tfvars"

# Plan the changes for the new environment
terraform plan -var-file="variables.${env}.tfvars"
