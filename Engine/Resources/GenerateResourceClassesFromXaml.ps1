<#
.SYNOPSIS
    Generate C# class from resource file.
#>
using namespace System;
using namespace System.IO;
using namespace System.Linq;
using namespace System.Xml.Linq;
using namespace System.Text.RegularExpressions;
using namespace System.Collections.Generic;

[Reflection.Assembly]::LoadWithPartialName("System.Xml.Linq") | Out-Null;

Clear-Host;

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
[DirectoryInfo]$root = New-Object DirectoryInfo($scriptPath);
# ----------------------------------------------------------------------------
function FindParentFile
{
    [OutputType([FileInfo[]])] param([string]$pattern);
    #----------------------------------------------------------
    [DirectoryInfo]$di = new-Object DirectoryInfo $scriptPath;
    do
    {
        $files = $di.GetFiles($pattern);
        # Return if project files were found.
        if ($files.Count -gt 0)
        {
            return $files;
        }
        # Continue to parent.
        $di = $di.Parent;
    } while($null -ne $di);
    return $null;
}
# ----------------------------------------------------------------------------
function GetProjectValue
{
    [OutputType([string])] param([string]$path, [string]$name);
    #----------------------------------------------------------
    [string]$content = [File]::ReadAllText($path);
	[Regex]$rx = New-Object Regex("(?<p><$name>)(?<v>[^<]*)(?<s><\/$name>)");
	$match = $rx.Match($content);
	if ($match.Success -eq $true) {
		return $match.Groups["v"].Value;
	}
	return $null;
}
# ----------------------------------------------------------------------------
function FindProjectFile
{
    [FileInfo[]]$list = FindParentFile "*.*proj";
    if ($list -ne $null -and $list.Count -gt 0){
        # Order by date descending to most recent file.
        $list = [Enumerable]::OrderByDescending($list, [Func[object,object]]{ param($x) $x.LastWriteTime });
        $list = [Enumerable]::ToArray($list);
        return $list[0];
    }
    return $null;
}
# ----------------------------------------------------------------------------
function IsDifferent($path, [byte[]]$bytes) {
    if (-not $bytes) {
        throw [System.ArgumentNullException]::new("bytes")
    }
    $fileInfo = Get-Item $path -ErrorAction SilentlyContinue
    # If the file does not exist or the size is different, then it is considered different.
    if (-not $fileInfo -or $fileInfo.Length -ne $bytes.Length) {
        return $true
    }
    # Compare checksums.
    $algorithm = [System.Security.Cryptography.SHA256]::Create()
    try {
        $byteHash = $algorithm.ComputeHash($bytes)
        $byteHashString = ($byteHash|ForEach-Object ToString X2) -join ''
        $fileBytes = [System.IO.File]::ReadAllBytes($fileInfo.FullName)
        $fileHash = $algorithm.ComputeHash($fileBytes)
        $fileHashString = ($fileHash|ForEach-Object ToString X2) -join ''
        #Write-Host $byteHashString
        #Write-Host $fileHashString
        $isDifferent = $byteHashString -ne $fileHashString
        return $isDifferent
    }
    finally {
        $algorithm.Dispose()
    }
}
# ----------------------------------------------------------------------------
function WriteIfDifferent($path, [byte[]]$bytes) {
    $isDifferent = IsDifferent $path $bytes
    if ($isDifferent) {
        [System.IO.File]::WriteAllBytes($path, $bytes)
        Write-Host "Saved: $path"
    } else {
        Write-Host "No Change: $path"
    }
    return $isDifferent
}
# ----------------------------------------------------------------------------
function SaveToFile($path, $contents) {
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($contents)
    $null = WriteIfDifferent $path $bytes
}
# ----------------------------------------------------------------------------

#------------------------------
# Get Project file.
#------------------------------

[FileInfo]$project = FindProjectFile;
if ($null -eq $project) {
    Write-Host "Project file not found.";
    return;
}
Write-Host "Project:   $($project.FullName)";

#------------------------------
# Get Default namespace.
#------------------------------

# Get from project file.
$defaultNamespace = GetProjectValue $project.FullName "RootNamespace";
# If default namespace not found.
if ("" -eq "$defaultNamespace") {
    # Visual studio use Project file name as default assembly and root namespace.
    $defaultNamespace = $project.BaseName;
}
# If namespace not found then...
if ("" -eq "$defaultNamespace") {
    Write-Host "Please provide default namespace";
    $defaultNamespace = Host-Read;
}
#Write-Host "Default Namespace: $defaultNamespace";

# Get Relative namespace.
$relativeNamespace = $scriptPath.Substring($project.Directory.FullName.Length).Replace("\", ".");
#Write-Host "Relative Namespace: $relativeNamespace";
$namespace = $defaultNamespace + $relativeNamespace;

Write-Host;
Write-Host "Namespace: $namespace";

#------------------------------
# Get Class Name
#------------------------------

# Get `*Resource.xaml` files.
$resourceFiles = Get-ChildItem -Path $scriptPath -Recurse -Include "*Resource*.xaml"

foreach ($resourceFile in $resourceFiles) {
    # Extract class name from file name (e.g., MainResource.xaml -> MainResource)
    $className = [IO.Path]::GetFileNameWithoutExtension($resourceFile.Name)
    
    # Prepare content for class file.
    $classContent = New-Object System.Text.StringBuilder
    [void]$classContent.AppendLine("using System;")
    [void]$classContent.AppendLine("using System.Linq;")
    [void]$classContent.AppendLine("using System.Windows;")
    [void]$classContent.AppendLine()
    [void]$classContent.AppendLine("namespace $namespace")
    [void]$classContent.AppendLine("{")
    [void]$classContent.AppendLine("    public static class $className")
    [void]$classContent.AppendLine("    {")

    # Begin update of the class content for Load and FindResource methods.
    # Begin update of the class content for Load, Unload, and FindResource methods.
    [void]$classContent.AppendLine()
    [void]$classContent.AppendLine("        private static Uri _currentLoadedResourceUri;")
    [void]$classContent.AppendLine()
    [void]$classContent.AppendLine("        public static void Load(string resourceFileName)")
    [void]$classContent.AppendLine("        {")
    [void]$classContent.AppendLine('            var resourcePath = $"pack://application:,,,/{resourceFileName}";')
    [void]$classContent.AppendLine("            var newResourceUri = new Uri(resourcePath, UriKind.RelativeOrAbsolute);")
    [void]$classContent.AppendLine("            if (_currentLoadedResourceUri?.ToString().Equals(newResourceUri.ToString(), StringComparison.OrdinalIgnoreCase) ?? false)")
    [void]$classContent.AppendLine("                return;")
    [void]$classContent.AppendLine("            Unload();")
    [void]$classContent.AppendLine("            var resourceDictionary = new ResourceDictionary { Source = newResourceUri };")
    [void]$classContent.AppendLine("            Application.Current.Resources.MergedDictionaries.Add(resourceDictionary);")
    [void]$classContent.AppendLine("            _currentLoadedResourceUri = newResourceUri;")
    [void]$classContent.AppendLine("        }")
    [void]$classContent.AppendLine()
    [void]$classContent.AppendLine("        public static void Unload()")
    [void]$classContent.AppendLine("        {")
    [void]$classContent.AppendLine("            if (_currentLoadedResourceUri == null)")
    [void]$classContent.AppendLine("                return;")
    [void]$classContent.AppendLine("            var oldDictionary = Application.Current.Resources.MergedDictionaries")
    [void]$classContent.AppendLine("                .FirstOrDefault(d => d.Source?.Equals(_currentLoadedResourceUri) ?? false);")
    [void]$classContent.AppendLine("            if (oldDictionary == null)")
    [void]$classContent.AppendLine("                return;")
    [void]$classContent.AppendLine("            Application.Current.Resources.MergedDictionaries.Remove(oldDictionary);")
    [void]$classContent.AppendLine("            _currentLoadedResourceUri = null;")
    [void]$classContent.AppendLine("        }")
    [void]$classContent.AppendLine()
    [void]$classContent.AppendLine("        public static string FindResource(string key)")
    [void]$classContent.AppendLine("        {")
    [void]$classContent.AppendLine("            var resource = Application.Current.TryFindResource(key);")
    [void]$classContent.AppendLine("            if (resource == null && _currentLoadedResourceUri == null)")
    [void]$classContent.AppendLine("            {")
    [void]$classContent.AppendLine("                var assembly = typeof($className).Assembly;")
    [void]$classContent.AppendLine("                var assemblyName = assembly.GetName().Name;")
    [void]$classContent.AppendLine("                Load($`"{assemblyName};component/Resources/$className.xaml`");")
    [void]$classContent.AppendLine("                resource = Application.Current.TryFindResource(key);")
    [void]$classContent.AppendLine("            }")
    [void]$classContent.AppendLine("            return resource as string;")
    [void]$classContent.AppendLine("        }")
    [void]$classContent.AppendLine()
    
    # Correct handling for the XML namespace where sys:String elements are defined.
    $namespaceXamlPresentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    $namespaceSys = "clr-namespace:System;assembly=mscorlib"
    $xamlContent = [System.Xml.Linq.XDocument]::Load($resourceFile.FullName)

    # Get the correct namespace object for "sys" elements.
    # Fixed: Use XNamespace::Get() method correctly in PowerShell
    $nsSys = [System.Xml.Linq.XNamespace]::Get($namespaceSys)

    # Define the XAML namespace.
    $nsXaml = [System.Xml.Linq.XNamespace]::Get("http://schemas.microsoft.com/winfx/2006/xaml")

    # Fetch string elements considering the sys namespace.
    $stringElements = $xamlContent.Descendants($nsSys + "String")

    foreach ($element in $stringElements) {
        # Adjusted to use the XAML namespace to access the x:Key attribute.
        $keyAttr = $element.Attribute($nsXaml + "Key")
        if ($keyAttr -ne $null) {
            $key = $keyAttr.Value
            Write-Host "Processing Key: $key" # Debug output to confirm keys are being processed.
            #[void]$classContent.AppendLine("        public const string $key = nameof($key);")
            [void]$classContent.AppendLine("        public static string $key => FindResource(nameof($key));")
        }
        else {
            Write-Host "Key attribute not found in element: $($element)"
        }
    }

    [void]$classContent.AppendLine("    }")
    [void]$classContent.AppendLine("}")

    # Write the builder content to a .cs file.
    $outputPath = Join-Path $scriptPath "$className.xaml.cs"
    SaveToFile $outputPath $classContent.ToString()
}
