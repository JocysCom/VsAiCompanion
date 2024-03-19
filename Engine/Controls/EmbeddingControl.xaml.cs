using JocysCom.ClassLibrary.Configuration;
using JocysCom.ClassLibrary.Controls;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	/// <summary>
	/// Interaction logic for EmbeddingControl.xaml
	/// </summary>
	public partial class EmbeddingControl : UserControl, INotifyPropertyChanged
	{
		public EmbeddingControl()
		{
			InitializeComponent();
			if (ControlsHelper.IsDesignMode(this))
				return;

		}

		private void OpenButton_Click(object sender, System.Windows.RoutedEventArgs e)
			=> ControlsHelper.OpenUrl(DataFolderPath);

		private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{

		}

		public string DataFolderPathShow
		{
			get => AssemblyInfo.ParameterizePath(DataFolderPath, true);
			set { }
		}

		public string DataFolderPath
		{
			get => _DataFolderPath;
			set { _DataFolderPath = value; }
		}
		string _DataFolderPath;

		#region ■ INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		#endregion

		private void ApplySettingsButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			EmbeddingHelper.ConvertToEmbeddingsCSV(DataFolderPath, AiModelBoxPanel._item?.AiModel);
		}

		private void BrowseButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{

		}

		private void EditButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{

		}
	}
}
