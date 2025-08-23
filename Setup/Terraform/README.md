# Azure Terraform Infrastructure Setup

# Files

- **step1_{step_name}.ps1** - Dployment steps to follow in order.
- **Terraform-{project_name}.md** - Terraform project.
- **shared_terraform_functions.ps1** - Common PowerShell functions.

## General Terraform Commands

Common Terraform commands that apply to all projects.

### Initialize and Apply Terraform Configuration

#### Initialize Terraform

Initialize Terraform with backend configuration for your environment:

```powershell
# Development environment
terraform init -upgrade -backend-config="backend.dev.tfvars"

# Production environment  
terraform init -upgrade -backend-config="backend.prod.tfvars"
```

#### Create Variables File

Create a `variables.dev.tfvars` file by copying and modifying the example provided in `variables.env.tfvars`. Replace placeholders with actual values.

```hcl
org = "contoso"
env = "prod"
kvs_openai_value = "<your_openai_api_key>"
kvs_speech_value = "<your_speech_service_api_key>"
rg_name = "rg-contoso-aicomp-dev-westus-001"
```

#### Format Terraform Configuration

```powershell
terraform fmt
```

#### Refresh State Without Making Changes

```powershell
terraform apply -refresh-only -var-file="variables.dev.tfvars"
```

#### Create an Execution Plan

```powershell
terraform plan -var-file="variables.dev.tfvars"
```

#### Apply Configuration

```powershell
terraform apply -var-file="variables.dev.tfvars"
```

### Switching to a New Environment

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

### Other Useful Commands

**Output Tenant ID of your Azure account as plain text**:

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

<https://learn.microsoft.com/en-us/azure/cloud-adoption-framework/ready/azure-best-practices/resource-naming>
<https://learn.microsoft.com/en-us/azure/cloud-adoption-framework/ready/azure-best-practices/resource-abbreviations>

#### Azure Terraform Visual Studio Code Extensions

<https://learn.microsoft.com/en-us/azure/developer/terraform/configure-vs-code-extension-for-terraform?source=recommendations&tabs=azure-powershell>

#### Azure Terraform Visual Studio Extensions

Basic language support for Terraform files

<https://marketplace.visualstudio.com/items?itemName=MadsKristensen.Terraform>
