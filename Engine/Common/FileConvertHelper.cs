using JocysCom.ClassLibrary.Processes;
using JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Policy;
using System.Text.Json;
using System.Windows;

namespace JocysCom.VS.AiCompanion.Engine
{


	internal class FileConvertHelper
	{

		public static string ConvertModelTrainingData(
			ConversionType conversionType,
			string sourceFileName,
			string targetFileName,
			string systemPromptContent
		)
		{
			return ConvertModelTrainingData(
				null, conversionType,
				null, null, sourceFileName, targetFileName, systemPromptContent);
		}

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
			var arguments = $"-ConversionType \"{conversionType}\" -FineTuningName \"{fineTuningName}\" -DataFolderName \"{dataFolderName}\" -SourceFileName \"{sourceFileName}\" -TargetFileName \"{targetFileName}\" -SystemPromptContent \"{systemPromptContent}\"";
			using (var shell = new HiddenShell())
				return shell.ExecuteCommand($"PowerShell -ExecutionPolicy Bypass -File \"{scriptPath}\" {arguments}");

			/*
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
			*/
		}

		public static Dictionary<string, ConvertTargetType[]> ConvertToTypesAvailable = new Dictionary<string, ConvertTargetType[]>()
		{
			{ ".csv",
				new ConvertTargetType[] { ConvertTargetType.JSON, ConvertTargetType.JSONL, ConvertTargetType.XLS, ConvertTargetType.RTF }
			},
			{ ".xls",
				new ConvertTargetType[] { ConvertTargetType.JSON, ConvertTargetType.JSONL, ConvertTargetType.RTF, ConvertTargetType.CSV }
			},
			{ ".rtf",
				new ConvertTargetType[] { }
			},
			{ ".json",
				new ConvertTargetType[] { ConvertTargetType.JSONL, ConvertTargetType.XLS, ConvertTargetType.RTF, ConvertTargetType.CSV }
			},
			{ ".jsonl",
				new ConvertTargetType[] { ConvertTargetType.JSON, ConvertTargetType.XLS, ConvertTargetType.RTF, ConvertTargetType.CSV }
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
				{ ConvertTargetType.XLS, ".xls" },
				{ ConvertTargetType.RTF, ".rtf" },
				{ ConvertTargetType.CSV, ".csv" },
			};
			var jsonToOtherType = new Dictionary<ConvertTargetType, ConversionType>()
			{
				{ ConvertTargetType.JSONL, ConversionType.JSON2JSONL },
				{ ConvertTargetType.XLS, ConversionType.JSON2XLS },
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
				var jsonTempFile = targetTuningDataFullPathBase + ".tmp.json";
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
						ConvertModelTrainingData(sourceToJsonTypes[sourceExt], sourceFullPath, jsonTempFile, systemPromptContent);
					}
				}
				// Get target file.
				var targetFile = targetType == ConvertTargetType.JSONL
					? targetTuningDataFullPathBase + targetExt
					: targetSourceDataFullPathBase + targetExt;
				// If target is JSON Lines file.
				if (targetType == ConvertTargetType.JSONL)
				{
					_ = Client.IsTextCompletionMode(aiModel)
						? ConvertJsonListToLines<text_completion_request>(jsonTempFile, targetFile, out status_details)
						: ConvertJsonListToLines<chat_completion_request>(jsonTempFile, targetFile, out status_details);
				}
				else if (targetType == ConvertTargetType.JSON)
				{
					File.Move(jsonTempFile, targetFile);
				}
				else
				{
					ConvertModelTrainingData(jsonToOtherType[targetType], jsonTempFile, targetFile, systemPromptContent);
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

		public static bool ConvertJsonListToLines<T>(string sourceFile, string targetFile, out string status_details)
		{
			if (!File.Exists(sourceFile))
			{
				status_details = $"File {sourceFile} does not exist.";
				return false;
			}
			try
			{
				var jsonData = File.ReadAllText(sourceFile, System.Text.Encoding.UTF8);
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
			File.WriteAllText(targetFile, contents, System.Text.Encoding.UTF8);
			// Add approximate token count.
			status_details = $"File converted successfuly. {items.Count} message(s) found.";
			return true;
		}



	}
}
