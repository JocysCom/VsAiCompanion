/*
# Outputs for Script 1: Azure AD Groups
output "group_low" {
  value = data.azuread_group.g2
}

output "group_medium" {
  value = data.azuread_group.g3
}

output "group_high" {
  value = data.azuread_group.g4
}

output "group_critical" {
  value = data.azuread_group.g5
}

# Outputs for Script 2: Key Vault and Secrets
output "key_vault_id" {
  value = data.azurerm_key_vault.kv1.id
}

output "secret_openai_api_key" {
  value = data.azurerm_key_vault_secret.kvs1.value
}

output "secret_speech_service_api_key" {
  value = data.azurerm_key_vault_secret.kvs2.value
}

# Outputs for Script 3: SQL Database
output "mssql_server_id" {
  value = azurerm_mssql_server.example.id
}

output "mssql_database_id" {
  value = azurerm_mssql_database.example.id
}
*/