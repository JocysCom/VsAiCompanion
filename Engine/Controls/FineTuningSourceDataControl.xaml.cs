using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.Collections;
using JocysCom.ClassLibrary.ComponentModel;
using JocysCom.ClassLibrary.Controls;
using JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	/// <summary>
	/// Interaction logic for ProjectsListControl.xaml
	/// </summary>
	public partial class FineTuningSourceDataControl : UserControl, IBindData<FineTune>
	{
		public FineTuningSourceDataControl()
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
			ControlsHelper.RestoreSelection(MainDataGrid, nameof(TemplateItem.Name), list, 0);
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

		private void DeleteButton_Click(object sender, RoutedEventArgs e)
		{
			var items = MainDataGrid.SelectedItems.Cast<file>().ToList();
			if (items.Count == 0)
				return;
			if (!AppHelper.AllowAction(AllowAction.Delete, items.Select(x => x.id).ToArray()))
				return;
			foreach (var item in items)
			{
				var path = Global.GetPath(Data, FineTune.SourceData, item.filename);
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

		public void Refresh()
		{
			var path = Global.GetPath(Data, FineTune.SourceData);
			var di = new DirectoryInfo(path);
			if (!di.Exists)
				di.Create();
			var dirFiles = di.GetFiles("*.json*");
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

		#region Validate

		private void ValidateButton_Click(object sender, RoutedEventArgs e)
		{
			var items = MainDataGrid.SelectedItems.Cast<file>();
			foreach (var item in items)
			{
				var filePath = Global.GetPath(Data, FineTune.SourceData, item.filename);
				var ext = System.IO.Path.GetExtension(item.filename).ToLower();
				string status_details = null;
				if (ext == ".json")
				{
					_ = Client.IsTextCompletionMode(Data.AiModel)
						? ValidateJsonFile<List<text_completion_request>>(filePath, out status_details)
						: ValidateJsonFile<List<chat_completion_request>>(filePath, out status_details);
				}
				if (ext == ".jsonl")
				{
					_ = Client.IsTextCompletionMode(Data.AiModel)
						? ValidateJsonlFile<text_completion_request>(filePath, out status_details)
						: ValidateJsonlFile<chat_completion_request>(filePath, out status_details);
				}
				item.status_details = $"{DateTime.Now}: {status_details}";
			}
			// Refresh items because DataGrid items don't implement the INotifyPropertyChanged interface.
			MainDataGrid.Items.Refresh();
		}

		public bool ValidateJsonlFile<T>(string filePath, out string status_details)
		{
			if (!File.Exists(filePath))
			{
				status_details = $"File {filePath} don't exists!";
				return false;
			}
			var i = 0;
			foreach (string line in File.ReadLines(filePath))
			{
				i++;
				try
				{
					var request = Client.Deserialize<T>(line);
					// Validate further if necessary
				}
				catch (JsonException ex)
				{
					// Handle the exception for an invalid JSON line
					status_details = ex.Message;
					return false;
				}
			}
			// Add approximate token count.
			status_details = $"Validated successfuly. {i} line(s) found.";
			return true;
		}

		public bool ValidateJsonFile<T>(string filePath, out string status_details)
		{
			if (!File.Exists(filePath))
			{
				status_details = $"File {filePath} don't exists!";
				return false;
			}
			var content = File.ReadAllText(filePath);
			try
			{
				var request = Client.Deserialize<T>(content);
			}
			catch (JsonException ex)
			{
				// Handle the exception for an invalid JSON line
				status_details = ex.Message;
				return false;
			}
			// Add approximate token count.
			status_details = $"Validated successfuly.";
			return true;
		}

		#endregion

		#region Convert

		private void ConvertButton_Click(object sender, RoutedEventArgs e)
		{
			var items = MainDataGrid.SelectedItems.Cast<file>();
			foreach (var item in items)
			{
				var sourcePath = Global.GetPath(Data, FineTune.SourceData, item.filename);
				var fi = new FileInfo(sourcePath);
				var ext = System.IO.Path.GetExtension(item.filename).ToLower();
				string status_details = null;
				if (ext == ".json")
				{
					var targetPath = Global.GetPath(Data, FineTune.SourceData, Path.GetFileNameWithoutExtension(fi.Name) + ".jsonl");
					_ = Client.IsTextCompletionMode(Data.AiModel)
						? ConvertJsonListToLines<text_completion_request>(sourcePath, targetPath, out status_details)
						: ConvertJsonListToLines<chat_completion_request>(sourcePath, targetPath, out status_details);

				}
				if (ext == ".jsonl")
				{
					var targetPath = Global.GetPath(Data, FineTune.SourceData, Path.GetFileNameWithoutExtension(fi.Name) + ".json");
					_ = Client.IsTextCompletionMode(Data.AiModel)
						? ConvertJsonLinesToList<text_completion_request>(sourcePath, targetPath, out status_details)
						: ConvertJsonLinesToList<chat_completion_request>(sourcePath, targetPath, out status_details);
				}
				item.status_details = $"{DateTime.Now}: {status_details}";
			}
			Refresh();
		}

		public bool ConvertJsonLinesToList<T>(string sourceFile, string targetFile, out string status_details)
		{
			if (!File.Exists(sourceFile))
			{
				status_details = $"File {sourceFile} don't exists!";
				return false;
			}
			var i = 0;
			var items = new List<T>();
			foreach (string line in File.ReadLines(sourceFile))
			{
				i++;
				try
				{
					var request = Client.Deserialize<T>(line);
					items.Add(request);
					// Validate further if necessary
				}
				catch (JsonException ex)
				{
					// Handle the exception for an invalid JSON line
					status_details = ex.Message;
					return false;
				}
			}
			var options = Client.GetJsonOptions();
			options.WriteIndented = true;
			var contents = JsonSerializer.Serialize(items, options);
			if (!AllowToWrite(targetFile))
			{
				status_details = "Overwrite denied.";
				return false;
			}
			File.WriteAllText(targetFile, contents, System.Text.Encoding.UTF8);
			// Add approximate token count.
			status_details = $"File converted successfuly. {items.Count} message(s) found.";
			return true;
		}

		public bool AllowToWrite(string targetFile)
		{
			var tfi = new FileInfo(targetFile);
			if (!tfi.Exists)
				return true;
			var text = $"Do you want to overwrite {tfi.Name} file?";
			var caption = $"{Global.Info.Product} - Overwrite";
			var result = MessageBox.Show(text, caption, MessageBoxButton.YesNo, MessageBoxImage.Question);
			if (result != MessageBoxResult.Yes)
				return false;
			File.Delete(targetFile);
			return true;
		}

		public bool ConvertJsonListToLines<T>(string sourceFile, string targetFile, out string status_details)
		{
			if (!File.Exists(sourceFile))
			{
				status_details = $"File {sourceFile} does not exist.";
				return false;
			}
			try
			{
				var jsonData = File.ReadAllText(sourceFile, System.Text.Encoding.UTF8);
				var data = Client.Deserialize<List<T>>(jsonData);
				if (!AllowToWrite(targetFile))
				{
					status_details = "Overwrite denied.";
					return false;
				}
				using (var writer = File.CreateText(targetFile))
				{
					foreach (var item in data)
					{
						var jsonLine = Client.Serialize(item);
						writer.WriteLine(jsonLine);
					}
				}
				status_details = $"File converted successfully. {data.Count} message(s) found.";
				return true;
			}
			catch (JsonException ex)
			{
				// Handle the exception for an invalid JSON line
				status_details = ex.Message;
				return false;
			}
		}


		#endregion

		private async void UploadButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			var items = MainDataGrid.SelectedItems.Cast<file>();
			if (!AppHelper.AllowAction(AllowAction.Upload, items.Select(x => x.filename).ToArray()))
				return;
			foreach (var item in items)
			{
				var sourcePath = Global.GetPath(Data, FineTune.SourceData, item.filename);
				var client = new Client(Data.AiService);
				await client.UploadFileAsync(sourcePath, "fine-tune");
			}
		}

	}

}

