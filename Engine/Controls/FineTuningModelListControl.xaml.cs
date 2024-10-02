using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.Collections;
using JocysCom.ClassLibrary.ComponentModel;
using JocysCom.ClassLibrary.Controls;
using JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT;
using JocysCom.VS.AiCompanion.Plugins.Core.VsFunctions;
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
	public partial class FineTuningModelListControl : UserControl, IBindData<FineTuningItem>
	{
		public FineTuningModelListControl()
		{
			InitializeComponent();
			//ScanProgressPanel.Visibility = Visibility.Collapsed;
			if (ControlsHelper.IsDesignMode(this))
				return;
			MainDataGrid.ItemsSource = CurrentItems;
			UpdateButtons();
		}

		public SortableBindingList<model> CurrentItems { get; set; } = new SortableBindingList<model>();

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
			var all = new Button[] { DeleteButton };
			foreach (var control in all)
				control.Visibility = args.Contains(control) ? Visibility.Visible : Visibility.Collapsed;
		}

		void UpdateButtons()
		{
			var selecetedItems = MainDataGrid.SelectedItems.Cast<model>();
			var isSelected = selecetedItems.Count() > 0;
			//var isBusy = (Global.MainControl?.InfoPanel?.Tasks?.Count ?? 0) > 0;
			DeleteButton.IsEnabled = selecetedItems.Any(x => (x.id ?? "").StartsWith("ft:"));
		}

		public void InsertItem(model item)
		{
			//var position = FindInsertPosition(CurrentItems, item);
			// Make sure new item will be selected and focused.
			//CurrentItems.Insert(position, item);
		}

		public void SaveSelection()
		{
			// Save selection.
			var selection = ControlsHelper.GetSelection<string>(MainDataGrid, nameof(model.id));
			if (selection.Count > 0 || Data.FineTuningModelListSelection == null)
				Data.FineTuningModelListSelection = selection;
		}

		private void RefreshButton_Click(object sender, RoutedEventArgs e)
		{
			_ = Helper.Debounce(Refresh, AppHelper.NavigateDelayMs);
		}

		private async void MainDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			await Helper.Debounce(UpdateButtons, AppHelper.NavigateDelayMs);
			SaveSelection();
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

		private void This_Loaded(object sender, RoutedEventArgs e)
		{
			if (ControlsHelper.IsDesignMode(this))
				return;
			if (MustRefresh && IsVisible)
				_ = Helper.Debounce(Refresh, AppHelper.NavigateDelayMs);
			if (ControlsHelper.AllowLoad(this))
			{
				AppHelper.InitHelp(this);
				UiPresetsManager.InitControl(this, true);
			}
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
					_ = Helper.Debounce(Refresh, AppHelper.NavigateDelayMs);
			}
		}
		public FineTuningItem _Data;

		public bool MustRefresh;

		private async void _Data_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(_Data.AiService))
			{
				if (await Global.IsGoodSettings(_Data.AiService))
					_ = Helper.Debounce(Refresh, AppHelper.NavigateDelayMs);
			}
		}

		#endregion

		private void HelpButton_Click(object sender, RoutedEventArgs e)
		{
			ControlsHelper.OpenUrl("https://platform.openai.com/docs/api-reference/models");

		}

		public async Task Refresh()
		{
			if (!Global.ValidateServiceAndModel(Data))
				return;
			SaveSelection();
			var client = new Client(Data.AiService);
			var models = await client.GetModels();
			CollectionsHelper.Synchronize(models, CurrentItems);
			MustRefresh = false;
			ControlsHelper.SetSelection(MainDataGrid, nameof(model.id), Data.FineTuningModelListSelection, 0);
		}

		public List<model> GetWithAllow(AllowAction action)
		{
			if (!Global.ValidateServiceAndModel(Data))
				return null;
			var items = MainDataGrid.SelectedItems.Cast<model>().ToList();
			if (items.Count == 0)
				return null;
			if (!AppHelper.AllowAction(action, items.Select(x => x.id).ToArray()))
				return null;
			return items;
		}

		private void DeleteButton_Click(object sender, RoutedEventArgs e)
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
					var response = await client.DeleteModelAsync(item.id);
					if (response?.deleted == true)
						CurrentItems.Remove(item);
				}
			});
		}

		private void CreateTask_Click(object sender, RoutedEventArgs e)
		{
			var item = MainDataGrid.SelectedItems.Cast<model>().FirstOrDefault();
			if (item == null)
				return;
			var task = AppHelper.GetNewTemplateItem(false);
			// Treat the new task as a chat; therefore, clear the input box after sending.
			task.MessageBoxOperation = MessageBoxOperation.ClearMessage;
			task.AttachContext = ContextType.ChatHistory;
			task.Name = $"{Data.Name} Assistant ({DateTime.Now:yyyyMMdd_HHmmss})";
			task.IconData = Data.IconData;
			task.AiServiceId = Data.AiServiceId;
			task.AiModel = item.id;
			task.IsSystemInstructions = true;
			task.TextInstructions = Data.SystemMessage;
			Global.InsertItem(task, ItemType.Task);
			// Select new task in the tasks list on the [Tasks] tab.
			Global.AppSettings.GetTaskSettings(ItemType.Task).ListSelection = new List<string> { task.Name };
			Global.RaiseOnTasksUpdated();
		}
	}
}
