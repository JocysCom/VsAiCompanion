# This file will contain the actual values for your variables and should not be checked into source control.

# To refresh the state without making any changes:
# terraform apply -refresh-only -var-file="variables.env.tfvars"

# Company
org = "jocyscom"
# Application/Product
app = "aicomp"
# Environment
env = "dev"

# Subscription ID: sub-dev-001
subscription_id = "62ccac6e-0f7c-49f1-8d76-6e1664f60ad8"
# Tenant ID: jocyscom.onmicrosoft.com
tenant_id = "e9207a28-f9d4-40a1-bb20-41ea135f3960"

location             = "westus"
resource_group_name  = "rg-jocyscom-aicomp-dev-westus-001"
storage_account_name = "staicompdevwestus001"
