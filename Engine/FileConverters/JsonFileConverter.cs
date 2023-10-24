using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml;
using JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using wp = DocumentFormat.OpenXml.Wordprocessing;
using DocumentFormat.OpenXml.Spreadsheet;

namespace JocysCom.VS.AiCompanion.Engine.FileConverters
{
	internal class JsonFileConverter
	{

		public static void WriteAsXml(string path, List<chat_completion_request> o)
		{
			JocysCom.ClassLibrary.Runtime.Serializer.SerializeToXmlFile(o, path);
		}

		public static List<chat_completion_request> ReadFromXml(string path)
		{
			return JocysCom.ClassLibrary.Runtime.Serializer.DeserializeFromXmlFile<List<chat_completion_request>>(path);
		}

		public static void WriteAsJson(string path, List<chat_completion_request> o)
		{
			var contents = Client.Serialize(o);
			File.WriteAllText(path, contents);
		}

		public static List<chat_completion_request> ReadFromJson(string path)
		{
			var json = File.ReadAllText(path);
			return Client.Deserialize<List<chat_completion_request>>(json);
		}

		#region Rich Text Format (*.rtf)

		static void AddRtfLine(System.Windows.Forms.RichTextBox rtf, string text = null, bool isBold = false)
		{
			if (!string.IsNullOrEmpty(text))
			{
				int startPos = rtf.Text.Length;
				rtf.AppendText(text);
				rtf.Select(startPos, text.Length);
				if (isBold)
					rtf.SelectionFont = new System.Drawing.Font(rtf.Font, FontStyle.Bold);
			}
			rtf.AppendText("\n");
		}

		public static void WriteAsRtf(string path, List<chat_completion_request> o)
		{
			var rtf = new System.Windows.Forms.RichTextBox();
			foreach (var request in o)
			{
				foreach (var message in request.messages)
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

		public static void WriteAsDocx(string path, List<chat_completion_request> o)
		{
			using (var wordDocument = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document))
			{
				var mainPart = wordDocument.AddMainDocumentPart();
				mainPart.Document = new wp.Document(new wp.Body());
				foreach (var request in o)
				{
					foreach (var message in request.messages)
					{
						if (message.role == message_role.user)
							AddDocxParagraph(mainPart.Document.Body, message.content, true);
						if (message.role == message_role.assistant)
							AddDocxParagraph(mainPart.Document.Body, message.content);
					}
				}
				mainPart.Document.Save();
				wordDocument.Dispose();
			}
		}

		#endregion

		#region Microsoft Excel (*.xlsx)

		public static void WriteAsXlsx(string path, List<chat_completion_request> o)
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
					foreach (var message in request.messages)
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
					sheetData.Append(row);
				}
				workbookPart.Workbook.Save();
			}
		}

		#endregion
	}
}
