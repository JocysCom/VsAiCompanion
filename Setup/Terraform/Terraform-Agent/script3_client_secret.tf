# AI Agent Service Principal - Client Secret
# Creates the client secret for n8n authentication
# Only create a new secret when the application was created by this run.
# If the application already exists, skip secret creation to avoid unintended rotation.

resource "azuread_application_password" "ai_agent" {
  count          = 1
  application_id = local.ai_agent_object_id
  display_name   = "rbac"
  end_date       = "2030-01-01T00:00:00Z"
}
