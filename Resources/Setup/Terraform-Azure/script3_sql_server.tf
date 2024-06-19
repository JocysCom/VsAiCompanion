# Create SQL Server

resource "azurerm_mssql_server" "sqlsrv" {
  name                = "sqlsrv-${var.org}-${var.app}-${var.env}"
  resource_group_name = data.azurerm_resource_group.rg.name
  location            = data.azurerm_resource_group.rg.location
  version             = "12.0"
  # Azure AD Administrator for the SQL Server. Use current user.
  azuread_administrator {
    login_username              = data.azuread_user.admin_user.user_principal_name
    object_id                   = data.azuread_user.admin_user.object_id
    #login_username              = data.azuread_group.g5.display_name
    #object_id                   = data.azuread_group.g5.object_id
    azuread_authentication_only = true
  }
  identity {
    type = "SystemAssigned"
  }
}

# Role Assignments. Requires "User Access Administrator" role on target resource

resource "azurerm_role_assignment" "sqlsrv_admin" {
  role_definition_name = "Contributor"
  principal_id         = data.azuread_group.g5.id
  scope                = azurerm_mssql_server.sqlsrv.id
}

resource "azurerm_mssql_firewall_rule" "sqlsrv_firewall_rule" {
  name             = "fw-${var.org}-${var.app}-sqlsrv-allow-${var.env}"
  server_id        = azurerm_mssql_server.sqlsrv.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}

# Assign SQL Server roles inside SQL Server by using SQL script.

resource "null_resource" "assign_sql_server_roles" {
  provisioner "local-exec" {
    command     = <<-EOT
    $tokenResponse = az account get-access-token --resource https://database.windows.net/ --output json
    $token = ($tokenResponse | ConvertFrom-Json).accessToken
    $serverName = "${azurerm_mssql_server.sqlsrv.fully_qualified_domain_name}"
    $sqlCommandText = Get-Content "script3_sql_server_roles.sql" -Raw
    Invoke-Sqlcmd -ServerInstance $serverName -Database 'master' -AccessToken $token -Query $sqlCommandText
    EOT
    interpreter = ["PowerShell", "-Command"]
  }
  depends_on = [
    azurerm_mssql_server.sqlsrv
  ]
}
