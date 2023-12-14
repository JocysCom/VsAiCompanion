# This is a PowerShell script used to interact with a chatbot API hosted at http://localhost:5000/predict. It is a small utility script that helps test the deployed API by sending POST requests to the chatbot endpoint. The SendMessage function takes user input as its parameter, formats it as JSON, and sends a POST request to the API. It then prints out the chatbot's response to the console. This script is set up to send two test messages to the bot: "Who are you?" and "What is the capital of France?" It is important to note that this script assumes the API server is running and accessible.

# Define the API endpoint
$apiUrl = "http://localhost:5000/predict"

function SendMessage {
    param([string]$userMessage)
    # Define the JSON body with the system content and the text you want to get a prediction for
    $body = @{
        messages = @(
            @{
                role = "system"
                content = "You are a helpful, respectful and honest assistant of Doughnut Dynamics."
            },
            @{
                role = "user"
                content = $userMessage
            }
        )
    } | ConvertTo-Json -Depth 3
    
    # Send a POST request to the API endpoint
    $response = Invoke-WebRequest -Uri $apiUrl -Method Post -Body $body -ContentType "application/json"
    # Display the response content
    $response.Content
}

SendMessage "Who are you?"
SendMessage "What is the capital of France?"
