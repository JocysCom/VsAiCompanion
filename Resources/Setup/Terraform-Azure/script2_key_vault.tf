# Define Key Vault Resource

resource "azurerm_key_vault" "kv" {
  name                = "kv-${var.org}-${var.app}-${var.env}"
  location            = data.azurerm_resource_group.rg.location
  resource_group_name = data.azurerm_resource_group.rg.name
  tenant_id           = data.azurerm_client_config.client_config.tenant_id
  sku_name            = "standard"
}

# Assign Key Vault Reader role to AI_RiskLevel groups

resource "azurerm_role_assignment" "key_vault_reader_low" {
  scope                = azurerm_key_vault.kv.id
  role_definition_name = "Key Vault Reader"
  principal_id         = data.azuread_group.g2.id
}

resource "azurerm_role_assignment" "key_vault_reader_medium" {
  scope                = azurerm_key_vault.kv.id
  role_definition_name = "Key Vault Reader"
  principal_id         = data.azuread_group.g3.id
}

resource "azurerm_role_assignment" "key_vault_reader_high" {
  scope                = azurerm_key_vault.kv.id
  role_definition_name = "Key Vault Reader"
  principal_id         = data.azuread_group.g4.id
}

resource "azurerm_role_assignment" "key_vault_reader_critical" {
  scope                = azurerm_key_vault.kv.id
  role_definition_name = "Key Vault Reader"
  principal_id         = data.azuread_group.g5.id
}

# Assign additional roles to AI_RiskLevel_Critical group

resource "azurerm_role_assignment" "key_vault_contributor_critical" {
  scope                = azurerm_key_vault.kv.id
  role_definition_name = "Key Vault Contributor"
  principal_id         = data.azuread_group.g5.id
}

resource "azurerm_role_assignment" "key_vault_administrator_critical" {
  scope                = azurerm_key_vault.kv.id
  role_definition_name = "Key Vault Administrator"
  principal_id         = data.azuread_group.g5.id
}

resource "azurerm_role_assignment" "key_vault_crypto_officer_critical" {
  scope                = azurerm_key_vault.kv.id
  role_definition_name = "Key Vault Crypto Officer"
  principal_id         = data.azuread_group.g5.id
}

resource "azurerm_role_assignment" "key_vault_certificates_officer_critical" {
  scope                = azurerm_key_vault.kv.id
  role_definition_name = "Key Vault Certificates Officer"
  principal_id         = data.azuread_group.g5.id
}

resource "azurerm_role_assignment" "key_vault_secrets_officer_critical" {
  scope                = azurerm_key_vault.kv.id
  role_definition_name = "Key Vault Secrets Officer"
  principal_id         = data.azuread_group.g5.id
}

# Add Access Policy for the Client Configuration used by Terraform
resource "azurerm_key_vault_access_policy" "key_vault_access_policy_terraform" {
  key_vault_id = azurerm_key_vault.kv.id
  tenant_id    = data.azurerm_client_config.client_config.tenant_id
  object_id    = data.azurerm_client_config.client_config.object_id
  secret_permissions = [
    "Get", "List", "Set", "Delete", "Recover", "Backup", "Restore", "Purge"
  ]
}

resource "azurerm_key_vault_access_policy" "key_vault_access_policy_read_secrets" {
  key_vault_id = azurerm_key_vault.kv.id
  tenant_id    = data.azurerm_client_config.client_config.tenant_id
  object_id    = data.azuread_group.g5.object_id
  secret_permissions = [
    "Get", "List"
  ]
}

# Define Key Vault Secrets using the simplified variable names

resource "azurerm_key_vault_secret" "kvs_openai" {
  name         = "openai-api-key"
  value        = var.kvs_openai_value
  key_vault_id = azurerm_key_vault.kv.id
  depends_on = [
    azurerm_role_assignment.key_vault_contributor_critical,
    azurerm_role_assignment.key_vault_administrator_critical,
    azurerm_role_assignment.key_vault_secrets_officer_critical,
    azurerm_role_assignment.key_vault_certificates_officer_critical,
    azurerm_role_assignment.key_vault_crypto_officer_critical,
    azurerm_role_assignment.key_vault_reader_critical,
    azurerm_key_vault_access_policy.key_vault_access_policy_terraform,
    azurerm_key_vault_access_policy.key_vault_access_policy_read_secrets
  ]
}

resource "azurerm_key_vault_secret" "kvs_speech" {
  name         = "ms-speech-service-api-key"
  value        = var.kvs_speech_value
  key_vault_id = azurerm_key_vault.kv.id
  depends_on = [
    azurerm_role_assignment.key_vault_contributor_critical,
    azurerm_role_assignment.key_vault_administrator_critical,
    azurerm_role_assignment.key_vault_secrets_officer_critical,
    azurerm_role_assignment.key_vault_certificates_officer_critical,
    azurerm_role_assignment.key_vault_crypto_officer_critical,
    azurerm_role_assignment.key_vault_reader_critical,
    azurerm_key_vault_access_policy.key_vault_access_policy_terraform,
    azurerm_key_vault_access_policy.key_vault_access_policy_read_secrets
  ]
}