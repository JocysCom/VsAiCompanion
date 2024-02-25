#------------------------------------------------------------------------------
class chat_completion_message {
    [string]$role
    [string]$content
}

class chat_completion_request {
    [System.Collections.Generic.List[chat_completion_message]]$messages
    # Create a constructor to initialize messages
    chat_completion_request() {
        $this.messages = New-Object System.Collections.Generic.List[chat_completion_message]
    }
}

# Create an empty array to store the JSON strings
#[System.Collections.Generic.List[chat_completion_request]]$requestList
#$requestList = New-Object System.Collections.Generic.List[chat_completion_request]

#------------------------------------------------------------------------------
function Get-JsonObjectFromFile(){
    param([Parameter(Mandatory = $true)] $fileName)

    [System.Collections.Generic.List[chat_completion_request]]$requestList
    $list = New-Object System.Collections.Generic.List[chat_completion_request]

    # Load the contents of the file.
    $jsonContent = Get-Content -Path $fileName
    # convert the JSON data to a PowerShell Custom Object.
    $customObject = $jsonContent | ConvertFrom-Json
    # Loop through each object in the $customObject and add them to the $requestList.
    foreach($object in $customObject) {
        # Create a new instance of chat_completion_request
        $request = New-Object chat_completion_request
        # Loop through each message in the 'messages' property of the current object
        foreach($message in $object.messages) {
            # Create a new chat_completion_message and set its properties
            $newChatMessage = New-Object chat_completion_message
            $newChatMessage.role = $message.role
            $newChatMessage.content = $message.content
            # Add the new message to the 'messages' property of the request
            $request.messages.Add($newChatMessage)
        }
        # Add the new request to the request list
        $list.Add($request)
    }
    return $list
}