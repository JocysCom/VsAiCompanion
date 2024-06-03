using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.Collections;
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
	/// Interaction logic for ProjectsListControl.xaml
	/// </summary>
	public partial class FineTuningRemoteFilesControl : UserControl, IBindData<FineTuningItem>
	{
		public FineTuningRemoteFilesControl()
		{
			InitializeComponent();
			//ScanProgressPanel.Visibility = Visibility.Collapsed;
			if (ControlsHelper.IsDesignMode(this))
				return;
			MainDataGrid.ItemsSource = CurrentItems;
			Global.OnFilesUpdaded += Global_OnFilesUpladed;
			UpdateButtons();
		}

		private void Global_OnFilesUpladed(object sender, EventArgs e)
		{
			if (Data == null)
				return;
			ControlsHelper.EnsureTabItemSelected(this);
			// Remove selection. Selection will be restored from bound item data.
			MainDataGrid.SelectedIndex = -1;
			TryRefresh();
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
			DeleteButton.IsEnabled = isSelected;
			CreateModelButton.IsEnabled = selecetedItems.Any(x => x.purpose == Client.FineTuningPurpose);
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
			if (selection.Count > 0 || Data.FineTuningRemoteDataSelection == null)
				Data.FineTuningRemoteDataSelection = selection;
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
				ControlsHelper.AppBeginInvoke(async () => await Refresh());
		}

		#region IBindData

		[Category("Main")]

		public FineTuningItem Data
		{
			get => _Data;
			set
			{
				if (_Data == value)
					return;
				if (_Data != null)
				{
					_Data.PropertyChanged -= _Data_PropertyChanged;
				}
				if (value != null)
				{
					value.PropertyChanged += _Data_PropertyChanged;
				}
				_Data = value;
				TryRefresh();
			}
		}
		public FineTuningItem _Data;

		public bool MustRefresh;

		private async void _Data_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(_Data.AiService))
			{
				if (await Global.IsGoodSettings(_Data.AiService))
					await Refresh();
			}
		}

		#endregion

		private void HelpButton_Click(object sender, RoutedEventArgs e)
		{
			ControlsHelper.OpenUrl("https://platform.openai.com/docs/api-reference/files");
		}

		private void CreateModel_Click(object sender, System.Windows.RoutedEventArgs e)
			=> CreateJobAndModel();

		public async Task Refresh()
		{
			if (Data == null)
				return;
			if (!Global.ValidateServiceAndModel(Data))
				return;
			SaveSelection();
			var client = new Client(Data.AiService);
			var files = await client.GetFilesAsync();
			var fileList = files?.FirstOrDefault()?.data ?? new List<file>();
			CollectionsHelper.Synchronize(fileList, CurrentItems);
			MustRefresh = false;
			ControlsHelper.SetSelection(MainDataGrid, nameof(file.id), Data.FineTuningRemoteDataSelection, 0);
		}

		private void DeleteButton_Click(object sender, RoutedEventArgs e) =>
			ControlsHelper.AppBeginInvoke(async () => await Delete());

		#region Actions

		public List<file> GetWithAllow(AllowAction action)
		{
			var items = MainDataGrid.SelectedItems.Cast<file>().ToList();
			if (items.Count == 0)
				return null;
			if (!Global.ValidateServiceAndModel(Data))
				return null;
			if (!AppHelper.AllowAction(action, items.Select(x => x.id).ToArray()))
				return null;
			return items;
		}

		public async Task Delete() // Changed return type to Task instead of void
		{
			var items = GetWithAllow(AllowAction.Delete);
			if (items == null)
				return;
			var errors = new ConcurrentBag<string>();
			// Create tasks for each item.
			var tasks = items.Select(DeleteItemAsync).ToList();
			// Await all tasks to complete.
			await Task.WhenAll(tasks);
			if (errors.Any())
			{
				var message = string.Join("\r\n", errors);
				Dispatcher.Invoke(() =>
					MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error));
			}
			// Define the async method for processing items.
			async Task DeleteItemAsync(file item)
			{
				var client = new Client(Data.AiService);
				var response = await client.DeleteFileAsync(item.id);
				if (!string.IsNullOrEmpty(client.LastError))
					errors.Add(client.LastError);
				if (response?.deleted == true)
					Dispatcher.Invoke(() => CurrentItems.Remove(item));
			}
		}

		void CreateJobAndModel()
		{
			if (!Global.ValidateServiceAndModel(Data))
				return;
			var items = MainDataGrid.SelectedItems.Cast<file>().ToList();
			if (!AppHelper.AllowAction($"create job{(items.Count > 1 ? "s" : "")} to fine tune \"{Data.AiModel}\" custom model from ", items.Select(x => x.filename).ToArray()))
				return;
			foreach (var item in items)
			{
				// Use begin invoke or grid update will deadlock on same thread.
				ControlsHelper.BeginInvoke(async () =>
				{
					var client = new Client(Data.AiService);
					var request = new fine_tune_request()
					{
						training_file = item.id,
						model = Data.AiModel,
					};
					var fineTune = await client.CreateFineTuneJob(request);
					if (!string.IsNullOrEmpty(client.LastError))
						MessageBox.Show(client.LastError, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
					if (fineTune != null)
					{
						Client.Serialize(fineTune);
					}
					Global.RaiseOnFineTuningJobCreated();
				});
			}

		}

		#endregion

		private void MainDataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			var isEditMode = AppHelper.IsGridInEditMode((DataGrid)sender);
			if (!isEditMode && e.Key == Key.Delete)
				ControlsHelper.AppBeginInvoke(async () => await Delete());
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

