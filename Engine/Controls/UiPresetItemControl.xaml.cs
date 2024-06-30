using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.Controls;
using JocysCom.VS.AiCompanion.Plugins.Core;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	/// <summary>
	/// </summary>
	public partial class UiPresetItemControl : UserControl
	{
		public UiPresetItemControl()
		{
			InitializeComponent();
			PathColumn.ItemsSource = AllPaths;
			Global.UiPresets.Items.ListChanged += Items_ListChanged;
			UpdateButtons();
		}

		/// <summary>
		/// Contains all items for `PathColumn`
		/// </summary>
		public ObservableCollection<string> AllPaths => Global.VisibilityPaths;


		private void Items_ListChanged(object sender, ListChangedEventArgs e)
		{
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

		public UiPresetItem Item
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
					ControlsHelper.SetItemsSource(MainDataGrid, null);
				}
				//IconPanel.BindData(value);
				//DataContext = value;
				ControlsHelper.SetItemsSource(MainDataGrid, value?.Items);
			}
		}
		UiPresetItem _Item;


		private void _Item_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(ListInfo.Path))
			{

			}
		}

		#endregion

		#region MainDataGrid

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

		private void AddButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			var item = new VisibilityItem();
			Item.Items.Add(item);
		}

		private void DeleteButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			Delete();
		}

		private void Delete()
		{
			var items = MainDataGrid.SelectedItems.OfType<VisibilityItem>().ToList();
			if (!AppHelper.AllowAction(AllowAction.Delete, items.Select(x => x.Path).ToArray()))
				return;
			// Use begin invoke or grid update will deadlock on same thread.
			ControlsHelper.BeginInvoke(() =>
			{
				foreach (var item in items)
					Item.Items.Remove(item);
			});
		}

		private void Edit()
		{
			if (MainDataGrid.SelectedItem != null)
			{
				MainDataGrid.CurrentCell = new DataGridCellInfo(MainDataGrid.SelectedItem, PathColumn);
				MainDataGrid.Focus();
				MainDataGrid.BeginEdit();
			}
		}

		private void EditButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			Edit();
		}

		private void MainDataGrid_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			Edit();
		}

		private async void MainDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			await Helper.Delay(UpdateButtons, AppHelper.NavigateDelayMs);
		}

		#endregion


		void UpdateButtons()
		{
			var isSelected = MainDataGrid.SelectedItems.Count > 0;
			EditButton.IsEnabled = isSelected;
			DeleteButton.IsEnabled = isSelected;
		}

		private void This_Loaded(object sender, System.Windows.RoutedEventArgs e)
		{
			if (ControlsHelper.IsDesignMode(this))
				return;
			if (ControlsHelper.AllowLoad(this))
			{
				AppHelper.InitHelp(this);
				UiPresetsManager.InitControl(this);
			}
		}

		private void ApplyButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			// Apply new prest to controls.
			UiPresetsManager.ApplyUiPreset(Global.AppSettings.UiPresetName, UiPresetsManager.AllUiElements.Keys.ToArray());
		}
	}

}
