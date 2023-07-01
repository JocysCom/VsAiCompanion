using System.Text;
using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows.Shapes;

namespace JocysCom.VS.AiCompanion.Engine
{
	public class DocItem
	{
		public DocItem() { }

		public DocItem(string contents, string path = "", string type = "")
		{
			Data = contents;
			IsText = contents != null;
			if (!string.IsNullOrEmpty(path))
			{
				FullName = path;
				Kind = PhysicalFile_guid;
				Name = System.IO.Path.GetFileName(path);
			}
		}

		private const string PhysicalFile_guid = "{6BB5F8EE-4483-11D3-8BCF-00C04F8EC28C}";

		/// <summary>Code language of the document.</summary>
		public string Language { get; set; }

		/// <summary>
		/// Name of the document.
		/// </summary>
		public string Name { get; set; }

		/// <summary>Full path to the document.</summary>
		public string FullName { get; set; }

		/// <summary>
		/// Optional content of the document.
		/// </summary>
		public string Data { get; set; }

		/// <summary>
		/// Error
		/// </summary>
		public string Error { get; set; }

		/// <summary>
		/// Optional Kind of the document.
		/// </summary>
		public string Kind { get; set; }

		/// <summary>
		/// If physical file then...
		/// https://github.com/MicrosoftDocs/visualstudio-docs/blob/main/docs/extensibility/ide-guids.md
		/// </summary>
		public bool IsFile => Kind == PhysicalFile_guid;

		public bool IsText { get; set; }

		/// <summary>Document type.</summary>
		public string Type { get; set; }

		public static string ConvertFile(List<DocItem> items)
		{
			var sb = new StringBuilder();
			if (items == null || items.Count == 0)
				return sb.ToString();
			foreach (var item in items)
			{
				if (sb.Length > 0)
					sb.AppendLine();
				item.LoadData();
				var content = string.IsNullOrEmpty(item.Error)
					? item.Data
					: $"Unable to read file: {item.Error}";
				var type = item.IsText ? "" : "BINARY ";
				sb.AppendLine($"=== BEGIN {type}FILE: {item.FullName} ===");
				sb.Append(content);
				sb.AppendLine($"=== END {type}FILE: {item.FullName} ===");
			}
			return sb.ToString();
		}

		public long LoadData(long maxSize = long.MaxValue)
		{
			// Don't load non files.
			if (!IsFile)
				return 0;
			if (string.IsNullOrEmpty(FullName))
				return 0;
			if (!File.Exists(FullName))
				return 0;
			try
			{
				var fi = new FileInfo(FullName);
				if (fi.Length > maxSize)
				{
					Error = "File content is too large to display.";
					return 0;
				}
				// Read first 8 KB max to check if file is binary.
				IsText = !IsBinary(FullName, 1024 * 8);
				if (IsText)
				{
					Data = File.ReadAllText(FullName);
				}
				else
				{
					var bytes = File.ReadAllBytes(FullName);
					Data = System.Convert.ToBase64String(bytes, Base64FormattingOptions.InsertLineBreaks);
				}
				return fi.Length;
			}
			catch (Exception ex)
			{
				Error = ex.Message;
			}
			return 0;
		}

		public static List<DocItem> ConvertFile(string s)
		{
			var items = new List<DocItem>();
			var pattern = @"=== BEGIN (\w+ )?FILE: (?<FullName>.+?) ===\r?\n(?<Data>.*?)\r?\n=== END FILE: \k<FullName> ===";
			var matches = Regex.Matches(s, pattern, RegexOptions.Singleline);
			foreach (Match match in matches)
			{
				var item = new DocItem();
				item.FullName = match.Groups["FullName"].Value;
				item.Data = match.Groups["Data"].Value;
				item.Name = System.IO.Path.GetFileName(item.FullName);
				items.Add(item);
			}
			return items;
		}

		public static bool IsBinary(string fileName, long bytesToCheck = long.MaxValue)
		{
			using (var stream = File.OpenRead(fileName))
				return IsBinary(stream, bytesToCheck);
		}

		public static bool IsBinary(byte[] data, long bytesToCheck = long.MaxValue)
		{
			using (var stream = new MemoryStream(data))
				return IsBinary(stream, bytesToCheck);
		}

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
	}

}
