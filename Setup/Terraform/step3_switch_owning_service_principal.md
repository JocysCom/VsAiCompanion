# Owning Service Principals Setup

## Overview

A service principal is the identity Azure DevOps uses with a [Service Connection](https://learn.microsoft.com/en-us/azure/DevOps/pipelines/library/service-endpoints?view=azure-DevOps&tabs=yaml) to create and manage resources during deployments to Azure Cloud.

This owning service principal will be used to run Terraform scripts for both `Terraform-*` projects.

## Create a Service Principal

To create a new service principal named `sp-<org>-<project>-<env>-001` using the [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/), run:

```PowerShell
az ad sp create-for-rbac --name sp-contoso-aicomp-dev-001
```

You'll get an output similar to this:

```PowerShell
The output includes credentials that you must protect. Be sure that you do not include these credentials in your code or check the credentials into your source control. For more information, see https://aka.ms/azadsp-cli
{
  "appId": "7677ecaf-c7ce-4c2b-8784-83be7c0b8989",
  "displayName": "sp-contoso-aicomp-dev-001",
  "password": "<password_that_you_must_protect>",
  "tenant": "c44788e7-1174-4930-a98f-5993c08cc7c4"
}
```

## Service Principal Permissions

Service principals need certain permissions to manage their resource groups. Azure domain administrators must manually add your new service principal to the `Azure Service Principals` group.

### Assign "Directory.Read.All" Role to a Service Principal

Your service principal must have the "Directory.Read.All" permission in order to read "AI_RiskLevel_`*" groups.
Your service principal must have the "Directory.Write.All" permission in order to create "AI_RiskLevel_`*" groups.

1. **Open Azure/Entra Portal**:
   - Go to [Azure Portal](https://portal.azure.com/) and sign in.

2. **Navigate to Azure Active Directory**:
   - In the left-hand navigation pane, select **Azure Active Directory**.

3. **Find Your Service Principal**:
   - Select **App registrations**.
   - Search for and select your service principal (e.g., `sp-contoso-aicomp-[dev|test|prod]-001`).

4. **Add API Permissions**:
   - In the service principal's menu, select **API permissions**.
   - Click on **Add a permission**.
   - Choose **Microsoft Graph**.

5. **Select Application Permissions**:
   - Under **Application permissions**, search for **Directory.Read.All**.
   - Check the box next to **Directory.Read.All** and click **Add permissions**.

6. **Grant Admin Consent**:
   - After adding the permissions, click on **Grant admin consent for <Your Organization>**.
   - Confirm the action by clicking **Yes**.
