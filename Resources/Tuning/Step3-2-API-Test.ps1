# Step3-2-API-Test.ps1

# Define the API endpoint
$apiUrl = "http://localhost:5000/predict"

function SendMessage {
	param([string]$userMessage)
	# Define the body with the text you want to get a prediction for
	$body = @{ text = $userMessage } | ConvertTo-Json
	# Send a POST request to the API endpoint
	$response = Invoke-WebRequest -Uri $apiUrl -Method Post -Body $body -ContentType "application/json"
	# Display the response content
	$response.Content
}

SendMessage "Who are you?"
SendMessage "What is the capital of France?"
