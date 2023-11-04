using CsvHelper;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows;
using wp = DocumentFormat.OpenXml.Wordprocessing;

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

		public static string ConvertFile(
			string fineTuneItemPath, string sourceDataName,
			file item, ConvertTargetType targetType, string aiModel,
			string systemMessage = null
		)
		{
			var sourceExt = Path.GetExtension(item.filename).ToLower();
			var targetExt = targetTypeToExtension[targetType];
			// If nothing to convert then continue.
			if (sourceExt == targetExt)
				return null;
			var sourceBase = Path.GetFileNameWithoutExtension(item.filename);
			var sourceFullName = Path.Combine(fineTuneItemPath, sourceDataName, item.filename);
			var targetFolder = targetType == ConvertTargetType.JSONL
				? FineTuningFolderType.TuningData
				: FineTuningFolderType.SourceData;
			var targetFullName = Path.Combine(fineTuneItemPath, targetFolder.ToString(), sourceBase + targetExt);
			if (Client.IsTextCompletionMode(aiModel))
			{
				Convert<text_completion_item>(sourceFullName, targetFullName, null);
			}
			else
			{
				Convert<chat_completion_request>(sourceFullName, targetFullName, (r) =>
				{
					if (string.IsNullOrEmpty(systemMessage))
						return;
					if (r.messages.Any(x => x.role == message_role.system))
						return;
					r.messages.Insert(0, new chat_completion_message(message_role.system, systemMessage));
				});
			}
			return targetFullName;
		}

		public static void Convert<T>(string sourcePath, string targetPath, Action<T> process) where T : class
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
				case ".xlsx":
					items = ReadFromXlsx<T>(sourcePath);
					break;
				default:
					break;
			}
			if (items == null)
			{
				MessageBox.Show($"Failed to read from from {sourcePath}!");
				return;
			}
			// Process items.
			if (process != null)
				foreach (var item in items)
					process(item);
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
			var options = Client.GetJsonOptions();
			options.WriteIndented = true;
			var contents = JsonSerializer.Serialize(o, options);
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

		static void AddRtfLine(StringBuilder rtf, string text = null, bool isBold = false)
		{
			if (!string.IsNullOrEmpty(text))
			{
				if (isBold)
					rtf.AppendLine(@"{\b " + text + @"\b0}");
				else
					rtf.AppendLine(text);
			}
			else
			{
				rtf.AppendLine();
			}
		}

		public static void WriteToRtf<T>(string path, List<T> o)
		{
			var rtf = new StringBuilder(@"{\rtf1\ansi");
			var boldFont = new System.Drawing.Font(System.Windows.Forms.Control.DefaultFont, System.Drawing.FontStyle.Bold);
			foreach (var request in o)
			{
				if (request is chat_completion_request cr)
				{
					foreach (var message in cr.messages)
					{
						AddRtfLine(rtf, message.content, message.role == message_role.user);
					}
				}
				else if (request is text_completion_item tr)
				{
					AddRtfLine(rtf, tr.prompt, true);
					AddRtfLine(rtf, tr.completion);
				}
			}
			rtf.Append(@"}");
			var rtb = new System.Windows.Forms.RichTextBox();
			rtb.Rtf = rtf.ToString();
			rtb.SaveFile(path, System.Windows.Forms.RichTextBoxStreamType.RichText);
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

		private static string GetExcelColumnName(Cell cell)
		{
			return new string(cell.CellReference
				.ToString()
				.ToCharArray()
				.Where(char.IsLetter)
				.ToArray());
		}

		static string GetCellValue(Cell cell, SharedStringTablePart stringTablePart)
		{
			return cell.DataType != null && cell.DataType.Value == CellValues.SharedString
				? stringTablePart.SharedStringTable.ChildElements[int.Parse(cell.CellValue.InnerXml)].InnerText
				: cell.CellValue?.InnerXml;
		}

		public static List<T> ReadFromXlsx<T>(string path) where T : class
		{
			var result = new List<T>();
			using (var spreadsheet = SpreadsheetDocument.Open(path, false))
			{
				var workbookPart = spreadsheet.WorkbookPart;
				var stringTablePart = workbookPart.GetPartsOfType<SharedStringTablePart>().FirstOrDefault();
				var worksheetPart = workbookPart.WorksheetParts.First();
				var sheetData = worksheetPart.Worksheet.Elements<SheetData>().First();
				var rows = sheetData.Elements<Row>().ToList();
				// if no rows found in the Excel.
				if (rows.Count < 1)
					return result;
				var headerRow = rows[0];
				var headerCells = headerRow.Elements<Cell>().ToList();
				var promptNames = new string[] { "prompt", "user", "question" };
				var answerNames = new string[] { "answer", "assistant" };
				var columns = headerCells.ToDictionary(
					k => GetCellValue(k, stringTablePart)?.ToLower(),
					v => v);
				var promptColumns = columns.Where(x => promptNames.Any(n => x.Key.Contains(n))).Select(x => x.Value).ToArray();
				var answerColumns = columns.Where(x => answerNames.Any(n => x.Key.Contains(n))).Select(x => x.Value).ToArray();
				// If prompt or answer column not found in the Excel then return.
				if (promptColumns.Length == 0 || answerColumns.Length == 0)
					return result;
				var promptColumnNames = promptColumns.Select(x => GetExcelColumnName(x)).ToArray();
				var answerColumnNames = answerColumns.Select(x => GetExcelColumnName(x)).ToArray();
				// Skip the header row.
				foreach (var row in rows.Skip(1))
				{
					// Get cells with values.
					var cells = row.Elements<Cell>().ToList();
					var columnCells = cells.ToDictionary(k => GetExcelColumnName(k), v => v);
					foreach (var p in promptColumnNames)
					{
						// Continue if cell is empty.
						if (!columnCells.ContainsKey(p))
							continue;
						var promtText = GetCellValue(columnCells[p], stringTablePart) ?? "";
						if (string.IsNullOrEmpty(promtText.Trim()))
							continue;
						foreach (var a in answerColumnNames)
						{
							// Continue if cell is empty.
							if (!columnCells.ContainsKey(a))
								continue;
							var answerText = GetCellValue(columnCells[a], stringTablePart) ?? "";
							if (string.IsNullOrEmpty(answerText.Trim()))
								continue;
							if (typeof(T) == typeof(chat_completion_request))
							{
								var cr = new chat_completion_request { messages = new List<chat_completion_message>() };
								var pMessage = new chat_completion_message(message_role.user, promtText);
								cr.messages.Add(pMessage);
								var aMessage = new chat_completion_message(message_role.assistant, answerText);
								cr.messages.Add(aMessage);
								result.Add(cr as T);
							}
							else if (typeof(T) == typeof(text_completion_item))
							{
								var tr = new text_completion_item();
								tr.prompt = promtText;
								tr.completion = answerText;
								result.Add(tr as T);
							}
						}
					}
				}

			}
			return result;
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
