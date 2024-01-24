using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.ComponentModel;
using JocysCom.ClassLibrary.Controls;
using JocysCom.ClassLibrary.Data;
using JocysCom.ClassLibrary.Files;
using JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	/// <summary>
	/// Interaction logic for AttachmentsControl.xaml
	/// </summary>
	public partial class AttachmentsControl : UserControl
	{
		public AttachmentsControl()
		{
			InitializeComponent();
			//ScanProgressPanel.Visibility = Visibility.Collapsed;
			if (ControlsHelper.IsDesignMode(this))
				return;
			MainDataGrid.ItemsSource = CurrentItems;
			UpdateButtons();
		}

		public SortableBindingList<file> CurrentItems { get; set; } = new SortableBindingList<file>();

		public void SelectById(string id)
		{
			var list = new List<string>() { id };
			ControlsHelper.SetSelection(MainDataGrid, nameof(file.id), list, 0);
		}

		public void ShowColumns(params DataGridColumn[] args)
		{
			var all = MainDataGrid.Columns.ToArray();
			foreach (var control in all)
				control.Visibility = args.Contains(control) ? Visibility.Visible : Visibility.Collapsed;
		}

		private async void MainDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			await Helper.Delay(UpdateButtons, AppHelper.NavigateDelayMs);
			SaveSelection();
		}

		void UpdateButtons()
		{
			var selecetedItems = MainDataGrid.SelectedItems.Cast<file>();
			var isSelected = selecetedItems.Count() > 0;
			RemoveButton.IsEnabled = isSelected;
		}

		private void AddButton_Click(object sender, RoutedEventArgs e)
		{
			/*
			var item = AppHelper.GetNewTemplateItem();
			// Treat the new task as a chat; therefore, clear the input box after sending.
			if (ItemControlType == ItemType.Task)
				item.MessageBoxOperation = MessageBoxOperation.ClearMessage;
			item.Name = $"Template_{DateTime.Now:yyyyMMdd_HHmmss}";
			// Set default icon. Make sure "document_gear.svg" Build Action is Embedded resource.
			var contents = Helper.FindResource<string>(ClientHelper.DefaultIconEmbeddedResource, GetType().Assembly);
			item.SetIcon(contents);
			InsertItem(item);
			*/
		}

		public void InsertItem(file item)
		{
			var position = FindInsertPosition(CurrentItems, item);
			// Make sure new item will be selected and focused.
			CurrentItems.Insert(position, item);
		}

		private int FindInsertPosition(IList<file> list, file item)
		{
			for (int i = 0; i < list.Count; i++)
				if (string.Compare(list[i].filename, item.filename, StringComparison.Ordinal) > 0)
					return i;
			// If not found, insert at the end
			return list.Count;
		}

		public void SaveSelection()
		{
			// Save selection.
			var selection = ControlsHelper.GetSelection<string>(MainDataGrid, nameof(file.id));
			if (selection.Count > 0 || Data.AttachmentsSelection == null)
				Data.AttachmentsSelection = selection;
		}

		private async void RefreshButton_Click(object sender, RoutedEventArgs e)
		{
			await Refresh();
		}

		private void CheckBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
			=> ControlsHelper.FileExplorer_DataGrid_CheckBox_PreviewMouseDown(sender, e);

		/// <summary>
		///  Event is fired when the DataGrid is rendered and its items are loaded,
		///  which means that you can safely select items at this point.
		/// </summary>
		private void MainDataGrid_Loaded(object sender, RoutedEventArgs e)
		{
			if (ControlsHelper.IsDesignMode(this))
				return;
		}

		private async void UserControl_Loaded(object sender, RoutedEventArgs e)
		{
			if (ControlsHelper.IsDesignMode(this))
				return;
			if (MustRefresh && IsVisible)
				await Refresh();
		}

		public void TryRefresh()
		{
			MustRefresh = true;
			if (IsVisible)
				Dispatcher.BeginInvoke((Action)(async () => await Refresh()));
		}

		#region IBindData

		[Category("Main")]

		public TemplateItem Data
		{
			get => _Data;
			set
			{
				if (_Data == value)
					return;
				CurrentItems.Clear();
				if (_Data != null)
				{
					_Data.PropertyChanged -= _Data_PropertyChanged;
				}
				if (value != null)
				{
					CurrentItems.AddRange(_Data.Attachments.Select(x => new file() { id = x }));
					value.PropertyChanged += _Data_PropertyChanged;
				}
				_Data = value;
				TryRefresh();
			}
		}
		public TemplateItem _Data;

		public bool MustRefresh;

		private async void _Data_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(_Data.AiService))
			{
				if (Global.IsGoodSettings(_Data.AiService))
					await Refresh();
			}
		}

		#endregion

		public async Task Refresh()
		{
			if (Data == null)
				return;
			SaveSelection();
			await Task.Delay(1);
			MustRefresh = false;
			ControlsHelper.SetSelection(MainDataGrid, nameof(file.id), Data.AttachmentsSelection, 0);
		}

		private void RemoveButton_Click(object sender, RoutedEventArgs e) =>
			Dispatcher.BeginInvoke(new Action(async () => await Remove()));

		#region Actions

		public List<file> GetWithAllow(AllowAction action)
		{
			var items = MainDataGrid.SelectedItems.Cast<file>().ToList();
			if (items.Count == 0)
				return null;
			if (!AppHelper.AllowAction(action, items.Select(x => x.id).ToArray()))
				return null;
			return items;
		}

		public async Task Remove()
		{
			var items = GetWithAllow(AllowAction.Remove);
			if (items == null)
				return;
			var errors = new ConcurrentBag<string>();
			// Create tasks for each item.
			var tasks = items.Select(RemoveItemAsync).ToList();
			// Await all tasks to complete.
			await Task.WhenAll(tasks);
			if (errors.Any())
			{
				var message = string.Join("\r\n", errors);
				Dispatcher.Invoke(() =>
					MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error));
			}
			// Define the async method for processing items.
			async Task RemoveItemAsync(file item)
			{
				await Task.Delay(1);
				Dispatcher.Invoke(() => CurrentItems.Remove(item));
			}
		}

		#endregion

		private void MainDataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			var isEditMode = AppHelper.IsGridInEditMode((DataGrid)sender);
			if (!isEditMode && e.Key == Key.Delete)
				Dispatcher.BeginInvoke(new Action(async () => await Remove()));
		}

		private void MainDataGrid_ContextMenu_Copy(object sender, RoutedEventArgs e) =>
			MainDataGrid_ContextMenu_Copy(false);

		private void MainDataGrid_ContextMenu_CopyWithHeaders(object sender, RoutedEventArgs e) =>
			MainDataGrid_ContextMenu_Copy(true);

		private void MainDataGrid_ContextMenu_CopyIdFileName(object sender, RoutedEventArgs e) =>
				MainDataGrid_ContextMenu_Copy(true, nameof(file.id), nameof(file.filename));

		void MainDataGrid_ContextMenu_Copy(bool withHeaders, params string[] columns)
		{
			var items = MainDataGrid.SelectedItems.Cast<file>().ToList();
			var table = SqlHelper.ConvertToTable(items, columns);
			var text = JocysCom.ClassLibrary.Files.CsvHelper.Write(table, withHeaders, "\t", CsvQuote.Strings);
			Clipboard.SetText(text);
		}

	}
}
