using JocysCom.ClassLibrary.Controls;
using System.IO;
using System.Linq;
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

		private void ValidateButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{

		}

		private void UploadButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{

		}

		private void CreateModel_Click(object sender, System.Windows.RoutedEventArgs e)
		{

		}
	}
}
