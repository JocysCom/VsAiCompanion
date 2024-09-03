<#
.SYNOPSIS
    Create list of BuiltIn PowerShell commands.
.NOTES
    Author:     Evaldas Jocys <evaldas@jocys.com>
    Modified:   2024-09-03
.LINK
    http://www.jocys.com
#>
using namespace System
using namespace System.IO
# ----------------------------------------------------------------------------
# Run as administrator.
If (-NOT ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {   
    # Pass arguments: script path, original user profile path and local app data path.
    $argumentList = "& '" + $MyInvocation.MyCommand.Path + "' '$($env:USERNAME)' '$($env:USERPROFILE)' '$($env:LOCALAPPDATA)'"
    Start-Process PowerShell.exe -Verb Runas -ArgumentList $argumentList
    return
}   
# ----------------------------------------------------------------------------
# Get current command path.
[string]$current = $MyInvocation.MyCommand.Path
# Get calling command path.
[string]$calling = @(Get-PSCallStack)[1].InvocationInfo.MyCommand.Path
# If executed directly then...
if ($calling -ne "") {
	$current = $calling
}
# ----------------------------------------------------------------------------
[FileInfo]$file = New-Object FileInfo($current)
# Set public parameters.
$global:scriptName = $file.Basename
$global:scriptPath = $file.Directory.FullName
# Change current directory.
Write-Host "Script Path:    $scriptPath"
[Environment]::CurrentDirectory = $scriptPath
Set-Location $scriptPath
# ----------------------------------------------------------------------------

# Prompt the user with a message asking if they want to update help
$userResponse = Read-Host "Do you want to update help? (y/N)"

# Check the user's response
if ($userResponse -match "^(yes|y|Y|YES)$") {
    # User wants to update help
    Write-Output "Updating help..."
    Update-Help
}

# Initialize a hashtable to store module skip status
$IsBuiltInModules = @{}

# Helper function to determine if a module is built-in, caching results for performance
function IsBuiltInModule ($moduleName) {
    # If the module name is empty, treat as built-in
    if (-not ($moduleName)) {
        return $true
    }
    # Check the cache first to see if we've already determined the module's status
    if ($IsBuiltInModules.ContainsKey($moduleName)) {
        return $IsBuiltInModules[$moduleName]
    }
    # Get the module base path
    $module = Get-Module -ListAvailable -Name $moduleName
    $isBuiltIn = $false
    if ($module) {
        # Determine if the module path is within the Windows directory
        $isBuiltIn = $module.ModuleBase -like "C:\Windows\*"
    }
    # Cache the result for later use
    $IsBuiltInModules[$moduleName] = $isBuiltIn
    Write-Host "IsBuiltInModules['$moduleName'] = $isBuiltIn"
    return $isBuiltIn
}

# Get all cmdlets, functions, and aliases, sorted by ModuleName, CommandType, and Name
$commands = Get-Command | Sort-Object -Property @{Expression = { $_.ModuleName }; Ascending = $true}, @{Expression = { $_.CommandType }; Ascending = $true}, @{Expression = { $_.Name }; Ascending = $true}

# Create a hashtable to store command details by module
$commandsByModule = @{}

# Iterate through each command and retrieve the necessary information
foreach ($command in $commands) {
    # Filter out invalid command names
    if ($command.Name -match "[:.\\]") {
        continue
    }

    # Skip processing if the module is not built-in
    if (-not (IsBuiltInModule $command.ModuleName)) {
        continue
    }

    # Initialize a hashtable for each command's details
    $commandInfo = @{
        Module      = $command.ModuleName
        Name        = $command.Name
        Synopsis    = ""
    }

    # Output command details
    $line = "Module: $($commandInfo.Module), Name: $($commandInfo.Name)"
    Write-Host $line

    # Get the help information
    $help = Get-Help $command.Name -ErrorAction SilentlyContinue
    # If help is available, add the synopsis
    if ($help) {
        $commandInfo.Synopsis = $help.Synopsis
    }
    
    # Group commands by module
    if (-not $commandsByModule.ContainsKey($command.ModuleName)) {
        $commandsByModule[$command.ModuleName] = @()
    }
    $commandsByModule[$command.ModuleName] += $commandInfo
}

# Export each module's command list to a separate JSON file
foreach ($moduleName in $commandsByModule.Keys) {
    $moduleCommands = $commandsByModule[$moduleName]
    $jsonOutput = $moduleCommands | ConvertTo-Json -Depth 3
    $fileName = "PowerShellCommands_$moduleName.json"
    $jsonOutput | Out-File $fileName -Encoding utf8
    Write-Output "Command list for module '$moduleName' has been exported to $fileName"
}

pause