using DocumentFormat.OpenXml.Packaging;
using JocysCom.ClassLibrary;
using JocysCom.VS.AiCompanion.Plugins.Core.VsFunctions;
using NPOI.HSSF.UserModel;
//using NPOI.HWPF;
//using NPOI.HWPF.Extractor;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Documents;
using UglyToad.PdfPig;

namespace JocysCom.VS.AiCompanion.Plugins.Core
{
	public partial class FileHelper
	{

		/// <summary>
		/// Read the content of a file in plain text. Supported document types include .docx, .xlsx, .xls, .pdf, .htm, .html and .pdf.
		/// </summary>
		/// <param name="path">The path of the file to be read.</param>
		public OperationResult<string> ReadFileAsPlainText(string path)
		{
			if (string.IsNullOrEmpty(path))
			{
				var ex = new ArgumentException("Path cannot be null or empty.", nameof(path));
				return new OperationResult<string>(ex);
			}
			var extension = Path.GetExtension(path).ToLowerInvariant();
			try
			{
				string content = null;
				switch (extension)
				{
					case ".docx":
						content = ReadDocxFile(path);
						break;
					//case ".doc":
					//content = ReadDocFile(path);
					//break;
					case ".rtf":
						content = ReadTextViaRichTextBox(path, DataFormats.Rtf);
						break;
					// Can be UI view for the model, therefore read native content.
					//case ".htm":
					//case ".html":
					//	content = ReadHtmlFile(path);
					//	break;
					case ".xlsx":
						content = ReadXlsxFile(path);
						break;
					case ".xls":
						content = ReadXlsFile(path);
						break;
					case ".pdf":
						content = ReadPdfFile(path);
						break;
					// Reading known text file.
					case ".txt":
					case ".log":
					case ".csv":
					case ".ini":
						content = ReadTextFile(path);
						break;
					default:
						if (!DocItem.IsBinary(path, 1024))
						{
							content = ReadTextFile(path);
						}
						else
						{
							var ex2 = new NotSupportedException($"The file type {extension} is not supported.");
							return new OperationResult<string>(ex2);
						}
						break;
				}
				return new OperationResult<string>(content);
			}
			catch (Exception ex)
			{
				return new OperationResult<string>(new Exception(ex.Message));
			}
		}

		#region .NET

		private string ReadHtmlFile(string path)
		{
			var doc = new HtmlAgilityPack.HtmlDocument();
			doc.Load(path);
			var htmlContent = doc.DocumentNode.OuterHtml;
			return HtmlHelper.ReadHtmlAsPlainText(htmlContent);
		}

		private string ReadTextViaRichTextBox(string path, string dataFormat)
		{
			string result = null;
			Thread thread = new Thread(() =>
			{
				// Use WPF RichTextBox to load and extract plain text from RTF
				using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
				{
					System.Windows.Controls.RichTextBox rtb = new System.Windows.Controls.RichTextBox();
					TextRange textRange = new TextRange(rtb.Document.ContentStart, rtb.Document.ContentEnd);
					// Load the RTF content into the RichTextBox
					textRange.Load(fs, dataFormat);
					// Now retrieve the plain text from the RichTextBox
					using (MemoryStream ms = new MemoryStream())
					{
						textRange = new TextRange(rtb.Document.ContentStart, rtb.Document.ContentEnd);
						textRange.Save(ms, System.Windows.DataFormats.Text);
						result = Encoding.UTF8.GetString(ms.ToArray());
					}
				}
			});

			thread.SetApartmentState(ApartmentState.STA); // Set the thread to STA
			thread.Start();
			thread.Join(); // Wait for the thread to finish
			return result;
		}

		#endregion

		#region DocumentFormat.OpenXml

		private string ReadDocxFile(string path)
		{
			using (WordprocessingDocument doc = WordprocessingDocument.Open(path, false))
			{
				var sb = new StringBuilder();
				foreach (var para in doc.MainDocumentPart.Document.Body.Elements<DocumentFormat.OpenXml.Wordprocessing.Paragraph>())
				{
					sb.AppendLine(para.InnerText);
					sb.AppendLine();
				}
				var text = sb.ToString();
				return text;
			}
		}

		private string ReadXlsxFile(string path)
		{
			XSSFWorkbook xssfwb;
			using (FileStream file = new FileStream(path, FileMode.Open, FileAccess.Read))
			{
				xssfwb = new XSSFWorkbook(file);
			}

			return ExtractTextFromExcel(xssfwb);
		}

		#endregion

		#region NPOI

		private string ReadXlsFile(string path)
		{
			HSSFWorkbook hssfwb;
			using (FileStream file = new FileStream(path, FileMode.Open, FileAccess.Read))
			{
				hssfwb = new HSSFWorkbook(file);
			}

			return ExtractTextFromExcel(hssfwb);
		}

		private string ExtractTextFromExcel(IWorkbook workbook)
		{
			var sb = new StringBuilder();
			var sheet = workbook.GetSheetAt(0); // Assuming we're only dealing with the first sheet
			int numberOfRows = sheet.LastRowNum + 1; // +1 because it's zero-based
			for (int i = 0; i < numberOfRows; i++)
			{
				var row = sheet.GetRow(i);
				if (row == null) continue; // Skip if the row is empty
				int numberOfCells = row.LastCellNum; // This is not zero-based
				for (int j = 0; j < numberOfCells; j++)
				{
					var cell = row.GetCell(j, MissingCellPolicy.RETURN_BLANK_AS_NULL);
					// Append cell data
					if (cell != null)
					{
						// Encase the cell value in double quotes and escape existing double quotes
						string cellValue = cell.ToString().Replace("\"", "\"\"");
						cellValue = $"\"{cellValue}\"";
						sb.Append(cellValue);
					}
					// If it's not the last cell, append the comma delimiter
					if (j < numberOfCells - 1)
						sb.Append(",");
				}
				// If it's not the last row, append a newline to separate rows
				if (i < numberOfRows - 1)
					sb.AppendLine();
			}
			return sb.ToString();
		}

		//private string ReadDocFile(string path)
		//{
		//	using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read))
		//	{
		//		HWPFDocument doc = new HWPFDocument(stream);
		//		var extractor = new WordExtractor(doc);
		//		var docText = extractor.Text;
		//		return docText.Trim();
		//	}
		//}

		#endregion


		private string ReadPdfFile(string path)
		{
			using (var doc = PdfDocument.Open(path))
			{
				var text = new System.Text.StringBuilder();
				foreach (var page in doc.GetPages())
				{
					text.AppendLine(page.Text);
				}
				return text.ToString();
			}
		}

	}
}
