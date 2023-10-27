using JocysCom.ClassLibrary.Controls;
using System.IO;
using System.Windows.Controls;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	/// <summary>
	/// Interaction logic for FineTuningControl.xaml
	/// </summary>
	public partial class FineTuningItemControl : UserControl
	{
		public FineTuningItemControl()
		{
			InitializeComponent();
			if (ControlsHelper.IsDesignMode(this))
				return;
		}

		public FineTuningItem Item
		{
			get => _Item;
			set
			{
				_Item = value;
				DataContext = value;
				AiModelBoxPanel.BindData(value);
				IconPanel.BindData(value);
				if (value != null)
				{
					var path = Global.GetPath(value);
					var di = new DirectoryInfo(path);
					DataFolderTextBox.Text = di.FullName;
					if (!di.Exists)
						di.Create();
				}
			}
		}
		FineTuningItem _Item;

		public ItemType DataType { get; set; }

		private void OpenButton_Click(object sender, System.Windows.RoutedEventArgs e)
			=> ControlsHelper.OpenUrl(DataFolderTextBox.Text);

		private void ConvertToJsonlButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			LogTextBox.Clear();
		}

		private void ConvertToJsonButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{

		}


	}
}
