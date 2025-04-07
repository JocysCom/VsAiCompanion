# Dot-source the core functions including Invoke-MenuLoop
. "$PSScriptRoot\Setup_0_Core.ps1"

Write-Host "--- Test Script Started ---"

# Define a simple menu
$testTitle = "Test Menu"
$testItems = [ordered]@{
    "1" = "Action One"
    "2" = "Action Two"
    "0" = "Exit Test"
}
$testActions = @{
    "1" = { Write-Host "--- Executed Action One ---" }
    "2" = { Write-Host "--- Executed Action Two ---" }
}

Write-Host "--- Calling Invoke-MenuLoop ---"

# Call the function
Invoke-MenuLoop -MenuTitle $testTitle -MenuItems $testItems -ActionMap $testActions -ExitChoice "0"

Write-Host "--- Invoke-MenuLoop Finished ---"
Write-Host "--- Test Script Ended ---"
