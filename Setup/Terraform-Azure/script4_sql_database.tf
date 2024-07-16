# Create SQL Database

resource "azurerm_mssql_database" "db" {
  name           = "sqldb-${var.org}-${var.app}-${var.env}"
  server_id      = azurerm_mssql_server.sqlsrv.id
  collation      = "SQL_Latin1_General_CP1_CI_AS"
  license_type   = "LicenseIncluded"
  max_size_gb    = 10
  sku_name       = "BC_Gen5_2"
  zone_redundant = false
}

resource "null_resource" "assign_sql_database_roles" {
  provisioner "local-exec" {
    command     = <<-EOT
    $token = $env:ARM_DATABASE_ACCESS_TOKEN
    $serverName = "${azurerm_mssql_server.sqlsrv.fully_qualified_domain_name}"
    $databaseName = "${azurerm_mssql_database.db.name}"
    $sqlCommandText = Get-Content "script4_sql_database_roles.sql" -Raw
    Invoke-Sqlcmd -ServerInstance $serverName -Database $databaseName -AccessToken $token -Query $sqlCommandText
    EOT
    interpreter = ["PowerShell", "-Command"]
  }
  depends_on = [
    azurerm_mssql_server.sqlsrv,
    azurerm_mssql_database.db,
    null_resource.assign_sql_server_roles0,
    null_resource.assign_sql_server_roles1
  ]
}
#$tokenResponse = az account get-access-token --resource https://database.windows.net/ --output json
#$token = ($tokenResponse | ConvertFrom-Json).accessToken
