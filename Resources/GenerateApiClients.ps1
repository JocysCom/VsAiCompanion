<#
.SYNOPSIS
    Generate ApiClients
.NOTES
    Modified: 2023-03-21
#>
using namespace System;
using namespace System.IO;
# ----------------------------------------------------------------------------
# Get current command path.
[string]$current = $MyInvocation.MyCommand.Path;
# Get calling command path.
[string]$calling = @(Get-PSCallStack)[1].InvocationInfo.MyCommand.Path;
# If executed directly then...
if ($calling -ne "") {
	$current = $calling;
}
# ----------------------------------------------------------------------------
[FileInfo]$file = New-Object FileInfo($current);
# Set public parameters.
$global:scriptName = $file.Basename;
$global:scriptPath = $file.Directory.FullName;
# Change current directory.
[Console]::WriteLine("Script Path: {0}", $scriptPath);
[Environment]::CurrentDirectory = $scriptPath;
Set-Location $scriptPath;
# ----------------------------------------------------------------------------
$global:NSwagDirectory = "NSwag"
$global:OutputDirectory = "Clients"
# ----------------------------------------------------------------------------
# Show menu
# ----------------------------------------------------------------------------
function ShowOptionsMenu {
	param($items, $title);
	#----------------------------------------------------------
	# Get local configurations.
	$keys = "1234567890ABCDEFGHIJKLMNOPQRSTUVWXYZ";
	$dic = @{};
	if ("$title" -eq "") { $title = "Options:"; }
	Write-Host $title;
	Write-Host;
	[int]$i = 0;
	foreach ($item in $items) {
		if ("$item" -eq "") { 
			Write-Host;
			continue;
		}
		$key = $keys[$i]; 
		$dic["$key"] = $item;
		Write-Host "    $key - $item";
		$i++;
	}
	Write-Host;
	$m = Read-Host -Prompt "Type option and press ENTER to continue";
	$m = $m.ToUpper();
	return $dic[$m.ToUpper()];
}
# ----------------------------------------------------------------------------
function FindExistingPath {
	[OutputType([string])] param([string[]]$ps);
	#----------------------------------------------------------
	foreach ($p in $ps) {
		if (Test-Path -Path $p) {
			return $p;
		}
	}
	return $null;
}
# ----------------------------------------------------------------------------
function ExpandVersionVariable {
	[OutputType([string])] param([string]$path);
	#----------------------------------------------------------
	$parts = $path.Split("\");
	$expanded = "";
	foreach ($part in $parts) {
		if ($part -eq "{version}") {
			[DirectoryInfo]$di = New-Object DirectoryInfo $expanded;
			$dirs = $di.GetDirectories();
			$rxs = "(?<pfx>[^.]+)(?<version>(?:(?<v1>\d+)\.)(?:(?<v2>\d+)\.)?(?:(?<v3>\d+)\.)?(?:(?<v4>\d+))?)(?<sfx>.*)";
			$rx = New-Object System.Text.RegularExpressions.Regex $rxs;
			$selectedFolder = $dirs[0].Name;
			$selectedVersion = New-Object Version("0.0.0.0");
			foreach ($dir in $dirs) {
				$rxMatches = $rx.Matches($dir.Name);
				if ($rxMatches.Count -gt 0) {
					$versionString = $rxMatches[0].Groups["version"].Value;
					$version = New-Object Version($versionString);
					if ($version -gt $selectedVersion) {
						$selectedFolder = $dir.Name;
						$selectedVersion = $version;
					}
				}
			}
			$part = $selectedFolder;
		}
		if ($expanded -ne "") {
			$expanded += "\";
		}
		$expanded += "$part";
	}
	return $expanded;
}
# ----------------------------------------------------------------------------
function GetNSwagPath {
	[OutputType([string])] param([string]$path);
	#----------------------------------------------------------
	$path = [System.Environment]::ExpandEnvironmentVariables($path);
	$path = ExpandVersionVariable $path;
	return $path;
}
# ----------------------------------------------------------------------------
function GenerateNSwagFiles {
	[OutputType([void])] param([string]$urlFileName, [string]$NSwagExePath, [string]$swagInput);
	#----------------------------------------------------------
	if ("$NSwagExePath" -eq "") {
		$NSwagExePath = GetNSwagPath "%USERPROFILE%\.nuget\packages\nswag.msbuild\{version}\tools\Net60\dotnet-nswag.exe";
	}
	$inUri = New-Object Uri $swagInput;
	$inHost = $inUri.Host;
	# If generate C# client.
	if ("$urlFileName" -match ".cs.") {
		# Set NSWag parameters.
		[string[]]$exeArgs = @(
			"openapi2csclient"
			"/JsonLibrary:SystemTextJson"
			#"/UseBaseUrl:false" # Indicates that the generated client will not use a base URL.
			#"/ClientBaseClass:System.Net.Http.HttpClient" # Specifies the base class for the generated client.
			#"/ClassStyle:Poco"
			#"/GenerateDataContractAttributes:false"
			#"/OperationGenerationMode:MultipleClientsFromOperationId"
			#"/GenerateClientClasses:true" # Specifies whether to generate client classes.
			#"/GenerateClientInterfaces:false" # Specifies whether to generate interfaces for the client classes.
			#"/UseHttpClientCreationMethod:true" # Specifies whether to call CreateHttpClientAsync on the base class to create a new HttpClient.
			#"/GenerateExceptionClasses:true" # Specifies whether to generate exception classes.
			#"/WrapDtoExceptions:false" # Specifies whether to wrap DTO exceptions in a SwaggerException instance.
			#"/GenerateContractsOutput:false" # Specifies whether to generate contracts output.
			#"/GenerateOptionalParameters:false" # Specifies whether to reorder parameters (required first, optional at the end) and generate optional parameters.
			#"/GenerateJsonMethods:false" # Specifies whether to render ToJson() and FromJson() methods for DTOs.
			#"/GenerateDataAnnotations:false" # Specifies whether to generate data annotation attributes on DTO classes.
			#"/GenerateDtoTypes:true" # Specifies whether to generate DTO classes.
			#"/GenerateOptionalPropertiesAsNullable:false" # Specifies whether optional schema properties (not required) are generated as nullable properties.
			#"/GenerateNullableReferenceTypes:false" # Specifies whether to generate Nullable Reference Type annotations.
			#"/InjectHttpClient:true"
    		#"/DisposeHttpClient:false"
    		#"/GenerateDefaultValues:false"
    		#"/JsonSerializerSettingsTransformationMethod:"""""
		);
		#"/BaseAddress:<base-address>
		$namespace = $urlFileName.split(".")[0];
		$className = $urlFileName.split(".")[1];
		$swagOutput = "$OutputDirectory\$namespace.$className.ApiClient.cs";
		& $NSwagExePath @exeArgs /input:"$swagInput" /output:"$swagOutput" /Namespace:"$namespace" /ClassName:"$className";
		# Workaround: Remove abstract class attribute.
		$content = Get-Content -Path $swagOutput -Raw
		$content = $content.replace('public abstract partial class', 'public partial class')
		$content = FixEnumValueNameFromEnumMember $content
		$content | Set-Content $swagOutput
	}
	elseif ("$urlFileName" -match ".ts.") {
		$GenerateTsApiClientTemplate = "Angular";
		$swagOutput = "$OutputDirectory\$inHost.ApiClient.$GenerateTsApiClientTemplate.ts";
		# Set genersal parameters.
		$exeArgs = @(
			"openapi2tsclient"
			"/AspNetCoreEnvironment:Development"
			"/TypeScriptVersion:4.3"
			"/InjectionTokenType:InjectionToken"
			"/UseSingletonProvider:true"
			"/UseBaseUrl:false"
			"/GenerateClientInterfaces:false"
		);
		# Get help about arguments: dotnet "%USERPROFILE%\.nuget\packages\nswag.msbuild\13.16.1\tools\Net60\dotnet-nswag.dll" help openapi2tsclient.
		& $NSwagExePath @exeArgs /input:"$swagInput" /output:"$swagOutput", /Template:$GenerateTsApiClientTemplate;
		# Use Knockout for UI, but normal JQuery to do API requests. Extra parameters used:
		#   /ImportRequiredTypes:false - prevent unnecessary 'import * as...' line which brakes CommonJS.
		#   /Namespace:ApiClient       - wrap in namespace to avoid conflicts with existing classes.
		#   /TypeStyle:KnockoutClass   - add Knockout compatibility.
		# Two files are needed in order to modify much less code: ApiClient.ts (Client API) and ApiClientKO.ts (Knockout UI).
		$GenerateTsApiClientTemplate = "JQueryCallbacks";
		$swagOutput1 = "$OutputDirectory\$inHost.ApiClient.$GenerateTsApiClientTemplate.ts"; 
		$swagOutput2 = "$OutputDirectory\$inHost.ApiClientKO.$GenerateTsApiClientTemplate.ts";
		& $NSwagExePath @exeArgs /input:"$swagInput" /output:"$swagOutput1" /Template:$GenerateTsApiClientTemplate /Namespace:ApiClient   /ImportRequiredTypes:false;
		& $NSwagExePath @exeArgs /input:"$swagInput" /output:"$swagOutput2" /Template:$GenerateTsApiClientTemplate /Namespace:ApiClientKO /ImportRequiredTypes:false /TypeStyle:KnockoutClass /GenerateClientClasses:false;
		# Workaround: Fix KnockoutClass file.
		(Get-Content $swagOutput2).replace('_data[', 'data[') | Set-Content $swagOutput2;
	}else{
		Write-Host "Unknown input file type: $urlFileName" -ForegroundColor Red;
	}
}
#
# ----------------------------------------------------------------------------
function FixEnumValueNameFromEnumMember {
	[OutputType([void])] param([string]$content);
	$replaceFunction = {
		param($match)
		# Get the captured groups
		$beforeValue = $match.Groups[1].Value
		$value = $match.Groups[2].Value
		$beforeName = $match.Groups[3].Value
		$name = $match.Groups[4].Value
		$afterName = $match.Groups[5].Value
		# Convert the first letter of the name to lower case
		$newName = $name.Substring(0, 1).ToLower() + $name.Substring(1)
		# Construct the new string
		$newString = $beforeValue + $value + $beforeName + $newName + $afterName
		Write-Host $newString
		return $newString
	}
	$pattern = '(\[System.Runtime.Serialization.EnumMember\(Value = @")(\w+)("\)\]\s+)(\w+)(\s+=\s+\d+,)'
	$newContent = [Regex]::Replace($content, $pattern, $replaceFunction)
	Write-Host "new content"
	return $newContent
}
# ----------------------------------------------------------------------------
function AddToGitIgnore {
	[OutputType([void])] param([string]$ignorePattern);
	$gitCommand = (Get-Command -Name "git" -ErrorAction SilentlyContinue);
	$gitignorePath = Join-Path (Get-Location) ".gitignore"
	if ($gitCommand){
		# Check if the .gitignore file exists
		if (-not (Test-Path $gitignorePath)) {
				# Create a new .gitignore file with the ignore pattern
				Set-Content -Path $gitignorePath -Value $ignorePattern
		} else {
				# Check if the ignore pattern is already in the .gitignore file
				$content = Get-Content $gitignorePath
				if (-not ($content -contains $ignorePattern)) {
						# Append the ignore pattern to the .gitignore file
						Add-Content -Path $gitignorePath -Value $ignorePattern
				} else {
						#Write-Host "'.gitignore already contains the pattern to ignore 'NSwag' folder."
				}
		}
	}
}
# ----------------------------------------------------------------------------
function CheckNSwag(){
	[OutputType([string])] param([bool]$useExistingCommand);
	if ($useExistingCommand){
		# Check if NSwag is installed
		#$nswagCommand = (Get-Command -Name "nswag" -ErrorAction SilentlyContinue);
		if ($nswagCommand){
			$NSwagExePath = $nswagCommand.Source;
			$nswagFolder = Split-Path -Path $nswagCommand.Path -Parent;
			$NSwagExePath = Join-Path -Path $nswagFolder -ChildPath "Net60\dotnet-nswag.exe";
			return $NSwagExePath;
		}
	}
	$NSwagInstalled = (Test-Path -Path "$NSwagDirectory" -ErrorAction SilentlyContinue);
	if (-not $NSwagInstalled) {
		Write-Host "NSwag not found. Downloading portable version..." -ForegroundColor Yellow;
    # Ignore portable NSwag folder first.
		AddToGitIgnore $NSwagDirectory;
		# Create the NSWag directory if it doesn't exist
		if (-not (Test-Path -Path $NSwagDirectory)) {
			New-Item -Path $NSwagDirectory -ItemType Directory | Out-Null;
		}
		# Download and extract portable version
		$NSwagZipUrl = "https://github.com/RicoSuter/NSwag/releases/latest/download/NSwag.zip";
		$NSwagZipPath = Join-Path -Path $NSwagDirectory -ChildPath "NSwag.zip";
		Invoke-WebRequest -Uri $NSwagZipUrl -OutFile $NSwagZipPath;
		Expand-Archive -Path $NSwagZipPath -DestinationPath $NSwagDirectory -Force;
		Remove-Item -Path $NSwagZipPath;
	}
	# Set the path to the nswag.exe file
	$NSwagExePath = Join-Path -Path $NSwagDirectory -ChildPath "Net60\dotnet-nswag.exe";
	return $NSwagExePath;
}
# ----------------------------------------------------------------------------
function GenerateApiClient {
	[OutputType([void])] param([string]$urlFileName);
	$filePath = "$scriptPath\$urlFileName";
	$content = Get-Content $filePath;
	$url = ($content | Where-Object { $_ -match "URL=(.+)" }) -replace "URL=","";
	Write-Host;
	Write-Host "CFG: $urlFileName";
	Write-Host "EXE: $NSwagExePath";
	Write-Host "URL: $url";
	Write-Host;
	GenerateNSwagFiles $urlFileName $NSwagExePath $url;
}
# ----------------------------------------------------------------------------
do {
	Clear-Host;
	Write-Host "File pattern: <Namespace>.<Class>[.<Tags>].<Language>.url";
	Write-Host;
	# Get the list of *.url files
	[string[]]$fileNames = (Get-ChildItem -Path $scriptPath -Filter *.url | Select-Object -ExpandProperty Name);
	[string[]]$menuOptions = $fileNames.Clone();
	$menuOptions += "ALL";
	$result = ShowOptionsMenu $menuOptions;
	# Process choice of the user.
	if ("$result" -eq ""){
		return;
	}
	$NSwagExePath = CheckNSwag $true;
	if ($result -eq "ALL"){
		foreach($fileName in $fileNames){
			GenerateApiClient $fileName;
		}
	}else{
		GenerateApiClient $result;
	}
	pause;
} while ($true);
