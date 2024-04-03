using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.Configuration;
using JocysCom.ClassLibrary.Controls;
using JocysCom.ClassLibrary.IO;
using JocysCom.VS.AiCompanion.DataClient;
using JocysCom.VS.AiCompanion.Plugins.Core.VsFunctions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	/// <summary>
	/// Interaction logic for EmbeddingControl.xaml
	/// </summary>
	public partial class EmbeddingsItemControl : UserControl, INotifyPropertyChanged
	{
		public EmbeddingsItemControl()
		{
			InitializeComponent();
			if (ControlsHelper.IsDesignMode(this))
				return;
			ScanProgressPanel.UpdateProgress();
		}

		#region List Panel Item

		TaskSettings PanelSettings { get; set; } = new TaskSettings();

		[Category("Main"), DefaultValue(ItemType.None)]
		public ItemType DataType
		{
			get => _DataType;
			set
			{
				_DataType = value;
				if (ControlsHelper.IsDesignMode(this))
					return;
				// Update panel settings.
				PanelSettings.PropertyChanged -= PanelSettings_PropertyChanged;
				PanelSettings = Global.AppSettings.GetTaskSettings(value);
				PanelSettings.PropertyChanged += PanelSettings_PropertyChanged;
				// Update the rest.
				//PanelSettings.UpdateBarToggleButtonIcon(BarToggleButton);
				PanelSettings.UpdateListToggleButtonIcon(ListToggleButton);
				//OnPropertyChanged(nameof(BarPanelVisibility));
			}
		}
		private ItemType _DataType;

		private async void PanelSettings_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			await Task.Delay(0);
		}

		private void ListToggleButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			PanelSettings.UpdateListToggleButtonIcon(ListToggleButton, true);
		}

		#endregion

		public Dictionary<EmbeddingGroup, string> EmbeddingGroups
		=> ClassLibrary.Runtime.Attributes.GetDictionary(
			(EmbeddingGroup[])Enum.GetValues(typeof(EmbeddingGroup)));

		public Dictionary<EmbeddingGroup, string> EmbeddingGroupFlags
		{
			get
			{
				if (_EmbeddingGroupFlags == null)
				{
					var values = (EmbeddingGroup[])Enum.GetValues(typeof(EmbeddingGroup));
					_EmbeddingGroupFlags = ClassLibrary.Runtime.Attributes.GetDictionary(values);
				}
				return _EmbeddingGroupFlags;
			}
			set => _EmbeddingGroupFlags = value;
		}
		Dictionary<EmbeddingGroup, string> _EmbeddingGroupFlags;

		bool HelpInit;

		private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (MainTabControl.SelectedItem == HelpTabPage && !HelpInit)
			{
				HelpInit = true;
				var bytes = AppHelper.ExtractFile("Documents.zip", "Feature ‐ Embeddings.rtf");
				ControlsHelper.SetTextFromResource(HelpRichTextBox, bytes);
			}
		}

		public EmbeddingsItem Item
		{
			get => _Item;
			set
			{
				if (_Item != null)
				{
					_Item.PropertyChanged -= _Item_PropertyChanged;
				}
				AiModelBoxPanel.Item = null;
				_Item = value;
				AiModelBoxPanel.Item = value;
				if (value != null)
				{
					value.PropertyChanged += _Item_PropertyChanged;
				}
				IconPanel.BindData(value);
				OnPropertyChanged(nameof(Item));
			}
		}
		EmbeddingsItem _Item;

		private void _Item_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(EmbeddingsItem.Source))
			{
			}
		}

		System.Windows.Forms.FolderBrowserDialog _FolderBrowser = new System.Windows.Forms.FolderBrowserDialog();

		private void BrowseButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			var dialog = _FolderBrowser;
			dialog.SelectedPath = Item.Source;
			DialogHelper.FixDialogFolder(dialog, Global.FineTuningPath);
			var result = dialog.ShowDialog();
			if (result != System.Windows.Forms.DialogResult.OK)
				return;
			Item.Source = AssemblyInfo.ParameterizePath(dialog.SelectedPath, true);
			UpdateFromFolder(dialog.SelectedPath);
		}

		private void UpdateFromFolder(string path)
		{
			// If name is default.
			if (!Item.Name.StartsWith("Embedding 2"))
				return;
			var di = new DirectoryInfo(path);
			var newName = di.Parent == null
				? di.Name
				: $"{di.Parent.Name} - {di.Name}";
			Global.Embeddings.RenameItem(Item, newName);
		}

		private void OpenButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			var path = AssemblyInfo.ExpandPath(Item.Source);
			if (!Directory.Exists(path))
				Directory.CreateDirectory(path);
			ControlsHelper.OpenUrl(path);
		}

		private void EditButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			var connectionString = DataConnectionDialogHelper.ShowDialog(Item.Target);
			if (connectionString != null)
				Item.Target = connectionString;
		}

		#region Database Connection Strings

		///// <summary>
		///// Database Administrative Connection String.
		///// </summary>
		//public string FilteredConnectionString
		//{
		//	get
		//	{
		//		var value = Global.AppSettings.Embedding.Target;
		//		if (string.IsNullOrWhiteSpace(value))
		//			return "";
		//		var filtered = ClassLibrary.Data.SqlHelper.FilterConnectionString(value);
		//		return filtered;
		//	}
		//	set
		//	{
		//	}
		//}

		#endregion

		#region ■ INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		#endregion

		private async void SearchButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			if (ControlsHelper.IsOnCooldown(sender))
				return;
			MainTabControl.SelectedItem = LogTabPage;
			LogTextBox.Text = "";
			var eh = new EmbeddingHelper();
			var systemMessage = await eh.SearchEmbeddingsToSystemMessage(Item, Item.Message, Item.Skip, Item.Take);
			if (eh.FileParts == null)
			{
				LogTextBox.Text += "\r\nSearch returned no results.";
				return;
			}
			LogTextBox.Text += eh.Log;
			LogTextBox.Text += "\r\n\r\n" + systemMessage;
		}

		private void CreateButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			MainTabControl.SelectedItem = LogTabPage;
			var path = AssemblyInfo.ExpandPath(Item.Target);
			InitSqlDatabase(path);
		}

		public void InitSqlDatabase(string stringOrPath)
		{
			if (EmbeddingHelper.IsPortable(stringOrPath))
				SqliteHelper.InitSqlLiteDatabase(stringOrPath);

		}

		FileProcessor _Scanner;
		Embeddings.EmbeddingsContext db;
		System.Security.Cryptography.SHA256 algorithm = System.Security.Cryptography.SHA256.Create();

		private void ScanStartButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			if (ControlsHelper.IsOnCooldown(sender))
				return;
			//var form = new MessageBoxWindow();
			//var result = form.ShowDialog("Start Processing?", "Processing", MessageBoxButton.OKCancel, MessageBoxImage.Question);
			//if (result != MessageBoxResult.OK)
			//	return;
			ScanStartButton.IsEnabled = false;
			ScanStopButton.IsEnabled = true;
			Global.MainControl.InfoPanel.AddTask(TaskName.Scan);
			ScanStarted = DateTime.Now;
			var success = System.Threading.ThreadPool.QueueUserWorkItem(ScanTask, Item);
			if (!success)
			{
				ScanProgressPanel.UpdateProgress("Scan failed!", "", true);
				ScanStartButton.IsEnabled = true;
				ScanStopButton.IsEnabled = false;
				Global.MainControl.InfoPanel.RemoveTask(TaskName.Scan);
			}
		}


		#region Locations Scanner

		DateTime ScanStarted;
		object AddAndUpdateLock = new object();
		Ignore.Ignore ExcludePatterns;
		Ignore.Ignore IncludePatterns;

		async void ScanTask(object state)
		{

			var item = (EmbeddingsItem)state;
			var paths = Array.Empty<string>();
			var source = AssemblyInfo.ExpandPath(item.Source);
			var target = AssemblyInfo.ExpandPath(item.Target);
			if (_Scanner != null)
			{
				_Scanner.IsStopping = true;
				_Scanner.Progress -= _Scanner_Progress;
				_Scanner.ProcessItem = null;
				_Scanner.FileFinder.IsIgnored = null;
			}
			if (db != null)
			{
				db.Dispose();
			}
			db = EmbeddingHelper.NewEmbeddingsContext(target);
			Ignores.Clear();
			_Scanner = new FileProcessor();
			_Scanner.ProcessItem = _Scanner_ProcessItem;
			_Scanner.FileFinder.IsIgnored = _Scanner_FileFinder_IsIgnored;
			_Scanner.Progress += _Scanner_Progress;
			ExcludePatterns = GetIgnoreFromText(item.ExcludePatterns);
			IncludePatterns = GetIgnoreFromText(item.IncludePatterns);
			Dispatcher.Invoke(new Action(() =>
			{
				MainTabControl.SelectedItem = LogTabPage;
				try
				{
					paths = new[] { source };


					if (EmbeddingHelper.IsPortable(target))
					{
						var dbFi = new FileInfo(target);
						// If database file don't exists or not initialized then...
						if (!dbFi.Exists || dbFi.Length == 0)
							InitSqlDatabase(target);
					}
				}
				catch (System.Exception ex)
				{
					LogTextBox.Text = ex.ToString();
				}
			}));
			Dispatcher.Invoke(new Action(() =>
			{
				ScanProgressPanel.UpdateProgress("...", "");
				ScanStartButton.IsEnabled = false;
				ScanStopButton.IsEnabled = true;
			}));
			// Mark all files as starting to process.
			var tempState = ProgressStatus.Started;
			await EmbeddingHelper.SetFileState(
				db, Item.EmbeddingGroupName, Item.EmbeddingGroupFlag, tempState);
			await _Scanner.Scan(paths, Item.SourcePattern, allDirectories: true);
			// Cleanup.
			var noErrors =
				_Scanner.ProcessItemStates[ProgressStatus.Exception] == 0 &&
				_Scanner.ProcessItemStates[ProgressStatus.Failed] == 0 &&
				_Scanner.ProcessItemStates[ProgressStatus.Canceled] == 0;
			// If cancellation was not requested and no errors then...
			if (!_Scanner.Cancellation.Token.IsCancellationRequested && noErrors)
			{
				// Delete unprocessed files.
				await EmbeddingHelper.DeleteByState(
					db,
					Item.EmbeddingGroupName, Item.EmbeddingGroupFlag, tempState);

			}
		}

		ConcurrentDictionary<string, Ignore.Ignore> Ignores = new ConcurrentDictionary<string, Ignore.Ignore>();

		private bool _Scanner_FileFinder_IsIgnored(string parentPath, string filePath, long fileLength)
		{
			var relativePath = PathHelper.GetRelativePath(parentPath + "\\", filePath, false)
				.Replace("\\", "/");
			if (IncludePatterns?.IsIgnored(relativePath) == false)
				return true;
			if (ExcludePatterns?.IsIgnored(relativePath) == true)
				return true;
			var ignore = Ignores.GetOrAdd(parentPath, x => GetIgnoreFromFile(Path.Combine(parentPath, ".gitignore")));
			if (ignore?.IsIgnored(relativePath) == true)
				return true;
			if (fileLength > 0)
			{
				var isBinary = DocItem.IsBinary(filePath, 1024);
				if (isBinary)
					return true;
			}
			return false;
		}

		private Ignore.Ignore GetIgnoreFromText(string text)
		{
			var ignore = new Ignore.Ignore();
			var lines = text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
			var containsRules = false;
			foreach (var line in lines)
			{
				if (string.IsNullOrWhiteSpace(line))
					continue;
				if (line.TrimStart().StartsWith("#"))
					continue;
				ignore.Add(line);
				containsRules = true;
			}
			return containsRules ? ignore : null;
		}

		private Ignore.Ignore GetIgnoreFromFile(string path)
		{
			var fi = new FileInfo(path);
			if (!fi.Exists)
				return null;
			var text = File.ReadAllText(path);
			return GetIgnoreFromText(text);
		}

		private async Task<ProgressStatus> _Scanner_ProcessItem(FileProcessor fp, ClassLibrary.ProgressEventArgs e)
		{
			await Task.Delay(50);
			try
			{
				var fi = (FileInfo)e.SubData;
				var processingState = await EmbeddingHelper.UpdateEmbedding(
					db, fi.FullName, algorithm,
					Item.AiService, Item.AiModel,
					Item.EmbeddingGroupName, Item.EmbeddingGroupFlag,
					fp.Cancellation.Token
					);
				return processingState;
			}
			catch (Exception ex)
			{
				e.Exception = ex;
				// Stop if too many exceptions.
				if (fp.ProcessItemStates[ClassLibrary.ProgressStatus.Exception] > 5)
					fp.IsStopping = true;
				return ClassLibrary.ProgressStatus.Exception;
			}
		}

		private void _Scanner_Progress(object sender, ClassLibrary.ProgressEventArgs e)
		{
			Dispatcher.Invoke(new Action(() =>
			{
				ScanProgressPanel.UpdateProgress(e);
				if (e.State == ClassLibrary.ProgressStatus.Completed)
				{
					Global.MainControl.InfoPanel.RemoveTask(TaskName.Scan);
					ScanStartButton.IsEnabled = true;
					ScanStopButton.IsEnabled = false;
				}
			}));
		}

		#endregion

		private void ScanStopButton_Click(object sender, RoutedEventArgs e)
		{
			var p = _Scanner;
			if (p != null)
			{
				_Scanner.Cancellation.Cancel();
				p.IsStopping = true;
			}
		}


		System.Windows.Forms.OpenFileDialog _OpenFileDialog;

		private void BrowseTargetButton_Click(object sender, RoutedEventArgs e)
		{
			if (_OpenFileDialog == null)
			{
				_OpenFileDialog = new System.Windows.Forms.OpenFileDialog();
				_OpenFileDialog.SupportMultiDottedExtensions = true;
				DialogHelper.AddFilter(_OpenFileDialog, ".db");
				DialogHelper.AddFilter(_OpenFileDialog);
				_OpenFileDialog.FilterIndex = 1;
				_OpenFileDialog.RestoreDirectory = true;
			}
			var dialog = _OpenFileDialog;
			var path = AssemblyInfo.ExpandPath(Item.Target);
			//if (EmbeddingHelper.IsFilePath(path))
			//DialogHelper.FixDialogFile(dialog, _OpenFileDialog.FileName);
			if (EmbeddingHelper.IsPortable(path))
			{
				dialog.FileName = Path.GetFileName(path);
				dialog.InitialDirectory = Path.GetDirectoryName(path);
			}
			dialog.Title = "Open " + JocysCom.ClassLibrary.Files.Mime.GetFileDescription(".db");
			var result = dialog.ShowDialog();
			if (result != System.Windows.Forms.DialogResult.OK)
				return;
			Item.Target = AssemblyInfo.ParameterizePath(dialog.FileName, true);
		}

	}
}
