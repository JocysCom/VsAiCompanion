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
			//Global.AppSettings.Embedding.AiModel = "text-embedding-ada-002";
			Item = Global.AppSettings.Embedding;
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

		public EmbeddingSettings Item
		{
			get => _Item;
			set
			{
				if (_Item != null)
				{
					_Item.PropertyChanged -= _Item_PropertyChanged;
				}
				_Item = value;
				if (value != null)
				{
					_Item.PropertyChanged += _Item_PropertyChanged;
				}
				DataContext = value;
				AiModelBoxPanel.BindData(value);
				OnPropertyChanged(nameof(FilteredConnectionString));
			}
		}
		EmbeddingSettings _Item;

		private void _Item_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(EmbeddingSettings.Source))
			{
				OnPropertyChanged(nameof(FilteredConnectionString));
			}
		}

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
			//#if NETFRAMEWORK
			Microsoft.Data.ConnectionUI.DataConnectionDialog dcd;
			dcd = new Microsoft.Data.ConnectionUI.DataConnectionDialog();
			//Adds all the standard supported databases
			//DataSource.AddStandardDataSources(dcd);
			//allows you to add datasources, if you want to specify which will be supported 
			dcd.DataSources.Add(Microsoft.Data.ConnectionUI.DataSource.SqlDataSource);
			dcd.SetSelectedDataProvider(Microsoft.Data.ConnectionUI.DataSource.SqlDataSource, Microsoft.Data.ConnectionUI.DataProvider.SqlDataProvider);
			dcd.ConnectionString = Item.Target ?? "";
			Microsoft.Data.ConnectionUI.DataConnectionDialog.Show(dcd);
			if (dcd.DialogResult == System.Windows.Forms.DialogResult.OK)
			{
				Item.Target = dcd.ConnectionString;
			}
			OnPropertyChanged(nameof(FilteredConnectionString));
			//#endif
		}

		#region Database Connection Strings

		/// <summary>
		/// Database Administrative Connection String.
		/// </summary>
		public string FilteredConnectionString
		{
			get
			{
				var value = Global.AppSettings.Embedding.Target;
				if (string.IsNullOrWhiteSpace(value))
					return "";
				var filtered = ClassLibrary.Data.SqlHelper.FilterConnectionString(value);
				return filtered;
			}
			set
			{
			}
		}

		#endregion
	}
}
