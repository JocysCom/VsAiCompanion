using JocysCom.ClassLibrary.Files;
using JocysCom.ClassLibrary.Runtime;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace JocysCom.ClassLibrary.Windows
{
	public static class ClipboardHelper
	{

		/// <summary>
		/// Copies the specified content as a text content and file to the clipboard.
		/// </summary>
		/// <param name="path">The name of the file to create (without path).</param>
		/// <param name="contents">The string contents of the file.</param>
		public static bool SetClipboard(string path)
		{
			if (!File.Exists(path))
				return false;
			var dataObject = new DataObject();
			var contents = File.ReadAllText(path);
			var isText = !Mime.IsBinary(path, 1024 * 8);
			if (isText)
				dataObject.SetText(contents);
			SetMimeContent(dataObject, path, contents);
			SetFileDropList(dataObject, path);
			Clipboard.SetDataObject(dataObject, true);
			return true;
		}

		private static void SetMimeContent(DataObject dataObject, string path, string contents)
		{
			var ext = Path.GetExtension(path);
			var format = Mime.GetMimeContentType(ext);
			dataObject.SetData(format, contents);
			dataObject.SetData(DataFormats.UnicodeText, contents);
		}

		private static void SetFileDropList(DataObject dataObject, params string[] paths)
		{
			// Set the FileDropList format.
			var fileDropList = new System.Collections.Specialized.StringCollection();
			fileDropList.AddRange(paths);
			dataObject.SetFileDropList(fileDropList);
		}

		/// <summary>
		/// Copies the specified content as a file to the clipboard.
		/// </summary>
		/// <param name="fileName">The name of the file to create (without path).</param>
		/// <param name="contents">The string contents of the file.</param>
		public static void SetClipboard(string fileName, string contents, string tempFolder = null)
		{
			// Write the contents to the temporary file
			if (tempFolder == null)
				tempFolder = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
			var tempFilePath = Path.Combine(tempFolder, fileName);
			Directory.CreateDirectory(tempFolder);
			File.WriteAllText(tempFilePath, contents);
			var dataObject = new DataObject();
			dataObject.SetText(contents);
			SetMimeContent(dataObject, fileName, contents);
			SetFileDropList(dataObject, fileName);
			// Because file don't exist, add a custom format to help identify clipboard data.
			// It will be used to remove file when clipboard is not needed.
			var clipboardIdentifier = Guid.NewGuid().ToString();
			dataObject.SetData(ClipboardFormatName, clipboardIdentifier);
			MonitorClipboard(ClipboardFormatName, clipboardIdentifier, tempFilePath);
			Clipboard.SetDataObject(dataObject, true);
		}

		public static Func<string, string> XmlToColorizedHtml;

		public static Exception SetXmlSerializable(Type itemType, object[] items, string[] names, string tempPath)
		{
			try
			{
				var copy = Array.CreateInstance(itemType, items.Length);
				Array.Copy(items, copy, copy.Length);
				// Serialize the items to a XML string.
				var xml = copy.Length == 1
					? Serializer.SerializeToXmlString(items[0], null, true)
					: Serializer.SerializeToXmlString(copy, null, true);
				var files = new System.Collections.Specialized.StringCollection();
				for (int i = 0; i < items.Length; i++)
				{
					var fileName = $"{names[i]}.xml";
					Directory.CreateDirectory(tempPath);
					var filePath = System.IO.Path.Combine(tempPath, fileName);
					System.IO.File.WriteAllText(filePath, xml);
					files.Add(filePath);
				}
				// Create a DataObject to hold both text and file drop list
				var dataObject = new DataObject();
				SetMimeContent(dataObject, ".xml", xml);
				if (XmlToColorizedHtml != null)
				{
					var html = XmlToColorizedHtml(xml);
					SetMimeContent(dataObject, ".html", html);
				}
				dataObject.SetText(xml, TextDataFormat.UnicodeText);
				dataObject.SetFileDropList(files);
				// Place the dataObject on the clipboard
				Clipboard.SetDataObject(dataObject, true);
			}
			catch (Exception ex)
			{
				return ex;
			}
			return null;
		}

		private static List<T> Deserialize<T>(Type itemType, string content)
		{
			var items = new List<T>();
			try
			{
				var xml = Clipboard.GetText()?.Trim();
				var isArray = xml.StartsWith($"<{nameof(Array)}");
				if (isArray)
				{
					var arrayType = Array.CreateInstance(itemType, 0).GetType();
					var array = Serializer.DeserializeFromXmlString(xml, arrayType) as Array;
					foreach (var item in array)
						items.Add((T)item);
				}
				else
				{
					var item = Serializer.DeserializeFromXmlString(xml, itemType);
					items.Add((T)item);
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine(ex.Message);
			}
			return items;
		}

		public static List<T> GetXmlSerializable<T>(Type itemType)
		{
			var dataObject = Clipboard.GetDataObject();
			var items = new List<T>();
			// Get content format by extention.
			var mimeFormat = Mime.GetMimeContentType(".xml");
			// Try to get object by content format.
			if (dataObject.GetDataPresent(mimeFormat))
			{
				var contents = dataObject.GetData(mimeFormat) as string;
				items = Deserialize<T>(itemType, contents);
				if (items.Any())
					return items;
			}
			// Try to get content from UnicodeText
			if (dataObject.GetDataPresent(DataFormats.UnicodeText))
			{
				var contents = Clipboard.GetText()?.Trim();
				items = Deserialize<T>(itemType, contents);
				if (items.Any())
					return items;
			}
			// Try to get content from FileDrop
			if (dataObject.GetDataPresent(DataFormats.FileDrop))
			{
				var files = dataObject.GetData(DataFormats.FileDrop) as string[];
				if (files != null)
				{
					foreach (string file in files)
					{
						if (Path.GetExtension(file).ToLower() == ".xml")
						{
							try
							{
								var xml = File.ReadAllText(file);
								var item = Serializer.DeserializeFromXmlString(xml, itemType);
								items.Add((T)item);
							}
							catch (Exception ex)
							{
								System.Diagnostics.Debug.WriteLine(ex.Message);
							}
						}
					}
				}
			}
			return items;
		}

		/// <summary>
		/// Get content as text from, clipbpoars. If content is binary then return path to tem file content.
		/// </summary>
		public static string GetContentFromClipboard(string fileExtension, string tempFolder = null)
		{
			var ext = fileExtension.ToLower();
			var dataObject = Clipboard.GetDataObject();
			// Get content format by extention.
			var mimeFormat = Mime.GetMimeContentType(ext);
			// Try to get object by content format.
			if (dataObject.GetDataPresent(mimeFormat))
			{
				var data = dataObject.GetData(mimeFormat);
				if (data is string contents)
					return contents;
			}
			// Try to get content from UnicodeText
			if (dataObject.GetDataPresent(DataFormats.UnicodeText))
			{
				var data = dataObject.GetData(DataFormats.UnicodeText);
				if (data is string contents)
				{
					if (ext == ".svg")
						return contents;
				}
			}
			// Try to get binary content like images...
			if (dataObject.GetDataPresent(DataFormats.Bitmap))
			{
				var image = (System.Drawing.Image)dataObject.GetData(DataFormats.Bitmap);
				if (image != null)
				{
					// Ensure temp folder is created
					if (tempFolder == null)
						tempFolder = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
					Directory.CreateDirectory(tempFolder);
					var imageFileName = Path.GetRandomFileName() + ext;
					var tempFilePath = Path.Combine(tempFolder, imageFileName);

					// Map extension to ImageFormat
					ImageFormat imageFormat;
					switch (ext)
					{
						case ".bmp":
							imageFormat = ImageFormat.Bmp;
							break;
						case ".jpg":
						case ".jpeg":
							imageFormat = ImageFormat.Jpeg;
							break;
						case ".png":
							imageFormat = ImageFormat.Png;
							break;
						case ".gif":
							imageFormat = ImageFormat.Gif;
							break;
						case ".tif":
						case ".tiff":
							imageFormat = ImageFormat.Tiff;
							break;
						default:
							// Default to PNG format if extension is unrecognized
							imageFormat = ImageFormat.Png;
							break;
					}
					// Save image to temp file
					image.Save(tempFilePath, imageFormat);
					return tempFilePath;
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
						if (Path.GetExtension(file).ToLower() == ext)
						{
							try
							{
								var contents = File.ReadAllText(file);
								return contents;
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

		/// <summary>
		/// Gets an image from the clipboard and saves it to a temporary file.
		/// Returns the path to the temporary image file.
		/// </summary>
		/// <param name="tempFolder">Optional. The folder where the temporary file should be saved. If null, a new temp folder will be created.</param>
		/// <returns>The path to the temporary image file if an image is found; otherwise, null.</returns>
		public static string GetImageFromClipboard(string tempFolder = null)
		{
			try
			{
				// Check if the clipboard contains image data
				if (Clipboard.ContainsImage())
				{
					// Retrieve the image from the clipboard
					BitmapSource bitmapSource = Clipboard.GetImage();
					if (bitmapSource != null)
					{
						// Determine the image format and extension
						// By default, we will use PNG
						string extension = ".png";
						BitmapEncoder encoder = new PngBitmapEncoder();

						// Optionally, check for more specific image formats in the clipboard data
						IDataObject dataObject = Clipboard.GetDataObject();
						if (dataObject != null)
						{
							if (dataObject.GetDataPresent("PNG"))
							{
								extension = ".png";
								encoder = new PngBitmapEncoder();
							}
							else if (dataObject.GetDataPresent("JFIF"))
							{
								extension = ".jpg";
								encoder = new JpegBitmapEncoder();
							}
							else if (dataObject.GetDataPresent("GIF"))
							{
								extension = ".gif";
								encoder = new GifBitmapEncoder();
							}
							else if (dataObject.GetDataPresent("TIFF"))
							{
								extension = ".tif";
								encoder = new TiffBitmapEncoder();
							}
							else if (dataObject.GetDataPresent(DataFormats.Dib))
							{
								extension = ".bmp";
								encoder = new BmpBitmapEncoder();
							}
							else
							{
								// Default to PNG format
								extension = ".png";
								encoder = new PngBitmapEncoder();
							}
						}

						// Ensure the temporary folder is created
						if (string.IsNullOrEmpty(tempFolder))
						{
							tempFolder = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
						}
						Directory.CreateDirectory(tempFolder);

						// Generate a random file name with the appropriate extension
						string imageFileName = Path.GetRandomFileName() + extension;
						string tempFilePath = Path.Combine(tempFolder, imageFileName);

						// Save the image to the temporary file
						using (FileStream fs = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write))
						{
							encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
							encoder.Save(fs);
						}

						return tempFilePath;
					}
				}
			}
			catch (Exception ex)
			{
				// Handle exceptions (e.g., access denied to clipboard)
				System.Diagnostics.Debug.WriteLine($"Error accessing clipboard image: {ex.Message}");
			}

			// Return null if no image is found or an error occurs
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

}
