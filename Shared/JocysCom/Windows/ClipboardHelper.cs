using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

public static class ClipboardHelper
{

	/// <summary>
	/// Copies the specified content as a file to the clipboard.
	/// </summary>
	/// <param name="fileName">The name of the file to create (without path).</param>
	/// <param name="contents">The string contents of the file.</param>
	public static void SetClipboard(string fileName, string contents, string tempFolder = null)
	{
		// Write the contents to the temporary file
		var ext = Path.GetExtension(fileName);
		if (tempFolder == null)
			tempFolder = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
		var tempFilePath = Path.Combine(tempFolder, fileName);
		Directory.CreateDirectory(tempFolder);
		File.WriteAllText(tempFilePath, contents);

		// Create clipboard object.
		var dataObject = new DataObject();

		// Add a custom format to help identify clipboard data
		var clipboardIdentifier = Guid.NewGuid().ToString();
		dataObject.SetData(ClipboardFormatName, clipboardIdentifier);

		// Set Text format
		dataObject.SetText(contents);

		// If the file is an SVG then...
		if (ext.Equals(".svg", StringComparison.OrdinalIgnoreCase))
		{
			// add it to the clipboard as an image.
			dataObject.SetData("image/svg+xml", contents);
			dataObject.SetData(DataFormats.UnicodeText, contents);
		}

		// Set the FileDropList format.
		var fileDropList = new System.Collections.Specialized.StringCollection();
		_ = fileDropList.Add(tempFilePath);
		dataObject.SetFileDropList(fileDropList);

		// Place the data object onto the clipboard
		Clipboard.SetDataObject(dataObject, true);

		// Start monitoring the clipboard
		MonitorClipboard(ClipboardFormatName, clipboardIdentifier, tempFilePath);
	}

	/// <summary>
	/// Looks for svg image, svg text and svg file.
	/// </summary>
	public static string GetSvgFromClipboard()
	{
		string svgContent = null;
		var dataObject = Clipboard.GetDataObject();
		// Try to get content from image/svg+xml
		if (dataObject.GetDataPresent("image/svg+xml"))
		{
			object data = dataObject.GetData("image/svg+xml");
			if (data is string svgData)
			{
				svgContent = svgData;
				return svgContent;
			}
		}
		// Try to get content from UnicodeText
		if (dataObject.GetDataPresent(DataFormats.UnicodeText))
		{
			object data = dataObject.GetData(DataFormats.UnicodeText);
			if (data is string textData && IsValidSvg(textData))
			{
				svgContent = textData;
				return svgContent;
			}
		}
		// Try to get content from FileDrop
		if (dataObject.GetDataPresent(DataFormats.FileDrop))
		{
			var files = dataObject.GetData(DataFormats.FileDrop) as string[];
			if (files != null)
			{
				foreach (string file in files)
				{
					if (Path.GetExtension(file).Equals(".svg", StringComparison.OrdinalIgnoreCase))
					{
						try
						{
							svgContent = File.ReadAllText(file);
							return svgContent;
						}
						catch (Exception ex)
						{
							System.Diagnostics.Debug.WriteLine($"Failed to read SVG file '{file}': {ex.Message}");
						}
					}
				}
			}
		}
		return null;
	}

	private static bool IsValidSvg(string content)
	{
		// Basic validation to check if content contains SVG tags
		return !string.IsNullOrWhiteSpace(content) &&
			   content.IndexOf("<svg", StringComparison.OrdinalIgnoreCase) > -1 &&
			   content.IndexOf("</svg>", StringComparison.OrdinalIgnoreCase) > -1;
	}

	public static void SetDragDropEffects(IDataObject dataObject, DragDropEffects effects)
	{
		var dropEffect = new MemoryStream();
		byte[] effect = BitConverter.GetBytes((int)effects);
		dropEffect.Write(effect, 0, effect.Length);
		dropEffect.Position = 0;
		dataObject.SetData("Preferred DropEffect", dropEffect);
	}

	public static string ClipboardFormatName
		=> $"{typeof(ClipboardHelper).Namespace}ClipboardFormat";


	private static async void MonitorClipboard(string customFormat, string identifier, string tempFilePath)
	{
		// Wait until the clipboard data changes
		while (true)
		{
			// Check every half-second
			await Task.Delay(500);
			if (ClipboardHasChanged(customFormat, identifier))
				break;
		}
		// Clean up the temporary file after the clipboard data changes
		//DeleteTemporaryFile(tempFilePath);
	}

	private static bool ClipboardHasChanged(string customFormat, string originalIdentifier)
	{
		var hasChanged = true;
		try
		{
			var currentClipboardData = Clipboard.GetDataObject();
			if (currentClipboardData == null || !currentClipboardData.GetDataPresent(customFormat))
				return hasChanged;
			var data = currentClipboardData.GetData(customFormat) as string;
			if (data == originalIdentifier)
				hasChanged = false;
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine(ex.Message);
		}
		return hasChanged;
	}

	private static void DeleteTemporaryFile(string tempFilePath)
	{
		try
		{
			if (!File.Exists(tempFilePath))
				return;
			File.Delete(tempFilePath);
			// Delete the directory if empty
			string directory = Path.GetDirectoryName(tempFilePath);
			if (Directory.Exists(directory) && Directory.GetFiles(directory).Length == 0)
				Directory.Delete(directory);
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine(ex.Message);
		}
	}
}
