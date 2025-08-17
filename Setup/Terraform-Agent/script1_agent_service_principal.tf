# AI Agent Service Principal
# Requires: Global Administrator for azuread resources
# Requires: Contributor role for Azure DevOps service connection

# Service Principal Creation (Global Admin Required)
resource "azuread_application" "ai_agent" {
  display_name = "sp-${var.org}-${var.app}-${var.env}-001"
  owners       = [data.azuread_client_config.current.object_id]

  required_resource_access {
    resource_app_id = "499b84ac-1321-427f-aa17-267ca6975798" # Azure DevOps

    resource_access {
      id   = "ee69721e-6c3a-468f-a9ec-302d16a4c599" # user_impersonation
      type = "Scope"
    }
  }
}

resource "azuread_service_principal" "ai_agent" {
  client_id = azuread_application.ai_agent.client_id
  owners    = [data.azuread_client_config.current.object_id]
}

resource "azuread_application_password" "ai_agent" {
  application_id = azuread_application.ai_agent.id
  display_name   = "AI Agent - Azure DevOps API Access"
  end_date       = "2030-01-00T00:00:00Z"
}

# Outputs for n8n configuration
output "agent_client_id" {
  value       = azuread_application.ai_agent.client_id
  description = "Use this in n8n Client ID field"
}

output "agent_client_secret" {
  value       = azuread_application_password.ai_agent.value
  sensitive   = true
  description = "Use this in n8n Client Secret field"
}

output "agent_object_id" {
  value       = azuread_service_principal.ai_agent.object_id
  description = "Service Principal Object ID for Azure DevOps permissions"
}

output "tenant_id" {
  value       = data.azuread_client_config.current.tenant_id
  description = "Azure AD Tenant ID"
}

output "service_principal_name" {
  value       = azuread_application.ai_agent.display_name
  description = "Service Principal display name"
}
