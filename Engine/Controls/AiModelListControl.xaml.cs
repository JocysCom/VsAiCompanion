using JocysCom.ClassLibrary.ComponentModel;
using JocysCom.ClassLibrary.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	/// <summary>
	/// Interaction logic for AiModelListControl.xaml
	/// </summary>
	public partial class AiModelListControl : UserControl, INotifyPropertyChanged
	{
		public AiModelListControl()
		{
			InitializeComponent();
			if (ControlsHelper.IsDesignMode(this))
				return;
			FilteredList = Global.AppSettings.AiModels;
			OnPropertyChanged(nameof(FilteredList));
			UpdateButtons();
		}

		private void Tasks_ListChanged(object sender, ListChangedEventArgs e)
			=> UpdateButtons();

		#region ■ Properties

		public SortableBindingList<AiModel> FilteredList { get; set; } = new SortableBindingList<AiModel>();

		public void SelectByName(string name)
		{
			var list = new List<string>() { name };
			ControlsHelper.SetSelection(MainDataGrid, nameof(AiModel.Name), list, 0);
		}

		public void ShowColumns(params DataGridColumn[] args)
		{
			var all = MainDataGrid.Columns.ToArray();
			foreach (var control in all)
				control.Visibility = args.Contains(control)
					? Visibility.Visible
					: Visibility.Collapsed;
		}

		public void ShowButtons(params Button[] args)
		{
			var all = new Button[] { AddButton, EditButton, DeleteButton };
			foreach (var control in all)
				control.Visibility = args.Contains(control)
					? Visibility.Visible
					: Visibility.Collapsed;
		}

		#endregion

		TaskSettings PanelSettings { get; } = Global.AppSettings.GetTaskSettings(ItemType.AiModel);

		private void MainDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (ControlsHelper.IsDesignMode(this))
				return;
			// If item selected then...
			if (MainDataGrid.SelectedIndex >= 0)
			{
				// Remember selection.
				PanelSettings.ListSelection = ControlsHelper.GetSelection<Guid>(MainDataGrid, nameof(AiModel.Id))
					.Select(x => x.ToString()).Distinct().ToList();
				PanelSettings.ListSelectedIndex = MainDataGrid.SelectedIndex;
			}
			else
			{
				var list = PanelSettings.GetSelectionListAsGuid();
				// Try to restore selection.
				ControlsHelper.SetSelection(
					MainDataGrid, nameof(AiModel.Id),
					list, PanelSettings.ListSelectedIndex
				);
			}
			UpdateButtons();
		}

		void UpdateButtons()
		{
			var selecetedItems = MainDataGrid.SelectedItems.Cast<AiModel>();
			var isSelected = selecetedItems.Any();
			EditButton.IsEnabled = isSelected;
			DeleteButton.IsEnabled = isSelected;
		}

		public event EventHandler<EventArgs> Add;

		private void AddButton_Click(object sender, RoutedEventArgs e)
		{
			var item = new AiModel();
			item.Name = "GPT-4";
			InsertItem(item);
			Add?.Invoke(this, EventArgs.Empty);
		}

		public void InsertItem(AiModel item)
		{
			// Make sure new item will be selected and focused.
			PanelSettings.ListSelection = new List<string>() { item.Name };
			PanelSettings.ListSelectedIndex = FilteredList.Count;
			FilteredList.Add(item);
			Global.AppData.Save();
		}

		private void DeleteButton_Click(object sender, RoutedEventArgs e)
		{
			var items = MainDataGrid.SelectedItems.Cast<AiModel>().ToList();
			if (items.Count == 0)
				return;
			//SelectedIndex = MainDataGrid.Items.IndexOf(items[0]);
			var text = $"Do you want to delete {items.Count} item{(items.Count > 1 ? "s" : "")}?";
			var caption = $"Delete";
			var result = MessageBox.Show(text, caption, MessageBoxButton.YesNo, MessageBoxImage.Question);
			if (result != MessageBoxResult.Yes)
				return;
			// Use begin invoke or grid update will deadlock on same thread.
			ControlsHelper.BeginInvoke(() =>
			{
				foreach (var item in items)
					FilteredList.Remove(item);
				Global.AppSettings.CleanupAiModels();
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
			if (e.Column == KeyColumn)
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
			else if (e.Column == KeyColumn)
			{
				var textBox = e.EditingElement as TextBox;
				var newName = textBox.Text.Trim();
				var error = "";
				if (string.IsNullOrEmpty(newName.Trim()))
					error = "Name can't be empty";
				//var item = (AiModel)e.Row.Item;
				if (!string.IsNullOrEmpty(error))
				{
					MessageBox.Show(error);
					e.Cancel = true;
					return;
				}
			}
		}

		private void EditButton_Click(object sender, RoutedEventArgs e)
		{
			if (MainDataGrid.SelectedItem != null)
			{
				MainDataGrid.CurrentCell = new DataGridCellInfo(MainDataGrid.SelectedItem, KeyColumn);
				MainDataGrid.Focus();
				MainDataGrid.BeginEdit();
			}
		}

		private void MainDataGrid_Loaded(object sender, RoutedEventArgs e)
		{
			if (ControlsHelper.IsDesignMode(this))
				return;
			var list = PanelSettings.GetSelectionListAsGuid();
			ControlsHelper.SetSelection(MainDataGrid, nameof(AiModel.Id), list, PanelSettings.ListSelectedIndex);
		}

		#region ■ INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		#endregion
	}

	#endregion
}
