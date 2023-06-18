$file = 'C:\path\to\your\file.txt'
$content = Get-Content $file
$words = $content -split '\s+'
$wordCount = $words.Count
Write-Host "Number of words in the file: $wordCount"
