using namespace System
using namespace System.IO
# Install necessary module
# ----------------------------------------------------------------------------
Install-Module -Name ImportExcel -Scope CurrentUser
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
function LoadExcel() {
	param([Parameter(Mandatory = $true)] $file)
	$global:file = $file
	$global:excel = New-Object -ComObject Excel.Application
	$global:excel.Visible = $false
	# open the Excel file
	$global:workbook = $global:excel.Workbooks.Open($file)
	$global:sheet = $global:workbook.Worksheets.Item(1)
	# get the number of rows in the sheet
	$global:rowMax = $global:sheet.UsedRange.Rows.Count
	$global:colMax = $global:sheet.UsedRange.Columns.Count
}
function DisposeExcel() {
	$global:excel.Quit()
	# clean up the COM objects used
	$null = [System.Runtime.Interopservices.Marshal]::ReleaseComObject($global:sheet)
	$null = [System.Runtime.Interopservices.Marshal]::ReleaseComObject($global:workbook)
	$null = [System.Runtime.Interopservices.Marshal]::ReleaseComObject($global:excel)
	[System.GC]::Collect()
	[System.GC]::WaitForPendingFinalizers()
}
#------------------------------------------------------------------------------
$companyName = "Company Name"
$dataFolder = "ModelTrainingData"
$dataFileBaseName = "data"
$saveAsSeparateFiles = $false
$saveAsRtf = $true
$saveAsOpenAiJson = $true
#------------------------------------------------------------------------------
# Fix column names first.
$dataFile = "$scriptPath\$dataFileBaseName.xlsx"
$data2File = "$scriptPath\$dataFileBaseName.fixed.xlsx"
if (-not [File]::Exists($data2File)) {
	Write-Host "Fixing Column Names"
	LoadExcel $dataFile
	# Rename column in the first row
	for ($c = 1; $c -le $colMax; $c++) {
		$value = $sheet.Cells.Item(1, $c).Text
		# Replace non-alphanumeric characters with '_'. Convert to lower case
		$newValue = ($value -replace '[^a-zA-Z0-9]+', '_').Trim("_").ToLower()
		$sheet.Cells.Item(1, $c) = $newValue
	}
	# save and exit
	$workbook.SaveAs($data2File)
	DisposeExcel
	$dataFile = $data2File
}
#------------------------------------------------------------------------------
# Functions
#------------------------------------------------------------------------------
function Get-ColumnIndices {
	param([Parameter(Mandatory = $true)] $worksheet)
	$dic = @{}
	$index = 1
	$cells = $worksheet.Rows.Item(1).Cells
	for ($c = 1; $c -le $colMax; $c++) {
		$cell = $cells[$c]
		$dic[$cell.Text] = $c
	}
	return $dic
}
#------------------------------------------------------------------------------
LoadExcel $data2File
#------------------------------------------------------------------------------
Write-Host "Get prompt and answer columns."
$columnIndices = Get-ColumnIndices $sheet
$promptColumns = @("question", "alternate_questions")
Write-Host "Question columns: $promptColumns"
$answerColumns = @()
$cells = $sheet.Rows.Item(1).Cells
for ($c = 1; $c -le $colMax; $c++) {
	$cell = $cells[$c]
	#Write-Host "Column: $($cell.Text)"
	if ($cell.Text.StartsWith("answer_")) {
		$answerColumns += $cell.Text
	}
}
Write-Host "Answer columns: $answerColumns"
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

$systemPromptContent =
	"You are a helpful, respectful and honest assistant of $companyName." +
	" Always answer as helpfully as possible, providing comprehensive and detailed information while being safe." +
	" Your answers should not include any harmful, unethical, racist, sexist, toxic, dangerous, or illegal content." +
	" Please ensure that your responses are socially unbiased and positive in nature. " + 
	" When answering a question, include as much detail as appropriate, providing a rich and thorough response. " +
	" Ensure that the information is accurate, structured, and supports the user's inquiry fully. " +
	" If a question does not make any sense, or is not factually coherent, explain why instead of answering something incorrect. " +
	" If you don't know the answer to a question, please don't share false information."

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
[System.Collections.Generic.List[chat_completion_request]]$requestList
$requestList = New-Object System.Collections.Generic.List[chat_completion_request]

# Create control to store RTF content.
[void][Reflection.Assembly]::LoadWithPartialName("System.Windows.Forms")
$rtfContent = New-Object System.Windows.Forms.RichTextBox

if ($saveAsRtf) {
	# Add the RTF text.
	$prompt = "$companyName Questions and Answers"
	$startPos = $rtfContent.Text.Length
	$rtfContent.AppendText("$prompt`r`n`r`n")
	$rtfContent.Select($startPos, $prompt.Length)
	$rtfContent.SelectionFont = New-Object Drawing.Font($rtfContent.Font, [Drawing.FontStyle]::Bold)
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
				# Add the RTF text.
				$startPos = $rtfContent.Text.Length
				$rtfContent.AppendText($prompt + "`r`n")
				$rtfContent.Select($startPos, $prompt.Length)
				$rtfContent.SelectionFont = New-Object Drawing.Font($rtfContent.Font, [Drawing.FontStyle]::Bold)
				$rtfContent.Select($rtfContent.Text.Length, 0)
				$rtfContent.AppendText($answer + "`r`n`r`n")
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
