using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.Collections;
using JocysCom.ClassLibrary.ComponentModel;
using JocysCom.ClassLibrary.Controls;
using JocysCom.ClassLibrary.Runtime;
using JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT;
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
	public partial class FineTuningTuningDataControl : UserControl, IBindData<FineTune>
	{
		public FineTuningTuningDataControl()
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
			ValidateButton.IsEnabled = isSelected;
			var item = selecetedItems.FirstOrDefault();
			List<ConvertTargetType> convertTypes = new List<ConvertTargetType>() { ConvertTargetType.None };
			// Allow only if one item is selected.
			if (item != null && selecetedItems.Count() == 1)
			{
				var ext = Path.GetExtension(item.filename).ToLower();
				if (FileConvertHelper.ConvertToTypesAvailable.ContainsKey(ext))
				{
					var addTypes = FileConvertHelper.ConvertToTypesAvailable[ext];
					convertTypes.AddRange(addTypes);
				}
			}
			ConvertTypeComboBox.ItemsSource = Attributes.GetDictionary(convertTypes.ToArray());
			ConvertTypeComboBox.IsReadOnly = selecetedItems.Count() != 1;
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
			if (!AppHelper.AllowAction(AllowAction.Delete, items.Select(x => x.id).ToArray()))
				return;
			foreach (var item in items)
			{
				var path = Global.GetPath(Data, FineTune.TuningData, item.filename);
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

		public void SaveSelection()
		{
			// Save selection.
			var selection = ControlsHelper.GetSelection<string>(MainDataGrid, nameof(file.filename));
			if (selection.Count > 0 || Data.FineTuningTuningDataSelection == null)
				Data.FineTuningTuningDataSelection = selection;
		}

		public void Refresh()
		{
			SaveSelection();
			var path = Global.GetPath(Data, FineTune.TuningData);
			var di = new DirectoryInfo(path);
			if (!di.Exists)
				di.Create();
			var dirFiles = di.GetFiles("*.jsonl");
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
			ControlsHelper.SetSelection(MainDataGrid, nameof(file.filename), Data.FineTuningTuningDataSelection, 0);
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

		public FineTune Data
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
		public FineTune _Data;

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
			var sourcePath = Global.GetPath(Data, FineTune.TuningData);
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
			var sourcePath = Global.GetPath(Data, FineTune.TuningData);
			FileConvertHelper.ConvertFile(sourcePath, FineTune.TuningData, items, convertType, Data.AiModel);
			Refresh();

		}

		private async void UploadButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			var items = MainDataGrid.SelectedItems.Cast<file>();
			if (!AppHelper.AllowAction(AllowAction.Upload, items.Select(x => x.filename).ToArray()))
				return;
			foreach (var item in items)
			{
				var sourcePath = Global.GetPath(Data, FineTune.TuningData, item.filename);
				var client = new Client(Data.AiService);
				await client.UploadFileAsync(sourcePath, "fine-tune");
			}
		}

	}

}

