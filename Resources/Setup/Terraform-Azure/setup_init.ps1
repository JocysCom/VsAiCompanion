# Ask for the environment abbreviation
$env = Read-Host -Prompt "Enter the name of the environment (dev, test, prod, ...)"
$kvs_openai_value = Read-Host -Prompt "Enter OpenAI API Key"
$vs_speech_value = Read-Host -Prompt "Enter Microsoft Speech Service API Key"

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

$resourceGroupName = $variablesConfig["resource_group_name"]
$storageAccountName = $variablesConfig["storage_account_name"]
$armSubscriptionId = $variablesConfig["subscription_id"]
$armTenantId = $variablesConfig["tenant_id"]

$sqlServerName = "sqlsrv-${org}-${app}-${env}"
$sqlDatabaseName = "sqldb-${org}-${app}-${env}"
$logAnalyticsWorkspaceName = "kv-logging-${org}-${app}-${env}"
$keyVaultName = "kv-${org}-${app}-${env}"

# Service Principal Name (user/application). It is a client that will is owner of resource group.
$spName = "sp-${org}-${app}-${env}-001"

#--------------------------------------------------------------
# Setup Azure service principal
#--------------------------------------------------------------

# Logout from Azure to ensure a clean login.
az logout

# Login to Azure with principal that will have owner permissions on azure resource where everything will be created.
az login --service-principal -u $armClientId -p $armClientSecret --tenant $armTenantId
#az account set --subscription $armSubscriptionId

# Get the service principal details.
$sp = az ad sp list --display-name $spName --query '[0]' | ConvertFrom-Json
$armClientId = $sp.appId
$armTenantId = $sp.appOwnerOrganizationId
$armClientSecretSecure = Read-Host -Prompt "Enter password for service principal '$spName'" -AsSecureString
$armClientSecret = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($armClientSecretSecure))

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
terraform init -reconfigure -backend-config="variables.${env}.tfvars"

# Refresh the state without making changes
terraform apply -refresh-only -var-file="variables.${env}.tfvars"

# Plan the changes for the new environment
terraform plan -var-file="variables.${env}.tfvars"
