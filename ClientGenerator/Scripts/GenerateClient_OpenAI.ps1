# GenerateClient_OpenAI.ps1

# Define paths
$yamlUrl = "https://raw.githubusercontent.com/openai/openai-openapi/master/openapi.yaml"
$yamlOutputFilePath = Join-Path $PSScriptRoot "..\..\Clients\OpenAI\openapi.yaml"
$clientGeneratorExePath = Join-Path $PSScriptRoot "..\bin\Debug\net8.0\win-x64\JocysCom.VS.AiCompanion.ClientGenerator.exe"
$clientsModelOutputDir = Join-Path $PSScriptRoot "..\..\Clients\OpenAI"

# Ensure output directory exists
if (-not (Test-Path $clientsModelOutputDir)) {
    New-Item -ItemType Directory -Path $clientsModelOutputDir
}

# Download the OpenAPI YAML specification
Write-Host "Downloading OpenAPI YAML specification..."
Invoke-WebRequest -Uri $yamlUrl -OutFile $yamlOutputFilePath

# Check if the Client Generator executable exists
if (Test-Path $clientGeneratorExePath) {
    # Run the Client Generator exe with the YAML file and output directory specified
    Write-Host "Generating client models..."
    & $clientGeneratorExePath $yamlOutputFilePath $clientsModelOutputDir
} else {
    Write-Error "ClientGenerator executable not found at '$clientGeneratorExePath'. Please build the project first."
}

Write-Host "Client generation complete."
