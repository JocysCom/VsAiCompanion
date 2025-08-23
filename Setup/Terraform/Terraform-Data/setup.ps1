# Terraform Data Setup Script
# This script sets up and deploys the Terraform Data infrastructure using shared functions

# Workaround: Exception of type 'Microsoft.Graph.AGS.Contracts.ClaimsChallengeRequiredException' was thrown.
# When set to 1: This environment variable instructs the Azure SDK to disable handling of the Conditional Access prompt,
# meaning it won't prompt users for additional credentials interactively to satisfy CA policies.
# Instead, it will fail and throw an exception if it encounters such a situation
$env:AZURE_IDENTITY_DISABLE_CP1 = "1"

# Clear any existing Azure CLI session and login
az account clear
az login

# Get the environment from user
$env = Read-Host -Prompt "Enter the name of the environment (dev, test, prod, ...)"

# Validate backend configuration file exists
if (-Not (Test-Path -Path "backend.${env}.tfvars")) {
	Write-Error "File 'backend.${env}.tfvars' does not exist. Exiting script."
	exit 1
}

# Import shared Terraform functions
. ..\shared_terrafrom_functionsp.ps1

# Validate required tools are installed
$requiredTools = @("az", "terraform", "pwsh")
$missingTools = @()
foreach ($tool in $requiredTools) {
	if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) {
		$missingTools += $tool
	}
}
if ($missingTools.Count -gt 0) {
	Write-Error "Missing required tools: $($missingTools -join ', '). Please run setup_terraform_tools.ps1 first."
	exit 1
}

# Load configuration
$backend = GetConfig "backend.${env}.tfvars"
$variables = GetConfig "variables.${env}.tfvars"

# Extract configuration values
$spTenantId = $backend["tenant_id"]
$subscriptionId = $backend["subscription_id"]
$resourceGroupName = $backend["resource_group_name"]
$storageAccountName = $backend["storage_account_name"]
$containerName = $backend["container_name"]

$org = $variables["org"]
$app = $variables["app"]
$env = $variables["env"]
$location = $variables["location"]

# Generate resource names
$sqlServerName = "sql-${org}-${app}-${env}"
$sqlDatabaseName = "sqldb-${org}-${app}-${env}"
$sqlIdentity = "identity-${org}-${app}-${env}-sql"
$logAnalyticsWorkspaceName = "kv-logging-${org}-${app}-${env}"
$keyVaultName = "kv-${org}-${app}-${env}"
$kvDiagnosticLogging = "kv-diagnostic-logging-${org}-${app}-${env}"
$spName = "sp-${org}-${app}-${env}-001"

#--------------------------------------------------------------
# Set active subscription
#--------------------------------------------------------------

if (-not (Test-SubscriptionExists $subscriptionId)) {
	exit 1
}

$subscriptionName = Set-ActiveSubscription $subscriptionId

#--------------------------------------------------------------
# Create Azure Service Principals group
#--------------------------------------------------------------

$spGroupName = "Azure Service Principals"
$spGroupMail = "AzureServicePrincipals"

$spGroup = New-AzureADGroupIfNotExists -GroupName $spGroupName -MailNickname $spGroupMail

if ($spGroup -eq $null) {
	Write-Warning "Failed to create or retrieve group '$spGroupName'. Some operations may fail."
}

$spGroupId = $spGroup.id

#--------------------------------------------------------------
# Create Azure Service Principal application
#--------------------------------------------------------------

$sp = New-ServicePrincipalIfNotExists -ServicePrincipalName $spName

if ($sp -eq $null) {
	Write-Error "Failed to create or retrieve service principal '$spName'. Exiting."
	exit 1
}

$spId = $sp.id
$spAppId = $sp.appId
$spTenantId = $sp.appOwnerOrganizationId

#--------------------------------------------------------------
# Configure Service Principal application permissions
#--------------------------------------------------------------

Grant-ServicePrincipalPermissions -ServicePrincipalAppId $spAppId

#--------------------------------------------------------------
# Assign Service Principal application to the group
#--------------------------------------------------------------

if ($spGroup -and $sp) {
	Add-ServicePrincipalToGroup -GroupId $spGroupId -ServicePrincipalId $spId -GroupName $spGroupName -ServicePrincipalName $spName
}

#--------------------------------------------------------------
# Create resource group
#--------------------------------------------------------------

$resourceGroup = New-ResourceGroupIfNotExists -ResourceGroupName $resourceGroupName -Location $location -SubscriptionId $subscriptionId
$resourceGroupId = $resourceGroup.id

# Assign roles to service principal
if (Get-UserConfirmation "Assign user roles to '$spName'?") {
	CreateRoleAssignment $resourceGroupId $spAppId "Owner"
	CreateRoleAssignment $resourceGroupId $spAppId "User Access Administrator"
}

#--------------------------------------------------------------
# Register providers
#--------------------------------------------------------------

Register-RequiredProviders -SubscriptionId $subscriptionId -SubscriptionName $subscriptionName

#--------------------------------------------------------------
# Create storage account
#--------------------------------------------------------------

New-StorageAccountIfNotExists -StorageAccountName $storageAccountName -ResourceGroupName $resourceGroupName -Location $location -ContainerName $containerName -SubscriptionId $subscriptionId

#--------------------------------------------------------------
# Setup service principal authentication
#--------------------------------------------------------------

$armClientSecretSecure = Read-Host -Prompt "Enter password for service principal '$spName'" -AsSecureString
$armClientSecret = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($armClientSecretSecure))

# Logout from Azure to ensure a clean login
az account clear

#--------------------------------------------------------------
# Handle resource deletion if requested
#--------------------------------------------------------------

if (Get-UserConfirmation "Do you want to delete resources in '$resourceGroupName'?") {
	if (-not (Ensure-AzModule)) {
		Write-Error "Failed to ensure Az PowerShell module is available."
		exit 1
	}
	SignInAsPrincipalUsingAzCli $spAppId $armClientSecret $spTenantId $subscriptionId
	SignInAsPrincipalUsingAzPowerShell $spAppId $armClientSecret $spTenantId $subscriptionId
	DeleteResources $resourceGroupName $sqlDatabaseName $sqlServerName $sqlIdentity $keyVaultName $logAnalyticsWorkspaceName $containerName $storageAccountName
	SignOut
	exit 0
}

#--------------------------------------------------------------
# Sign in as service principal and setup environment
#--------------------------------------------------------------

SignInAsPrincipalUsingAzCli $spAppId $armClientSecret $spTenantId $subscriptionId

# Verify service principal can access groups (requires "Directory Readers" role)
az ad group list --filter "displayName eq 'AI_RiskLevel_Low'" --verbose

# Get access keys and tokens for Terraform
Write-Host "Getting Azure resource management access key..."
$accessKey = (az storage account keys list --resource-group $resourceGroupName --account-name $storageAccountName --query '[0].value' -o tsv)

Write-Host "Getting Azure resource management database access token..."
$databaseAccessToken = (az account get-access-token --resource https://database.windows.net/ --output json | Out-String | ConvertFrom-Json).accessToken

# Set all Terraform environment variables
Set-TerraformEnvironmentVariables -TenantId $spTenantId -SubscriptionId $subscriptionId -ClientId $spAppId -ClientSecret $armClientSecret -AccessKey $accessKey -DatabaseAccessToken $databaseAccessToken

# Ensure service principal has proper roles (redundant but ensures consistency)
CreateRoleAssignment $resourceGroupId $spAppId "Owner"
CreateRoleAssignment $resourceGroupId $spAppId "User Access Administrator"

# Get additional secrets for Key Vault
$KVS_OPENAI_VALUE_Secure = Read-Host -Prompt "Enter OpenAI API Key" -AsSecureString
$env:TF_VAR_KVS_OPENAI_VALUE = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($KVS_OPENAI_VALUE_Secure))

$KVS_SPEECH_VALUE_Secure = Read-Host -Prompt "Enter Microsoft Speech Service API Key" -AsSecureString
$env:TF_VAR_KVS_SPEECH_VALUE = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($KVS_SPEECH_VALUE_Secure))

#--------------------------------------------------------------
# Import existing resources into Terraform (if requested)
#--------------------------------------------------------------

if (Get-UserConfirmation "Do you want to import existing resources into Terraform?") {
	$rgPath = "/subscriptions/$subscriptionId/resourceGroups/$resourceGroupName"
	$keyVaultPath = "$rgPath/providers/Microsoft.KeyVault/vaults/$keyVaultName"
	$sqlPath = "$rgPath/providers/Microsoft.Sql/servers/$sqlServerName"
	$storagePath = "$rgPath/providers/Microsoft.Storage/storageAccounts/$storageAccountName"

	# Import SQL resources
	ImportResource "azurerm_mssql_server" "sqlsrv" $sqlPath "" $env
	ImportResource "azurerm_mssql_server_security_alert_policy" "sqlsrv_audit_policy" "$sqlPath/securityAlertPolicies/default" "" $env
	ImportServicePrincipal "azuread_service_principal" "sql_identity_sp" $sqlIdentity $env
	ImportIdentity "azurerm_user_assigned_identity" "sql_identity" $sqlIdentity $resourceGroupName $env

	# Import Key Vault resources
	ImportResource "azurerm_key_vault" "kv" $keyVaultPath "" $env
	ImportRoleResource "azurerm_role_assignment" "key_vault_reader_low" $keyVaultPath "AI_RiskLevel_Low" "Key Vault Reader" $env
	ImportRoleResource "azurerm_role_assignment" "key_vault_reader_medium" $keyVaultPath "AI_RiskLevel_Medium" "Key Vault Reader" $env
	ImportRoleResource "azurerm_role_assignment" "key_vault_reader_high" $keyVaultPath "AI_RiskLevel_High" "Key Vault Reader" $env
	ImportRoleResource "azurerm_role_assignment" "key_vault_reader_critical" $keyVaultPath "AI_RiskLevel_Critical" "Key Vault Reader" $env
	ImportRoleResource "azurerm_role_assignment" "key_vault_contributor_critical" $keyVaultPath "AI_RiskLevel_Critical" "Key Vault Contributor" $env
	ImportRoleResource "azurerm_role_assignment" "key_vault_administrator_critical" $keyVaultPath "AI_RiskLevel_Critical" "Key Vault Administrator" $env
	ImportRoleResource "azurerm_role_assignment" "key_vault_crypto_officer_critical" $keyVaultPath "AI_RiskLevel_Critical" "Key Vault Crypto Officer" $env
	ImportRoleResource "azurerm_role_assignment" "key_vault_certificates_officer_critical" $keyVaultPath "AI_RiskLevel_Critical" "Key Vault Certificates Officer" $env
	ImportRoleResource "azurerm_role_assignment" "key_vault_secrets_officer_critical" $keyVaultPath "AI_RiskLevel_Critical" "Key Vault Secrets Officer" $env

	ImportSecretResource "azurerm_key_vault_secret" "kvs_openai" $keyVaultName "openai-api-key" $rgPath $env
	ImportSecretResource "azurerm_key_vault_secret" "kvs_speech" $keyVaultName "ms-speech-service-api-key" $rgPath $env
	ImportResource "azurerm_monitor_diagnostic_setting" "kv_diagnostic_logging" "$keyVaultPath|$kvDiagnosticLogging" "" $env

	# Import Storage resources
	ImportResource "azurerm_storage_management_policy" "storage_management" "$storagePath/managementPolicies/default" "" $env

	pause
}

#--------------------------------------------------------------
# Terraform Operations
#--------------------------------------------------------------

# Initialize Terraform
if (Get-UserConfirmation "Do you want to initialize Terraform?") {
	terraform init -backend-config="backend.${env}.tfvars" -var-file="variables.${env}.tfvars"
}

# Reconfigure and run operations
Write-Host "Initializing Terraform with backend reconfiguration..."
terraform init -reconfigure -backend-config="backend.${env}.tfvars"

Write-Host "Planning Terraform changes..."
terraform plan -var-file="variables.${env}.tfvars"

Write-Host "Refreshing Terraform state..."
terraform apply -refresh-only -var-file="variables.${env}.tfvars"

Write-Host ""
Write-Host "Setup completed successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Review the Terraform plan output"
Write-Host "  2. Run 'terraform apply -var-file=`"variables.${env}.tfvars`"' to apply changes"
Write-Host "  3. Verify deployed resources in the Azure portal"
Write-Host ""
Write-Host "Note: Uncomment the final terraform apply line in this script to auto-apply changes."
# Uncomment the line below to automatically apply changes (use with caution)
# terraform apply -var-file="variables.${env}.tfvars"
