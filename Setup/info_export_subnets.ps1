# Below is an example script that retrieves all Azure Virtual Networks and their subnets,
# along with columns for IPv4 address range start and end (based on the CIDR).
# Note: Subnets often have multiple address prefixes. This script will handle each prefix
# and join them for CSV output.

function Get-IPv4Range {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Cidr
    )
    # Split the CIDR into IP and prefix length
    $parts      = $Cidr -split '/'
    $ipString   = $parts[0]
    $prefixInt  = [int]$parts[1]

    # Convert IP to 32-bit unsigned integer
    $ip = [System.Net.IPAddress]::Parse($ipString)
    $ipBytes = $ip.GetAddressBytes()
    [Array]::Reverse($ipBytes)
    $ipAsInt  = [BitConverter]::ToUInt32($ipBytes, 0)

    # Calculate the subnet mask as a 32-bit integer
    $mask = 0xFFFFFFFF - ([math]::Pow(2, (32 - $prefixInt)) - 1)

    # Calculate the network (start) and broadcast (end) addresses
    $network = $ipAsInt -band $mask
    $bcast   = $network  -bor (-bnot $mask)

    # Helper function to convert a 32-bit integer back to an IPAddress
    function Convert-IntToIPAddress {
        param([uint32]$IntAddress)
        $bytes = [BitConverter]::GetBytes($IntAddress)
        [Array]::Reverse($bytes)
        return [System.Net.IPAddress]::new($bytes)
    }

    return [PSCustomObject]@{
        Start = (Convert-IntToIPAddress -IntAddress $network).ToString()
        End   = (Convert-IntToIPAddress -IntAddress $bcast).ToString()
    }
}

# Login to Azure if necessary
Connect-AzAccount

# Get the folder where this script is located
$scriptFolder = Split-Path -Path $MyInvocation.MyCommand.Definition

# Construct the path for the 'Data' folder
$dataFolderPath = Join-Path $scriptFolder 'Data'

# Create the folder if it doesn't exist
if (!(Test-Path -Path $dataFolderPath)) {
    New-Item -ItemType Directory -Path $dataFolderPath | Out-Null
}

# CSV path inside the 'Data' folder
$csvPath = Join-Path $dataFolderPath 'vnet_subnets.csv'

# Get all subscriptions for the current Azure context
$allSubs = Get-AzSubscription

# Prepare an array for storing data
$subnetData = @()

foreach ($sub in $allSubs) {
    # Set the Azure context to the subscription
    Set-AzContext -Subscription $sub.Id | Out-Null
    
    # Get all virtual networks in the current subscription
    $vnets = Get-AzVirtualNetwork

    foreach ($vn in $vnets) {
        foreach ($sn in $vn.Subnets) {
            # If $sn.AddressPrefix is a list of prefixes, handle them all
            $allAddressPrefixes   = $sn.AddressPrefix
            $combinedRangeStart   = @()
            $combinedRangeEnd     = @()

            foreach ($prefix in $allAddressPrefixes) {
                $range = Get-IPv4Range -Cidr $prefix
                $combinedRangeStart += $range.Start
                $combinedRangeEnd   += $range.End
            }

            $subnetData += [PSCustomObject]@{
                SubscriptionName    = $sub.Name
                ResourceGroupName   = $vn.ResourceGroupName
                LocationName        = $vn.Location
                VirtualNetworkName  = $vn.Name
                SubnetName          = $sn.Name
                # Join multiple prefixes with "; "
                SubnetIPv4          = ($allAddressPrefixes -join "; ")
                RangeStart          = ($combinedRangeStart -join "; ")
                RangeEnd            = ($combinedRangeEnd   -join "; ")
            }
        }
    }
}

# Export the array to the CSV file
$subnetData | Export-Csv -Path $csvPath -NoTypeInformation

Write-Host "Subnet information exported to $csvPath"