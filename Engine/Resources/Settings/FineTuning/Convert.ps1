using namespace System
using namespace System.IO
# ----------------------------------------------------------------------------
# Declare command line parameters
param(
	[string]$ConversionType,
    [string]$FineTuningName,
    [string]$DataFolderName,
    [string]$SourceFileName,
    [string]$TargetFileName,
	[string]$SystemPromptContent
)
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
# Include other scripts
# ----------------------------------------------------------------------------
Invoke-Expression -Command: (Get-Content -Path ".\ConvertJsonHelper.ps1" -Raw)
Invoke-Expression -Command: (Get-Content -Path ".\ConvertRtfHelper.ps1" -Raw)
Invoke-Expression -Command: (Get-Content -Path ".\ConvertTextHelper.ps1" -Raw)
Invoke-Expression -Command: (Get-Content -Path ".\ConvertExcelHelper.ps1" -Raw)
#------------------------------------------------------------------------------
# Script
#------------------------------------------------------------------------------
$companyName = "Company Name"
$dataFolder = "ModelTrainingData"
$dataFileBaseName = "data"
$saveAsSeparateFiles = $false
$saveAsOpenAiJson = $true
$SystemPromptContent =
	"You are a helpful, respectful and honest assistant of $companyName."
	# +
	#" Always answer as helpfully as possible, providing comprehensive and detailed information while being safe." +
	#" Your answers should not include any harmful, unethical, racist, sexist, toxic, dangerous, or illegal content." +
	#" Please ensure that your responses are socially unbiased and positive in nature. " + 
	#" When answering a question, include as much detail as appropriate, providing a rich and thorough response. " +
	#" Ensure that the information is accurate, structured, and supports the user's inquiry fully. " +
	#" If a question does not make any sense, or is not factually coherent, explain why instead of answering something incorrect. " +
	#" If you don't know the answer to a question, please don't share false information."
# ----------------------------------------------------------------------------
# Show menu
# ----------------------------------------------------------------------------
function ShowOptionsMenu {
	param($items, $title)
	#----------------------------------------------------------
	# Get local configurations.
	$keys = "1234567890ABCDEFGHIJKLMNOPQRSTUVWXYZ"
	$dic = @{}
	if ("$title" -eq "") { $title = "Options:" }
	Write-Host $title
	Write-Host
	[int]$i = 0
	foreach ($item in $items) {
		if ("$item" -eq "") { 
			Write-Host
			continue
		}
		$key = $keys[$i] 
		$dic["$key"] = $item
		Write-Host "	$key - $item"
		$i++
	}
	Write-Host
	$m = Read-Host -Prompt "Type option and press ENTER to continue"
	$m = $m.ToUpper()
	return $dic[$m.ToUpper()]
}
#------------------------------------------------------------------------------
# Don't asks for options if one of the main options are empty.
$askOptions = "$ConversionType" -eq "" -or "$SourceFileName" -eq "" -or "$TargetFileName" -eq ""
#------------------------------------------------------------------------------
if ($askOptions -and "$ConversionType" -eq ""){
	# Select conversion type.
	$conversion_JSON2RTF = "JSON to RTF"
	$conversion_JSON2CSV = "JSON to CSV"
	$conversion_JSON2XLS = "JSON to XLS"
	$conversion_RTF2JSON = "RTF to JSON"
	$conversion_CSV2JSON = "CSV to JSON"
	$conversion_XLS2JSON = "XLS to JSON"
	$conversion_JSON2JSONL = "JSON to JSONL"
	$conversion_JSONL2JSON = "JSONL to JSON"
	# Create array.
	$conversionTypes = @();
	$conversionTypes += $conversion_JSON2RTF
	$conversionTypes += $conversion_JSON2CSV
	$conversionTypes += $conversion_JSON2XLS
	$conversionTypes += $conversion_RTF2JSON
	$conversionTypes += $conversion_CSV2JSON
	$conversionTypes += $conversion_XLS2JSON
	$conversionTypes += $conversion_JSON2JSONL
	$conversionTypes += $conversion_JSONL2JSON
	$ConversionType = ShowOptionsMenu $conversionTypes "Select Conversion Type"
	# Create dictionary
	$ConversionExtensions = @{}
	# Store source file pattern and target file extension value as array.
	$ConversionExtensions[$conversion_JSON2RTF] = @("*.json", ".rtf")
	$ConversionExtensions[$conversion_JSON2CSV] = @("*.json", ".csv")
	$ConversionExtensions[$conversion_JSON2XLS] = @("*.json", ".xls")
	$ConversionExtensions[$conversion_RTF2JSON] = @("*.rtf",  ".rtf")
	$ConversionExtensions[$conversion_CSV2JSON] = @("*.csv",  ".csv")
	$ConversionExtensions[$conversion_XLS2JSON] = @("*.xls*", ".xls")
	$ConversionExtensions[$conversion_JSON2JSONL] = @("*.json", ".jsonl")
	$ConversionExtensions[$conversion_JSONL2JSON] = @("*.jsonl", ".json")
}
if ($askOptions -and "$FineTuningName" -eq ""){
	# Select fine tuning settings.
	[DirectoryInfo]$scriptDi = New-Object DirectoryInfo $scriptPath
	[string[]]$fineTuningMenuNames = $scriptDi.GetDirectories("*.*") | ForEach-Object { $_.Name }
	$FineTuningName = ShowOptionsMenu $fineTuningMenuNames "Select Fine-Tuning Settings"
}
if ($askOptions -and "$DataFolderName" -eq ""){
	# Select folder with files.
	[DirectoryInfo]$fineTuningDi = New-Object DirectoryInfo "$scriptPath\$FineTuningName"
	[string[]]$dataFolderMenuNames = $fineTuningDi.GetDirectories("*.*") | ForEach-Object { $_.Name }
	$DataFolderName = ShowOptionsMenu $dataFolderMenuNames "Select Data Folder"
}
if ($askOptions -and "$SourceFileName" -eq ""){
	# Select file.
	[DirectoryInfo]$sourceFolderDi = New-Object DirectoryInfo "$scriptPath\$FineTuningName\$DataFolderName"
	[string]$sourceFilePattern = $ConversionExtensions[$ConversionType][0];
	[string[]]$sourceFileMenuNames = $sourceFolderDi.GetFiles($sourceFilePattern) | ForEach-Object { $_.Name }
	$sourceFileMenuName = ShowOptionsMenu $sourceFileMenuNames "Select Source Data File"
	$sourceFileBase = [Path]::GetFileNameWithoutExtension($sourceFileMenuName)
	$SourceFileName = "$scriptPath\$FineTuningName\$DataFolderName\$sourceFileMenuName"
}
if ($askOptions -and "$TargetFileName" -eq ""){
	$targteFileNameBase = "$scriptPath\$FineTuningName\$DataFolderName\$sourceFileBase"
	$targteFileNameExt = $ConversionExtensions[$ConversionType][1]
	$TargetFileName = "$($targteFileNameBase)$($targteFileNameExt)"
}
#------------------------------------------------------------------------------
if ($ConversionType -eq $conversion_JSON2RTF){
	Convert-JSON2RTF $SourceFileName $TargetFileName
}
if ($ConversionType -eq $conversion_JSON2CSV){
	Convert-JSON2CSV $SourceFileName $TargetFileName
}
if ($ConversionType -eq $conversion_JSON2XLS){
	Convert-JSON2XLS $SourceFileName $TargetFileName
}
if ($ConversionType -eq $conversion_RTF2JSON){
	Convert-RTF2JSON $SourceFileName $TargetFileName
}
if ($ConversionType -eq $conversion_CSV2JSON){
	Convert-CSV2JSON $SourceFileName $TargetFileName
}
if ($ConversionType -eq $conversion_XLS2JSON){
	Convert-XLS2JSON $SourceFileName $TargetFileName
}
if ($ConversionType -eq $conversion_JSON2JSONL){
	Convert-JSON2JSONL $SourceFileName $TargetFileName
}
if ($ConversionType -eq $conversion_JSONL2JSON){
	Convert-JSONL2JSON $SourceFileName $TargetFileName
}
return
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
				$sm.content = $SystemPromptContent
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
#------------------------------------------------------------------------------
DisposeExcel
#------------------------------------------------------------------------------
