using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Windows;
using System;
using System.Windows.Controls;
using System.Windows.Input;
using JocysCom.ClassLibrary.Collections;
using JocysCom.ClassLibrary.ComponentModel;

namespace JocysCom.ClassLibrary.Controls
{
	/// <summary>
	/// Interaction logic for KeyValueUserControl.xaml
	/// </summary>
	public partial class KeyValueUserControl : UserControl
	{
		public KeyValueUserControl()
		{
			InitializeComponent();
			if (ControlsHelper.IsDesignMode(this))
				return;
			CurrentItems = new SortableBindingList<KeyValue>();
			UpdateButtons();
		}

		private void Tasks_ListChanged(object sender, ListChangedEventArgs e)
			=> UpdateButtons();

		#region ■ Properties

		[Category("Main"), DefaultValue(null)]
		public SortableBindingList<KeyValue> CurrentItems
		{
			get => _CurrentItems;
			set
			{
				if (ControlsHelper.IsDesignMode(this))
					return;
				// Update other controls.
				MainDataGrid.SelectionChanged -= MainDataGrid_SelectionChanged;
				_CurrentItems = value;
				// Re-attach events.
				MainDataGrid.SelectionChanged += MainDataGrid_SelectionChanged;
				ShowButtons(AddButton, EditButton, DeleteButton);
			}
		}
		SortableBindingList<KeyValue> _CurrentItems;


		public void SelectByName(string name)
		{
			var list = new List<string>() { name };
			ControlsHelper.SetSelection(MainDataGrid, nameof(KeyValue.Key), list, 0);
		}

		public void ShowColumns(params DataGridColumn[] args)
		{
			var all = MainDataGrid.Columns.ToArray();
			foreach (var control in all)
				control.Visibility = args.Contains(control) ? Visibility.Visible : Visibility.Collapsed;
		}

		public void ShowButtons(params Button[] args)
		{
			var all = new Button[] { AddButton, EditButton, DeleteButton };
			foreach (var control in all)
				control.Visibility = args.Contains(control) ? Visibility.Visible : Visibility.Collapsed;
		}

		#endregion

		private void MainDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			UpdateButtons();
		}

		void UpdateButtons()
		{
			var selecetedItems = MainDataGrid.SelectedItems.Cast<KeyValue>();
			var isSelected = selecetedItems.Count() > 0;
			EditButton.IsEnabled = isSelected;
			DeleteButton.IsEnabled = isSelected;
		}

		public event EventHandler<EventArgs> Add;

		private void AddButton_Click(object sender, RoutedEventArgs e)
		{
			Add?.Invoke(this, EventArgs.Empty);
		}

		private void DeleteButton_Click(object sender, RoutedEventArgs e)
		{
			var items = MainDataGrid.SelectedItems.Cast<KeyValue>().ToList();
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
					CurrentItems.Remove(item);
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
				var item = (KeyValue)e.Row.Item;
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
				MainDataGrid.CurrentCell = new DataGridCellInfo(MainDataGrid.SelectedItem, ValueColumn);
				MainDataGrid.Focus();
				MainDataGrid.BeginEdit();
			}
		}

		private void MainDataGrid_Loaded(object sender, RoutedEventArgs e)
		{

		}
	}

	#endregion
}
