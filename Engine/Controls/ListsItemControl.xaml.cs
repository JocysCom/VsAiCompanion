using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.Controls;
using JocysCom.VS.AiCompanion.Plugins.Core;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;

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
			UpdateButtons();
		}

		#region List Panel Item

		TaskSettings PanelSettings { get; set; } = new TaskSettings();

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
				//PanelSettings.UpdateBarToggleButtonIcon(BarToggleButton);
				PanelSettings.UpdateListToggleButtonIcon(ListToggleButton);
				//OnPropertyChanged(nameof(BarPanelVisibility));
			}
		}
		private ItemType _DataType;

		private async void PanelSettings_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			await Task.Delay(0);
		}

		private void ListToggleButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			PanelSettings.UpdateListToggleButtonIcon(ListToggleButton, true);
		}

		#endregion

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
					ControlsHelper.SetItemsSource(MainDataGrid, null);
				}
				IconPanel.BindData(value);
				DataContext = value;
				ControlsHelper.SetItemsSource(MainDataGrid, value.Items);
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

		private void MainDataGrid_Loaded(object sender, System.Windows.RoutedEventArgs e)
		{
		}

		private void MainDataGrid_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
		{
			var isEditMode = AppHelper.IsGridInEditMode((DataGrid)sender);
			var grid = (DataGrid)sender;
			if (e.Key == Key.Enter)
			{
				// Commit manually and supress selection of next row.
				if (isEditMode)
				{
					e.Handled = true;
					grid.CommitEdit(DataGridEditingUnit.Cell, true);
					grid.CommitEdit(DataGridEditingUnit.Row, true);
				}
			}
			if (!isEditMode && e.Key == Key.Delete)
				Delete();
		}

		private void MainDataGrid_PreviewMouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{

		}

		private void AddButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{

		}

		private void DeleteButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			Delete();
		}

		private void Delete()
		{
			var items = MainDataGrid.SelectedItems.Cast<ListItem>().ToList();
			if (!AppHelper.AllowAction(AllowAction.Delete, items.Select(x => x.Key).ToArray()))
				return;
			// Use begin invoke or grid update will deadlock on same thread.
			ControlsHelper.BeginInvoke(() =>
			{
				foreach (var item in items)
					Item.Items.Remove(item);
			});
		}

		private void EditButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			if (MainDataGrid.SelectedItem != null)
			{
				MainDataGrid.CurrentCell = new DataGridCellInfo(MainDataGrid.SelectedItem, KeyColumn);
				MainDataGrid.Focus();
				MainDataGrid.BeginEdit();
			}
		}


		private async void MainDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			await Helper.Delay(UpdateOnSelectionChanged, AppHelper.NavigateDelayMs);
		}

		private void UpdateOnSelectionChanged()
		{
			//// If item selected then...
			//if (MainDataGrid.SelectedIndex >= 0)
			//{
			//	// Remember selection.
			//	PanelSettings.ListSelection = ControlsHelper.GetSelection<string>(MainDataGrid, nameof(ISettingsListFileItem.Name));
			//	PanelSettings.ListSelectedIndex = MainDataGrid.SelectedIndex;
			//}
			//else
			//{
			//	// Try to restore selection.
			//	ControlsHelper.SetSelection(
			//		MainDataGrid, nameof(ISettingsListFileItem.Name),
			//		PanelSettings.ListSelection, PanelSettings.ListSelectedIndex
			//	);
			//}
			UpdateButtons();
		}


		void UpdateButtons()
		{
			//var selecetedItems = MainDataGrid.SelectedItems.AsQueryable();
			var isSelected = MainDataGrid.SelectedItems.Count > 0;
			EditButton.IsEnabled = isSelected;
			DeleteButton.IsEnabled = isSelected;
		}

		private void UserControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
		{
			// Fast workaround
			//MainDataGrid.ItemsSource = _Item?.Items;
			if (ControlsHelper.IsDesignMode(this))
				return;
			AppHelper.AddHelp(IsEnabledCheckBox, IsEnabledCheckBox.Content as string, Engine.Resources.Resources.List_IsEnabled);
			AppHelper.AddHelp(IsReadOnlyCheckBox, IsReadOnlyCheckBox.Content as string, Engine.Resources.Resources.List_IsReadOnly);
			AppHelper.AddHelp(InstructionsLabel, Engine.Resources.Resources.Instructions, Engine.Resources.Resources.List_Instructions);
			AppHelper.AddHelp(InstructionsTextBox, Engine.Resources.Resources.Instructions, Engine.Resources.Resources.List_Instructions);
			AppHelper.AddHelp(DescriptionLabel, Engine.Resources.Resources.Description, Engine.Resources.Resources.List_Description);
			AppHelper.AddHelp(DescriptionTextBox, Engine.Resources.Resources.Description, Engine.Resources.Resources.List_Description);
		}

		private void UserControl_Unloaded(object sender, System.Windows.RoutedEventArgs e)
		{
			// Fast workaround
			//MainDataGrid.ItemsSource = null;

		}

	}

}
