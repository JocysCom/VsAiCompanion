# Download Python: https://www.python.org/downloads/
$url = "https://www.python.org/ftp/python/3.11.6/python-3.11.6-amd64.exe"
$outputFilePath = ".\python-3.11.6-amd64.exe"
Invoke-WebRequest -Uri $url -OutFile $outputFilePath