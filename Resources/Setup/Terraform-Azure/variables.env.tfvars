# This file will contain the actual values for your variables and should not be checked into source control.

# To refresh the state without making any changes:
# terraform apply -refresh-only -var-file="variables.env.tfvars"

org = "contoso"
env = "dev"

kvs_openai_value = "<your_openai_api_key>"
kvs_speech_value = "<your_speech_service_api_key>"

rg_name              = "rg-contoso-aicomp-dev-uswest-001"
storage_account_name = "staicompdevuswest001"
