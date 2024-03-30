# Convert *.md files to *.rtf

# To convert back, use Microsoft Word to save the RTF file as Web Page, Filtered (*.html).
# Then, use pandoc to convert it back to *.md.

function Convert-MarkdownToRtf {
    param (
        [Parameter(Mandatory)]
        [string]$WikiFolderPath,

        [string]$OutputFolderPath = "Documents",

        [string]$PandocPath = "c:\Users\EJocys\AppData\Roaming\Jocys.com\VS AI Companion\Tools\pandoc.exe"
    )

    # Ensure the output folder exists
    $AbsolutePathToOutputFolder = Join-Path -Path $PSScriptRoot -ChildPath $OutputFolderPath
    if (-not (Test-Path $AbsolutePathToOutputFolder)) {
        New-Item -ItemType Directory -Path $AbsolutePathToOutputFolder -Force | Out-Null
    }

    # Get all markdown files in the specified folder
    $MarkdownFiles = Get-ChildItem -Path $WikiFolderPath -Filter *.md

	#& $PandocPath -D html > "$AbsolutePathToOutputFolder\custom-template.html"
	#& $PandocPath -D rtf > "$AbsolutePathToOutputFolder\custom-template.rtf"

    foreach ($File in $MarkdownFiles) {
        
		$ResourcePath = $WikiFolderPath
		Write-Host "Resources: $ResourcePath"
		$FileBase = $File.BaseName -replace '[-]', ' '
		$FileBase = $File.BaseName -replace '‐', '-'
		
		$InputFilePath = $File.FullName
        $OutputHtmlFileName =  $FileBase + ".html"
        $OutputHtmlFilePath = Join-Path -Path $AbsolutePathToOutputFolder -ChildPath $OutputHtmlFileName

        # Convert .md to .html using pandoc
        & $PandocPath -f markdown -t html --resource-path=$ResourcePath -s -o $OutputHtmlFilePath $InputFilePath --metadata title="$FileBase"

        $OutputRtfFileName = $FileBase + ".rtf"
        $OutputRtfFilePath = Join-Path -Path $AbsolutePathToOutputFolder -ChildPath $OutputRtfFileName

        # Convert .html to .rtf using pandoc
        & $PandocPath -f html -t rtf --resource-path=$ResourcePath  -s -o $OutputRtfFilePath $OutputHtmlFilePath --metadata title=""

		Remove-Item $OutputHtmlFilePath

        Write-Host "Converted: $OutputRtfFilePath"
    }
}

$WikiFolderPath = "..\..\..\VsAiCompanion.wiki"
Convert-MarkdownToRtf -WikiFolderPath $WikiFolderPath