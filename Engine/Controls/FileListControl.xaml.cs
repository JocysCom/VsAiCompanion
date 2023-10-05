using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.ComponentModel;
using JocysCom.ClassLibrary.Configuration;
using JocysCom.ClassLibrary.Controls;
using JocysCom.ClassLibrary.Controls.Themes;
using JocysCom.ClassLibrary.Runtime;
using JocysCom.VS.AiCompanion.Engine.Companions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	/// <summary>
	/// Interaction logic for ProjectsListControl.xaml
	/// </summary>
	public partial class FileListControl : UserControl
	{
		public FileListControl()
		{
			InitializeComponent();
			//ScanProgressPanel.Visibility = Visibility.Collapsed;
			if (ControlsHelper.IsDesignMode(this))
				return;
			CurrentItems = Global.GetItems(ItemControlType);
			ExportSaveFileDialog = new System.Windows.Forms.SaveFileDialog();
			// Configure converter.
			var gridFormattingConverter = MainDataGrid.Resources.Values.Cast<Converters.ItemFormattingConverter>().First();
			gridFormattingConverter.ConvertFunction = _MainDataGridFormattingConverter_Convert;
			UpdateButtons();
		}

		private void Tasks_ListChanged(object sender, ListChangedEventArgs e)
			=> UpdateButtons();

		bool selectionsUpdating = false;
		private void CurrentItems_ListChanged(object sender, ListChangedEventArgs e)
		{
			ControlsHelper.BeginInvoke(() =>
			{
				if (e.ListChangedType == ListChangedType.ItemChanged)
				{
					if (!selectionsUpdating && e.PropertyDescriptor?.Name == nameof(TemplateItem.IsChecked))
					{
						selectionsUpdating = true;
						var selectedItems = MainDataGrid.SelectedItems.Cast<TemplateItem>().ToList();
						// Get updated item.
						var item = (TemplateItem)MainDataGrid.Items[e.NewIndex];
						if (selectedItems.Contains(item))
						{
							// Update other items to same value.
							selectedItems.Remove(item);
							foreach (var selecetdItem in selectedItems)
								if (selecetdItem.IsChecked != item.IsChecked)
									selecetdItem.IsChecked = item.IsChecked;
						}
						selectionsUpdating = false;
					}
				}
			});
		}

		object _MainDataGridFormattingConverter_Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
		{
			var sender = (FrameworkElement)values[0];
			var template = (FrameworkElement)values[1];
			var cell = (DataGridCell)(template ?? sender).Parent;
			var value = values[2];
			var item = (TemplateItem)cell.DataContext;
			// Format ConnectionClassColumn value.
			// Format StatusCodeColumn value.
			if (cell.Column == StatusCodeColumn)
			{
				switch (item.StatusCode)
				{
					case MessageBoxImage.Error:
						return Icons.Current[Icons.Icon_Error];
					case MessageBoxImage.Question:
						return Icons.Current[Icons.Icon_Question];
					case MessageBoxImage.Warning:
						return Icons.Current[Icons.Icon_Warning];
					case MessageBoxImage.Information:
						return Icons.Current[Icons.Icon_Information];
					default:
						return null;
				}
			}
			return value;
		}

		public SortableBindingList<TemplateItem> CurrentItems { get; set; } = new SortableBindingList<TemplateItem>();

		#region ■ Properties

		[Category("Main"), DefaultValue(ItemType.None)]
		public ItemType ItemControlType
		{
			get => _ItemControlType;
			set
			{
				_ItemControlType = value;
				if (ControlsHelper.IsDesignMode(this))
					return;
				// Get settings.
				SettingsData.FilesChanged -= SettingsData_FilesChanged;
				SettingsData = Global.GetSettings(value);
				SettingsData.FilesChanged += SettingsData_FilesChanged;
				// Update panel settings.
				PanelSettings.PropertyChanged -= PanelSettings_PropertyChanged;
				PanelSettings = Global.AppSettings.GetTaskSettings(value);
				PanelSettings.PropertyChanged += PanelSettings_PropertyChanged;
				// Update other controls.
				MainDataGrid.SelectionChanged -= MainDataGrid_SelectionChanged;
				CurrentItems.ListChanged -= CurrentItems_ListChanged;
				CurrentItems = Global.GetItems(value);
				InitSearch();
				// Re-attach events.
				MainDataGrid.SelectionChanged += MainDataGrid_SelectionChanged;
				CurrentItems.ListChanged += CurrentItems_ListChanged;
				ShowColumns(IconColumn, NameColumn);
				ShowButtons(AddButton, DeleteButton);
			}
		}

		private void SettingsData_FilesChanged(object sender, EventArgs e)
		{
			Dispatcher.BeginInvoke(new Action(() =>
			{
				// Reload data from the disk.
				SettingsData.Load();
			}));
		}

		public void SelectByName(string name)
		{
			var list = new List<string>() { name };
			ControlsHelper.RestoreSelection(MainDataGrid, nameof(TemplateItem.Name), list, 0);
		}

		private ItemType _ItemControlType;

		TaskSettings PanelSettings { get; set; } = new TaskSettings();
		ISettingsData SettingsData { get; set; } = new SettingsData<TemplateItem>();

		private void PanelSettings_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
		}

		public void ShowColumns(params DataGridColumn[] args)
		{
			var all = MainDataGrid.Columns.ToArray();
			foreach (var control in all)
				control.Visibility = args.Contains(control) ? Visibility.Visible : Visibility.Collapsed;
		}

		public void ShowButtons(params Button[] args)
		{
			var all = new Button[] { AddButton, DeleteButton };
			foreach (var control in all)
				control.Visibility = args.Contains(control) ? Visibility.Visible : Visibility.Collapsed;
		}

		#endregion

		System.Windows.Forms.SaveFileDialog ExportSaveFileDialog;

		private void ExportButton_Click(object sender, RoutedEventArgs e)
		{
			var dialog = ExportSaveFileDialog;
			dialog.DefaultExt = "*.csv";
			dialog.Filter = "Data (*.csv)|*.csv|All files (*.*)|*.*";
			dialog.FilterIndex = 1;
			dialog.RestoreDirectory = true;
			if (string.IsNullOrEmpty(dialog.FileName))
				dialog.FileName = $"{ItemControlType}";
			//if (string.IsNullOrEmpty(dialog.InitialDirectory)) dialog.InitialDirectory = ;
			dialog.Title = "Export Data File";
			var result = dialog.ShowDialog();
			if (result == System.Windows.Forms.DialogResult.OK)
			{
				var table = ConvertToTable(CurrentItems);
				var data = JocysCom.ClassLibrary.Files.CsvHelper.Write(table);
				System.IO.File.WriteAllText(dialog.FileName, data);
			}
		}

		private async void MainDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			await Helper.Delay(UpdateOnSelectionChanged, AppHelper.NavigateDelayMs);
		}

		private void UpdateOnSelectionChanged()
		{
			// If item selected then...
			if (MainDataGrid.SelectedIndex >= 0)
			{
				// Remember selection.
				PanelSettings.ListSelection = ControlsHelper.GetSelection<string>(MainDataGrid, nameof(TemplateItem.Name));
				PanelSettings.ListSelectedIndex = MainDataGrid.SelectedIndex;
			}
			else
			{
				// Try to restore selection.
				ControlsHelper.RestoreSelection(
					MainDataGrid, nameof(TemplateItem.Name),
					PanelSettings.ListSelection, PanelSettings.ListSelectedIndex
				);
			}
			UpdateButtons();
		}


		void UpdateButtons()
		{
			var allowEnable = true;
			var selecetedItems = MainDataGrid.SelectedItems.Cast<TemplateItem>();
			var isSelected = selecetedItems.Count() > 0;
			if (ItemControlType == ItemType.Template)
			{
				// Count updatable references.
				allowEnable = selecetedItems.Count(x => x.StatusCode == MessageBoxImage.Information) > 0;
			}
			var isBusy = (Global.MainControl?.InfoPanel?.Tasks?.Count ?? 0) > 0;
			DeleteButton.IsEnabled = isSelected;
		}

		/// <summary>
		/// Convert List to DataTable. Can be used to pass data into stored procedures. 
		/// </summary>
		public static DataTable ConvertToTable<T>(IEnumerable<T> list)
		{
			if (list == null) return null;
			var table = new DataTable();
			var props = typeof(T).GetProperties().Where(x => x.CanRead).ToArray();
			foreach (var prop in props)
				table.Columns.Add(prop.Name, prop.PropertyType);
			var values = new object[props.Length];
			foreach (T item in list)
			{
				for (int i = 0; i < props.Length; i++)
					values[i] = props[i].GetValue(item, null);
				table.Rows.Add(values);
			}
			return table;
		}

		private void UserControl_Loaded(object sender, RoutedEventArgs e)
		{
			if (ControlsHelper.IsDesignMode(this))
				return;
			Global.MainControl.InfoPanel.Tasks.ListChanged -= Tasks_ListChanged;
			Global.MainControl.InfoPanel.Tasks.ListChanged += Tasks_ListChanged;
		}

		private void AddButton_Click(object sender, RoutedEventArgs e)
		{
			var item = AppHelper.GetNewTemplateItem();
			// Treat the new task as a chat; therefore, clear the input box after sending.
			if (ItemControlType == ItemType.Task)
				item.MessageBoxOperation = MessageBoxOperation.ClearMessage;
			item.Name = $"Template_{DateTime.Now:yyyyMMdd_HHmmss}";
			// Set default icon. Make sure "document_gear.svg" Build Action is Embedded resource.
			var contents = Helper.FindResource<string>(ClientHelper.DefaultIconEmbeddedResource, GetType().Assembly);
			item.SetIcon(contents);
			InsertItem(item);
		}

		public void InsertItem(TemplateItem item)
		{
			var position = FindInsertPosition(CurrentItems, item);
			// Make sure new item will be selected and focused.
			PanelSettings.ListSelection = new List<string>() { item.Name };
			PanelSettings.ListSelectedIndex = position;
			CurrentItems.Insert(position, item);
			SettingsData.Save();
		}

		private int FindInsertPosition(IList<TemplateItem> list, TemplateItem item)
		{
			for (int i = 0; i < list.Count; i++)
				if (string.Compare(list[i].Name, item.Name, StringComparison.Ordinal) > 0)
					return i;
			// If not found, insert at the end
			return list.Count;
		}

		private void DeleteButton_Click(object sender, RoutedEventArgs e)
		{
			var items = MainDataGrid.SelectedItems.Cast<TemplateItem>().ToList();
			if (items.Count == 0)
				return;
			//SelectedIndex = MainDataGrid.Items.IndexOf(items[0]);
			var text = $"Do you want to delete {items.Count} item{(items.Count > 1 ? "s" : "")}?";
			var caption = $"{Global.Info.Product} - Delete";
			var result = MessageBox.Show(text, caption, MessageBoxButton.YesNo, MessageBoxImage.Question);
			if (result != MessageBoxResult.Yes)
				return;

			// Use begin invoke or grid update will deadlock on same thread.
			ControlsHelper.BeginInvoke(() =>
			{
				var list = ItemControlType == ItemType.Template
					? Global.Templates
					: Global.Tasks;
				foreach (var item in items)
					list.DeleteItem(item);
			});
		}


		#region Grid Editing

		private void MainDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			var grid = sender as DataGrid;
			if (grid.SelectedItem != null && e.ChangedButton == MouseButton.Left)
				grid.BeginEdit();
		}

		private string _originalValue;

		private void MainDataGrid_PreparingCellForEdit(object sender, DataGridPreparingCellForEditEventArgs e)
		{
			if (e.Column == NameColumn)
			{
				var textBox = e.EditingElement as TextBox;
				textBox.Focus();
				textBox.SelectAll();
				_originalValue = textBox.Text; // Store the original value
			}
		}

		private void MainDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
		{
			if (e.EditAction == DataGridEditAction.Cancel) // Check if editing was cancelled
			{
				var textBox = e.EditingElement as TextBox;
				textBox.Text = _originalValue; // Restore the original value
			}
			else if (e.Column == NameColumn)
			{
				var textBox = e.EditingElement as TextBox;
				var newName = textBox.Text.Trim();
				var item = (TemplateItem)e.Row.Item;
				var list = ItemControlType == ItemType.Template
					? Global.Templates
					: Global.Tasks;
				var error = list.RenameItem(item, newName);
				if (!string.IsNullOrEmpty(error))
				{
					MessageBox.Show(error);
					e.Cancel = true;
					return;
				}
			}
		}

		/// <summary>
		///  Event is fired when the DataGrid is rendered and its items are loaded,
		///  which means that you can safely select items at this point.
		/// </summary>
		private void MainDataGrid_Loaded(object sender, RoutedEventArgs e)
		{
			if (ControlsHelper.IsDesignMode(this))
				return;
			var list = PanelSettings.ListSelection;
			if (list?.Count > 0)
				ControlsHelper.RestoreSelection(MainDataGrid, nameof(TemplateItem.Name), list, 0);
			SearchTextBox.Text = PanelSettings.SearchText;
		}

		#region Search Filter

		private void InitSearch()
		{
			_SearchHelper = new SearchHelper<TemplateItem>((x) =>
			{
				var s = SearchTextBox.Text;
				return string.IsNullOrEmpty(s) ||
					(x.Name ?? "").IndexOf(s, StringComparison.OrdinalIgnoreCase) > -1 ||
					(x.Text ?? "").IndexOf(s, StringComparison.OrdinalIgnoreCase) > -1;
			}, null, new SortableBindingList<TemplateItem>());
			_SearchHelper.SetSource(CurrentItems);
			_SearchHelper.Synchronized += _SearchHelper_Synchronized;
			MainDataGrid.ItemsSource = _SearchHelper.FilteredList;
		}

		private void _SearchHelper_Synchronized(object sender, EventArgs e)
		{
			// Try to restore selection.
			ControlsHelper.RestoreSelection(
				MainDataGrid, nameof(TemplateItem.Name),
				PanelSettings.ListSelection, 0
			);
		}

		private SearchHelper<TemplateItem> _SearchHelper;

		private void SearchTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
		{
		}

		private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			_SearchHelper.Filter();
			if (PanelSettings.SearchText != SearchTextBox.Text)
				PanelSettings.SearchText = SearchTextBox.Text;
		}

		#endregion

		private void FilterButton_Click(object sender, RoutedEventArgs e)
		{
			_SearchHelper.Filter();
		}

		Task TitleTask;

	}

	#endregion

}

