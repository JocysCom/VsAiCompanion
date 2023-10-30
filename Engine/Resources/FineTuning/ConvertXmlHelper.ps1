# Convert JSON To CSV
function Convert-JSON2XML {
    param(
        [Parameter(Mandatory = $true)] $sourceFileName,
        [Parameter(Mandatory = $true)] $targetFileName
    )
    # Load JSON content and convert it to `chat_completion_request[]`
    [chat_completion_request[]]$requestList = Get-Content -Path $sourceFileName | ConvertFrom-Json
    # Convert your $requestList to XML
    $xml = $requestList | ConvertTo-Xml
    # Save the XML data to a file
    $xml.OuterXml | Set-Content -Path $targetFileName
}    
