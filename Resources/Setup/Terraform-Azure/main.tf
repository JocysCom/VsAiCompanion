# Required Provider source and version being used
# To upgrade providers: terraform init -upgrade
terraform {
  required_providers {
    # https://registry.terraform.io/providers/hashicorp/azurerm/latest
    azurerm = {
      source  = "hashicorp/azurerm"
      version = ">= 3.107.0"
    }
    # https://registry.terraform.io/providers/hashicorp/azuread/latest
    azuread = {
      source  = "hashicorp/azuread"
      version = ">= 2.51.0"
    }
  }
  backend "azurerm" {}
}

# Configure the Microsoft Azure Active Directory provider
provider "azuread" {
  # Optionally: configure authentication details
}

# Configure the Microsoft Azure Resource Manager provider
provider "azurerm" {
  features {}
  # Optionally: configure authentication details
  subscription_id = var.ARM_SUBSCRIPTION_ID
  client_id       = var.ARM_CLIENT_ID
  client_secret   = var.ARM_CLIENT_SECRET
  tenant_id       = var.ARM_TENANT_ID
}

variable "ARM_SUBSCRIPTION_ID" {
  description = "Subscription ID"
  type        = string
}

variable "ARM_CLIENT_ID" {
  description = "Service Principal Client ID"
  type        = string
}

variable "ARM_CLIENT_SECRET" {
  description = "Service Principal Client Secret"
  type        = string
}

variable "ARM_TENANT_ID" {
  description = "Tenant ID"
  type        = string
}

# Retrieve Azure Client Configuration including the Tenant ID
data "azurerm_client_config" "client_config" {}

# Retrieve Azure Resource Group data by name
data "azurerm_resource_group" "rg" {
  name = var.rg_name
}

# External data source to run the PowerShell command
data "external" "user_principal_name" {
  program = [
    "PowerShell",
    "-Command",
    "(az ad signed-in-user show --query userPrincipalName -o tsv) | % { @{userPrincipalName = $_} | ConvertTo-Json -Compress }"
  ]
}

# External IP Address.
data "external" "external_ip" {
  program = ["pwsh", "-Command", "Invoke-RestMethod -Uri http://ifconfig.me/ip | % { @{ip = $_} | ConvertTo-Json -Compress }"]
}

# Azure AD user data source
#data "azuread_user" "admin_user" {
#  user_principal_name = data.external.user_principal_name.result.userPrincipalName
#}

# Directly reference the service principal information
data "azuread_service_principal" "sp_admin" {
  client_id = var.ARM_CLIENT_ID
}
