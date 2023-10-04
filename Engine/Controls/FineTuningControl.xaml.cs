using JocysCom.ClassLibrary.Controls;
using JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT;
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

		#region Validate Data

		private void ValidateButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			LogTextBox.Clear();
			var filePath = Path.Combine(DataFolderTextBox.Text, "data.jsonl");
			ValidateJsonlFile(filePath);
		}

		public bool ValidateJsonlFile(string filePath)
		{
			var i = 0;
			foreach (string line in File.ReadLines(filePath))
			{
				i++;
				try
				{
					var request = Client.Deserialize<chat_completion_request>(line);
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
			LogTextBox.AppendText($"Data validated successfuly. {i} line(s) found.");
			return true;
		}

		#endregion

		private void UploadButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{

		}

		private void CreateModel_Click(object sender, System.Windows.RoutedEventArgs e)
		{

		}
	}
}
