using JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT;
using System.Collections.Generic;
using System;
using System.IO;
using System.Text.Json;

namespace JocysCom.VS.AiCompanion.Engine
{
	public static class FileValidateHelper
	{

		public static void Validate(string path, file[] items, string aiModel)
		{
			foreach (var item in items)
			{
				var filePath = Path.Combine(path, item.filename);
				var ext = Path.GetExtension(item.filename).ToLower();
				string status_details = null;
				if (ext == ".json")
				{
					_ = Client.IsTextCompletionMode(aiModel)
						? ValidateJsonFile<List<text_completion_request>>(filePath, out status_details)
						: ValidateJsonFile<List<chat_completion_request>>(filePath, out status_details);
				}
				if (ext == ".jsonl")
				{
					_ = Client.IsTextCompletionMode(aiModel)
						? ValidateJsonlFile<text_completion_request>(filePath, out status_details)
						: ValidateJsonlFile<chat_completion_request>(filePath, out status_details);
				}
				item.status_details = $"{DateTime.Now}: {status_details}";
			}
		}

		public static bool ValidateJsonlFile<T>(string filePath, out string status_details)
		{
			if (!File.Exists(filePath))
			{
				status_details = $"File {filePath} don't exists!";
				return false;
			}
			var i = 0;
			foreach (string line in File.ReadLines(filePath))
			{
				i++;
				try
				{
					var request = Client.Deserialize<T>(line);
					// Validate further if necessary
				}
				catch (JsonException ex)
				{
					// Handle the exception for an invalid JSON line
					status_details = ex.Message;
					return false;
				}
			}
			// Add approximate token count.
			status_details = $"Validated successfuly. {i} line(s) found.";
			return true;
		}

		public static bool ValidateJsonFile<T>(string filePath, out string status_details)
		{
			if (!File.Exists(filePath))
			{
				status_details = $"File {filePath} don't exists!";
				return false;
			}
			var content = File.ReadAllText(filePath);
			try
			{
				var request = Client.Deserialize<T>(content);
			}
			catch (JsonException ex)
			{
				// Handle the exception for an invalid JSON line
				status_details = ex.Message;
				return false;
			}
			// Add approximate token count.
			status_details = $"Validated successfuly.";
			return true;
		}

	}
}
