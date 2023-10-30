# ----------------------------------------------------------------------------
# Install necessary module
Install-Module -Name ImportExcel -Scope CurrentUser
# ----------------------------------------------------------------------------
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
# Convert names with spaces to names with underline.
# For example: "Column Name" to "column_name".
function Update-ColumnNames() {
    param(
        [Parameter(Mandatory = $true)] $sourceFileName,
        [Parameter(Mandatory = $true)] $targetFileName
    )
    Write-Host "Fixing Column Names"
    # Fix column names first.
    if ([File]::Exists($targetFileName)){
        Write-Host "File '$targetFileName' already exists"
        return
    }
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
}
#------------------------------------------------------------------------------
# Get dictionay where key is column name and value is column index.
# For example: [{ "column_name",  1 }]. Column index is 1 based.
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
# Rerturns array of column names which contains the word.
function Get-ColumnNames {
	param(
        [Parameter(Mandatory = $true)] $worksheet,
        [Parameter(Mandatory = $true)] $word
    )
    $columns = @()
    $cells = $worksheet.Rows.Item(1).Cells
    for ($c = 1; $c -le $colMax; $c++) {
        $cell = $cells[$c]
        #Write-Host "Column: $($cell.Text)"
        if ($cell.Text.IndexOf($word, [System.StringComparison]::InvariantCultureIgnoreCase) -ge 0) {
            $columns += $cell.Text
        }
    }
    return $columns
}
#------------------------------------------------------------------------------
# Convert JSON To XLSX
function Convert-JSON2XLSX {
    param(
        [Parameter(Mandatory = $true)] $sourceFileName,
        [Parameter(Mandatory = $true)] $targetFileName
    )
    # Load JSON content and convert it to `chat_completion_request[]`
    [chat_completion_request[]]$requestList = Get-Content -Path $sourceFileName | ConvertFrom-Json
    # Prepare data for Excel
    $excelData = foreach($request in $requestList) {
        # Filter messages by 'user' and 'assistant' roles
        $userContent = ($request.messages | Where-Object {$_.role -eq 'user'}).content
        $assistantContent = ($request.messages | Where-Object {$_.role -eq 'assistant'}).content
        # Return a custom object for each request
        [PSCustomObject]@{
            'question' = if ($userContent) {$userContent} else {''}
            'answer' = if ($assistantContent) {$assistantContent} else {''}
        }
    }
    # Export data to Excel
    $excelData | Export-Excel -Path $targetFileName -WorksheetName 'ChatData' -AutoSize
}