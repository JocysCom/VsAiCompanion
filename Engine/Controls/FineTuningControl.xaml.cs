using JocysCom.ClassLibrary.Controls;
using JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Controls;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	/// <summary>
	/// Interaction logic for FineTuningControl.xaml
	/// </summary>
	public partial class FineTuningControl : UserControl
	{
		public FineTuningControl()
		{
			InitializeComponent();
			if (ControlsHelper.IsDesignMode(this))
				return;
			var item = Global.FineTunes.Items.FirstOrDefault();
			Item = item;
			DataContext = item;
			AiModelBoxPanel.BindData(item);
			var di = new DirectoryInfo(Global.AppData.XmlFile.Directory.FullName + $"\\FineTune\\{item.Name}");
			DataFolderTextBox.Text = di.FullName;
			if (!di.Exists)
				di.Create();
		}

		FineTune Item { get; set; }

		private void OpenButton_Click(object sender, System.Windows.RoutedEventArgs e)
			=> ControlsHelper.OpenUrl(DataFolderTextBox.Text);

		#region Convert Data

		public bool ConvertJsonLinesToJson<T>(string sourceFile, string targetFile)
		{
			if (!File.Exists(sourceFile))
			{
				LogTextBox.AppendText($"File {sourceFile} don't exists!");
				return false;
			}
			var i = 0;
			var result = new List<T>();
			foreach (string line in File.ReadLines(sourceFile))
			{
				i++;
				try
				{
					var request = Client.Deserialize<T>(line);
					result.Add(request);
					// Validate further if necessary
				}
				catch (JsonException ex)
				{
					// Handle the exception for an invalid JSON line
					LogTextBox.AppendText(ex.Message);
					return false;
				}
			}
			// Add approximate token count.
			LogTextBox.AppendText($"File converted successfuly. {result.Count} message(s) found.");
			var contents = Client.Serialize(result);
			if (File.Exists(targetFile))
				File.Delete(targetFile);
			File.WriteAllText(targetFile, contents, System.Text.Encoding.UTF8);
			return true;
		}

		public bool ConvertJsonToJsonLines<T>(string sourceFile, string targetFile)
		{
			if (!File.Exists(sourceFile))
			{
				LogTextBox.AppendText($"File {sourceFile} does not exist.");
				return false;
			}
			try
			{
				var jsonData = File.ReadAllText(sourceFile, System.Text.Encoding.UTF8);
				var data = Client.Deserialize<List<T>>(jsonData);
				if (File.Exists(targetFile))
					File.Delete(targetFile);
				using (var writer = File.CreateText(targetFile))
				{
					foreach (var item in data)
					{
						var jsonLine = Client.Serialize(item);
						writer.WriteLine(jsonLine);
					}
				}
				LogTextBox.AppendText($"File converted successfully. {data.Count} message(s) found.");
				return true;
			}
			catch (JsonException ex)
			{
				// Handle the exception for an invalid JSON line
				LogTextBox.AppendText(ex.Message);
				return false;
			}
		}

		#endregion

		#region Validate Data

		public bool ValidateJsonlFile<T>(string filePath)
		{
			if (!File.Exists(filePath))
			{
				LogTextBox.AppendText($"File {filePath} don't exists!");
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
					LogTextBox.AppendText(ex.Message);
					return false;
				}
			}
			// Add approximate token count.
			LogTextBox.AppendText($"JSON Lines data validated successfuly. {i} line(s) found.");
			return true;
		}

		public bool ValidateJsonFile<T>(string filePath)
		{
			if (!File.Exists(filePath))
			{
				LogTextBox.AppendText($"File {filePath} don't exists!");
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
				LogTextBox.AppendText(ex.Message);
				return false;
			}
			// Add approximate token count.
			LogTextBox.AppendText($"JSON data validated successfuly.");
			return true;
		}


		#endregion

		private async void UploadButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			var sourcePath = System.IO.Path.Combine(DataFolderTextBox.Text, JsonlFileTextBox.Text);
			var client = new Client(Item.AiService);
			await client.UploadFileAsync(sourcePath, "fine-tune");
			await FileListPanel.Refresh();
		}

		private void CreateModel_Click(object sender, System.Windows.RoutedEventArgs e)
		{

		}

		private void ConvertToJsonlButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			LogTextBox.Clear();
			var sourcePath = System.IO.Path.Combine(DataFolderTextBox.Text, JsonFileTextBox.Text);
			var targetPath = System.IO.Path.Combine(DataFolderTextBox.Text, JsonlFileTextBox.Text);
			_ = Client.IsTextCompletionMode(Item.AiModel)
				? ConvertJsonToJsonLines<text_completion_request>(sourcePath, targetPath)
				: ConvertJsonToJsonLines<chat_completion_request>(sourcePath, targetPath);
		}

		private void ConvertToJsonButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			LogTextBox.Clear();
			var sourcePath = System.IO.Path.Combine(DataFolderTextBox.Text, JsonlFileTextBox.Text);
			var targetPath = System.IO.Path.Combine(DataFolderTextBox.Text, JsonFileTextBox.Text);
			_ = Client.IsTextCompletionMode(Item.AiModel)
				? ConvertJsonLinesToJson<text_completion_request>(sourcePath, targetPath)
				: ConvertJsonLinesToJson<chat_completion_request>(sourcePath, targetPath);
		}

		private void ValidateJsonButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			LogTextBox.Clear();
			var filePath = System.IO.Path.Combine(DataFolderTextBox.Text, JsonFileTextBox.Text);
			_ = Client.IsTextCompletionMode(Item.AiModel)
				? ValidateJsonFile<List<text_completion_request>>(filePath)
				: ValidateJsonFile<List<chat_completion_request>>(filePath);
		}

		private void ValidateJsonlButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			LogTextBox.Clear();
			var filePath = System.IO.Path.Combine(DataFolderTextBox.Text, JsonlFileTextBox.Text);
			_ = Client.IsTextCompletionMode(Item.AiModel)
				? ValidateJsonlFile<text_completion_request>(filePath)
				: ValidateJsonlFile<chat_completion_request>(filePath);
		}


	}
}
