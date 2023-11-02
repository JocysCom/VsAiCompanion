using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.Collections;
using JocysCom.ClassLibrary.ComponentModel;
using JocysCom.ClassLibrary.Controls;
using JocysCom.ClassLibrary.Runtime;
using JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT;
using JocysCom.VS.AiCompanion.Engine.FileConverters;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	/// <summary>
	/// Interaction logic for ProjectsListControl.xaml
	/// </summary>
	public partial class FineTuningSourceDataControl : UserControl, IBindData<FineTuningItem>
	{
		public FineTuningSourceDataControl()
		{
			InitializeComponent();
			//ScanProgressPanel.Visibility = Visibility.Collapsed;
			if (ControlsHelper.IsDesignMode(this))
				return;
			MainDataGrid.ItemsSource = CurrentItems;
			ConvertTypeComboBox.ItemsSource = Attributes.GetDictionary<ConvertTargetType>();
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
			ValidateButton.IsEnabled = isSelected;
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
			if (Data == null)
				return;
			// Save selection.
			var selection = ControlsHelper.GetSelection<string>(MainDataGrid, nameof(file.id));
			if (selection.Count > 0 || Data.FineTuningSourceDataSelection == null)
				Data.FineTuningSourceDataSelection = selection;
		}

		private void RefreshButton_Click(object sender, RoutedEventArgs e)
		{
			Refresh();
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
			if (MustRefresh && IsVisible)
				Refresh();
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
					Refresh();
			}
		}
		public FineTuningItem _Data;

		public bool MustRefresh;

		private void _Data_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(_Data.AiService))
			{
				if (Global.IsGoodSettings(_Data.AiService))
					Refresh();
			}
		}


		#endregion

		private void HelpButton_Click(object sender, RoutedEventArgs e)
		{
			ControlsHelper.OpenUrl("https://platform.openai.com/docs/guides/fine-tuning/preparing-your-dataset");
		}

		private void ValidateButton_Click(object sender, RoutedEventArgs e)
		{
			var items = MainDataGrid.SelectedItems.Cast<file>().ToArray();
			var sourcePath = Global.GetPath(Data, FineTuningItem.SourceData);
			FileValidateHelper.Validate(sourcePath, items, Data.AiModel);
			// Refresh items because DataGrid items don't implement the INotifyPropertyChanged interface.
			MainDataGrid.Items.Refresh();
		}

		public ConvertTargetType ConvertType { get; set; } = ConvertTargetType.None;

		private void ConvertTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			var box = (ComboBox)sender;
			var convertType = e.AddedItems.Cast<KeyValuePair<ConvertTargetType, string>>().FirstOrDefault().Key;
			if (convertType == ConvertTargetType.None)
				return;
			box.SelectedValue = ConvertTargetType.None;
			// Convert.
			var items = MainDataGrid.SelectedItems.Cast<file>().ToArray();
			var fineTuneItemPath = Global.GetPath(Data);
			FileConvertHelper.ConvertFile(fineTuneItemPath, FineTuningItem.SourceData, items, convertType, Data.AiModel);
			Refresh();
		}

		public void Refresh()
		{
			if (Data == null)
				return;
			SaveSelection();
			var path = Global.GetPath(Data, FineTuningItem.SourceData);
			var di = new DirectoryInfo(path);
			if (!di.Exists)
				di.Create();
			var dirFiles = di.GetFiles("*.*");
			var files = dirFiles.Select(x => new file()
			{
				id = x.Name,
				created_at = x.LastWriteTime,
				bytes = x.Length,
				filename = x.Name,
				purpose = System.IO.Path.GetExtension(x.Name).ToLower() == ".jsonl" ? "fine-tuning" : null,
			}).ToList();
			CollectionsHelper.Synchronize(files, CurrentItems);
			// Refresh items because DataGrid items don't implement the INotifyPropertyChanged interface.
			MainDataGrid.Items.Refresh();
			MustRefresh = false;
			ControlsHelper.SetSelection(MainDataGrid, nameof(file.id), Data.FineTuningSourceDataSelection, 0);
		}

		private async void UploadButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			var items = MainDataGrid.SelectedItems.Cast<file>();
			if (!AppHelper.AllowAction(AllowAction.Upload, items.Select(x => x.filename).ToArray()))
				return;
			if (!Global.ValidateServiceAndModel(Data.AiService, Data.AiModel))
				return;
			foreach (var item in items)
			{
				var sourcePath = Global.GetPath(Data, FineTuningItem.SourceData, item.filename);
				var client = new Client(Data.AiService);
				await client.UploadFileAsync(sourcePath, "fine-tune");
			}
		}

		private void DeleteButton_Click(object sender, RoutedEventArgs e)
		{
			var items = MainDataGrid.SelectedItems.Cast<file>().ToList();
			if (items.Count == 0)
				return;
			if (!Global.ValidateServiceAndModel(Data.AiService, Data.AiModel))
				return;
			if (!AppHelper.AllowAction(AllowAction.Delete, items.Select(x => x.id).ToArray()))
				return;
			foreach (var item in items)
			{
				var path = Global.GetPath(Data, FineTuningItem.SourceData, item.filename);
				var fi = new FileInfo(path);
				if (fi.Exists)
				{
					try
					{
						fi.Delete();
					}
					catch { }
				}
			}
			Refresh();
		}

		private void OpenButton_Click(object sender, RoutedEventArgs e)
		{

        }
    }

}

