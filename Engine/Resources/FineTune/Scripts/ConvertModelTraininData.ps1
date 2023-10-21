using namespace System
using namespace System.IO
# ----------------------------------------------------------------------------
# Include other scripts
Invoke-Expression -Command: (Get-Content -Path ".\ConvertJsonHelper.ps1" -Raw)
Invoke-Expression -Command: (Get-Content -Path ".\ConvertRtfHelper.ps1" -Raw)
Invoke-Expression -Command: (Get-Content -Path ".\ConvertTextHelper.ps1" -Raw)
Invoke-Expression -Command: (Get-Content -Path ".\ConvertExcelHelper.ps1" -Raw)
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
#------------------------------------------------------------------------------
# Script
#------------------------------------------------------------------------------
$companyName = "Company Name"
$dataFolder = "ModelTrainingData"
$dataFileBaseName = "data"
$saveAsSeparateFiles = $false
$saveAsRtf = $true
$saveAsOpenAiJson = $true
$systemPromptContent =
	"You are a helpful, respectful and honest assistant of $companyName."
	# +
	#" Always answer as helpfully as possible, providing comprehensive and detailed information while being safe." +
	#" Your answers should not include any harmful, unethical, racist, sexist, toxic, dangerous, or illegal content." +
	#" Please ensure that your responses are socially unbiased and positive in nature. " + 
	#" When answering a question, include as much detail as appropriate, providing a rich and thorough response. " +
	#" Ensure that the information is accurate, structured, and supports the user's inquiry fully. " +
	#" If a question does not make any sense, or is not factually coherent, explain why instead of answering something incorrect. " +
	#" If you don't know the answer to a question, please don't share false information."
#------------------------------------------------------------------------------
# Fix column names first.
$dataFile = "$scriptPath\$dataFileBaseName.xlsx"
$data2File = "$scriptPath\$dataFileBaseName.fixed.xlsx"
Update-ColumnNames $dataFile $data2File
$dataFile = $data2File
#------------------------------------------------------------------------------
LoadExcel $data2File
#------------------------------------------------------------------------------
Write-Host "Get prompt and answer columns."
$columnIndices = Get-ColumnIndices $sheet
$promptColumns = Get-ColumnNames $sheet "question"
$answerColumns = Get-ColumnNames $sheet "answer"
Write-Host "Question columns: $promptColumns"
Write-Host "Answer columns: $answerColumns"
DisposeExcel

#Convert-JSON2RTF "data-demo.json" "data-demo.rtf"
#Convert-JSON2XLSX "data-demo.json" "data-demo.xlsx"
#Convert-JSON2CSV "data-demo.json" "data-demo.csv"
return
#------------------------------------------------------------------------------
if (-not [Directory]::Exists("$dataFolder")) {
	Write-Host "Create Directory: $dataFolder"
	$null = [Directory]::CreateDirectory($dataFolder)
}

if ($saveAsSeparateFiles) {
	$sd = "$dataFolder\$separateFilesFolder"
	if (-not [Directory]::Exists($sd)) {
		Write-Host "Create Directory: $sd"
		$null = [Directory]::CreateDirectory($sd)
	}
}

if ($saveAsRtf) {
}

# Begin assembling data.
$i = 0
for ($r = 2; $r -le $rowMax; $r++) {
	$cells = $sheet.Rows.Item($r).Cells
	$prompts = @();
	foreach ($promptColumn in $promptColumns) {
		$value = $cells[$columnIndices[$promptColumn]].Text.Trim()
		if ($value -eq "") { continue }
		$prompts += $value;
	}
	$answers = @();
	foreach ($answerColumn in $answerColumns) {
		$value = $cells[$columnIndices[$answerColumn]].Text.Trim()
		if ($value -eq "") { continue }
		$answers += $value;
	}
	foreach ($prompt in $prompts) {
		foreach ($answer in $answers) {
			if ($saveAsOpenAiJson) {
				# Create request.
				$request = [chat_completion_request]::new()
				# Add `system` message.
				[chat_completion_message]$sm = [chat_completion_message]::new()
				$sm.role = "system"
				$sm.content = $systemPromptContent
				$null = $request.messages.Add($sm)
				# Add `user` message.
				[chat_completion_message]$um = [chat_completion_message]::new()
				$um.role    = "user"
				$um.content = $prompt
				$null = $request.messages.Add($um)
				# Add `assistant` answer.
				[chat_completion_message]$am = [chat_completion_message]::new()
				$am.role    = "assistant"
				$am.content = $answer
				$request.messages.Add($am)
				# Add request to the list.
				$null = $requestList.Add($request)
			}
			if ($saveAsSeparateFiles) {
				# Convert the custom object to a JSON string and save to file.
				$jsonData = $customMessage | ConvertTo-Json
				# Save the JSON string to a .json file
				$jsonFile = "$dataFileBaseName-{0:0000}.json" -f $i
				$jsonData | Out-File "$dataFolder\$jsonFile"
			}
			if ($saveAsRtf) {
				Add-RtfLine $rtfContent $prompt $true
				Add-RtfLine $rtfContent $answer
				Add-RtfLine $rtfContent "`r`n"
			}
			# Report progress.
			$i++
			Write-Progress -Activity "Save in Progress" -Status "$r/$rowMax Complete:" -PercentComplete ($r * 100 / $rowMax)
		}
	}
	#if ($i -gt 10) { break }
}

if ($saveAsOpenAiJson) {
	# Save OpenAI fine-tuning content.
	$openAiJsonString = $requestList | ConvertTo-Json -Depth 5
	$openAiJsonString | Out-File "$dataFolder\$dataFileBaseName.json"
}

if ($saveAsRtf) {
	# Save RTF content.
	$rtfContent.SaveFile("$dataFolder\$dataFileBaseName.rtf")
}
#------------------------------------------------------------------------------
DisposeExcel
#------------------------------------------------------------------------------
