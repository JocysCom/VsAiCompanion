using JocysCom.ClassLibrary.ComponentModel;
using JocysCom.ClassLibrary.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	/// <summary>
	/// Interaction logic for AiServiceListControl.xaml
	/// </summary>
	public partial class AiServiceListControl : UserControl
	{
		public AiServiceListControl()
		{
			InitializeComponent();
			if (ControlsHelper.IsDesignMode(this))
				return;
			//CurrentItems = new SortableBindingList<AiService>();
			CurrentItems = Global.AppSettings.AiServices;
			MainDataGrid.ItemsSource = CurrentItems;
			Global.OnAiServicesUpdated += Global_OnAiServicesUpdated;
			UpdateButtons();
		}

		private void Global_OnAiServicesUpdated(object sender, EventArgs e)
		{
			CurrentItems = Global.AppSettings.AiServices;
			MainDataGrid.ItemsSource = CurrentItems;
		}

		private void Tasks_ListChanged(object sender, ListChangedEventArgs e)
			=> UpdateButtons();

		#region ■ Properties

		[Category("Main"), DefaultValue(null)]
		public SortableBindingList<AiService> CurrentItems
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
		SortableBindingList<AiService> _CurrentItems;


		public void SelectByName(string name)
		{
			var list = new List<string>() { name };
			ControlsHelper.SetSelection(MainDataGrid, nameof(AiService.Name), list, 0);
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

		TaskSettings PanelSettings { get; } = Global.AppSettings.GetTaskSettings(ItemType.AiService);

		private void MainDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			// If item selected then...
			if (MainDataGrid.SelectedIndex >= 0)
			{
				// Remember selection.
				PanelSettings.ListSelection = ControlsHelper.GetSelection<string>(MainDataGrid, nameof(AiService.Name));
				PanelSettings.ListSelectedIndex = MainDataGrid.SelectedIndex;
			}
			else
			{
				// Try to restore selection.
				ControlsHelper.SetSelection(
					MainDataGrid, nameof(AiService.Name),
					PanelSettings.ListSelection, PanelSettings.ListSelectedIndex
				);
			}
			UpdateButtons();
		}

		void UpdateButtons()
		{
			var selecetedItems = MainDataGrid.SelectedItems.Cast<AiService>();
			var isSelected = selecetedItems.Count() > 0;
			EditButton.IsEnabled = isSelected;
			DeleteButton.IsEnabled = isSelected;
		}

		public event EventHandler<EventArgs> Add;

		private void AddButton_Click(object sender, RoutedEventArgs e)
		{
			var item = new AiService();
			item.Name = "Open AI";
			InsertItem(item);
			Add?.Invoke(this, EventArgs.Empty);
		}

		public void InsertItem(AiService item)
		{
			// Make sure new item will be selected and focused.
			PanelSettings.ListSelection = new List<string>() { item.Name };
			PanelSettings.ListSelectedIndex = CurrentItems.Count();
			CurrentItems.Add(item);
			Global.AppData.Save();
		}

		private void DeleteButton_Click(object sender, RoutedEventArgs e)
		{
			var items = MainDataGrid.SelectedItems.Cast<AiService>().ToList();
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
				var item = (AiService)e.Row.Item;
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
			var list = PanelSettings.ListSelection;
			ControlsHelper.SetSelection(MainDataGrid, nameof(AiModel.Name), list, PanelSettings.ListSelectedIndex);
		}
	}

	#endregion
}
