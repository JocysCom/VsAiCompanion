using JocysCom.ClassLibrary.Configuration;
using JocysCom.ClassLibrary.Controls;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	/// <summary>
	/// Interaction logic for AssistantItemUserControl.xaml
	/// </summary>
	public partial class AssistantItemControl : UserControl, INotifyPropertyChanged
	{
		public AssistantItemControl()
		{
			InitializeComponent();
		}

		[Category("Main"), DefaultValue(ItemType.None)]
		public ItemType DataType
		{
			get => _DataType;
			set
			{
				_DataType = value;
				// Update panel settings.
				PanelSettings.PropertyChanged -= PanelSettings_PropertyChanged;
				PanelSettings = Global.AppSettings.GetTaskSettings(value);
				PanelSettings.PropertyChanged += PanelSettings_PropertyChanged;
				// Update the rest.
				PanelSettings.UpdateBarToggleButtonIcon(BarToggleButton);
				PanelSettings.UpdateListToggleButtonIcon(ListToggleButton);
				OnPropertyChanged(nameof(BarPanelVisibility));
			}
		}
		private ItemType _DataType;

		#region Item
		public AssistantItem Item
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
					var path = Global.GetPath(value);
					if (!Directory.Exists(path))
						Directory.CreateDirectory(path);
					_Item.PropertyChanged += _Item_PropertyChanged;
				}
				DataContext = value;
				AiModelBoxPanel.BindData(value);
				IconPanel.BindData(value);
				// SourceFilesPanel.Data = value;
				//TuningFilesPanel.Data = value;
				//RemoteFilesPanel.Data = value;
				//TuningJobsListPanel.Data = value;
				//ModelsPanel.Data = value;
				OnPropertyChanged(nameof(DataFolderPath));
				OnPropertyChanged(nameof(DataFolderPathShow));
			}
		}
		AssistantItem _Item;

		private void _Item_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(FineTuningItem.Name))
			{
				OnPropertyChanged(nameof(DataFolderPath));
				OnPropertyChanged(nameof(DataFolderPathShow));
			}
		}


		#endregion

		public string DataFolderPathShow
		{
			get => AssemblyInfo.ParameterizePath(DataFolderPath, true);
			set { }
		}

		public string DataFolderPath
		{
			get => Item == null ? "" : Global.GetPath(Item);
			set { }
		}


		#region PanelSettings

		TaskSettings PanelSettings { get; set; } = new TaskSettings();

		private void PanelSettings_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(PanelSettings.IsBarPanelVisible))
			{
				PanelSettings.UpdateBarToggleButtonIcon(BarToggleButton);
				OnPropertyChanged(nameof(BarPanelVisibility));
			}
		}

		private void ListToggleButton_Click(object sender, RoutedEventArgs e)
		{
			PanelSettings.UpdateListToggleButtonIcon(ListToggleButton, true);
		}

		public Visibility BarPanelVisibility
			=> PanelSettings.IsBarPanelVisible ? Visibility.Visible : Visibility.Collapsed;

		private void BarToggleButton_Click(object sender, RoutedEventArgs e)
		{
			PanelSettings.UpdateBarToggleButtonIcon(BarToggleButton, true);
		}

		#endregion

		#region ■ INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		#endregion

		private void OpenButton_Click(object sender, RoutedEventArgs e)
			=> ControlsHelper.OpenUrl(DataFolderPath);

		private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{

		}
	}
}
