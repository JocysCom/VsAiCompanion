using JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT;
using System.Collections.Generic;
using System;
using System.IO;

namespace JocysCom.VS.AiCompanion.Engine.FileConverters
{
	public static class FileValidateHelper
	{
		public static void Validate(string path, file[] items, string aiModel)
		{
			foreach (var item in items)
			{
				string statusDetails;
				var sourcePath = Path.Combine(path, item.filename);
				string error;
				// Check if the file exists before attempting to read.
				if (!File.Exists(sourcePath))
				{
					statusDetails = $"{DateTime.Now}: The file {sourcePath} does not exist.";
					item.status_details = statusDetails;
					continue;
				}
				try
				{
					if (Client.IsTextCompletionMode(aiModel))
					{
						List<text_completion_response> result;
						var success = FileConvertHelper.TryReadFrom(sourcePath, out result, out error);
						statusDetails = success
							? $"Validated successfully. {result.Count} item(s) found."
							: error;
					}
					else
					{
						List<chat_completion_request> result;
						var success = FileConvertHelper.TryReadFrom(sourcePath, out result, out error);
						statusDetails = success
							? $"Validated successfully. {result.Count} item(s) found."
							: error;
					}
				}
				catch (Exception ex)
				{
					statusDetails = $"{DateTime.Now}: An error occurred during validation: {ex.Message}";
				}
				item.status_details = statusDetails;
			}
		}
	}
}
