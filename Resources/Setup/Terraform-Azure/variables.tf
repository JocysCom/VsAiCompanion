
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
  default     = "aicomp"
}

variable "env" {
  description = "The environment for the deployment (e.g., dev, test, prod)"
  type        = string
  default     = "dev"
}

# API Keys

variable "kvs_openai_value" {
  description = "The value for the OpenAI API Key secret"
  type        = string
  sensitive   = true
  default     = "<Your-OpenAI-API-Key>"
}

variable "kvs_speech_value" {
  description = "The value for the Speech Service API Key secret"
  type        = string
  sensitive   = true
  default     = "<Your-Speech-Service-API-Key>"
}

variable "rg_name" {
  description = "The value for the default resource group name"
  type        = string
  sensitive   = true
  default     = "contoso-rg-dev-openai-uswest-001"
}