# Create primary identity that SQL Server will use to identify.
resource "azurerm_user_assigned_identity" "sql_identity" {
  name                = "identity-${var.org}-${var.app}-${var.env}-sql"
  resource_group_name = data.azurerm_resource_group.rg.name
  location            = data.azurerm_resource_group.rg.location
}

# Create SQL Server
resource "azurerm_mssql_server" "sqlsrv" {
  name                = "sql-${var.org}-${var.app}-${var.env}"
  resource_group_name = data.azurerm_resource_group.rg.name
  location            = data.azurerm_resource_group.rg.location
  version             = "12.0"
  # Azure AD Administrator for the SQL Server. Use current user.
  azuread_administrator {
    login_username = data.azuread_service_principal.sp_admin.display_name
    object_id      = data.azuread_service_principal.sp_admin.object_id
    #login_username = data.azuread_user.admin_user.user_principal_name
    #object_id      = data.azuread_user.admin_user.object_id
    #login_username              = data.azuread_group.g5.display_name
    #object_id                   = data.azuread_group.g5.object_id
    azuread_authentication_only = true
  }
  identity {
    type = "UserAssigned"
    identity_ids = [
      azurerm_user_assigned_identity.sql_identity.id
    ]
  }
  primary_user_assigned_identity_id = azurerm_user_assigned_identity.sql_identity.id
}

#-------------------------------------------------------------

# Create an Azure Role Assignment (RBAC) for the SQL managed identity with the role "Reader" on the SQL Server. 
# Reader - View all resources, but does not allow you to make any changes.
resource "azurerm_role_assignment" "sql_identity_reader" {
  scope                = azurerm_mssql_server.sqlsrv.id
  role_definition_name = "Reader"
  principal_id         = azurerm_user_assigned_identity.sql_identity.principal_id
}

#-------------------------------------------------------------

# Allow SQL Server identity to Access Entra information.

# Obtain Well-Known Application IDs for Microsoft Graph
data "azuread_application_published_app_ids" "well_known" {}

# Get the Microsoft Graph Service Principal
data "azuread_service_principal" "msgraph" {
  client_id = data.azuread_application_published_app_ids.well_known.result.MicrosoftGraph
}

# Create Service Principal for Managed Identity
resource "azuread_service_principal" "sql_identity_sp" {
  client_id = azurerm_user_assigned_identity.sql_identity.client_id
}

#https://registry.terraform.io/providers/hashicorp/azuread/latest/docs/resources/app_role_assignment

# User.Read.All: Allows access to Microsoft Entra user information.
resource "azuread_app_role_assignment" "sql_identity_user_reader" {
  app_role_id         = data.azuread_service_principal.msgraph.app_role_ids["User.Read.All"]
  principal_object_id = azuread_service_principal.sql_identity_sp.object_id
  resource_object_id  = data.azuread_service_principal.msgraph.object_id
}

# GroupMember.Read.All: Allows access to Microsoft Entra group information.
resource "azuread_app_role_assignment" "sql_identity_group_reader" {
  app_role_id         = data.azuread_service_principal.msgraph.app_role_ids["GroupMember.Read.All"]
  principal_object_id = azuread_service_principal.sql_identity_sp.object_id
  resource_object_id  = data.azuread_service_principal.msgraph.object_id
}

# Application.Read.ALL: Allows access to Microsoft Entra service principal (application) information.
resource "azuread_app_role_assignment" "sql_identity_app_reader" {
  app_role_id         = data.azuread_service_principal.msgraph.app_role_ids["Application.Read.All"]
  principal_object_id = azuread_service_principal.sql_identity_sp.object_id
  resource_object_id  = data.azuread_service_principal.msgraph.object_id
}

# Output the Managed Identity details
output "managed_identity_client_id" {
  value = azurerm_user_assigned_identity.sql_identity.client_id
}

output "managed_identity_principal_id" {
  value = azurerm_user_assigned_identity.sql_identity.principal_id
}

#resource "azuread_directory_role" "directory_readers" {
#  display_name = "Directory Readers"
#}

#resource "azuread_directory_role_assignment" "sql_directory_reader" {
#  role_id      = azuread_directory_role.directory_readers.template_id
#  principal_object_id = azurerm_mssql_server.sqlsrv.identity[0].principal_id
#}


# Role Assignments. Requires "User Access Administrator" role on target resource

resource "azurerm_role_assignment" "sqlsrv_admin" {
  role_definition_name = "Contributor"
  #principal_id         = data.azuread_group.g5.id
  principal_id = data.azuread_service_principal.sp_admin.id
  scope        = azurerm_mssql_server.sqlsrv.id
}


resource "azurerm_mssql_firewall_rule" "sqlsrv_firewall_rule" {
  name             = "fw-${var.org}-${var.app}-sqlsrv-allow-${var.env}"
  server_id        = azurerm_mssql_server.sqlsrv.id
  start_ip_address = data.external.external_ip.result.ip
  end_ip_address   = data.external.external_ip.result.ip
}

# Allow all domain users to connect to the SQL server. Restrict later to the organization range.
resource "azurerm_mssql_firewall_rule" "sqlsrv_firewall_rule_all" {
  name             = "fw-${var.org}-${var.app}-sqlsrv-allow-all-${var.env}"
  server_id        = azurerm_mssql_server.sqlsrv.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "255.255.255.255"
}

# Assign SQL Server roles inside SQL Server by using SQL script.

resource "null_resource" "assign_sql_server_roles0" {
  provisioner "local-exec" {
    command     = <<-EOT
    $token = (az account get-access-token --resource https://database.windows.net/ --query accessToken -o tsv)
    $conn = New-Object System.Data.SqlClient.SqlConnection
    $conn.ConnectionString = "Server=${azurerm_mssql_server.sqlsrv.fully_qualified_domain_name}; Database=master;"
    $conn.AccessToken = $token
    $sqlIdentity = "${azurerm_user_assigned_identity.sql_identity.name}"


    $query = @"
    CREATE USER [$sqlIdentity] FROM EXTERNAL PROVIDER;
    ALTER ROLE [db_datareader] ADD MEMBER [$sqlIdentity];
    ALTER ROLE [db_datawriter] ADD MEMBER [$sqlIdentity];
    GRANT CONNECT TO [$sqlIdentity];
    "@

    $cmd = New-Object System.Data.SqlClient.SqlCommand($query, $conn)
    $conn.Open()
    $cmd.ExecuteNonQuery()
    $conn.Close()
    EOT
    interpreter = ["PowerShell", "-Command"]
  }
  depends_on = [
    azurerm_mssql_server.sqlsrv,
  ]
}


resource "null_resource" "assign_sql_server_roles" {
  provisioner "local-exec" {
    command     = <<-EOT
    $token = $env:ARM_DATABASE_ACCESS_TOKEN
    $serverName = "${azurerm_mssql_server.sqlsrv.fully_qualified_domain_name}"
    $sqlCommandText = Get-Content "script3_sql_server_roles.sql" -Raw
    Invoke-Sqlcmd -ServerInstance $serverName -Database 'master' -AccessToken $token -Query $sqlCommandText
    EOT
    interpreter = ["PowerShell", "-Command"]
  }
  depends_on = [
    azurerm_mssql_server.sqlsrv,
    #azurerm_role_assignment.sqlsrv_directory_reader
    #azuread_directory_role_assignment.sql_directory_reader
  ]
}

# Set local variable for admin emails using the service principal's information
locals {
  admin_emails = [data.azuread_service_principal.sp_admin.display_name]
}

# SQL Server Auditing Policy
# Required to PASS Tool: checkov, Rule ID: CKV_AZURE_24, 
# Description: Ensure that 'Auditing' Retention is 'greater than 90 days' for SQL servers
resource "azurerm_mssql_server_security_alert_policy" "sqlsrv_audit_policy" {
  server_name         = azurerm_mssql_server.sqlsrv.name
  resource_group_name = data.azurerm_resource_group.rg.name

  state                = "Enabled"
  email_account_admins = true

  # Rule ID: CKV_AZURE_24 - Ensure that 'Auditing' Retention is 'greater than 90 days' for SQL servers
  retention_days = 120

  # Rule ID: CKV_AZURE_26 - Ensure that 'Send Alerts To' is enabled for MSSQL servers
  email_addresses = ["user@localhost.lan"]

  # Define where to send audit logs
  storage_account_access_key = data.azurerm_storage_account.storage_account.primary_access_key
  storage_endpoint           = data.azurerm_storage_account.storage_account.primary_blob_endpoint
}

#$tokenResponse = az account get-access-token --resource https://database.windows.net/ --output json
#$token = ($tokenResponse | ConvertFrom-Json).accessToken
