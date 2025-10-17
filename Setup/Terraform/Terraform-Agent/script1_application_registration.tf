# AI Agent Service Principal - Application Registration
# Smart creation: Use existing application if available, create if not

locals {
  app_display_name = "sp-${var.org}-${var.app}-${var.agent_name}-${var.env}-001"
}

# Look up an existing application by display name
data "azuread_applications" "existing" {
  display_names = [local.app_display_name]
}

# Create application only when it doesn't already exist
resource "azuread_application" "ai_agent" {
  count        = length(data.azuread_applications.existing.applications) == 0 ? 1 : 0
  display_name = local.app_display_name
  owners       = [data.azuread_client_config.current.object_id]

  # Sign-in audience: "AzureADMyOrg" restricts authentication to your tenant
  sign_in_audience = "AzureADMyOrg"

  required_resource_access {
    resource_app_id = "499b84ac-1321-427f-aa17-267ca6975798" # Azure DevOps

    resource_access {
      id   = "ee69721e-6c3a-468f-a9ec-302d16a4c599" # user_impersonation
      type = "Scope"
    }
  }

  lifecycle {
    ignore_changes = [
      display_name,
      owners,
      required_resource_access,
      sign_in_audience
    ]
  }
}

# Consolidate IDs from existing or newly created resources
locals {
  existing_app_count      = length(data.azuread_applications.existing.applications)
  app_was_created         = local.existing_app_count == 0
  ai_agent_application_id = local.existing_app_count > 0 ? data.azuread_applications.existing.applications[0].app_id : azuread_application.ai_agent[0].client_id
  ai_agent_object_id      = local.existing_app_count > 0 ? data.azuread_applications.existing.applications[0].id     : azuread_application.ai_agent[0].object_id
}
