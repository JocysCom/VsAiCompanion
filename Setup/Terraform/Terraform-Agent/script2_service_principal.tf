# AI Agent Service Principal - Service Principal Creation
# Prefer existing SP if found; create only when not present

# Look up an existing service principal by display name
data "azuread_service_principals" "existing_sp" {
  display_names = [local.app_display_name]
}

resource "azuread_service_principal" "ai_agent" {
  count     = length(data.azuread_service_principals.existing_sp.service_principals) == 0 ? 1 : 0
  client_id = local.ai_agent_application_id
  owners    = [data.azuread_client_config.current.object_id]

  lifecycle {
    ignore_changes = [
      owners
    ]
  }
}

# Consolidate SP identifiers from existing or newly created resources
locals {
  existing_sp_count     = length(data.azuread_service_principals.existing_sp.service_principals)
  ai_agent_sp_object_id = local.existing_sp_count > 0 ? data.azuread_service_principals.existing_sp.service_principals[0].id : azuread_service_principal.ai_agent[0].object_id
  ai_agent_sp_client_id = local.ai_agent_application_id
}
