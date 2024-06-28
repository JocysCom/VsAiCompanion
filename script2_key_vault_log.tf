# Add this new resource for storage management policy
resource "azurerm_storage_management_policy" "storage_management" {
  storage_account_id = azurerm_storage_account.storage_account.id

  rule {
    name    = "retention-rule"
    enabled = true

    filters {
      blob_types = ["blockBlob"]
    }

    actions {
      base_blob {
        delete {
          days_after_modification_greater_than = 90
        }
      }
    }
  }
}

# Log Analytics Workspace for storing logs
resource "azurerm_log_analytics_workspace" "kv_logging_workspace" {
  name                = "kv-logging-${var.org}-${var.app}-${var.env}"
  location            = data.azurerm_resource_group.rg.location
  resource_group_name = data.azurerm_resource_group.rg.name
  sku                 = "PerGB2018"
}

# Diagnostic settings for logging
resource "azurerm_monitor_diagnostic_setting" "kv_diagnostic_logging" {
  name                       = "kv-diagnostic-logging-${var.org}-${var.app}-${var.env}"
  target_resource_id         = azurerm_key_vault.kv.id
  log_analytics_workspace_id = azurerm_log_analytics_workspace.kv_logging_workspace.id

  log {
    category = "AuditEvent"
    enabled  = true
  }

  metric {
    category = "AllMetrics"
    enabled  = true
  }
}
