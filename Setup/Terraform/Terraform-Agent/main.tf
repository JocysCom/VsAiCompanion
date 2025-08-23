# Terraform configuration for AI Agent Service Principal
# Based on Terraform-Companion structure for consistency

terraform {
  required_version = ">= 1.0"

  required_providers {
    azuread = {
      source  = "hashicorp/azuread"
      version = "~> 2.51"
    }
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.107"
    }
  }

  backend "azurerm" {
    # Configuration passed via `-backend-config="backend.{env}.tfvars"` in the `init` command.
  }
}

# Configure the Microsoft Azure Active Directory provider
provider "azuread" {
  tenant_id     = var.ARM_TENANT_ID
  client_id     = var.ARM_CLIENT_ID
  client_secret = var.ARM_CLIENT_SECRET
}

# Configure the Microsoft Azure Resource Manager provider
provider "azurerm" {
  features {}
  subscription_id = var.ARM_SUBSCRIPTION_ID
  tenant_id       = var.ARM_TENANT_ID
  client_id       = var.ARM_CLIENT_ID
  client_secret   = var.ARM_CLIENT_SECRET
}

# 'TF_VAR_*' Environment variables from Terraform-Data service principal
# These credentials authenticate Terraform to create the AI Agent service principal

variable "ARM_TENANT_ID" {
  description = "Directory (Tenant) ID"
  type        = string
}

variable "ARM_SUBSCRIPTION_ID" {
  description = "Subscription ID"
  type        = string
}

variable "ARM_CLIENT_ID" {
  description = "Service Principal Client ID (from Terraform-Data project)"
  type        = string
}

variable "ARM_CLIENT_SECRET" {
  description = "Service Principal Client Secret (from Terraform-Data project)"
  type        = string
}

# Data sources
data "azuread_client_config" "current" {}

# Retrieve Azure Resource Group data by name
data "azurerm_resource_group" "rg" {
  name = var.resource_group_name
}

# Azure Storage Account to store Terraform state files
data "azurerm_storage_account" "storage_account" {
  name                = var.storage_account_name
  resource_group_name = data.azurerm_resource_group.rg.name
}

