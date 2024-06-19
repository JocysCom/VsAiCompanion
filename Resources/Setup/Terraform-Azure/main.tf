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
}

# Configure the Microsoft Azure Resource Manager provider
provider "azurerm" {
  features {}
  # Optionally: configure authentication details
}

# Configure the Microsoft Azure Active Directory provider
provider "azuread" {
  # Optionally: configure authentication details
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

# Azure AD user data source
data "azuread_user" "admin_user" {
  user_principal_name = data.external.user_principal_name.result.userPrincipalName
}
