### Installation Instructions

#### Pre-requisites:
1. **PowerShell 7 (x64)** - Ensure you have PowerShell 7 installed on your system.
2. **Azure CLI** - Azure CLI must be installed and configured on your machine.

#### Step 1: Open PowerShell 7 as Administrator
1. Press `WIN + S` to open Windows Search.
2. Type `pwsh`.
3. Right-click on the `PowerShell 7 (x64)` app item and select `Run as Administrator`.

#### Step 2: Install Required PowerShell Modules
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

#### Step 3: Install Terraform
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

#### Step 4: Clone the Repository
```powershell
git clone https://github.com/JocysCom/VsAiCompanion.git
cd VsAiCompanion\Resources\Setup\Terraform-Azure
```

#### Step 5: Authenticate with Azure
1. **Login to Azure via CLI**:
    ```powershell
    az login
    ```

#### Step 6: Initialize and Apply Terraform Configuration
1. **Initialize Terraform**:
    ```powershell
    terraform init
    ```
2. **Plan and Apply Configuration**:
    ```powershell
    terraform apply -var-file="variables.dev.tfvars"
    ```

#### Step 7: Create `variables.prod.tfvars` File
- Create a `variables.prod.tfvars` file by copying and modifying the example provided in `variables.env.tfvars`. Replace placeholders with actual values.
    ```hcl
    org = "contoso"
    env = "prod"
    kvs_openai_value = "<your_openai_api_key>"
    kvs_speech_value = "<your_speech_service_api_key>"
    rg_name = "contoso-openai-sandbox-prod-uks-001"
    ```

### Aditional Help

#### Common Terraform Commands
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

#### AI Prompt Template

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

#### Azure Best Naming Practices

https://learn.microsoft.com/en-us/azure/cloud-adoption-framework/ready/azure-best-practices/resource-naming
https://learn.microsoft.com/en-us/azure/cloud-adoption-framework/ready/azure-best-practices/resource-abbreviations

#### Azure Terraform Visual Studio Code Extensions

https://learn.microsoft.com/en-us/azure/developer/terraform/configure-vs-code-extension-for-terraform?source=recommendations&tabs=azure-powershell

#### Azure Terraform Visual Studio Extensions

Basic language support for Terraform files

https://marketplace.visualstudio.com/items?itemName=MadsKristensen.Terraform