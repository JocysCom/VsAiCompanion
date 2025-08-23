# AI Agent Service Principal - Application Registration
# Smart creation: Use existing application if available, create if not

locals {
  app_display_name = "sp-${var.org}-${var.app}-${var.agent_name}-${var.env}-001"
}

# Create application (will fail gracefully if already exists due to ignore_changes)
resource "azuread_application" "ai_agent" {
  display_name = local.app_display_name
  owners       = [data.azuread_client_config.current.object_id]

  # Sign-in audience: "AzureADMyOrg" is a Microsoft constant (not a placeholder)
  # This restricts authentication to users from your organization's Azure AD tenant only
  # Other valid values: "AzureADMultipleOrgs", "AzureADandPersonalMicrosoftAccount", "PersonalMicrosoftAccount"
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

# Use the application resource directly
locals {
  ai_agent_application_id = azuread_application.ai_agent.client_id
  ai_agent_object_id      = azuread_application.ai_agent.object_id
}
