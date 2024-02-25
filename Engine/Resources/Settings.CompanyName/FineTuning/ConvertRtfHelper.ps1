# Create control to store RTF content.
[void][Reflection.Assembly]::LoadWithPartialName("System.Windows.Forms")
#$rtfContent = New-Object System.Windows.Forms.RichTextBox
#------------------------------------------------------------------------------
function Add-RtfLine(){
    param(
        [Parameter(Mandatory = $true)] $rtf,
        [Parameter(Mandatory = $true)] $text,
        $isBold
    )
	$startPos = $rtf.Text.Length
	$rtf.AppendText($text)
	$rtf.Select($startPos, $text.Length)
    if ($isBold -eq $true){
    	$rtf.SelectionFont = New-Object Drawing.Font($rtf.Font, [Drawing.FontStyle]::Bold)
    }
	$rtf.AppendText("`r`n")
}
#------------------------------------------------------------------------------
# Convert JSON To RTF
function Convert-JSON2RTF {
	param(
        [Parameter(Mandatory = $true)] $sourceFileName,
        [Parameter(Mandatory = $true)] $targetFileName
    )
	[System.Collections.Generic.List[chat_completion_request]]$requestList
	$list = Get-JsonObjectFromFile $sourceFileName
	$rtf = New-Object System.Windows.Forms.RichTextBox
	#Add-RtfLine $rtfContent "$companyName Questions and Answers"
	#Add-RtfLine $rtfContent "`r`n"
	foreach($request in $list) {
		foreach($message in $request.messages) {
			if ($message.role -eq 'user') {
				Add-RtfLine $rtf $message.content $true
				Add-RtfLine $rtf "`r`n"
			}
			if ($message.role -eq 'assistant') {
				Add-RtfLine $rtf $message.content
				Add-RtfLine $rtf "`r`n"
			}
		}
	}
	# Save RTF content.
	$rtf.SaveFile($targetFileName)
}
