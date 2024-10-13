using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace JocysCom.VS.AiCompanion.Plugins.Core.VsFunctions
{
	/// <summary>
	/// Visual Studio Document Item.
	/// </summary>
	public class DocItem
	{

		/// <summary>
		/// Visual Studio Document Item.
		/// </summary>
		public DocItem() { }

		/// <summary>
		/// Visual Studio Document Item.
		/// </summary>
		public DocItem(string contents, string path = "", string type = "")
		{
			ContentData = contents;
			IsText = contents != null;
			if (!string.IsNullOrEmpty(path))
			{
				FullName = path;
				Kind = PhysicalFile_guid;
				Name = System.IO.Path.GetFileName(path);
			}
		}

		private const string PhysicalFile_guid = "{6BB5F8EE-4483-11D3-8BCF-00C04F8EC28C}";

		/// <summary>
		/// Name of the document.
		/// </summary>
		public string Name { get; set; }

		/// <summary>Full path to the document.</summary>
		public string FullName { get; set; }

		/// <summary>
		/// Parent project name of the document.
		/// </summary>
		public string ProjectName { get; set; }

		/// <summary>
		/// If physical file then...
		/// https://github.com/MicrosoftDocs/visualstudio-docs/blob/main/docs/extensibility/ide-guids.md
		/// </summary>
		public bool IsFile => Kind == PhysicalFile_guid;

		/// <summary>
		/// True if content is text.
		/// </summary>
		public bool IsText { get; set; }

		/// <summary>
		/// True if content is saved.
		/// </summary>
		public bool IsSaved { get; set; }

		/// <summary>Document type.</summary>
		public string DocumentType { get; set; }

		/// <summary>Document context type.</summary>
		[DefaultValue(ContextType.None)]
		public ContextType ContextType { get; set; }

		/// <summary>Code language of the document.</summary>
		public string Language { get; set; }

		/// <summary>
		/// Error
		/// </summary>
		public string Error { get; set; }

		/// <summary>
		/// Optional Kind of the document.
		/// </summary>
		public string Kind { get; set; }

		/// <summary>File content size.</summary>
		[DefaultValue(null)]
		public long? Size { get; set; }

		/// <summary>File last write time (UTC).</summary>
		[DefaultValue(null)]
		public DateTime? LastWriteTime { get; set; }

		/// <summary>File creation time (UTC).</summary>
		[DefaultValue(null)]
		public DateTime? CreationTime { get; set; }

		/// <summary>
		/// Hint for AI.
		/// </summary>
		public string ContentHint { get; set; }

		// Place the property with the largest serializable content at the end.

		/// <summary>
		/// Optional content of the document.
		/// </summary>
		public string ContentData { get; set; }

		#region Methods

		/// <summary>
		///  Convert string representation back to DocItem.
		/// </summary>
		/// <param name="s">Content to convert</param>
		/// <returns></returns>
		public static List<DocItem> ConvertFile(string s)
		{
			var items = new List<DocItem>();
			var pattern = @"=== BEGIN (\w+ )?FILE: (?<FullName>.+?) ===\r?\n(?<Data>.*?)\r?\n=== END FILE: \k<FullName> ===";
			var matches = Regex.Matches(s, pattern, RegexOptions.Singleline);
			foreach (Match match in matches)
			{
				var item = new DocItem();
				item.FullName = match.Groups["FullName"].Value;
				item.ContentData = match.Groups["Data"].Value;
				item.Name = System.IO.Path.GetFileName(item.FullName);
				items.Add(item);
			}
			return items;
		}

		/// <summary>
		/// Convert to flat string representation.
		/// </summary>
		/// <param name="items">Item to convert.</param>
		public static string ConvertFile(IList<DocItem> items)
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
					? item.ContentData
					: $"Unable to read file: {item.Error}";
				var type = item.IsText ? "" : "BINARY ";
				sb.AppendLine($"=== BEGIN {type}FILE: {item.FullName} ===");
				sb.Append(content);
				if (!content.EndsWith(Environment.NewLine))
					sb.AppendLine();
				sb.AppendLine($"=== END {type}FILE: {item.FullName} ===");
			}
			return sb.ToString();
		}

		/// <summary>
		/// Load content into `Data` property.
		/// </summary>
		public long LoadData(byte[] data)
		{
			ContentData = Convert.ToBase64String(data, Base64FormattingOptions.InsertLineBreaks);
			ContentHint = "Base64EncodedBinary"; // Explicitly states the encoding method for binary data
			return data.Length;
		}

		/// <summary>
		/// Load file metadata.
		/// </summary>
		public bool LoadFileInfo()
		{
			var s = FullName;
			if (string.IsNullOrEmpty(s))
				return false;
			var invalid = s.ToCharArray().Intersect(System.IO.Path.GetInvalidPathChars());
			if (invalid.Any())
				return false;
			try
			{
				var fi = new FileInfo(s);
				if (!fi.Exists)
					return false;
				FullName = fi.FullName;
				Name = fi.Name;
				Size = fi.Length;
				LastWriteTime = fi.LastWriteTimeUtc;
				CreationTime = fi.CreationTimeUtc;
			}
			catch (Exception)
			{
				return false;
			}
			return true;
		}

		/// <summary>
		/// Get content data as orignal binary data.
		/// </summary>
		public byte[] GetDataBinary()
		{
			return Convert.FromBase64String(ContentData);
		}

		/// <summary>
		/// Load content into `Data` property.
		/// </summary>
		/// <param name="maxSize">Maximum bytes to load.</param>
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
				IsText = !JocysCom.ClassLibrary.Files.Mime.IsBinary(FullName, 1024 * 8);
				if (IsText)
				{
					ContentData = File.ReadAllText(FullName);
					ContentHint = "PlainText"; // Simplified hint, assumes understanding that this is directly readable
				}
				else
				{
					var bytes = File.ReadAllBytes(FullName);
					LoadData(bytes);
					return fi.Length;
				}
			}
			catch (Exception ex)
			{
				Error = ex.Message;
			}
			return 0;
		}

		#endregion

	}


}
