#if NETSTANDARD // .NET Standard
#else

using Microsoft.Win32;
using System;
using System.IO;

namespace JocysCom.ClassLibrary.Files
{
	public class Mime
	{

		/// <summary>
		/// Get MIME Content Type by file extension. For example ".gif" -> "image/gif".
		/// This is needed if you want to stream files thru scripts.
		/// For example: getFile.aspx?name=SomePicture.gif
		/// </summary>
		/// <param name="fileExtension">File extension.</param>
		/// <returns>MIME Content Type</returns>
		public static string GetMimeContentType(string fileExtension)
		{
			var contentType = string.Empty;
			var key = Registry.ClassesRoot;
			RegistryKey rKey;
			rKey = key.OpenSubKey(fileExtension);
			if (rKey != null)
			{
				var value = rKey.GetValue("Content Type");
				if (value != null)
					contentType = value.ToString();
				rKey.Dispose();
			}
			return contentType;
		}

		/// <summary>
		/// Get MIME file extension by MIME Content Type. For example "image/jpeg" -> ".jpg".
		/// This is needed if you want to save embded objects from MIME Message to disk.
		/// </summary>
		/// <param name="contentType">MIME Content Type</param>
		/// <returns></returns>
		public static string GetMimeFileExtension(string contentType)
		{
			string fileExtension = string.Empty;
			var key = Registry.ClassesRoot.OpenSubKey("MIME").OpenSubKey("Database");
			var rKey = key.OpenSubKey("Content Type").OpenSubKey(contentType);
			if (rKey != null)
			{
				object value = rKey.GetValue("Extension");
				if (value != null) fileExtension = value.ToString();
				rKey.Dispose();
			}
			return fileExtension;
		}

		/// <summary>
		/// Gets a value that indicates the name of the associated application with the behavior to handle this extension.
		/// </summary>
		/// <param name="fileExtension">File extension.</param>
		public static string GetProgId(string fileExtension)
		{
			var key = Registry.ClassesRoot.OpenSubKey(fileExtension);
			if (key is null)
				return null;
			var val = key.GetValue("", null, RegistryValueOptions.DoNotExpandEnvironmentNames);
			key.Dispose();
			return val is null
				? string.Empty
				: val.ToString();
		}

		/// <summary>
		/// Gets a value that determines what the friendly name of the file is.
		/// </summary>
		/// <param name="fileExtension">File extension.</param>
		public static string GetFileDescription(string fileExtension)
		{
			var progId = GetProgId(fileExtension);
			if (string.IsNullOrEmpty(progId))
				return string.Empty;
			var key = Registry.ClassesRoot;
			key = key.OpenSubKey(progId);
			if (key is null)
				return null;
			var val = key.GetValue("", null, RegistryValueOptions.DoNotExpandEnvironmentNames);
			key.Dispose();
			if (val is null)
				return string.Empty;
			return val.ToString();
		}

		/// <summary>
		/// https://en.wikipedia.org/wiki/Data_URI_scheme
		/// </summary>
		public static string GetResourceDataUri(string resourceName, byte[] data)
		{
			var extension = System.IO.Path.GetExtension(resourceName);
			var contentType = GetMimeContentType(extension);
			var base64 = System.Convert.ToBase64String(data);
			//data:[<mediatype>][;base64],<data>
			return "data:" + contentType + ";base64," + base64;
		}

		public static bool TryParseDataUri(string uri, out string mimeType, out byte[] data)
		{
			mimeType = "";
			data = null;
			// Check if the URI starts with "data:"
			if (!uri.StartsWith("data:"))
				return false;
			// Find the first comma; the information before it is the MIME type and encoding
			var commaIndex = uri.IndexOf(',');
			if (commaIndex == -1)
				return false;
			// Extracting the MIME type and encoding info
			var typeAndEncoding = uri.Substring(5, commaIndex - 5); // Extract info between "data:" and the comma
			var base64Data = uri.Substring(commaIndex + 1); // Extract the data after the comma
															// Default MIME type if none is specified
			if (string.IsNullOrWhiteSpace(typeAndEncoding))
			{
				mimeType = "text/plain;charset=US-ASCII";
			}
			else
			{
				var semiColonIndex = typeAndEncoding.IndexOf(';');
				if (semiColonIndex != -1)
					mimeType = typeAndEncoding.Substring(0, semiColonIndex);
				else
					mimeType = typeAndEncoding; // If there's no semi-colon, the whole string is the MIME type
			}
			// Check if the data is encoded in base64
			if (typeAndEncoding.EndsWith(";base64", StringComparison.OrdinalIgnoreCase))
				data = System.Convert.FromBase64String(base64Data);
			else
				// Handling data not encoded in base64 (Not covered as the initial method only handles base64)
				return false;
			return true;
		}

		#region IsBinary

		/// <summary>
		/// Return true if content of the file is binary.
		/// </summary>
		/// <param name="fileName">File to check.</param>
		/// <param name="bytesToCheck">Maximum amount of bytes to check.</param>
		public static bool IsBinary(string fileName, long bytesToCheck = long.MaxValue)
		{
			using (var stream = File.OpenRead(fileName))
				return IsBinary(stream, bytesToCheck);
		}

		/// <summary>
		/// Return true if content is binary.
		/// </summary>
		/// <param name="data">Bytes to check.</param>
		/// <param name="bytesToCheck">Maximum amount of bytes to check.</param>
		public static bool IsBinary(byte[] data, long bytesToCheck = long.MaxValue)
		{
			using (var stream = new MemoryStream(data))
				return IsBinary(stream, bytesToCheck);
		}

		/// <summary>
		/// Return true if sram content is binary.
		/// </summary>
		/// <param name="stream">Stream to check.</param>
		/// <param name="bytesToCheck">Maximum amount of bytes to check.</param>
		public static bool IsBinary(Stream stream, long bytesToCheck = long.MaxValue)
		{
			long bytesChecked = 0;
			var buffer = new byte[4096]; // 4 KB chunks
			while (bytesChecked < bytesToCheck)
			{
				int bytesRead = stream.Read(buffer, 0, buffer.Length);
				if (bytesRead == 0)
					break; // End of file
				for (int i = 0; i < bytesRead && bytesChecked < bytesToCheck; i++, bytesChecked++)
				{
					byte b = buffer[i];
					// Found a control character that isn't tab, line feed, or carriage return.
					// This is a simplistic check and may not hold true for all text files,
					// especially those using non-ASCII or non-UTF-8 encodings.
					if (b < 0x20 && b != 0x09 && b != 0x0A && b != 0x0D)
						return true;
				}
			}
			// If we made it through the whole file without finding a non-text character,
			// then we'll assume it's not a binary file.
			return false;
		}

		#endregion

	}
}

#endif
