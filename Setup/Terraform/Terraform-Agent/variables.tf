
# Resource Naming
# Organization-[Department-]ResourceType-Application-Environment-[AzureRegion-][Instance]

variable "org" {
  description = "The company prefix to use for resources"
  type        = string
  default     = "contoso"
}

variable "app" {
  description = "The Application"
  type        = string
  default     = "product"
}

variable "agent_name" {
  description = "The AI agent/user name for service principal naming"
  type        = string
  default     = "agent"
}

variable "env" {
  description = "The environment for the deployment (e.g., dev, test, prod)"
  type        = string
  default     = "env"
}

variable "location" {
  description = "Location of all resources"
  type        = string
  default     = "westus"
}

variable "resource_group_name" {
  description = "The value for the default resource group name"
  type        = string
  default     = "<contoso-rg-dev-openai-westus-001>"
}

variable "storage_account_name" {
  description = "The name of the Azure Storage Account for storing TF state"
  type        = string
  default     = "<staicompdevwestus001>"
}
