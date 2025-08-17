#  AI Agent Service Principal for Azure DevOps and n8n

## Purpose

- Create an Ai Agent service principal for AI agent automation in Azure DevOps workflows.
- Configure n8n OAuth2 credential for AI agent using client credentials flow (app-only authentication).
- Provide alternative to Personal Access Tokens (PAT) when full user account privileges are not needed.

## Context

- **AI Agent Identity**: Evo Jocys (AI agent for automation)
- **Service Principal**: AI Agent (for automated workflows with limited scope)
- **Use Case**: n8n workflows where the AI agent performs automated tasks without requiring full user privileges

## Scope and constraints

- This guide creates an Evo-specific service principal with client credentials flow for app-only authentication.
- No user interaction required after initial setup.
- Uses Azure Resource Manager APIs to access Azure DevOps resources via service principal.
- Complements the normal Evo user account by providing scoped access for automation.

## Definitions

- Service principal: Microsoft Entra application identity representing the AI Agent for automated authentication.
- Client credentials flow: OAuth2 flow that allows the AI Agent to authenticate using its own credentials without user interaction.
- App-only authentication: Authentication method where the AI Agent acts on its own behalf, not on behalf of a user.

## Prerequisites

- Microsoft Entra ID Administrator access.
- Azure DevOps Organization Administrator access.
- n8n public base URL (this document uses <https://n8n.example.com>).

### Steps Requiring Global Administrator

❌ **Step 1**: Create AI Agent service principal

- **Issue**: Creating app registrations requires **Application Administrator** or **Global Administrator** role
- **Required Permission**: `microsoft.directory/applications/create`
- **Your Current Access**: Only subscription-level roles (no Microsoft Entra ID admin roles)

❌ **Step 1 (API Permissions)**: Grant admin consent for Azure DevOps API

- **Issue**: Granting tenant-wide admin consent requires **Global Administrator** or **Privileged Role Administrator**
- **Required Permission**: Ability to grant admin consent for delegated permissions
- **Your Current Access**: No Microsoft Entra ID admin roles

**Alternative Approach**:
Request a Global Administrator to complete Step 1.

## Overview

### Steps

- **Step 1**: Create AI Agent service principal in Microsoft Entra (**Requires Global Admin**)
- **Step 2**: Configure Azure DevOps service connection with the Evo service principal
- **Step 3**: Configure n8n Microsoft Entra ID credential for AI Agent client credentials flow
- **Step 4**: Update HTTP Request nodes to use Evo OAuth2 credential and verify

## Step 1 — Create AI Agent service principal

1. In Azure Portal: Microsoft Entra ID → App registrations → New registration.
2. Name: AI Agent
3. Supported account types: Accounts in this organizational directory only
4. Redirect URI: Leave blank (not needed for client credentials flow)
5. Register the application.
6. In the registered app → Certificates & secrets → New client secret.
7. Description: AI Agent - Azure DevOps API Access
8. Expires: Choose appropriate expiration (12-24 months recommended).
9. Add and copy the secret VALUE immediately (shown only once).
10. Go to API permissions → Add a permission → APIs my organization uses → Search "Azure DevOps" → Select "Azure DevOps" → Application permissions → user_impersonation.
11. Grant admin consent for your organization.

### New variables available after Step 1

- {tenant_id} — Contoso Microsoft Entra tenant ID
- {ai_agent_client_id} — Application (client) ID of the AI Agent service principal. Find it in Azure Portal: App registrations → select "AI Agent" → Application (client) ID.
- {ai_agent_client_secret} — Client secret VALUE created for AI Agent in Step 1. Copy the VALUE immediately when created; it is shown only once.
- {Evo_object_id} — Object ID of the AI Agent service principal. Find it in Azure Portal: App registrations → select "AI Agent" → Overview → Managed application in local directory (click the link) → Object ID.

## Step 2 — Configure Azure DevOps service connection for AI Agent

1. In Azure DevOps: Project settings → Service connections → New service connection.
2. Select "Azure Resource Manager" → Next.
3. Authentication method: Service principal (manual).
4. Scope level: Subscription or Management Group.
5. Fill in the AI Agent service principal details:
   - Subscription ID: Choose appropriate subscription
   - Subscription Name: Choose corresponding subscription name from above
   - Service Principal Id: {ai_client_id}
   - Service principal key: {ai_client_secret}
   - Tenant ID.
6. Service connection name: AI Agent Service Connection
7. Grant access permission to all pipelines: Check if needed.
8. Verify and save.

Alternatively, grant the AI Agent service principal direct permissions in Azure DevOps:

1. In Azure DevOps: Organization settings → Permissions.
2. Add the AI Agent service principal using its Object ID: {agent_object_id}.
3. Assign the following permissions:
   - **Work Items (read)**: Read work items, work item queries, and work item revisions
   - **Work Items (write)**: Read and write work items (if workflow updates work items)
   - **Project and Team (read)**: Access project information
   - **Version Control (read)**: Access repository information (if analyzing code repositories)

### New variables available after Step 2

- {azure_subscription_id} — Choose the appropriate Azure subscription ID from Step 2 based on your environment.
- {devops_organization} — Azure DevOps organization name: `JocysCom`
- {devops_project} — Your Azure DevOps project name (example: `VsCompanion`).

## Step 3 — Create AI Agent OAuth2 credential in n8n

1. In n8n: Credentials → New → Microsoft Entra ID (Azure Active Directory) API.
2. Authentication method: Service Principal (Client Credentials).
3. Populate fields:

- Name: AI Agent - Azure DevOps
- Grant Type: Client Credentials
- Authorization URL: <https://login.microsoftonline.com/{tenant_id}/oauth2/v2.0/authorize>
- Access Token URL: <https://login.microsoftonline.com/{tenant_id}/oauth2/v2.0/token>
- Client ID: {ai_agent_client_id}
- Client Secret: {ai_agent_client_secret}
- Scope: <https://management.azure.com/.default>
- Resource: <https://management.azure.com/>

**Note**: We use Azure Resource Manager endpoints because Azure DevOps REST APIs are accessible through ARM when using service principal authentication.

## Step 4 — Update HTTP Request nodes for AI Agent

1. For each Azure DevOps HTTP Request node in your n8n workflows:

- Open node settings.
- Change Authentication to OAuth2.
- Select the "AI Agent - Azure DevOps" OAuth2 credential.
- Update the URL to use Azure Resource Manager format:
  - **Old format**: `https://dev.azure.com/{organization}/{project}/_apis/wit/workitems/{id}`
  - **New format**: `https://management.azure.com/subscriptions/{azure_subscription_id}/resourceGroups/{resource_group}/providers/Microsoft.VisualStudio/accounts/{devops_organization}/projects/{devops_project}/workitems/{id}?api-version=2019-06-01-preview`
- Remove manual Authorization headers.
- Keep necessary headers such as Accept: application/json.
- Test-run the node; expect HTTP 200 responses.

## Alternative approach: PAT with Evo user account

If the above ARM approach doesn't work for your specific APIs, you can use a Personal Access Token (PAT) from the Evo user account:

1. Sign in to Azure DevOps using the normal Evo user account (<Evo@jocys.com>).
2. Create a PAT with the required scopes for the n8n workflow.
3. Use Basic authentication in n8n with the PAT.

**Note**: This approach uses the full user account privileges instead of the scoped service principal approach. Use this only when the service principal method doesn't support the required APIs.

## Troubleshooting

- 401 Unauthorized during token exchange:
  - Ensure the AI Agent service principal has been granted admin consent for Azure DevOps API permissions.
  - Confirm Evo Client ID, Client Secret, and Tenant ID are correct.
  - Verify the scope is set to <https://management.azure.com/.default>.

- 403 Forbidden when accessing Azure DevOps APIs:
  - Ensure the AI Agent service principal has been added to Azure DevOps with appropriate permissions.
  - Verify the Azure Resource Manager API endpoints are correct.
  - Check that the AI Agent service principal has permissions on the Azure subscription containing the Azure DevOps organization.

- Resource not found errors:
  - Verify the Azure subscription ID, resource group, and Azure DevOps organization names are correct in the URL.
  - Ensure the Azure DevOps organization is properly linked to your Azure subscription.

## Security notes

- Use the dedicated AI Agent service principal for Azure DevOps automation following your organization's security policies.
- Store Evo client secrets securely and rotate them before expiration.
- Grant only minimal required permissions to the AI Agent service principal.
- Monitor AI Agent service principal usage through Azure audit logs.
- Consider using certificate-based authentication instead of client secrets for enhanced security.

## Limitations

- Some Azure DevOps REST APIs may not be fully supported through Azure Resource Manager endpoints.
- The URL format is more complex than direct Azure DevOps REST API calls.
- May require additional Azure subscription permissions beyond Azure DevOps organization permissions.

**Note**: If this approach doesn't meet your needs, consider using Azure DevOps REST APIs directly with Personal Access Tokens (PATs) from the Evo user account (<evo@jocys.com>) as a fallback, though this won't use the scoped service principal authentication.
