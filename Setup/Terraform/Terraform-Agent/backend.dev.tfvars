# Example file for development environment.
# Terraform's backend configuration determines where the state file is stored.
# By default, Terraform stores the state file locally.
# Azure is one of the common backends used for storing Terraform state files.
# https://staicompdevwestus001.blob.core.windows.net/tfstate/terraform.tfstate
tenant_id            = "e9207a28-f9d4-40a1-bb20-41ea135f3960" # Tenant ID: jocyscom.onmicrosoft.com
subscription_id      = "62ccac6e-0f7c-49f1-8d76-6e1664f60ad8" # Subscription ID: sub-dev-001
resource_group_name  = "rg-jocyscom-aicomp-dev-westus-001"
storage_account_name = "staicompdevwestus001"
container_name       = "tfstate"
key                  = "ai_agent.terraform.tfstate"
