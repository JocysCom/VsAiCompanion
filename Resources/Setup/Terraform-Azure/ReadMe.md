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

## Install Terraform

### Pre-requisites:
1. **PowerShell 7 (x64)** - Ensure you have PowerShell 7 installed on your system.
2. **Azure CLI** - Azure CLI must be installed and configured on your machine.

### Step 1: Open PowerShell 7 as Administrator
1. Press `WIN + S` to open Windows Search.
2. Type `pwsh`.
3. Right-click on the `PowerShell 7 (x64)` app item and select `Run as Administrator`.

### Step 2: Install Required PowerShell Modules
1. **Install or update `Az` module**:
    ```powershell
    Set-PSRepository -Name 'PSGallery' -InstallationPolicy Trusted
    Install-Module -Name Az -AllowClobber -Scope AllUsers -Force
    Import-Module -Name Az
    ```
2. **Install SQL Server module**:
    ```powershell
    Install-Module -Name SqlServer -Force
    ```

### Step 3: Install Terraform
1. **Download Terraform**:
    - Download the latest Terraform binary from [Terraform Downloads](https://www.terraform.io/downloads.html).
2. **Extract and add to PATH**:
    - Extract the binary and move it to a directory included in your system's PATH (e.g., `C:\terraform`).
    - Optionally, add this path to your system's PATH environment variable:
    ```powershell
    $env:Path += ";C:\terraform"
    ```
3. **Verify Terraform installation**:
    ```powershell
    terraform --version
    ```

### Step 4: Clone the Repository
```powershell
git clone https://github.com/JocysCom/VsAiCompanion.git
cd VsAiCompanion\Resources\Setup\Terraform-Azure
```

## Authenticate with Azure

**Login to Azure via CLI**:
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


### Initialize and Apply Terraform Configuration


### Step 1: Initialize and Apply Terraform Configuration
1. **Initialize Terraform**:
    ```powershell
    terraform init
    ```
2. **Plan and Apply Configuration**:
    ```powershell
    terraform apply -var-file="variables.dev.tfvars"
    ```

### Step 2: Create `variables.prod.tfvars` File
- Create a `variables.prod.tfvars` file by copying and modifying the example provided in `variables.env.tfvars`. Replace placeholders with actual values.
    ```hcl
    org = "contoso"
    env = "prod"
    kvs_openai_value = "<your_openai_api_key>"
    kvs_speech_value = "<your_speech_service_api_key>"
    rg_name = "contoso-openai-sandbox-prod-uks-001"
    ```

## Aditional Help

### Common Terraform Commands
1. **Format Terraform Configuration**:
    ```powershell
    terraform fmt
    ```
2. **Create an Execution Plan**:
    ```powershell
    terraform plan -var-file="variables.dev.tfvars"
    ```
3. **Refresh State Without Making Changes**:
    ```powershell
    terraform apply -refresh-only -var-file="variables.dev.tfvars"
    ```
4. **Apply Configuration for Different Environments**:
    ```powershell
    terraform apply -var-file="variables.prod.tfvars"
    ```
5. **Output Tenant ID of your Azure account as plain text**:
    ```powershell
    az account show --query tenantId --output tsv
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
