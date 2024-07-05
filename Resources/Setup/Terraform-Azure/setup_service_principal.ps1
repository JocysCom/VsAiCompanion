# Setup Azure service principal

# Function to retrieve service principal details
function Get-ServicePrincipalDetails {
    param (
        [Parameter(Mandatory=$true)]
        [string]$spName
    )

    # Retrieve the service principal details
    $sp = az ad sp list --display-name $spName --query '[0]' | ConvertFrom-Json

    if (-not $sp) {
        Write-Error "Service Principal '$spName' not found."
        exit 1
    }

    return $sp
}

# Function to retrieve tenant ID
function Get-TenantId {
    $tenantId = az account show --query 'tenantId' -o tsv
    if (-not $tenantId) {
        Write-Error "Failed to retrieve tenant ID."
        exit 1
    }
    return $tenantId
}

# Prompt user for password
function PromptForPassword {
    param (
        [Parameter(Mandatory=$true)]
        [string]$message
    )

    $password = Read-Host -Prompt $message -AsSecureString
    $passwordPlainText = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($password))
    
    return $passwordPlainText
}

# Main script
# Ask for the service principal name
$spName = Read-Host -Prompt "Enter the name of the service principal"

try {
    # Ensure Azure CLI is installed
    az --version | Out-Null
} catch {
    Write-Error "Azure CLI is not installed. Please install Azure CLI first."
    exit 1
}

# Retrieve service principal details
$sp = Get-ServicePrincipalDetails -spName $spName

# Retrieve tenant ID
$tenantId = Get-TenantId

# Prompt for password
$password = PromptForPassword -message "Enter password for service principal '$spName'"

# Set environment variables. Use `TF_VAR_` prefix to make recognized by Terraform.
$env:TF_VAR_ARM_CLIENT_ID = $sp.appId
$env:TF_VAR_ARM_CLIENT_SECRET = $password
$env:TF_VAR_ARM_TENANT_ID = $tenantId
$env:TF_VAR_ARM_SUBSCRIPTION_ID = (az account show --query 'id' -o tsv)

$env:ARM_CLIENT_NAME = $spName
$env:ARM_CLIENT_ID = $sp.appId
$env:ARM_CLIENT_SECRET = $password
$env:ARM_TENANT_ID = $tenantId
$env:ARM_SUBSCRIPTION_ID = (az account show --query 'id' -o tsv)

Write-Output "Environment variables set successfully."
Write-Output "TF_VAR_ARM_CLIENT_ID: $($env:TF_VAR_ARM_CLIENT_ID)"
Write-Output "TF_VAR_ARM_TENANT_ID: $($env:TF_VAR_ARM_TENANT_ID)"
Write-Output "TF_VAR_ARM_SUBSCRIPTION_ID: $($env:TF_VAR_ARM_SUBSCRIPTION_ID)"
