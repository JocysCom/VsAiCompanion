using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml;
using JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using wp = DocumentFormat.OpenXml.Wordprocessing;

namespace JocysCom.VS.AiCompanion.Engine.FileConverters
{


	internal class FileConvertHelper
	{


		/// <summary>
		/// Convert data files.
		/// </summary>
		/// <param name="scriptPath">Full path to ConvertModelTraininData.ps1</param>
		public static string ConvertModelTrainingData(
			string scriptPath,
			ConversionType conversionType,
			string fineTuningName,
			string dataFolderName,
			string sourceFileName,
			string targetFileName,
			string systemPromptContent
		)
		{
			//var arguments = $"-ConversionType \"{conversionType}\" -FineTuningName \"{fineTuningName}\" -DataFolderName \"{dataFolderName}\" -SourceFileName \"{sourceFileName}\" -TargetFileName \"{targetFileName}\" -SystemPromptContent \"{systemPromptContent}\"";
			//using (var shell = new HiddenShell())
			//	return shell.ExecuteCommand($"PowerShell -ExecutionPolicy Bypass -File \"{scriptPath}\" {arguments}");


			var processInfo = new ProcessStartInfo();
			processInfo.FileName = "PowerShell.exe";
			processInfo.RedirectStandardOutput = true;
			processInfo.RedirectStandardError = true;
			processInfo.UseShellExecute = false;
			processInfo.CreateNoWindow = true;
			processInfo.Arguments = $"-ExecutionPolicy ByPass -File \"{scriptPath}\" " +
				$"-ConversionType \"{conversionType}\" " +
				$"-FineTuningName \"{fineTuningName}\" " +
				$"-DataFolderName \"{dataFolderName}\" " +
				$"-SourceFileName \"{sourceFileName}\" " +
				$"-TargetFileName \"{targetFileName}\" " +
				$"-SystemPromptContent \"{systemPromptContent}\"";
			var process = new Process();
			process.StartInfo = processInfo;
			var output = new StringBuilder();
			var errors = new StringBuilder();
			process.OutputDataReceived += (sender, e) =>
			{
				if (!string.IsNullOrEmpty(e.Data))
					output.AppendLine(e.Data);
			};
			process.ErrorDataReceived += (sender, e) =>
			{
				if (!string.IsNullOrEmpty(e.Data))
					errors.AppendLine(e.Data);
			};
			process.Start();
			process.BeginOutputReadLine();
			process.BeginErrorReadLine();
			process.WaitForExit();
			string outputString = output.ToString();
			string errorsString = errors.ToString();
			return outputString + errorsString;

		}

		public static Dictionary<string, ConvertTargetType[]> ConvertToTypesAvailable = new Dictionary<string, ConvertTargetType[]>()
		{
			{ ".csv",
				new ConvertTargetType[] { ConvertTargetType.JSON, ConvertTargetType.JSONL, ConvertTargetType.XLSX, ConvertTargetType.RTF }
			},
			{ ".xls",
				new ConvertTargetType[] { ConvertTargetType.JSON, ConvertTargetType.JSONL, ConvertTargetType.RTF, ConvertTargetType.CSV }
			},
			{ ".rtf",
				new ConvertTargetType[] { }
			},
			{ ".json",
				new ConvertTargetType[] { ConvertTargetType.JSONL, ConvertTargetType.XLSX, ConvertTargetType.RTF, ConvertTargetType.CSV }
			},
			{ ".jsonl",
				new ConvertTargetType[] { ConvertTargetType.JSON, ConvertTargetType.XLSX, ConvertTargetType.RTF, ConvertTargetType.CSV }
			}
		};

		public static void ConvertFile(
			string fineTuneItemPath, string sourceDataName,
			file[] items, ConvertTargetType targetType, string aiModel,
			string systemPromptContent = null
		)
		{
			var sourceToJsonTypes = new Dictionary<string, ConversionType>()
			{
				{ ".xls", ConversionType.XLS2JSON},
				{ ".csv", ConversionType.CSV2JSON},
				{ ".rtf", ConversionType.RTF2JSON},
				{ ".jsonl", ConversionType.JSONL2JSON},
			};
			var targetTypeToExtension = new Dictionary<ConvertTargetType, string>()
			{
				{ ConvertTargetType.JSON, ".json" },
				{ ConvertTargetType.JSONL, ".jsonl" },
				{ ConvertTargetType.XLSX, ".xlsx" },
				{ ConvertTargetType.RTF, ".rtf" },
				{ ConvertTargetType.DOCX, ".docx" },
				{ ConvertTargetType.CSV, ".csv" },
			};
			var jsonToOtherType = new Dictionary<ConvertTargetType, ConversionType>()
			{
				{ ConvertTargetType.JSONL, ConversionType.JSON2JSONL },
				{ ConvertTargetType.XLSX, ConversionType.JSON2XLS },
				{ ConvertTargetType.RTF, ConversionType.JSON2RTF },
				{ ConvertTargetType.CSV, ConversionType.JSON2CSV },
			};
			// Process files.
			foreach (var item in items)
			{
				var sourceExt = Path.GetExtension(item.filename).ToLower();
				var targetExt = targetTypeToExtension[targetType];
				// If nothing to convert then continue.
				if (sourceExt == targetExt)
					continue;
				var sourceBase = Path.GetFileNameWithoutExtension(item.filename);
				var sourceFullPath = Path.Combine(fineTuneItemPath, sourceDataName, item.filename);
				var targetSourceDataFullPathBase = Path.Combine(fineTuneItemPath, FineTune.SourceData, sourceBase);
				var targetTuningDataFullPathBase = Path.Combine(fineTuneItemPath, FineTune.TuningData, sourceBase);
				var jsonTempFile = targetSourceDataFullPathBase + ".tmp.json";
				string status_details = null;
				// If can convert to JSON then...
				if (sourceToJsonTypes.ContainsKey(sourceExt))
				{
					// Convert to temp file.
					if (sourceExt == ".jsonl")
					{
						_ = Client.IsTextCompletionMode(aiModel)
						? ConvertJsonLinesToList<text_completion_request>(sourceFullPath, jsonTempFile, out status_details)
						: ConvertJsonLinesToList<chat_completion_request>(sourceFullPath, jsonTempFile, out status_details);
					}
					else
					{
						//ConvertModelTrainingData(sourceToJsonTypes[sourceExt], sourceFullPath, jsonTempFile, systemPromptContent);
					}
				}
				// Set source and target
				var sourceFile = sourceExt == ".json"
					? sourceFullPath
					: jsonTempFile;
				var targetFile = targetType == ConvertTargetType.JSONL
					? targetTuningDataFullPathBase + targetExt
					: targetSourceDataFullPathBase + targetExt;
				// If target is JSON Lines file.
				if (targetType == ConvertTargetType.JSONL)
				{
					_ = Client.IsTextCompletionMode(aiModel)
						? ConvertJsonListToLines<text_completion_request>(sourceFile, targetFile, out status_details)
						: ConvertJsonListToLines<chat_completion_request>(sourceFile, targetFile, out status_details);
				}
				// Note: Source was not JSON.
				else if (targetType == ConvertTargetType.JSON)
				{
					File.Move(jsonTempFile, targetFile);
				}
				else if (targetType == ConvertTargetType.RTF)
				{
					var o = ReadFromJson<chat_completion_request>(sourceFile);
					WriteAsRtf(targetFile, o);
				}
				else if (targetType == ConvertTargetType.DOCX)
				{
					var o = ReadFromJson<chat_completion_request>(sourceFile);
					WriteAsDocx(targetFile, o);
				}
				else if (targetType == ConvertTargetType.XLSX)
				{
					var o = ReadFromJson<chat_completion_request>(sourceFile);
					WriteAsXlsx(targetFile, o);
				}
				item.status_details = $"{DateTime.Now}: {status_details}";
				if (File.Exists(jsonTempFile))
					File.Delete(jsonTempFile);
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

		public static bool ConvertJsonListToLines<T>(string sourceFile, string targetFile, out string status_details)
		{
			if (!File.Exists(sourceFile))
			{
				status_details = $"File {sourceFile} does not exist.";
				return false;
			}
			try
			{
				var jsonData = File.ReadAllText(sourceFile, Encoding.UTF8);
				var data = Client.Deserialize<List<T>>(jsonData);
				if (!AllowToWrite(targetFile))
				{
					status_details = "Overwrite denied.";
					return false;
				}
				using (var writer = File.CreateText(targetFile))
				{
					foreach (var item in data)
					{
						var jsonLine = Client.Serialize(item);
						writer.WriteLine(jsonLine);
					}
				}
				status_details = $"File converted successfully. {data.Count} message(s) found.";
				return true;
			}
			catch (JsonException ex)
			{
				// Handle the exception for an invalid JSON line
				status_details = ex.Message;
				return false;
			}
		}

		public static bool ConvertJsonLinesToList<T>(string sourceFile, string targetFile, out string status_details)
		{
			if (!File.Exists(sourceFile))
			{
				status_details = $"File {sourceFile} don't exists!";
				return false;
			}
			var i = 0;
			var items = new List<T>();
			foreach (string line in File.ReadLines(sourceFile))
			{
				i++;
				try
				{
					var request = Client.Deserialize<T>(line);
					items.Add(request);
					// Validate further if necessary
				}
				catch (JsonException ex)
				{
					// Handle the exception for an invalid JSON line
					status_details = ex.Message;
					return false;
				}
			}
			var options = Client.GetJsonOptions();
			options.WriteIndented = true;
			var contents = JsonSerializer.Serialize(items, options);
			if (!AllowToWrite(targetFile))
			{
				status_details = "Overwrite denied.";
				return false;
			}
			File.WriteAllText(targetFile, contents, Encoding.UTF8);
			// Add approximate token count.
			status_details = $"File converted successfuly. {items.Count} message(s) found.";
			return true;
		}

		#endregion

		#region Java Script Object Notation (*.json)

		public static void WriteAsJson(string path, List<chat_completion_request> o)
		{
			var contents = Client.Serialize(o);
			File.WriteAllText(path, contents);
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

		#region Microsoft Excel Worksheet (*.xlsx)

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
