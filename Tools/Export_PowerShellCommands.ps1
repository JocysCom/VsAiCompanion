# Get all cmdlets, functions, and aliases
$commands = Get-Command

# Create an array to store the command details
$commandDetails = @()

# Iterate through each command and retrieve the necessary information
foreach ($command in $commands) {
    # Initialize a hashtable for each command's details
    $commandInfo = @{
        CommandType = $command.CommandType.ToString()
        Name        = $command.Name
        Version     = ($command.Version -as [string])
        Source      = $command.ModuleName
        Description = ""
    }
	
	$line = "CommandType: {0}, Name: {1}, Version: {2}, Source: {3}" -f `
        $commandInfo.CommandType, $commandInfo.Name, $commandInfo.Version, $commandInfo.Source
    Write-Output $line    
    
    # Get the help information
    $help = Get-Help $command.Name -ErrorAction SilentlyContinue
    
    # If help is available, add the first line of the description
    if ($help) {
        $description = $help.Description | Select-Object -First 1
        $commandInfo.Description = $description
    }
    
    # Add this command's details to the array
    $commandDetails += $commandInfo
}

# Convert the command details to JSON
$jsonOutput = $commandDetails | ConvertTo-Json -Depth 3

# Export to a JSON file
$jsonOutput | Out-File "PowerShellCommands.json" -Encoding utf8

Write-Output "Command list has been exported to PowerShellCommands.json"