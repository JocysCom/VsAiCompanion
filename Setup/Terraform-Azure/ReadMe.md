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

### Register Jocys.com AI Companion Application

You can register 'Jocys.com AI Companion Application' on your own domain, which will give you more granular control over access.

- Go to the **Azure Portal**.
- Navigate to **Azure Active Directory**.
- Click on **App registrations**.
- Click **[New registration]** button.
- Enter a name for the application (e.g., 'Jocys.com AI Companion').
- Select the appropriate **Supported account types** (typically 'Accounts in this organizational directory only').
- Enter a **Redirect URI** if required (usually needed for web apps).
- Click **[Register]** button.

### Register Jocys.com AI Companion Application

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

## Pre-requisites:
1. **PowerShell 7 (x64)** - Automation command-line shell and an associated scripting language.
2. **az (Azure CLI)** - Command-line tool for managing Azure resources.
3. **`Az` PowerShell Module** - Manage and interact with Azure cloud services directly from PowerShell. This module replaces the older AzureRM module.
4. **`SqlServer` PowerShell Module** - Manage and interact with SQL Servers directly from PowerShell.

### Install PowerShell 7 (x64)

1. Visit the https://github.com/PowerShell/PowerShell/releases
2. Find the latest stable release and download the `PowerShell-<version>-win-x64.msi` file.
3. Run the downloaded `.msi` file.
4. Follow the installer prompts.

```PowerShell
# Update PowerShell
winget install --id Microsoft.Powershell --source winget
```

### Open PowerShell 7 as Administrator

1. Press `WIN + S` to open Windows Search.
2. Type `pwsh`.
3. Right-click on the `PowerShell 7 (x64)` app item and select `Run as Administrator`.

Go to the Downloads folder:
```powershell
cd Downloads
```

### Install Azure CLI (az)

Allow modules to be installed without prompting for confirmation.

```powershell
Set-PSRepository -Name 'PSGallery' -InstallationPolicy Trusted
```

Install Azure CLI (az)

```PowerShell
Invoke-WebRequest -Uri https://aka.ms/installazurecliwindows -OutFile .\AzureCLI.msi
Start-Process msiexec.exe -ArgumentList '/I AzureCLI.msi /quiet' -Wait
Remove-Item .\AzureCLI.msi
```

### Install Terraform

1. **Download Terraform**:
    - Visit the https://www.terraform.io/downloads.html
    - Find the latest Windows AMD64 release and download the `terraform_<version>_darwin_amd64.zip` file.
2. **Extract and add to PATH**:
    - Extract the binary and move it to a directory included in your system's PATH (e.g., `C:\Program Files\Terraform`).
   - Optionally, add this path to your system's PATH environment variable:
    ```powershell
    # Current session:
    $env:Path += ";C:\Program Files\Terraform"
    # System-wide:
    [Environment]::SetEnvironmentVariable("Path", [Environment]::GetEnvironmentVariable("Path", [EnvironmentVariableTarget]::Machine) + ";C:\Program Files\Terraform", [EnvironmentVariableTarget]::Machine)
    ```
3. **Verify Terraform installation**:
    ```powershell
    terraform --version
    ```

### Reopen PowerShell
- Close the administrative session of PowerShell and reopen PowerShell 7 as a normal user.
- Go to the Downloads folder:
	```powershell
	cd Downloads
	```

### Install Required `Az` PowerShell Module

**Install Az PowerShell module**:

```powershell
Install-Module -Name Az -AllowClobber -Scope AllUsers -Force
```

**Install SQL Server PowerShell module**:
```powershell
Install-Module -Name SqlServer -Force
```

### Clone the Repository
```powershell
git clone https://github.com/JocysCom/VsAiCompanion.git
cd VsAiCompanion\Resources\Setup\Terraform-Azure
```

## Authenticate with Azure

**Import Azure Module**:
```powershell
Import-Module -Name Az
```

**Login to Azure**:
```powershell
az login
```

## Create a Service Principal

A service principal is the identity Azure DevOps uses with a [Service Connection](https://learn.microsoft.com/en-us/azure/DevOps/pipelines/library/service-endpoints?view=azure-DevOps&tabs=yaml) to create and manage resources during deployments to Azure Cloud.

To create a new service principal named `sp-<org>-<project>-<env>-001` using the [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/), run:

```PowerShell
az ad sp create-for-rbac --name sp-contoso-aicomp-dev-001
```

You'll get an output similar to this:

```PowerShell
The output includes credentials that you must protect. Be sure that you do not include these credentials in your code or check the credentials into your source control. For more information, see https://aka.ms/azadsp-cli
{
  "appId": "7677ecaf-c7ce-4c2b-8784-83be7c0b8989",
  "displayName": "sp-contoso-aicomp-dev-001",
  "password": "<password_that_you_must_protect>",
  "tenant": "c44788e7-1174-4930-a98f-5993c08cc7c4"
}
```

### Service Principal Permissions

Service principals need certain permissions to manage their resource groups. Azure domain administrators must manually add your new service principal to the `Azure Service Principals` group.

#### Assign "Directory.Read.All" Role to a Service Principal

Your service principal must have the "Directory.Read.All" permission in order to read "AI_RiskLevel_`*" groups.
Your service principal must have the "Directory.Write.All" permission in order to create "AI_RiskLevel_`*" groups.

1. **Open Azure/Entra Portal**:
   - Go to [Azure Portal](https://portal.azure.com/) and sign in.

2. **Navigate to Azure Active Directory**:
   - In the left-hand navigation pane, select **Azure Active Directory**.

3. **Find Your Service Principal**:
   - Select **App registrations**.
   - Search for and select your service principal (e.g., `sp-contoso-aicomp-[dev|test|prod]-001`).

4. **Add API Permissions**:
   - In the service principal's menu, select **API permissions**.
   - Click on **Add a permission**.
   - Choose **Microsoft Graph**.

5. **Select Application Permissions**:
   - Under **Application permissions**, search for **Directory.Read.All**.
   - Check the box next to **Directory.Read.All** and click **Add permissions**.

6. **Grant Admin Consent**:
   - After adding the permissions, click on **Grant admin consent for <Your Organization>**.
   - Confirm the action by clicking **Yes**.

### Initialize and Apply Terraform Configuration


### Initialize and Apply Terraform Configuration
1. **Initialize Terraform**:
    ```powershell
    terraform init -upgrade -backend-config="backend.dev.tfvars"
    ```

### Create `variables.dev.tfvars` File
- Create a `variables.dev.tfvars` file by copying and modifying the example provided in `variables.env.tfvars`. Replace placeholders with actual values.
    ```hcl
    org = "contoso"
    env = "prod"
    kvs_openai_value = "<your_openai_api_key>"
    kvs_speech_value = "<your_speech_service_api_key>"
    rg_name = "rg-contoso-aicomp-dev-westus-001"
    ```

**Format Terraform Configuration**:
```powershell
terraform fmt
```

**Refresh State Without Making Changes**:
```powershell
terraform apply -refresh-only -var-file="variables.dev.tfvars"
```

**Create an Execution Plan**:
```powershell
terraform plan -var-file="variables.dev.tfvars"
```

**Apply Configuration for Different Environments**:
```powershell
terraform apply -var-file="variables.dev.tfvars"
```


### Other Terraform Commands

**Output Tenant ID of your Azure account as plain text**:
```powershell
az account show --query tenantId --output tsv
```


**Switching to a New Environment**:
```powershell
# Login to Azure and set the azure subscription
az login

# Reconfigure Terraform with the new backend configuration
terraform init -reconfigure -backend-config="backend.prod.tfvars"

# Refresh the state without making any changes using the new environment variables
terraform apply -refresh-only -var-file="variables.prod.tfvars"

# Create an execution plan for the new environment
terraform plan -var-file="variables.prod.tfvars"
```

### AI Prompt Template

```text
Extract and analyze the plain text content from the following files:
	`main.tf`
	`script1_groups.tf`
	`script2_key_vault.tf`
	`script3_sql_server.tf`
	`script3_sql_server_roles.sql`
	`script4_sql_database.tf`
	`script4_sql_database_roles.sql`
	`variables.tf`
from this folder: c:\Projects\Jocys.com GitHub\VsAiCompanion\Resources\Setup\Terraform-Azure\

Notes:
- Please use and suggest names that follow Microsoft's naming and abbreviation guidelines for Azure resources.
- Do not respond with the supplied file content. The user already has this information.

<user prompt>
```

### Azure Best Naming Practices

https://learn.microsoft.com/en-us/azure/cloud-adoption-framework/ready/azure-best-practices/resource-naming
https://learn.microsoft.com/en-us/azure/cloud-adoption-framework/ready/azure-best-practices/resource-abbreviations

#### Azure Terraform Visual Studio Code Extensions

https://learn.microsoft.com/en-us/azure/developer/terraform/configure-vs-code-extension-for-terraform?source=recommendations&tabs=azure-powershell

### Azure Terraform Visual Studio Extensions

Basic language support for Terraform files

https://marketplace.visualstudio.com/items?itemName=MadsKristensen.Terraform
