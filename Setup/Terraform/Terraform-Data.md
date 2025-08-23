# Terraform Data Project

Core infrastructure setup including Azure AD groups, Key Vault, SQL Server, and Database.

## Prerequisites

📋 **[Setup Terraform Tools](step1_setup_terraform_tools.md)** - Install PowerShell 7, Azure CLI, Terraform, and required modules

📋 **[Switch Owning Service Principal](step3_switch_owning_service_principal.md)** - Configure the service principal needed to run Terraform scripts

📋 **[General Terraform Commands](step4_terraform_commands.md)** - Common Terraform commands for all projects

## Terraform Files

- **main.tf**: Defines the required providers and versions, configuration of Microsoft Azure Resource Manager and Active Directory providers, and sets up necessary data sources to get client configurations and resource group information.
- **main.yml**: Contains Azure pipeline configuration for automating the installation of PowerShell modules and running Terraform commands.
- **ReadMe.md**: Provides installation instructions for the setup, including pre-requisites, step-by-step setup processes, and additional help resources.
- **script1_groups.tf**: Sets up Azure AD groups with specified display names and security-enabled settings.
- **script2_key_vault.tf**: Contains definitions to create an Azure Key Vault, assign roles to different AI risk level groups, and manage Key Vault secrets using provided variables.
- **script3_sql_server.tf**: Defines the setup for creating an Azure SQL Server, assignment of a role for Azure Active Directory administrator, and a firewall rule. Also includes the setup for assigning SQL Server roles using a SQL script.
- **script3_sql_server_roles.sql**: SQL script to create logins for different AI risk levels on the SQL Server using Azure Active Directory identities.
- **script4_sql_database.tf**: Sets up an Azure SQL Database, including its basic configuration and assigns SQL Database roles through a PowerShell script.
- **script4_sql_database_roles.sql**: SQL script to create users in the SQL database and assign them appropriate database roles based on AI risk levels.
- **variables.tf**: Defines Terraform variables for resource naming conventions and API keys.
- **variables.env.tfvars**: Specifies actual values for Terraform variables, including organization, environment, API keys, and resource group name.

## Register Jocys.com AI Companion Application

You can register 'Jocys.com AI Companion Application' on your own domain, which will give you more granular control over access.

- Go to the **Azure Portal**.
- Navigate to **Azure Active Directory**.
- Click on **App registrations**.
- Click **[New registration]** button.
- Enter a name for the application (e.g., 'Jocys.com AI Companion').
- Select the appropriate **Supported account types** (typically 'Accounts in this organizational directory only').
- Enter a **Redirect URI** if required (usually needed for web apps).
- Click **[Register]** button.

### Add API Permissions to Application

To enable access to the optional cloud resources that could be used by the application, you must grant the necessary permissions to access Microsoft Account (`https://graph.microsoft.com/.default`), Key Vaults (`https://vault.azure.net/.default`), and Azure SQL Database (`https://database.windows.net/.default`).

- Go to the **Azure Portal**.
- Navigate to **Azure Active Directory** > **App registrations**.
- Select your application ('Jocys.com AI Companion').
- Go to the **API permissions** section tab.

#### Add Ability to Login with Microsoft Account

- Click on **[Add a permission]** button.
- Select **Microsoft Graph**.
- Choose **Delegated permissions**.
- Search for and select **User.Read**.
- Click **[Add permissions]** button.
- If you have administrative privileges, click **[Grant admin consent for [Your Tenant]]** button.  
  If you do not have administrative privileges, request an admin to grant consent or ask for the necessary permissions.

#### Add Ability to Access Secret Values from Key Vault (API Keys)

- Click on **[Add a permission]** button.
- Select **APIs my organization uses**.
- Search for and select **Azure Key Vault**.
- Choose **Delegated permissions**.
- Search for and select **user_impersonation**.
- Click **[Add permissions]** button.
- If you have administrative privileges, click **[Grant admin consent for [Your Tenant]]** button.  
  If you do not have administrative privileges, request an admin to grant consent or ask for the necessary permissions.

#### Add Ability to Access Azure SQL Database

- Click on **[Add a permission]** button.
- Select **APIs my organization uses**.
- Search for and select **Azure SQL Database**.
- Choose **Delegated permissions**.
- Search for and select **user_impersonation**.
- Click **[Add permissions]** button.
- If you have administrative privileges, click **[Grant admin consent for [Your Tenant]]** button.  
  If you do not have administrative privileges, request an admin to grant consent or ask for the necessary permissions.
