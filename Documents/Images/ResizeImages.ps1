using namespace System
using namespace System.IO

# Add reference to System.Drawing assembly
Add-Type -AssemblyName System.Drawing

# ----------------------------------------------------------------------------
# Get current command path.
[string]$current = $MyInvocation.MyCommand.Path
[FileInfo]$scriptFile = New-Object FileInfo($current)
# Set public parameters.
$global:scriptName = $scriptFile.Basename
$global:scriptPath = $scriptFile.Directory.FullName
# Cange current dir.
[Console]::WriteLine("Path: {0}", $scriptPath)
[Environment]::CurrentDirectory = $scriptPath

# Create the destination folder if it doesn't exist
if (!(Test-Path -Path ".\Half\")) {
    New-Item -ItemType Directory -Path ".\Half\"
}

# Get all PNG files in the current directory
$images = Get-ChildItem -Path ".\*.png"

# Iterate over each image
foreach ($image in $images) {
    # Load the image file
    $fullPath = Resolve-Path $image.FullName
    $img = [System.Drawing.Image]::FromFile($fullPath)

    # Calculate the new dimensions
    [int]$newWidth = [math]::Truncate($img.Width / 2)
    [int]$newHeight = [math]::Truncate($img.Height / 2)

    Write-Host "$newWidth x $newHeight"

    # Make sure the new dimensions are at least 1
    if ($newWidth -lt 1) { $newWidth = 1 }
    if ($newHeight -lt 1) { $newHeight = 1 }

    # Create a new bitmap object
    $bmp = New-Object System.Drawing.Bitmap($newWidth, $newHeight)

    # Draw the original image onto the new bitmap, effectively resizing it
    $graphics = [System.Drawing.Graphics]::FromImage($bmp)
    $graphics.DrawImage($img, 0, 0, $newWidth, $newHeight)

    # Save the new image to the 'Half' directory
    $newPath = Join-Path -Path "Half\" -ChildPath $image.Name

    $bmp.Save($newPath, [System.Drawing.Imaging.ImageFormat]::Png)

    # Dispose the image and graphics objects
    $graphics.Dispose()
    $img.Dispose()
}
