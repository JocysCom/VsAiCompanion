# This file will contain the actual values for your variables and should not be checked into source control.

# To refresh the state without making any changes:
# terraform apply -refresh-only -var-file="variables.env.tfvars"

# Company
org = "jocyscom"
# Application/Product
app = "aicomp"
# Environment
env = "prod"

# Subscription ID: sub-prod-001
subscription_id = "0d685c68-b2bb-4551-8eaa-5c9d0cce055b"
# Tenant ID: jocyscom.onmicrosoft.com
tenant_id = "e9207a28-f9d4-40a1-bb20-41ea135f3960"

location             = "westus"
resource_group_name  = "rg-jocyscom-aicomp-prod-westus-001"
storage_account_name = "staicompprodwestus001"
