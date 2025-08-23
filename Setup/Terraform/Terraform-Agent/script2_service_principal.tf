# AI Agent Service Principal - Service Principal Creation
# Creates service principal (will use existing if already exists due to ignore_changes)

resource "azuread_service_principal" "ai_agent" {
  client_id = local.ai_agent_application_id
  owners    = [data.azuread_client_config.current.object_id]

  lifecycle {
    ignore_changes = [
      owners
    ]
  }
}

# Use the service principal resource directly
locals {
  ai_agent_sp_object_id = azuread_service_principal.ai_agent.object_id
  ai_agent_sp_client_id = local.ai_agent_application_id
}
