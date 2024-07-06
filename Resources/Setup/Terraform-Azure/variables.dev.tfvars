# This file will contain the actual values for your variables and should not be checked into source control.

# To refresh the state without making any changes:
# terraform apply -refresh-only -var-file="variables.env.tfvars"

# Company
org = "jocyscom"
# Application/Product
app = "aicomp"
# Environment
env = "dev"

location             = "westus"
resource_group_name  = "rg-jocyscom-aicomp-dev-westus-001"
storage_account_name = "staicompdevwestus001"
