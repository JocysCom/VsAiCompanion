# Example file for production environment.
# Terraform's backend configuration determines where the state file is stored.
# By default, Terraform stores the state file locally.
# Azure is one of the common backends used for storing Terraform state files.
# https://staicompprodwestus001.blob.core.windows.net/tfstate/terraform.tfstate
tenant_id            = "e9207a28-f9d4-40a1-bb20-41ea135f3960" # Tenant ID: jocyscom.onmicrosoft.com
subscription_id      = "0d685c68-b2bb-4551-8eaa-5c9d0cce055b" # Subscription ID: sub-prod-001
resource_group_name  = "rg-jocyscom-aicomp-prod-westus-001"
storage_account_name = "staicompprodwestus001"
container_name       = "tfstate"
key                  = "agent.terraform.tfstate"
