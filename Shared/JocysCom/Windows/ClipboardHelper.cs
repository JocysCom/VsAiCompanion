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
		var ext = Path.GetExtension(fileName);
		// Generate a unique temporary file path
		if (tempFolder == null)
			tempFolder = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

		var tempFilePath = Path.Combine(tempFolder, fileName);

		// Ensure the directory exists
		Directory.CreateDirectory(Path.GetDirectoryName(tempFilePath));

		// Write the contents to the temporary file
		File.WriteAllText(tempFilePath, contents);

		// Create a FileDrop data object
		DataObject dataObject = new DataObject();
		string[] files = new string[] { tempFilePath };
		dataObject.SetData(DataFormats.FileDrop, files);

		// Set the preferred drop effect (Copy)
		MemoryStream dropEffect = new MemoryStream();
		byte[] dropEffectBytes = new byte[] { 5, 0, 0, 0 }; // 5 for Copy
		dropEffect.Write(dropEffectBytes, 0, dropEffectBytes.Length);
		dataObject.SetData("Preferred DropEffect", dropEffect);

		// Add a custom format to help identify our clipboard data
		string customClipboardFormat = "MyAppClipboardFormat";
		string clipboardIdentifier = Guid.NewGuid().ToString();
		dataObject.SetData(customClipboardFormat, clipboardIdentifier);

		// Place the data object onto the clipboard
		Clipboard.SetDataObject(dataObject, true);

		// Start monitoring the clipboard
		MonitorClipboard(customClipboardFormat, clipboardIdentifier, tempFilePath);
	}

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
		DeleteTemporaryFile(tempFilePath);
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
