# Deploy Terraform Project via Azure DevOps Pipelines

Complete deployment process for Terraform Project with **dev and prod environments**.

## 🔧 Step 1: Configure Environment Variable Groups

Create **separate** variable groups for each environment:

### {environment} Environment Variables:

Variable Group: `Terraform-{ProjectName}-{environment}

```
armClientId: {owning_service_principal_app_id}
armClientSecret: {owning_service_principal_app_secret} (mark as secret)
armTenantId: {tenant_id}
armSubscriptionId: {subscription_id}
```

### How Variable Groups Link to Pipeline

The pipeline dynamically selects Variable groups using this YAML configuration:

```yaml
variables:
- group: 'Terraform-{ProjectName}-${{ parameters.environment }}'
```

When you run the pipeline it will ask to select environment parameter.

## 🔧 Step 2: Create Pipeline

1. **Azure DevOps** → Pipelines → New Pipeline
2. **Choose source**: Azure Repos Git / GitHub
3. **Select repository**: Your Terraform-{ProjectName} repository
4. **Configure**: Existing Azure Pipelines YAML file
5. **Select**: `/Terraform-{ProjectName}/azure-pipelines.yml`
6. **Review & Save**

## 🚀 Step 3: Environment-Specific Deployment

```
Azure DevOps → Pipelines → [Terraform-{ProjectName}] → Run Pipeline
Environment: {environment}
```

- Uses `backend.{environment}.tfvars` and `variables.{environment}.tfvars`
