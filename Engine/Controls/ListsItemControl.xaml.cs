using JocysCom.VS.AiCompanion.Plugins.Core;
using System.ComponentModel;
using System.Windows.Controls;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	/// <summary>
	/// Interaction logic for ListsItemControl.xaml
	/// </summary>
	public partial class ListsItemControl : UserControl
	{
		public ListsItemControl()
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
				//PanelSettings.PropertyChanged -= PanelSettings_PropertyChanged;
				//PanelSettings = Global.AppSettings.GetTaskSettings(value);
				//PanelSettings.PropertyChanged += PanelSettings_PropertyChanged;
				// Update the rest.
				//PanelSettings.UpdateBarToggleButtonIcon(BarToggleButton);
				//PanelSettings.UpdateListToggleButtonIcon(ListToggleButton);
				//OnPropertyChanged(nameof(BarPanelVisibility));
			}
		}
		private ItemType _DataType;

		#region Item
		public ListInfo Item
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
					//var path = Global.GetPath(value);
					//if (!Directory.Exists(path))
					//	Directory.CreateDirectory(path);
					_Item.PropertyChanged += _Item_PropertyChanged;
				}
				IconPanel.BindData(_Item);
				DataContext = value;
				//AiModelBoxPanel.BindData(value);
				//IconPanel.BindData(value);
				// SourceFilesPanel.Data = value;
				//TuningFilesPanel.Data = value;
				//RemoteFilesPanel.Data = value;
				//TuningJobsListPanel.Data = value;
				//ModelsPanel.Data = value;
				//OnPropertyChanged(nameof(DataFolderPath));
				//OnPropertyChanged(nameof(DataFolderPathShow));
			}
		}
		ListInfo _Item;


		private void _Item_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(ListInfo.Name))
			{
				//OnPropertyChanged(nameof(DataFolderPath));
				//OnPropertyChanged(nameof(DataFolderPathShow));
			}
		}

		#endregion

	}
}
