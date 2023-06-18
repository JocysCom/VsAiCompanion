using System.Text;
using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace JocysCom.VS.AiCompanion.Engine
{
	public class DocItem
	{
		public DocItem() { }

		public DocItem(string text)
		{
			Data = text;
			Kind = PhysicalFile_guid;
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
		/// Optional Kind of the document.
		/// </summary>
		public string Kind { get; set; }

		/// <summary>Document type.</summary>
		public string Type { get; set; }

		/// <summary>
		/// If physical file then...
		/// https://github.com/MicrosoftDocs/visualstudio-docs/blob/main/docs/extensibility/ide-guids.md
		/// </summary>
		public bool IsText => Kind == PhysicalFile_guid;

		public static string ConvertFile(List<DocItem> items)
		{
			var sb = new StringBuilder();
			if (items == null || items.Count == 0)
				return sb.ToString();
			foreach (var item in items)
			{
				if (sb.Length > 0)
					sb.AppendLine();
				sb.AppendLine($"=== BEGIN FILE: {item.FullName} ===");
				if (item.IsText)
				{
					try
					{
						var content = File.ReadAllText(item.FullName);
						sb.AppendLine(content);
					}
					catch (Exception e)
					{
						sb.AppendLine($"Unable to read file: {e.Message}");
					}
				}
				sb.AppendLine($"=== END FILE: {item.FullName} ===");
			}
			return sb.ToString();
		}

		public static List<DocItem> ConvertFile(string s)
		{
			var items = new List<DocItem>();
			var pattern = @"=== BEGIN FILE: (?<FullName>.+?) ===\r?\n(?<Data>.*?)\r?\n=== END FILE: \k<FullName> ===";
			var matches = Regex.Matches(s, pattern, RegexOptions.Singleline);
			foreach (Match match in matches)
			{
				var item = new DocItem();
				item.FullName = match.Groups["FullName"].Value;
				item.Data = match.Groups["Data"].Value;
				item.Name = Path.GetFileName(item.FullName);
				items.Add(item);
			}
			return items;
		}

	}

}
