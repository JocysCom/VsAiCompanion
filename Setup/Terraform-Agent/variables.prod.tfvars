# This file will contain the actual values for your variables and should not be checked into source control.

# To refresh the state without making any changes:
# terraform apply -refresh-only -var-file="variables.dev.tfvars"

# Company
org = "jocyscom"
# Application/Product
app = "aicomp"
# Environment
env = "prod"

location             = "westus"
resource_group_name  = "rg-jocyscom-aicomp-prod-westus-001"
storage_account_name = "staicompprodwestus001"
