using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.Controls;
using JocysCom.VS.AiCompanion.Plugins.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	/// <summary>
	/// Interaction User Control.
	/// </summary>
	public partial class ListInfoControl : UserControl, INotifyPropertyChanged
	{
		public ListInfoControl()
		{
			InitializeComponent();
			var statuses = new List<ProgressStatus?>() { null };
			var values = Enum.GetValues(typeof(ProgressStatus)).Cast<ProgressStatus>().Select(x => (ProgressStatus?)x);
			statuses.AddRange(values);
			StatusColumn.ItemsSource = statuses;
			UpdateButtons();
			Global.Tasks.Items.ListChanged += Items_ListChanged;
			RefreshPaths();
		}

		private void Items_ListChanged(object sender, ListChangedEventArgs e)
		{
			if (e.PropertyDescriptor?.Name == nameof(TemplateItem.Name))
			{
				_ = Helper.Debounce(RefreshPaths);
			}
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

		#region ■ Properties


		/// <summary>
		/// Gets the data item associated with this control.
		/// </summary>
		public ListInfo Item
		{
			get => _Item;
		}
		ListInfo _Item;

		public async Task BindData(ListInfo value)
		{
			await Task.Delay(0);
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
			ControlsHelper.SetItemsSource(MainDataGrid, value?.Items);
			//AiModelBoxPanel.BindData(value);
			//IconPanel.BindData(value);
			// SourceFilesPanel.Data = value;
			//TuningFilesPanel.Data = value;
			//RemoteFilesPanel.Data = value;
			//TuningJobsListPanel.Data = value;
			//ModelsPanel.Data = value;
			//OnPropertyChanged(nameof(DataFolderPath));
			OnPropertyChanged(nameof(Item));
		}


		private void _Item_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				default:
					break;
			}
		}

		#endregion

		public ObservableCollection<string> Paths { get; set; } = new ObservableCollection<string>();

		public void RefreshPaths()
		{

			var listPaths = Global.Lists.Items.Select(x => x.Path ?? "")
				.Distinct()
				.OrderBy(x => x)
				.ToList();
			if (!listPaths.Contains(""))
				listPaths.Insert(0, "");
			var taskNames = Global.Tasks.Items.Select(x => x.Name).Except(listPaths);
			listPaths.AddRange(taskNames);
			var paths = Paths;
			JocysCom.ClassLibrary.Collections.CollectionsHelper.Synchronize(listPaths, paths);
		}

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

		private void Edit()
		{
			if (MainDataGrid.SelectedItem != null)
			{
				MainDataGrid.CurrentCell = new DataGridCellInfo(MainDataGrid.SelectedItem, KeyColumn);
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
			await Helper.Debounce(UpdateButtons, AppHelper.NavigateDelayMs);
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
			// Fast workaround
			//MainDataGrid.ItemsSource = _Item?.Items;
			if (ControlsHelper.IsDesignMode(this))
				return;
			if (ControlsHelper.AllowLoad(this))
			{
				AppHelper.InitHelp(this);
				UiPresetsManager.InitControl(this);
			}
			AppHelper.AddHelp(IsEnabledCheckBox, IsEnabledCheckBox.Content as string, Engine.Resources.MainResources.main_List_IsEnabled);
			AppHelper.AddHelp(IsReadOnlyCheckBox, IsReadOnlyCheckBox.Content as string, Engine.Resources.MainResources.main_List_IsReadOnly);
			AppHelper.AddHelp(InstructionsLabel, Engine.Resources.MainResources.main_Instructions, Engine.Resources.MainResources.main_List_Instructions);
			AppHelper.AddHelp(InstructionsTextBox, Engine.Resources.MainResources.main_Instructions, Engine.Resources.MainResources.main_List_Instructions);
			AppHelper.AddHelp(DescriptionLabel, Engine.Resources.MainResources.main_Description, Engine.Resources.MainResources.main_List_Description);
			AppHelper.AddHelp(DescriptionTextBox, Engine.Resources.MainResources.main_Description, Engine.Resources.MainResources.main_List_Description);
		}


		#region ■ INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		#endregion

	}

}
