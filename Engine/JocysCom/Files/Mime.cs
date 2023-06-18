#if NETSTANDARD // .NET Standard
#else

using Microsoft.Win32;
using System;

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
			return "data:" + contentType + ";base64," + base64;
		}



	}
}

#endif
