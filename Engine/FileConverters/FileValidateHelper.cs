using JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT;
using System.Collections.Generic;
using System;
using System.IO;
using System.Text.Json;

namespace JocysCom.VS.AiCompanion.Engine.FileConverters
{
	public static class FileValidateHelper
	{

		public static void Validate(string path, file[] items, string aiModel)
		{
			foreach (var item in items)
			{
				string status_details;
				var sourcePath = Path.Combine(path, item.filename);
				string error;

				if (Client.IsTextCompletionMode(aiModel))
				{
					List<text_completion_response> result;
					var success = FileConvertHelper.TryReadFrom(sourcePath, out result, out error);
					status_details = success
						? $"Validated successfuly. {result.Count} item(s) found."
						: error;
				}
				else
				{
					List<text_completion_item> result;
					var success = FileConvertHelper.TryReadFrom(sourcePath, out result, out error);
					status_details = success
						? $"Validated successfuly. {result.Count} item(s) found."
						: error;
				}
				item.status_details = $"{DateTime.Now}: {status_details}";
			}
		}

	}
}
