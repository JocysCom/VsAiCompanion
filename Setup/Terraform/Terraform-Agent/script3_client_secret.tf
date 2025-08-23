# AI Agent Service Principal - Client Secret
# Creates the client secret for n8n authentication

resource "azuread_application_password" "ai_agent" {
  application_id = local.ai_agent_object_id
  display_name   = "rbac"
  end_date       = "2030-01-01T00:00:00Z"
}
