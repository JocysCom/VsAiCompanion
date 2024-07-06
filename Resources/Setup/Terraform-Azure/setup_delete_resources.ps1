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

# Read and parse the config files
$backendConfig = GetConfig "backend.${env}.tfvars"
$resourceGroupName = $backendConfig["resource_group_name"]
$storageAccountName = $backendConfig["storage_account_name"]

$variablesConfig = GetConfig "variables.${env}.tfvars"
$org = $variablesConfig["org"]
$app = $variablesConfig["app"]
$sqlServerName = "sqlsrv-${org}-${app}-${env}"
$sqlDatabaseName = "sqldb-${org}-${app}-${env}"
$logAnalyticsWorkspaceName = "kv-logging-${org}-${app}-${env}"
$keyVaultName = "kv-${org}-${app}-${env}"


# Logout from Azure to ensure a clean login
az logout

$armClientId = $env:TF_VAR_ARM_CLIENT_ID
$armClientSecret = $env:TF_VAR_ARM_CLIENT_SECRET
$armTenantId = $env:TF_VAR_ARM_TENANT_ID
$armSubscriptionId = $env:TF_VAR_ARM_SUBSCRIPTION_ID

function SignIn {

	# Login to Azure with principal that will have owner permissions on azure resoure where everything will be created.
	az login --service-principal -u $armClientId -p $armClientSecret --tenant $armTenantId

	Write-Host "Select Azure CLI Subscription: $armSubscriptionId"
	az account set --subscription $armSubscriptionId

	# Login to Azure with service principal using Az module
	$securePassword = ConvertTo-SecureString $armClientSecret -AsPlainText -Force
	$creds = New-Object System.Management.Automation.PSCredential ($armClientId, $securePassword)
	Connect-AzAccount -ServicePrincipal -Tenant $armTenantId -Credential $creds

	Write-Host "Select Az Subscription: $armSubscriptionId"
	Select-AzSubscription -Subscription $armSubscriptionId

}

function SignOut {
	# Logout from Azure CLI
	az logout
	# Logout from Azure
	Disconnect-AzAccount
}


function DeleteReources {

	$lockName = "ResourceLock"
	$lockLevel = "CanNotDelete"

	try {
		Write-Host "Removing lock: $lockName from resource group: $resourceGroupName"
		Remove-AzResourceLock -LockName $lockName -ResourceGroupName $resourceGroupName -Force
	} catch {
		Write-Host "Lock does not exist or could not be removed. Continuing..."
	}

	Start-Sleep -Seconds 2

	Write-Host "Deleting SQL database: $sqlDatabaseName"
	az sql db delete --resource-group $resourceGroupName --server $sqlServerName --name $sqlDatabaseName --yes
		
	Start-Sleep -Seconds 2

	Write-Host "Deleting SQL server: $sqlServerName"
	az sql server delete --resource-group $resourceGroupName --name $sqlServerName --yes

	Start-Sleep -Seconds 2

	Write-Host "Deleting Log Analytics workspace: $logAnalyticsWorkspaceName"
	az monitor log-analytics workspace delete --resource-group $resourceGroupName --workspace-name $logAnalyticsWorkspaceName --yes

	Start-Sleep -Seconds 2

	#Write-Host "Deleting Key Vault: $keyVaultName"
	#az keyvault delete --subscription $armSubscriptionId -g $resourceGroupName -n $keyVaultName

	#az keyvault recover --subscription $armSubscriptionId -n $keyVaultName

	Start-Sleep -Seconds 2

	#$servicePrincipalObjectId = az ad sp show --id $armClientId --query "id" -o tsv
	#$servicePrincipalName = az ad sp show --id $armClientId --query "appDisplayName" -o tsv
	#$servicePrincipalObjectId
	#$servicePrincipalName

	# Unfortunatelly but service principal can't purge key vault.
	#Write-Host "purging Key Vault: $keyVaultName"
	#az keyvault purge --subscription $armSubscriptionId -n $keyVaultName --location "uksouth"

	Start-Sleep -Seconds 2

	# Recreate the lock on the resource group
	New-AzResourceLock -LockName $lockName -ResourceGroupName $resourceGroupName -LockLevel $lockLevel -Force

}

# Allow to manage existing resource with Terraform.
function ImportResources {
	# enable access to keyvault.
	#az keyvault update --name $keyVaultName --resource-group $resourceGroupName --set properties.networkAcls="{'defaultAction': 'Allow', 'bypass': 'AzureServices'}"


	$importPath = "/subscriptions/$armSubscriptionId/resourceGroups/$resourceGroupName/providers/Microsoft.KeyVault/vaults/$keyVaultName"
	# Key Vault
	terraform import -var-file="variables.dev.tfvars" azurerm_key_vault.kv $importPath
	# Access policy
	terraform import -var-file="variables.dev.tfvars" azurerm_key_vault_access_policy.key_vault_access_policy_terraform "$importPath/objectId/{guid_1}"
	# Access policy
	terraform import -var-file="variables.dev.tfvars" azurerm_key_vault_access_policy.key_vault_access_policy_read_secrets "$importPath/objectId/{guid_2}"
}
	

SignIn
ImportResources
#DeleteResources
#SignOut
