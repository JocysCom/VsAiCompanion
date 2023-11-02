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
	public partial class FineTuningJobListControl : UserControl, IBindData<FineTuningItem>
	{
		public FineTuningJobListControl()
		{
			InitializeComponent();
			//ScanProgressPanel.Visibility = Visibility.Collapsed;
			if (ControlsHelper.IsDesignMode(this))
				return;
			MainDataGrid.ItemsSource = CurrentItems;
			Global.OnFineTuningJobCreated += Global_OnFineTuningJobCreated;
			UpdateButtons();
		}

		private void Global_OnFineTuningJobCreated(object sender, EventArgs e)
		{
			TryRefresh();
		}

		public SortableBindingList<fine_tuning_job> CurrentItems { get; set; } = new SortableBindingList<fine_tuning_job>();

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

		private async void MainDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			await Helper.Delay(UpdateButtons, AppHelper.NavigateDelayMs);
			SaveSelection();
		}

		void UpdateButtons()
		{
			var selecetedItems = MainDataGrid.SelectedItems.Cast<fine_tuning_job>();
			var isSelected = selecetedItems.Count() > 0;
			//var isBusy = (Global.MainControl?.InfoPanel?.Tasks?.Count ?? 0) > 0;
			CancelButton.IsEnabled = isSelected;
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

		public void InsertItem(fine_tuning_job item)
		{
			var position = FindInsertPosition(CurrentItems, item);
			// Make sure new item will be selected and focused.
			CurrentItems.Insert(position, item);
		}

		private int FindInsertPosition(IList<fine_tuning_job> list, fine_tuning_job item)
		{
			for (int i = 0; i < list.Count; i++)
				if (string.Compare(list[i].id, item.id, StringComparison.Ordinal) > 0)
					return i;
			// If not found, insert at the end
			return list.Count;
		}

		public void SaveSelection()
		{
			// Save selection.
			var selection = ControlsHelper.GetSelection<string>(MainDataGrid, nameof(fine_tuning_job.id));
			if (selection.Count > 0 || Data.FineTuningJobListSelection == null)
				Data.FineTuningJobListSelection = selection;
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

		private void UserControl_Loaded(object sender, RoutedEventArgs e)
		{
			if (ControlsHelper.IsDesignMode(this))
				return;
			TryRefresh();
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
				if (IsVisible)
					Dispatcher.BeginInvoke((Action)(async () => await Refresh()));
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
			ControlsHelper.OpenUrl("https://platform.openai.com/docs/api-reference/fine-tuning");
		}

		public async Task Refresh()
		{
			if (!Global.ValidateServiceAndModel(Data.AiService, Data.AiModel))
				return;
			SaveSelection();
			var client = new Client(Data.AiService);
			var request = new fine_tuning_jobs_request();
			request.limit = 1000;
			var response = await client.GetFineTuningJobsAsync(request);
			var items = response.First()?.data;
			CollectionsHelper.Synchronize(items, CurrentItems);
			MustRefresh = false;
			ControlsHelper.SetSelection(MainDataGrid, nameof(fine_tuning_job.id), Data.FineTuningJobListSelection, 0);
		}

		public List<fine_tuning_job> GetWithAllow(AllowAction action)
		{
			var items = MainDataGrid.SelectedItems.Cast<fine_tuning_job>().ToList();
			if (items.Count == 0)
				return null;
			if (!Global.ValidateServiceAndModel(Data.AiService, Data.AiModel))
				return null;
			if (!AppHelper.AllowAction(action, items.Select(x => x.id).ToArray()))
				return null;
			return items;
		}

		/// <summary>
		///  Delete is not allowed.
		/// </summary>
		private void Delete()
		{
			var items = GetWithAllow(AllowAction.Delete);
			if (items == null)
				return;
			var client = new Client(Data.AiService);
			// Use begin invoke or grid update will deadlock on same thread.
			ControlsHelper.BeginInvoke(async () =>
			{
				foreach (var item in items)
				{
					var response = await client.DeleteFineTuningJobAsync(item.id);
					if (response?.deleted == true)
						CurrentItems.Remove(item);

				}
			});
		}

		private void CancelButton_Click(object sender, RoutedEventArgs e)
		{
			var items = GetWithAllow(AllowAction.Cancel);
			if (items == null)
				return;
			// Use begin invoke or grid update will deadlock on same thread.
			ControlsHelper.BeginInvoke(async () =>
			{
				foreach (var item in items)
				{
					var client = new Client(Data.AiService);
					var response = await client.CancelFineTuningJobAsync(item.id);
					if (!string.IsNullOrEmpty(client.LastError))
						MessageBox.Show(client.LastError, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				}
				await Refresh();
			});
		}

		public void TryRefresh()
		{
			MustRefresh = true;
			if (IsVisible)
				Dispatcher.BeginInvoke((Action)(async () => await Refresh()));
		}
	}

}

