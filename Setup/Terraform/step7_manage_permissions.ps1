<#
.SYNOPSIS
    This script manages API permissions for an Azure AD application.

.DESCRIPTION
    The script performs the following tasks:
    1. Logs into Azure using the Azure CLI (`az login`).
    2. Asks the user to enter the Application Client ID.
    3. Displays a menu with the following options:
        a. Show current API permissions of the application.
        b. Add missing API permissions (Azure Key Vault, Azure SQL Database, Microsoft Graph).
        c. Grant admin consent for the added permissions.
        d. Exit the script.

        The script uses Azure CLI commands and Microsoft's Graph API to manage and display the permissions.

.PARAMETER
    None.

.EXAMPLE
    To run the script, use the following command in PowerShell:

        .\Manage-AzureAppPermissions.ps1

.NOTES
    - Ensure you have the Azure CLI installed and properly configured before running the script.
    - You may need appropriate permissions to add API permissions or grant admin consent.
#>

# Function to call Azure login
function Login-Az {
    Write-Host "Logging into Azure..."
    az login
}

# Function to get the application details
function Get-AppDetails {
    param (
        [string]$AppId
    )

    $appDetails = az ad app show --id $AppId | ConvertFrom-Json
    return $appDetails
}

# Function to show current API permissions
function Show-ApiPermissions {
    param (
        [string]$AppId
    )

    # Add these lines at the beginning of the function
    $OutputEncoding = [console]::InputEncoding = [console]::OutputEncoding = New-Object System.Text.UTF8Encoding
    $PSDefaultParameterValues['*:Encoding'] = 'utf8'
    #$env:PYTHONIOENCODING = "UTF-8"

    $permissions = az ad app show --id $AppId --query "requiredResourceAccess" | ConvertFrom-Json
    foreach ($perm in $permissions) {

        # Suppresses all warnings.
        $resourceApp = az ad sp show --id $perm.resourceAppId 2>$null | ConvertFrom-Json

        # Debug: Output the entire resourceApp object
        #Write-Host "Debug: Full resourceApp object for $($perm.resourceAppId):"
        #Write-Host ($resourceApp | ConvertTo-Json -Depth 5)

        Write-Host "`nAPI Resource: $($resourceApp.displayName) (API: $($perm.resourceAppId))"

        foreach ($resAccess in $perm.resourceAccess) {
            $accessType = $resAccess.type -eq 'Role' ? 'Application' : 'Delegated'
            $accessValue = $null
            $accessDescription = $null

            # Debug: Output the current resAccess object
            #Write-Host "Debug: Current resAccess object:"
            #Write-Host ($resAccess | ConvertTo-Json)

            # Check in multiple possible locations
            $possibleLocations = @(
                'oauth2PermissionScopes',
                'oauth2Permissions',
                'appRoles'
            )

            foreach ($location in $possibleLocations) {
                if ($null -ne $resourceApp.$location) {
                    $access = $resourceApp.$location | Where-Object { $_.id -eq $resAccess.id }
                    if ($access) {
                        $accessValue = $access.value
                        $accessDescription = $access.adminConsentDescription ??
                        $access.userConsentDescription ??
                        $access.description ??
                        "No description available."
                        break
                    }
                }
            }

            # If still not found, try to find by value instead of id
            if ($null -eq $accessValue) {
                foreach ($location in $possibleLocations) {
                    if ($null -ne $resourceApp.$location) {
                        $access = $resourceApp.$location | Where-Object { $_.value -eq $resAccess.id }
                        if ($access) {
                            $accessValue = $access.value
                            $accessDescription = $access.adminConsentDescription ??
                            $access.userConsentDescription ??
                            $access.description ??
                            "No description available."
                            break
                        }
                    }
                }
            }

            # Make sure accessValue is not empty
            if (-not $accessValue) {
                $accessValue = "Unknown"
            }

            Write-Host "  Permission: $accessValue (Type: $accessType)"
            Write-Host "  Description: $accessDescription"
            # Output the results
            Write-Host "  Add Command: az ad app permission add \"
            Write-Host "    --id $AppId \ # Application (client) ID"
            Write-Host "    --api $($perm.resourceAppId) \ # Resource ID"
            Write-Host "    --api-permissions $($resAccess.id)=$($resAccess.type) # Permission ID = Type"
            Write-Host ""
        }
    }
}

# Function to add missing API permissions
function Add-MissingApiPermissions {
    param (
        [string]$AppId
    )

    # Get existing permissions
    $existingPermissions = az ad app show --id $AppId --query "requiredResourceAccess[].{resourceAppId:resourceAppId, resourceAccess:resourceAccess[]}" | ConvertFrom-Json

    # Permissions to add
    $permissions = @(
        @{ "api" = "00000003-0000-0000-c000-000000000000"; "permission" = "e1fe6dd8-ba31-4d61-89e7-88639da4683d"; "type" = "Scope" }  # Microsoft Graph User.Read
        @{ "api" = "cfa8b339-82a2-471a-a3c9-0fc0be7a4093"; "permission" = "f53da476-18e3-4152-8e01-aec403e6edc0"; "type" = "Scope" }  # Azure Key Vault user_impersonation
        @{ "api" = "022907d3-0f1b-48f7-badc-1ba6abab6d66"; "permission" = "c39ef2d1-04ce-46dc-8b5f-e9a5c60f0fc9"; "type" = "Scope" }  # Azure SQL DB and Data Warehouse user_impersonation
    )

    foreach ($perm in $permissions) {
        $existingPermission = $existingPermissions | Where-Object { $_.resourceAppId -eq $perm.api } |
        Select-Object -ExpandProperty resourceAccess |
        Where-Object { $_.id -eq $perm.permission }

        if (-not $existingPermission) {
            az ad app permission add --id $AppId --api $perm.api --api-permissions "$($perm.permission)=$($perm.type)"
            Write-Host "Added permission: $($perm.api) - $($perm.permission)"
        }
        else {
            Write-Host "Permission already exists: $($perm.api) - $($perm.permission)"
        }
    }
}

# Function to grant admin consent for API permissions
function Grant-AdminConsent {
    param (
        [string]$AppId
    )
    $apis = @(
        "00000003-0000-0000-c000-000000000000",  # Microsoft Graph
        "cfa8b339-82a2-471a-a3c9-0fc0be7a4093",  # Azure Key Vault
        "022907d3-0f1b-48f7-badc-1ba6abab6d66"  # Azure SQL DB and Data Warehouse
    )
    foreach ($api in $apis) {
        az ad app permission grant --id $AppId --api $api --scope "/.default"
        Write-Host "Granted admin consent for API: $api"
    }
    Write-Host "Granted admin consent for all required APIs."
}

# Function to display and handle the menu
function Show-Menu {
    param (
        [string]$AppName,
        [string]$AppId
    )

    do {
        Write-Host "`n$AppName - API Permissions:`n"
        Write-Host "  1. Show Permissions"
        Write-Host "  2. Add Missing Permissions"
        Write-Host "  3. Grant Admin Consent"
        Write-Host "  4. Exit"
        Write-Host ""
        $choice = Read-Host "Enter your choice"

        switch ($choice) {
            1 {
                Show-ApiPermissions -AppId $AppId
            }
            2 {
                Add-MissingApiPermissions -AppId $AppId
            }
            3 {
                Grant-AdminConsent -AppId $AppId
            }
            4 {
                Write-Host "Exiting..."
            }
            Default {
                Write-Host "Invalid choice, please try again."
            }
        }
    } until ($choice -eq 4)
}

# Main script execution
Login-Az

$AppId = Read-Host "Enter the Application Client ID"
$appDetails = Get-AppDetails -AppId $AppId
$appName = $appDetails.displayName

Show-Menu -AppName $appName -AppId $AppId
