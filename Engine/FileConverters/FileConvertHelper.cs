using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml;
using JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using wp = DocumentFormat.OpenXml.Wordprocessing;
using System.Data;
using System.Globalization;
using CsvHelper;
using System.Linq;
using System;

namespace JocysCom.VS.AiCompanion.Engine.FileConverters
{


	internal class FileConvertHelper
	{

		public static ConvertTargetType[] AllExcept(params ConvertTargetType[] args)
		{
			return ((ConvertTargetType[])Enum.GetValues(typeof(ConvertTargetType)))
				.Except(new ConvertTargetType[] { ConvertTargetType.None })
				.Except(args)
				.ToArray();
		}

		public static Dictionary<ConvertTargetType, string> targetTypeToExtension =
			new Dictionary<ConvertTargetType, string>
			{
				{ ConvertTargetType.JSON, ".json" },
				{ ConvertTargetType.JSONL, ".jsonl" },
				{ ConvertTargetType.XLSX, ".xlsx" },
				{ ConvertTargetType.RTF, ".rtf" },
				{ ConvertTargetType.DOCX, ".docx" },
				{ ConvertTargetType.CSV, ".csv" },
			};


		public static Dictionary<string, ConvertTargetType[]> ConvertToTypesAvailable =
			targetTypeToExtension.ToDictionary(k => k.Value, v => AllExcept(v.Key));

		public static void ConvertFile(
			string fineTuneItemPath, string sourceDataName,
			file[] items, ConvertTargetType targetType, string aiModel,
			string systemPromptContent = null
		)
		{
			// Process files.
			foreach (var item in items)
			{
				var sourceExt = Path.GetExtension(item.filename).ToLower();
				var targetExt = targetTypeToExtension[targetType];
				// If nothing to convert then continue.
				if (sourceExt == targetExt)
					continue;
				var sourceBase = Path.GetFileNameWithoutExtension(item.filename);
				var sourceFullName = Path.Combine(fineTuneItemPath, sourceDataName, item.filename);
				var targetFolder = targetType == ConvertTargetType.JSONL
					? FineTuningItem.TuningData
					: FineTuningItem.SourceData;
				var targetFullName = Path.Combine(fineTuneItemPath, targetFolder, sourceBase + targetExt);
				if (Client.IsTextCompletionMode(aiModel))
					Convert<text_completion_item>(sourceFullName, targetFullName);
				else
					Convert<chat_completion_request>(sourceFullName, targetFullName);
			}
		}

		public static void Convert<T>(string sourcePath, string targetPath)
		{
			var sourceExt = Path.GetExtension(sourcePath).ToLower();
			var targetExt = Path.GetExtension(targetPath).ToLower();
			List<T> items = null;
			// Read from file.
			switch (sourceExt)
			{
				case ".jsonl":
					items = ReadFromJsonl<T>(sourcePath);
					break;
				case ".json":
					items = ReadFromJson<T>(sourcePath);
					break;
				default:
					break;
			}
			if (items == null)
			{
				MessageBox.Show($"Failed to read from from {sourcePath}!");
				return;
			}
			// Write to file.
			switch (targetExt)
			{
				case ".jsonl":
					WriteToJsonl(targetPath, items);
					break;
				case ".json":
					WriteToJson(targetPath, items);
					break;
				case ".xlsx":
					WriteToXlsx(targetPath, items);
					break;
				case ".rtf":
					WriteToRtf(targetPath, items);
					break;
				case ".docx":
					WriteToDocx(targetPath, items);
					break;
				case ".csv":
					WriteToCsv(targetPath, items);
					break;
				default:
					break;
			}
		}

		public static bool AllowToWrite(string targetFile)
		{
			var tfi = new FileInfo(targetFile);
			if (!tfi.Exists)
				return true;
			var text = $"Do you want to overwrite {tfi.Name} file?";
			var caption = $"{Global.Info.Product} - Overwrite";
			var result = MessageBox.Show(text, caption, MessageBoxButton.YesNo, MessageBoxImage.Question);
			if (result != MessageBoxResult.Yes)
				return false;
			File.Delete(targetFile);
			return true;
		}

		#region Java Script Object Notation Lines (*.jsonl)

		public static string WriteToJsonl<T>(string path, List<T> o)
		{
			using (var writer = File.CreateText(path))
				foreach (var item in o)
					writer.WriteLine(Client.Serialize(item));
			return null;
		}

		public static List<T> ReadFromJsonl<T>(string path)
		{
			var items = new List<T>();
			foreach (string line in File.ReadLines(path))
				items.Add(Client.Deserialize<T>(line));
			return items;
		}

		#endregion

		#region Java Script Object Notation (*.json)

		public static string WriteToJson<T>(string path, List<T> o)
		{
			var contents = Client.Serialize(o);
			File.WriteAllText(path, contents);
			return null;
		}

		public static List<T> ReadFromJson<T>(string path)
		{
			var json = File.ReadAllText(path);
			return Client.Deserialize<List<T>>(json);
		}

		#endregion

		#region Extensible Markup Language (*.xml)

		public static void WriteAsXml(string path, List<chat_completion_request> o)
		{
			JocysCom.ClassLibrary.Runtime.Serializer.SerializeToXmlFile(o, path);
		}

		public static List<chat_completion_request> ReadFromXml(string path)
		{
			return JocysCom.ClassLibrary.Runtime.Serializer.DeserializeFromXmlFile<List<chat_completion_request>>(path);
		}

		#endregion

		#region Rich Text Format (*.rtf)

		static void AddRtfLine(System.Windows.Forms.RichTextBox rtf, string text = null, bool isBold = false)
		{
			if (!string.IsNullOrEmpty(text))
			{
				int startPos = rtf.Text.Length;
				rtf.AppendText(text);
				rtf.Select(startPos, text.Length);
				if (isBold)
					rtf.SelectionFont = new System.Drawing.Font(rtf.Font, System.Drawing.FontStyle.Bold);
			}
			rtf.AppendText("\n");
		}

		public static void WriteToRtf<T>(string path, List<T> o)
		{
			var rtf = new System.Windows.Forms.RichTextBox();
			foreach (var request in o)
			{
				if (request is chat_completion_request cr)
				{
					foreach (var message in cr.messages)
					{
						if (message.role == message_role.user)
						{
							AddRtfLine(rtf, message.content, true);
							AddRtfLine(rtf);
						}
						if (message.role == message_role.assistant)
						{
							AddRtfLine(rtf, message.content);
							AddRtfLine(rtf);
						}
					}
				}
				if (request is text_completion_item tr)
				{
					AddRtfLine(rtf, tr.prompt, true);
					AddRtfLine(rtf);
					AddRtfLine(rtf, tr.completion);
					AddRtfLine(rtf);
				}
			}
			rtf.SaveFile(path, System.Windows.Forms.RichTextBoxStreamType.RichText);
		}

		#endregion

		#region Microsoft Word Document (*.docx)

		static void AddDocxParagraph(wp.Body body, string text = null, bool isBold = false)
		{
			var para = body.AppendChild(new wp.Paragraph());
			var run = para.AppendChild(new wp.Run());
			if (!string.IsNullOrEmpty(text))
			{
				run.AppendChild(new wp.Text(text));
				if (isBold)
				{
					run.RunProperties = new wp.RunProperties();
					run.RunProperties.AppendChild(new wp.Bold());
				}
			}
		}

		public static void WriteToDocx<T>(string path, List<T> o)
		{
			using (var wordDocument = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document))
			{
				var mainPart = wordDocument.AddMainDocumentPart();
				mainPart.Document = new wp.Document(new wp.Body());
				foreach (var request in o)
				{
					if (request is chat_completion_request cr)
					{
						foreach (var message in cr.messages)
						{
							if (message.role == message_role.user)
								AddDocxParagraph(mainPart.Document.Body, message.content, true);
							if (message.role == message_role.assistant)
								AddDocxParagraph(mainPart.Document.Body, message.content);
						}
					}
					if (request is text_completion_item tr)
					{
						AddDocxParagraph(mainPart.Document.Body, tr.prompt, true);
						AddDocxParagraph(mainPart.Document.Body, tr.completion);
					}
				}
				mainPart.Document.Save();
				wordDocument.Dispose();
			}
		}

		#endregion

		#region Microsoft Excel Worksheet (*.xlsx)

		public static void WriteToXlsx<T>(string path, List<T> o)
		{
			using (var spreadsheet = SpreadsheetDocument.Create(path, SpreadsheetDocumentType.Workbook))
			{
				var workbookPart = spreadsheet.AddWorkbookPart();
				workbookPart.Workbook = new Workbook();

				var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
				var workSheet = new Worksheet();
				worksheetPart.Worksheet = workSheet;

				var sheets = spreadsheet.WorkbookPart.Workbook.AppendChild(new Sheets());
				var sheet = new Sheet() { Id = spreadsheet.WorkbookPart.GetIdOfPart(worksheetPart), SheetId = 1, Name = "Sheet 1" };
				sheets.Append(sheet);

				SheetData sheetData = worksheetPart.Worksheet.AppendChild(new SheetData());

				// Column headers
				var headerRow = new Row();
				var headerCell1 = new Cell() { DataType = CellValues.String, CellValue = new CellValue("user") };
				var headerCell2 = new Cell() { DataType = CellValues.String, CellValue = new CellValue("assistant") };
				headerRow.Append(headerCell1, headerCell2);
				sheetData.Append(headerRow);

				foreach (var request in o)
				{
					var row = new Row();
					if (request is chat_completion_request cr)
					{
						foreach (var message in cr.messages)
						{
							var cell = new Cell();
							if (message.role == message_role.user)
							{
								cell.DataType = CellValues.String;
								cell.CellValue = new CellValue(message.content);
								row.Append(cell);
							}
							if (message.role == message_role.assistant)
							{
								cell.DataType = CellValues.String;
								cell.CellValue = new CellValue(message.content);
								row.Append(cell);
							}
						}
					}
					if (request is text_completion_item tr)
					{
						var cell = new Cell();
						cell.DataType = CellValues.String;
						cell.CellValue = new CellValue(tr.prompt);
						row.Append(cell);
						cell.DataType = CellValues.String;
						cell.CellValue = new CellValue(tr.completion);
						row.Append(cell);
					}
					sheetData.Append(row);
				}
				workbookPart.Workbook.Save();
			}
		}

		#endregion

		#region Comma Separated Values (*.csv)

		public static void WriteToCsv<T>(string path, List<T> o)
		{
			var isChat = typeof(T) == typeof(chat_completion_request);
			using (var writer = new StreamWriter(path))
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				if (isChat)
				{
					csv.WriteField(message_role.user);
					csv.WriteField(message_role.assistant);
					csv.NextRecord();
				}
				else
				{
					csv.WriteField(nameof(text_completion_item.prompt));
					csv.WriteField(nameof(text_completion_item.completion));
					csv.NextRecord();
				}
				foreach (var request in o)
				{
					if (request is chat_completion_request cr)
					{
						var userContet = cr.messages.FirstOrDefault(x => x.role == message_role.user)?.content ?? "";
						var assistantContent = cr.messages.FirstOrDefault(x => x.role == message_role.assistant)?.content ?? "";
						csv.WriteField(userContet);
						csv.WriteField(assistantContent);
						csv.NextRecord();
					}
					else if (request is text_completion_item tr)
					{
						csv.WriteField(tr.prompt);
						csv.WriteField(tr.completion);
						csv.NextRecord();
					}
				}
			}
		}

		public static List<T> ReadFromCsv<T>(string path)
		{
			var list = new List<T>();
			var isChat = typeof(T) == typeof(chat_completion_message);
			using (var reader = new StreamReader(path))
			{
				var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
				csv.Read(); // Must call this to read the header record.
				csv.ReadHeader();
				var columns = csv.HeaderRecord;
				var indexes = columns.Select(name => csv.GetFieldIndex(name)).ToArray();
				while (csv.Read())
				{
					var items = new List<string>();
					foreach (var i in indexes)
						items.Add(csv.GetField(i));
					var item = (T)Activator.CreateInstance(typeof(T));
					if (item is chat_completion_request cr)
					{
						cr.messages = new List<chat_completion_message>();
						cr.messages.Add(new chat_completion_message(message_role.user, items[0]));
						cr.messages.Add(new chat_completion_message(message_role.user, items[1]));
					}
					else if (item is text_completion_item tr)
					{
						tr.prompt = items[0];
						tr.completion = items[1];
					}
					list.Add(item);
				}
			}
			return list;

		}

		#endregion

	}
}
