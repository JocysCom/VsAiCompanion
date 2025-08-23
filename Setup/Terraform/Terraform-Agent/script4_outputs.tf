# AI Agent Service Principal - Outputs
# Outputs for n8n configuration

output "agent_client_id" {
  value       = local.ai_agent_application_id
  description = "Use this in n8n Client ID field"
}

output "agent_client_secret" {
  value       = azuread_application_password.ai_agent.value
  sensitive   = true
  description = "Use this in n8n Client Secret field"
}

output "agent_object_id" {
  value       = local.ai_agent_sp_object_id
  description = "Service Principal Object ID for Azure DevOps permissions"
}

output "tenant_id" {
  value       = data.azuread_client_config.current.tenant_id
  description = "Azure AD Tenant ID"
}

output "service_principal_name" {
  value       = local.app_display_name
  description = "Service Principal display name"
}

output "application_id" {
  value       = local.ai_agent_object_id
  description = "Azure AD Application Object ID"
}

output "deployment_method" {
  value       = "ignore_changes_lifecycle"
  description = "Terraform deployment method used (ignore_changes for idempotency)"
}

# Local value for manual creation commands (accessible during terraform plan)
locals {
  manual_creation_commands = <<-EOT
# Manual Azure AD Object Creation Commands. Prerequisites: Sign in as an admin
az login

# Existing Terraform service principal
$OWNING_APP_DISPLAY_NAME="sp-${var.org}-${var.app}-${var.env}-001"

# New service principal to create
$APP_DISPLAY_NAME="${local.app_display_name}"

# Create Azure AD Application with Azure DevOps permissions. GUID Explanations:
# "499b84ac-1321-427f-aa17-267ca6975798" = Azure DevOps Services (well-known resource)
# "ee69721e-6c3a-468f-a9ec-302d16a4c599" = user_impersonation scope for Azure DevOps
$REQUIRED_RESOURCE_ACCESSES='[{ "resourceAppId": "499b84ac-1321-427f-aa17-267ca6975798", "resourceAccess": [{ "id": "ee69721e-6c3a-468f-a9ec-302d16a4c599", "type": "Scope" }]}]'
az ad app create --display-name "$APP_DISPLAY_NAME" --sign-in-audience "AzureADMyOrg" --required-resource-accesses "$REQUIRED_RESOURCE_ACCESSES"

# Create and get the Application IDs
$APP_ID=$(az ad app list --display-name "$APP_DISPLAY_NAME" --query '[0].appId' -o tsv)
$APP_OBJECT_ID=$(az ad app list --display-name "$APP_DISPLAY_NAME" --query '[0].id' -o tsv)

# Create the Service Principal
az ad sp create --id $APP_ID

# (Optional) Granting admin consent for Azure DevOps permissions.
az ad app permission admin-consent --id $APP_ID

# Add the existing Terraform service principal as owner so it can manage the app
$OWNING_APP_OBJECT_ID=$(az ad app list --display-name "$OWNING_APP_DISPLAY_NAME" --query '[0].id' -o tsv)
az ad app owner add --id $APP_OBJECT_ID --owner-object-id $OWNING_APP_OBJECT_ID

EOT
}

output "manual_creation_commands" {
  value       = local.manual_creation_commands
  description = "Manual Azure CLI commands for Global Admins to pre-create resources (accessible during plan: terraform console -> local.manual_creation_commands)"
}

