using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.Collections;
using JocysCom.ClassLibrary.ComponentModel;
using JocysCom.ClassLibrary.Controls;
using JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT;
using System;
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
	public partial class FineTuningRemoteDataControl : UserControl, IBindData<FineTuningItem>
	{
		public FineTuningRemoteDataControl()
		{
			InitializeComponent();
			//ScanProgressPanel.Visibility = Visibility.Collapsed;
			if (ControlsHelper.IsDesignMode(this))
				return;
			var item = Global.FineTunes.Items.FirstOrDefault();
			Data = item;
			MainDataGrid.ItemsSource = CurrentItems;
			UpdateButtons();
		}

		public SortableBindingList<file> CurrentItems { get; set; } = new SortableBindingList<file>();

		public void SelectByName(string name)
		{
			var list = new List<string>() { name };
			ControlsHelper.SetSelection(MainDataGrid, nameof(TemplateItem.Name), list, 0);
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

		private async void MainDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			await Helper.Delay(UpdateButtons, AppHelper.NavigateDelayMs);
			SaveSelection();
		}

		void UpdateButtons()
		{
			var selecetedItems = MainDataGrid.SelectedItems.Cast<file>();
			var isSelected = selecetedItems.Count() > 0;
			//var isBusy = (Global.MainControl?.InfoPanel?.Tasks?.Count ?? 0) > 0;
			DeleteButton.IsEnabled = isSelected;
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

		private void DeleteButton_Click(object sender, RoutedEventArgs e)
		{
			var items = MainDataGrid.SelectedItems.Cast<file>().ToList();
			if (items.Count == 0)
				return;
			if (!AppHelper.AllowAction(AllowAction.Delete, items.Select(x => $"{x.id} - {x.filename}").ToArray()))
				return;
			// Use begin invoke or grid update will deadlock on same thread.
			ControlsHelper.BeginInvoke(async () =>
			{
				var client = new Client(Data.AiService);
				foreach (var item in items)
				{
					var response = await client.DeleteFileAsync(item.id);
					if (response.deleted)
						CurrentItems.Remove(item);
				}
			});
		}

		public void SaveSelection()
		{
			// Save selection.
			var selection = ControlsHelper.GetSelection<string>(MainDataGrid, nameof(file.filename));
			if (selection.Count > 0 || Data.FineTuningRemoteDataSelection == null)
				Data.FineTuningRemoteDataSelection = selection;
		}

		public async Task Refresh()
		{
			if (!Global.ValidateServiceAndModel(Data.AiService, Data.AiModel))
				return;
			SaveSelection();
			var client = new Client(Data.AiService);
			var files = await client.GetFilesAsync();
			var fileList = files.First()?.data;
			CollectionsHelper.Synchronize(fileList, CurrentItems);
			MustRefresh = false;
			ControlsHelper.SetSelection(MainDataGrid, nameof(file.filename), Data.FineTuningRemoteDataSelection, 0);
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
				MustRefresh = true;
			}
		}
		public FineTuningItem _Data;

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

		private void HelpButton_Click(object sender, RoutedEventArgs e)
		{
			ControlsHelper.OpenUrl("https://platform.openai.com/docs/api-reference/files");
		}

		private void CreateModel_Click(object sender, System.Windows.RoutedEventArgs e)
		{
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
					var message = fineTune == null
							? client.LastError
							: Client.Serialize(fineTune);
					//LogTextBox.AppendText(message);
				});
			}
		}

	}

}

