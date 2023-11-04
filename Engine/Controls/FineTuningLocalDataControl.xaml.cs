using DocumentFormat.OpenXml.Bibliography;
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
	public partial class FineTuningLocalDataControl : UserControl, IBindData<FineTuningItem>
	{
		public FineTuningLocalDataControl()
		{
			InitializeComponent();
			//ScanProgressPanel.Visibility = Visibility.Collapsed;
			if (ControlsHelper.IsDesignMode(this))
				return;
			MainDataGrid.ItemsSource = CurrentItems;
			Global.OnSourceDataFilesUpdated += Global_OnSourceDataFilesUpdated;
			Global.OnTuningDataFilesUpdated += Global_OnTuningDataFilesUpdated;
			UpdateButtons();
		}

		private void Global_OnTuningDataFilesUpdated(object sender, EventArgs e)
		{
			if (Data == null)
				return;
			if (FolderType != FineTuningFolderType.TuningData)
				return;
			ControlsHelper.EnsureTabItemSelected(this);
			// Remove selection. Selection will be restored from bound item data.
			MainDataGrid.SelectedIndex = -1;
			Refresh();
		}

		private void Global_OnSourceDataFilesUpdated(object sender, EventArgs e)
		{
			if (Data == null)
				return;
			if (FolderType != FineTuningFolderType.SourceData)
				return;
			ControlsHelper.EnsureTabItemSelected(this);
			// Remove selection. Selection will be restored from bound item data.
			MainDataGrid.SelectedIndex = -1;
			Refresh();
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
			ValidateButton.IsEnabled = isSelected;
			OpenButton.IsEnabled = isSelected;
			UploadButton.IsEnabled = isSelected;
			var isOneItemSelected = selecetedItems.Count() == 1;
			ConvertTypeComboBox.IsEnabled = isOneItemSelected;
			var convertTypes = new List<ConvertTargetType>() { ConvertTargetType.None };
			// Allow only if one item is selected.
			if (isOneItemSelected)
			{
				var item = selecetedItems.FirstOrDefault();
				var ext = Path.GetExtension(item.filename).ToLower();
				if (FileConvertHelper.ConvertToTypesAvailable.ContainsKey(ext))
				{
					var addTypes = FileConvertHelper.ConvertToTypesAvailable[ext];
					convertTypes.AddRange(addTypes);
				}
			}
			ConvertTypeComboBox.ItemsSource = Attributes.GetDictionary(convertTypes.ToArray());
			ConvertTypeComboBox.IsReadOnly = isOneItemSelected && convertTypes.Count > 1;
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

		#region ■ Properties

		[Category("Main"), DefaultValue(FineTuningFolderType.None)]
		public FineTuningFolderType FolderType { get; set; }

		#endregion

		private void ValidateButton_Click(object sender, RoutedEventArgs e)
		{
			var items = MainDataGrid.SelectedItems.Cast<file>().ToArray();
			var sourcePath = Global.GetPath(Data, FolderType.ToString());
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
			if (items.Length == 0)
				return;
			var item = items.First();
			var fineTuneItemPath = Global.GetPath(Data);
			var targetFullName = FileConvertHelper.ConvertFile(fineTuneItemPath, FolderType.ToString(), item, convertType, Data.AiModel, Data.SystemMessage);
			if (string.IsNullOrEmpty(targetFullName))
				return;
			var targetName = Path.GetFileName(targetFullName);
			var targetFolder = convertType == ConvertTargetType.JSONL
			? FineTuningFolderType.TuningData
			: FineTuningFolderType.SourceData;
			if (targetFolder == FineTuningFolderType.SourceData)
			{
				Data.FineTuningSourceDataSelection = new List<string>() { targetName };
				Global.RaiseOnSourceDataFilesUpdated();
			}
			else if (targetFolder == FineTuningFolderType.TuningData)
			{
				Data.FineTuningTuningDataSelection = new List<string>() { targetName };
				Global.RaiseOnTuningDataFilesUpdated();
			}
		}

		public void Refresh()
		{
			if (Data == null)
				return;
			SaveSelection();
			var path = Global.GetPath(Data, FolderType.ToString());
			var di = new DirectoryInfo(path);
			if (!di.Exists)
				di.Create();
			var filePattern = "*.*";
			if (FolderType == FineTuningFolderType.TuningData)
				filePattern = "*.jsonl";
			var dirFiles = di.GetFiles(filePattern);
			var files = dirFiles.Select(x => new file()
			{
				id = x.Name,
				created_at = x.LastWriteTime,
				bytes = x.Length,
				filename = x.Name,
				purpose = Path.GetExtension(x.Name).ToLower() == ".jsonl"
					? "fine-tuning"
					: null,
			}).ToList();
			CollectionsHelper.Synchronize(files, CurrentItems);
			// Refresh items because DataGrid items don't implement the INotifyPropertyChanged interface.
			MainDataGrid.Items.Refresh();
			MustRefresh = false;
			ControlsHelper.SetSelection(MainDataGrid, nameof(file.id), Data.FineTuningSourceDataSelection, 0);
		}

		public List<file> GetWithAllow(AllowAction action, bool checkData)
		{
			var items = MainDataGrid.SelectedItems.Cast<file>().ToList();
			if (items.Count == 0)
				return null;
			if (checkData && !Global.ValidateServiceAndModel(Data.AiService, Data.AiModel))
				return null;
			if (!AppHelper.AllowAction(action, items.Select(x => x.id).ToArray()))
				return null;
			return items;
		}

		private void UploadButton_Click(object sender, System.Windows.RoutedEventArgs e)
			=> Upload();

		private void DeleteButton_Click(object sender, RoutedEventArgs e)
			=> Delete();


		#region Actions

		private void Delete()
		{
			var items = GetWithAllow(AllowAction.Delete, false);
			if (items == null)
				return;
			foreach (var item in items)
			{
				var path = Global.GetPath(Data, FolderType.ToString(), item.filename);
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

		private void Open()
		{
			var items = MainDataGrid.SelectedItems.Cast<file>().ToList();
			foreach (var item in items)
			{
				var path = Global.GetPath(Data, FolderType.ToString(), item.filename);
				var fi = new FileInfo(path);
				if (fi.Exists)
				{
					try
					{
						ControlsHelper.OpenUrl(fi.FullName);
					}
					catch { }
				}
			}
		}

		private void Upload()
		{
			var items = GetWithAllow(AllowAction.Upload, true);
			if (items == null)
				return;
			// Use begin invoke or grid update will deadlock on same thread.
			_ = ControlsHelper.BeginInvoke(async () =>
			{
				var selection = new List<string>();
				foreach (var item in items)
				{
					var sourcePath = Global.GetPath(Data, FolderType.ToString(), item.filename);
					var client = new Client(Data.AiService);
					var ext = Path.GetExtension(item.filename).ToLower();
					var purpose = ext == ".jsonl"
						? Client.FineTuningPurpose
						: "";
					var file = await client.UploadFileAsync(sourcePath, purpose);
					if (!string.IsNullOrEmpty(client.LastError))
						MessageBox.Show(client.LastError, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
					if (file != null)
						selection.Add(file.id);
				}
				Data.FineTuningRemoteDataSelection = selection;
				Global.RaiseOnFilesUploaded();
			});
		}

		#endregion

		private void OpenButton_Click(object sender, RoutedEventArgs e)
			=> Open();


		private void MainDataGrid_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
			=> Open();

		private void MainDataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			var isEditMode = AppHelper.IsGridInEditMode((DataGrid)sender);
			if (!isEditMode && e.Key == Key.Delete)
				Delete();
		}
	}

}

