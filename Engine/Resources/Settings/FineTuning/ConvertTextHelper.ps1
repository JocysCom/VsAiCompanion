# Convert JSON To CSV
function Convert-JSON2CSV {
    param(
        [Parameter(Mandatory = $true)] $sourceFileName,
        [Parameter(Mandatory = $true)] $targetFileName
    )
    # Load JSON content and convert it to `chat_completion_request[]`
    [chat_completion_request[]]$requestList = Get-Content -Path $sourceFileName | ConvertFrom-Json
    # Prepare data for CSV
    $csvData = foreach($request in $requestList) {
        # Filter messages by 'user' and 'assistant' roles
        $userContent = ($request.messages | Where-Object {$_.role -eq 'user'}).content
        $assistantContent = ($request.messages | Where-Object {$_.role -eq 'assistant'}).content

        # Return a custom object for each request
        [PSCustomObject]@{
            'question' = if ($userContent) {$userContent} else {''}
            'answer' = if ($assistantContent) {$assistantContent} else {''}
        }
    }
    # Export data to CSV
    $csvData | Export-Csv -Path $targetFileName -NoTypeInformation
}