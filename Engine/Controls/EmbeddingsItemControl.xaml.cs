using DocumentFormat.OpenXml.Wordprocessing;
using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.Collections;
using JocysCom.ClassLibrary.Configuration;
using JocysCom.ClassLibrary.Controls;
using JocysCom.ClassLibrary.Data;
using JocysCom.ClassLibrary.IO;
using JocysCom.ClassLibrary.Runtime;
using JocysCom.VS.AiCompanion.DataClient;
using JocysCom.VS.AiCompanion.DataClient.Common;
using LiteDB;
using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
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
			ImportButton.Visibility = InitHelper.IsDebug
				? Visibility.Visible
				: Visibility.Collapsed;
			ExportButton.Visibility = InitHelper.IsDebug
				? Visibility.Visible
				: Visibility.Collapsed;
			ScanProgressPanel.UpdateProgress();
			Func<bool> action = () =>
			{
				SearchButton_Click(MessageTextBox, new RoutedEventArgs());
				return true;
			};
			AppControlsHelper.UseEnterToSend(MessageTextBox, action);
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

		bool HelpInit;

		private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (MainTabControl.SelectedItem == HelpTabPage && !HelpInit)
			{
				HelpInit = true;
				var bytes = AppHelper.ExtractFile("Documents.zip", "Feature - Embeddings.rtf");
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
				GroupFlagNameEditMode(false);
				GroupNameEditMode(false);
				TargetEditMode(false);
				MaskConnectionString();
				_ = Helper.Delay(EmbeddingGroupFlags_OnPropertyChanged);
				_ = Helper.Delay(EmbeddingGroupNames_OnPropertyChanged);
				// Make sure that embedding selection could be initially made.
				if (!EmbeddingGroupNames.Contains(value?.EmbeddingGroupName))
					EmbeddingGroupNames.Add(value?.EmbeddingGroupName);
				AiModelBoxPanel.Item = value;
				if (value != null)
				{
					value.PropertyChanged += _Item_PropertyChanged;
				}
				IconPanel.BindData(value);
				LogPanel.Clear();
				UpdateGroupName();
				OnPropertyChanged(nameof(Item));
			}
		}
		EmbeddingsItem _Item;


		private void _Item_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(EmbeddingsItem.EmbeddingGroupName))
				_ = Helper.Delay(EmbeddingGroupFlags_OnPropertyChanged);
			if (e.PropertyName == nameof(EmbeddingsItem.Target))
				MaskConnectionString();
			if (e.PropertyName == nameof(EmbeddingsItem.Source))
				UpdateGroupName();
			if (e.PropertyName == nameof(EmbeddingsItem.OverrideGroupName))
				UpdateGroupName();
		}

		void UpdateGroupName()
		{
			//if (!Item.OverrideGroupName)
			//{
			//	var source = AssemblyInfo.ExpandPath(Item.Source);
			//	(var groupName, var flag) = EmbeddingHelper.GetGroupAndFlagNames(source, source);
			//	Item.EmbeddingGroupName = groupName;
			//}
		}

		System.Windows.Forms.FolderBrowserDialog _FolderBrowser = new System.Windows.Forms.FolderBrowserDialog();

		private void BrowseSourceButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			var source = AssemblyInfo.ExpandPath(Item.Source);
			var dialog = _FolderBrowser;
			dialog.SelectedPath = source;
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


		System.Windows.Forms.OpenFileDialog _OpenFileDialog;

		private void BrowseTargetButton_Click(object sender, RoutedEventArgs e)
		{
			var path = AssemblyInfo.ExpandPath(Item.Target);
			if (_OpenFileDialog == null)
			{
				_OpenFileDialog = new System.Windows.Forms.OpenFileDialog();
				_OpenFileDialog.SupportMultiDottedExtensions = true;
				DialogHelper.AddFilter(_OpenFileDialog, SqlInitHelper.SqliteExt);
				DialogHelper.AddFilter(_OpenFileDialog);
				_OpenFileDialog.FilterIndex = 1;
				_OpenFileDialog.RestoreDirectory = true;
			}
			var dialog = _OpenFileDialog;
			//if (EmbeddingHelper.IsFilePath(path))
			//DialogHelper.FixDialogFile(dialog, _OpenFileDialog.FileName);
			if (SqlInitHelper.IsPortable(path))
			{
				dialog.FileName = Path.GetFileName(path);
				dialog.InitialDirectory = Path.GetDirectoryName(path);
			}
			dialog.Title = "Open " + JocysCom.ClassLibrary.Files.Mime.GetFileDescription(SqlInitHelper.SqliteExt);
			var result = dialog.ShowDialog();
			if (result != System.Windows.Forms.DialogResult.OK)
				return;
			Item.Target = AssemblyInfo.ParameterizePath(dialog.FileName, true);
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

		private async void SearchButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			if (ControlsHelper.IsOnCooldown(sender))
				return;
			MainTabControl.SelectedItem = LogTabPage;
			LogPanel.Clear();
			var eh = new EmbeddingHelper();
			var systemMessage = await eh.SearchEmbeddingsToSystemMessage(Item,
				Item.EmbeddingGroupName, Item.EmbeddingGroupFlag,
				Item.Message, Item.Skip, Item.Take);
			if (eh.FileParts == null)
			{
				LogPanel.Add("\r\nSearch returned no results.");
				return;
			}
			LogPanel.Add(eh.Log);
			LogPanel.Add("\r\n\r\n" + systemMessage);
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


		#region Scanner

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
			var connectionString = SqlInitHelper.IsPortable(target)
				? SqlInitHelper.PathToConnectionString(target)
				: target;
			Ignores.Clear();
			_Scanner = new FileProcessor();
			_Scanner.ProcessItem = _Scanner_ProcessItem;
			_Scanner.FileFinder.IsIgnored = _Scanner_FileFinder_IsIgnored;
			_Scanner.Progress += _Scanner_Progress;
			ExcludePatterns = GetIgnoreFromText(item.ExcludePatterns);
			IncludePatterns = GetIgnoreFromText(item.IncludePatterns);
			ControlsHelper.AppInvoke(new Action(() =>
			{
				MainTabControl.SelectedItem = LogTabPage;
				try
				{
					paths = new[] { source };
					SqlInitHelper.InitSqlDatabase(connectionString);
				}
				catch (System.Exception ex)
				{
					LogPanel.Clear();
					LogPanel.Add(ex.ToString());
				}
			}));
			ControlsHelper.AppInvoke(new Action(() =>
			{
				ScanProgressPanel.UpdateProgress("...", "");
				ScanStartButton.IsEnabled = false;
				ScanStopButton.IsEnabled = true;
			}));
			// Mark all files as starting to process.
			var tempState = ProgressStatus.Started;
			db = SqlInitHelper.NewEmbeddingsContext(connectionString);
			await SqlInitHelper.SetFileState(
				db, Item.EmbeddingGroupName, Item.EmbeddingGroupFlag, (int)tempState);
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
				await SqlInitHelper.DeleteByState(
					db,
					Item.EmbeddingGroupName, Item.EmbeddingGroupFlag, (int)tempState);

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
				// If can't read file then....
				var fh = new Plugins.Core.FileHelper();
				var result = fh.ReadFileAsPlainText(filePath);
				var content = result?.Data;
				if (string.IsNullOrWhiteSpace(content))
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
			var text = System.IO.File.ReadAllText(path);
			return GetIgnoreFromText(text);
		}

		private async Task<ProgressStatus> _Scanner_ProcessItem(FileProcessor fp, ClassLibrary.ProgressEventArgs e)
		{
			//await Task.Delay(50);
			try
			{
				var fi = (FileInfo)e.SubData;
				var processingState = await EmbeddingHelper.UpdateEmbedding(
					Item,
					db, fi.FullName, algorithm,
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
			ControlsHelper.AppInvoke(new Action(() =>
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


		#region Export/ Import

		System.Windows.Forms.OpenFileDialog ImportOpenFileDialog { get; } = new System.Windows.Forms.OpenFileDialog();

		private void ImportButton_Click(object sender, RoutedEventArgs e)
		{
		}

		System.Windows.Forms.SaveFileDialog ExportSaveFileDialog { get; } = new System.Windows.Forms.SaveFileDialog();

		private void ExportButton_Click(object sender, RoutedEventArgs e)
		{
			var target = AssemblyInfo.ExpandPath(Item.Target);
			var connectionString = SqlInitHelper.IsPortable(target)
				? SqlInitHelper.PathToConnectionString(target)
				: target;
			var db = SqlInitHelper.NewEmbeddingsContext(connectionString);
			var items = db.FileParts.Where(x =>
				x.GroupName == Item.EmbeddingGroupName &&
				x.GroupFlag == (long)Item.EmbeddingGroupFlag)
				.ToList();
			AppControlsHelper.Export(items);
		}


		#endregion


		#region Edit Group Name

		void GroupNameEditMode(bool editMode)
		{
			if (editMode)
				GroupNameTextBox.Text = Item.EmbeddingGroupName;
			Panel.SetZIndex(GroupNameComboBox, editMode ? 0 : 1);
			Panel.SetZIndex(GroupNameTextBox, editMode ? 1 : 0);
			GroupNameEditButton.Visibility = !editMode
				? Visibility.Visible
				: Visibility.Collapsed;
			GroupNameDeleteButton.Visibility = !editMode
				? Visibility.Visible
				: Visibility.Collapsed;
			GroupNameApplyButton.Visibility = editMode
				? Visibility.Visible
				: Visibility.Collapsed;
			GroupNameCancelButton.Visibility = editMode
				? Visibility.Visible
				: Visibility.Collapsed;
			GroupNameTextBox.Visibility = editMode
				? Visibility.Visible
				: Visibility.Collapsed;
			GroupNameComboBox.Visibility = !editMode
				? Visibility.Visible
				: Visibility.Collapsed;
			GroupNameTextBox.Focus();
			GroupNameTextBox.SelectAll();
		}

		private void GroupNameEditButton_Click(object sender, RoutedEventArgs e)
		{
			GroupNameEditMode(true);
		}

		private async void GroupNameDeleteButton_Click(object sender, RoutedEventArgs e)
		{
			var groupName = GroupNameComboBox.SelectedValue as string;
			var items = new string[] { $"{groupName} embedding files" };
			if (!AppHelper.AllowAction(AllowAction.Delete, items))
				return;
			await ExecuteMethod(async (CancellationToken cancellationToken) =>
			{
				await Task.Delay(0);
				LogPanel.Add($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} Deleting From database... ");
				var isPortable = SqlInitHelper.IsPortable(Item.Target);
				var target = AssemblyInfo.ExpandPath(Item.Target);
				var connectionString = isPortable
					? SqlInitHelper.PathToConnectionString(target)
					: target;
				ControlsHelper.AppInvoke(() =>
				{
					MainTabControl.SelectedItem = LogTabPage;
				});
				var rowsAffected = 0;
				try
				{
					db = SqlInitHelper.NewEmbeddingsContext(connectionString);
					rowsAffected = await SqlInitHelper.DeleteByState(db, groupName);
				}
				catch (Exception ex)
				{
					LogPanel.Clear();
					LogPanel.Add(ex.ToString());
					LogPanel.Add("\r\n");
				}
				LogPanel.Add($"{rowsAffected} row(s) affected\r\n");
				UpdateGroupNamesFromDatabase();
				EmbeddingHelper.UpdateGroupFlagsFromDatabase(Item?.Name, _EmbeddingGroupFlags);
			});
		}

		private void GroupNameApplyButton_Click(object sender, RoutedEventArgs e)
		{
			GroupNameApplyChanges();
		}

		private void GroupNameCancelButton_Click(object sender, RoutedEventArgs e)
		{
			GroupNameEditMode(false);
		}

		private void GroupNameTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
		{
			if (e.Key == System.Windows.Input.Key.Enter)
			{
				e.Handled = true;
				GroupNameApplyChanges();
			}
			if (e.Key == System.Windows.Input.Key.Escape)
			{
				GroupNameEditMode(false);
			}
		}

		void GroupNameApplyChanges()
		{
			LogPanel.Clear();
			((UIElement)OverrideGroupNameGroupBox.Content).IsEnabled = false;
			try
			{
				var oldName = Item.EmbeddingGroupName;
				var newName = GroupNameTextBox.Text;
				var target = AssemblyInfo.ExpandPath(Item.Target);
				var connectionString = SqlInitHelper.IsPortable(target)
					? SqlInitHelper.PathToConnectionString(target)
					: target;
				db = SqlInitHelper.NewEmbeddingsContext(connectionString);
				// Update group name of groups.
				var groups = db.Groups.Where(x => x.Name == oldName).ToArray();
				foreach (var group in groups)
					group.Name = newName;
				db.SaveChanges();
				// Update group name of files.
				var files = db.Files.Where(x => x.GroupName == oldName).ToArray();
				foreach (var file in files)
					file.GroupName = newName;
				db.SaveChanges();
				// Update group name of file parts.
				var fileParts = db.FileParts.Where(x => x.GroupName == oldName).ToArray();
				foreach (var filePart in fileParts)
					filePart.GroupName = newName;
				db.SaveChanges();
				// Make sure new value is in the list and select new value.
				EmbeddingGroupNames.Add(newName);
				Item.EmbeddingGroupName = newName;
				EmbeddingGroupNames.Remove(oldName);
			}
			catch (Exception ex)
			{
				LogPanel.Clear();
				LogPanel.Add(ex.ToString());
			}
			GroupNameEditMode(false);
			((UIElement)OverrideGroupNameGroupBox.Content).IsEnabled = true;
		}

		public void EmbeddingGroupNames_OnPropertyChanged()
		{
			if (_IsLoaded)
				OnPropertyChanged(nameof(EmbeddingGroupNames));
		}

		public ObservableCollection<string> EmbeddingGroupNames
		{
			get
			{
				if (_EmbeddingGroupNames == null)
				{
					var oc = new ObservableCollection<string>();
					if (!string.IsNullOrEmpty(Item?.EmbeddingGroupName))
						oc.Add(Item.EmbeddingGroupName);
					_EmbeddingGroupNames = oc;
				}
				if (Item?.IsEnabled == true)
					UpdateGroupNamesFromDatabase();
				return _EmbeddingGroupNames;
			}
			set => _EmbeddingGroupNames = value;
		}
		ObservableCollection<string> _EmbeddingGroupNames;


		#endregion

		#region Edit Group Flag

		void GroupFlagNameEditMode(bool editMode)
		{
			Panel.SetZIndex(GroupFlagComboBox, editMode ? 0 : 1);
			Panel.SetZIndex(GroupFlagNameTextBox, editMode ? 1 : 0);
			GroupFlagEditButton.Visibility = !editMode
				? Visibility.Visible
				: Visibility.Collapsed;
			GroupFlagDeleteButton.Visibility = !editMode
				? Visibility.Visible
				: Visibility.Collapsed;
			GroupFlagApplyButton.Visibility = editMode
				? Visibility.Visible
				: Visibility.Collapsed;
			GroupFlagCancelButton.Visibility = editMode
				? Visibility.Visible
				: Visibility.Collapsed;
			GroupFlagNameTextBox.Visibility = editMode
				? Visibility.Visible
				: Visibility.Collapsed;
			GroupFlagComboBox.Visibility = !editMode
				? Visibility.Visible
				: Visibility.Collapsed;
			GroupFlagNameTextBox.Focus();
			GroupFlagNameTextBox.SelectAll();
		}

		private void GroupFlagEditButton_Click(object sender, RoutedEventArgs e)
		{
			var parts = EmbeddingGroupFlags.First(x => x.Key == Item.EmbeddingGroupFlag)?.Value.Split(':');
			if (parts?.Count() > 1)
				GroupFlagNameTextBox.Text = parts[1].Trim();
			else
				GroupFlagNameTextBox.Clear();
			GroupFlagNameEditMode(true);
		}

		private async void GroupFlagDeleteButton_Click(object sender, RoutedEventArgs e)
		{
			var groupName = GroupNameComboBox.SelectedValue as string;
			var groupFlag = GroupFlagComboBox.SelectedValue as EmbeddingGroupFlag?;
			var groupFlagDescription = Attributes.GetDescription(groupFlag);
			if (groupFlag == null)
				return;
			var items = new string[] { $"{groupName}\\{groupFlagDescription} embedding files" };
			if (!AppHelper.AllowAction(AllowAction.Delete, items))
				return;
			await ExecuteMethod(async (CancellationToken cancellationToken) =>
			{
				await Task.Delay(0);
				LogPanel.Add($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} Deleting From database... ");
				var isPortable = SqlInitHelper.IsPortable(Item.Target);
				var target = AssemblyInfo.ExpandPath(Item.Target);
				var connectionString = isPortable
					? SqlInitHelper.PathToConnectionString(target)
					: target;
				ControlsHelper.AppInvoke(() =>
				{
					MainTabControl.SelectedItem = LogTabPage;
				});
				var rowsAffected = 0;
				try
				{
					db = SqlInitHelper.NewEmbeddingsContext(connectionString);
					rowsAffected = await SqlInitHelper.DeleteByState(db, groupName, groupFlag);
				}
				catch (Exception ex)
				{
					LogPanel.Clear();
					LogPanel.Add(ex.ToString());
					LogPanel.Add("\r\n");
				}
				LogPanel.Add($"{rowsAffected} row(s) affected\r\n");
				UpdateGroupNamesFromDatabase();
				EmbeddingHelper.UpdateGroupFlagsFromDatabase(Item?.Name, _EmbeddingGroupFlags);
			});
		}

		private void GroupFlagApplyButton_Click(object sender, RoutedEventArgs e)
		{

			GroupFlagApplyChanges();
		}

		private void GroupFlagCancelButton_Click(object sender, RoutedEventArgs e)
		{
			GroupFlagNameEditMode(false);
		}

		private void GroupFlagNameTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
		{
			if (e.Key == System.Windows.Input.Key.Enter)
			{
				e.Handled = true;
				GroupFlagApplyChanges();
			}
			if (e.Key == System.Windows.Input.Key.Escape)
			{
				GroupFlagNameEditMode(false);
			}
		}

		void UpdateGroupNamesFromDatabase()
		{
			EmbeddingHelper.UpdateGroupNamesFromDatabase(
				Item?.Name, _EmbeddingGroupNames,
				// Add required names to list.
				"", Item?.EmbeddingGroupName ?? "");
		}

		void GroupFlagApplyChanges()
		{
			LogPanel.Clear();
			try
			{
				var target = AssemblyInfo.ExpandPath(Item.Target);
				var connectionString = SqlInitHelper.IsPortable(target)
					? SqlInitHelper.PathToConnectionString(target)
					: target;
				db = SqlInitHelper.NewEmbeddingsContext(connectionString);
				var flag = (long)Item.EmbeddingGroupFlag;
				var item = db.Groups
					.Where(x => x.Name == Item.EmbeddingGroupName)
					.FirstOrDefault(x => x.Flag == flag);
				if (item == null)
				{
					item = new Embeddings.Embedding.Group();
					item.Timestamp = DateTime.UtcNow.Ticks;
					item.Name = Item.EmbeddingGroupName;
					item.Flag = (long)Item.EmbeddingGroupFlag;
					db.Groups.Add(item);
				}
				var flagName = Item.EmbeddingGroupFlagName ?? "";
				if (item.FlagName != flagName)
					item.FlagName = flagName;
				db.SaveChanges();
			}
			catch (Exception ex)
			{
				LogPanel.Clear();
				LogPanel.Add(ex.ToString());
			}
			GroupFlagNameEditMode(false);
			_ = Helper.Delay(EmbeddingGroupFlags_OnPropertyChanged);
		}

		public void EmbeddingGroupFlags_OnPropertyChanged()
		{
			if (_IsLoaded)
				OnPropertyChanged(nameof(EmbeddingGroupFlags));
		}

		public ObservableCollection<KeyValue<EmbeddingGroupFlag, string>> EmbeddingGroupFlags
		{
			get
			{
				if (_EmbeddingGroupFlags == null)
				{
					var values = (EmbeddingGroupFlag[])Enum.GetValues(typeof(EmbeddingGroupFlag));
					var dic = new ObservableCollection<KeyValue<EmbeddingGroupFlag, string>>();
					foreach (var value in values)
						dic.Add(new KeyValue<EmbeddingGroupFlag, string>(value, Attributes.GetDescription(value)));
					_EmbeddingGroupFlags = dic;
				}
				if (Item?.IsEnabled == true)
					EmbeddingHelper.UpdateGroupFlagsFromDatabase(Item?.Name, _EmbeddingGroupFlags);
				return _EmbeddingGroupFlags;
			}
			set => _EmbeddingGroupFlags = value;
		}
		ObservableCollection<KeyValue<EmbeddingGroupFlag, string>> _EmbeddingGroupFlags;

		#endregion

		#region Target Connection

		void TargetEditMode(bool isEditMode)
		{
			TargetTextBox.Visibility = isEditMode
				? Visibility.Visible
				: Visibility.Collapsed;
			TargetSwitchToViewButton.Visibility = isEditMode
				? Visibility.Visible
				: Visibility.Collapsed;
			TargetMaskedTextBox.Visibility = !isEditMode
				? Visibility.Visible
				: Visibility.Collapsed;
			TargetSwitchToEditButton.Visibility = !isEditMode
				? Visibility.Visible
				: Visibility.Collapsed;
		}

		private void TargetSwitchToEditButton_Click(object sender, RoutedEventArgs e)
		{
			TargetEditMode(true);
		}

		private void TargetSwitchToView_Click(object sender, RoutedEventArgs e)
		{
			TargetEditMode(false);
		}

		private void MaskConnectionString()
		{
			var masked = SqlHelper.FilterConnectionString(Item?.Target);
			ControlsHelper.SetText(TargetMaskedTextBox, masked);
		}

		#endregion

		private bool _IsLoaded;

		private void This_Loaded(object sender, RoutedEventArgs e)
		{
			if (ControlsHelper.IsDesignMode(this))
				return;
			PanelSettings = Global.AppSettings.GetTaskSettings(ItemType.Embeddings);
			if (ControlsHelper.AllowLoad(this))
			{
				AppHelper.InitHelp(this);
				UiPresetsManager.InitControl(this);
			}
			_IsLoaded = true;
		}

		private void This_Unloaded(object sender, RoutedEventArgs e)
		{
			_IsLoaded = false;
		}

		private async void TargetTestButton_Click(object sender, RoutedEventArgs e)
		{
			await ExecuteMethod(async (CancellationToken cancellationToken) =>
			{
				await Task.Delay(0);
				LogPanel.Add($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} Testing database... ");
				var isPortable = SqlInitHelper.IsPortable(Item.Target);
				var target = AssemblyInfo.ExpandPath(Item.Target);
				var connectionString = isPortable
							? SqlInitHelper.PathToConnectionString(target)
							: target;
				ControlsHelper.AppInvoke(() =>
				{
					MainTabControl.SelectedItem = LogTabPage;
				});
				var success = false;
				try
				{
					success = SqlInitHelper.InitSqlDatabase(connectionString);
				}
				catch (Exception ex)
				{
					LogPanel.Clear();
					LogPanel.Add(ex.ToString());
					LogPanel.Add("\r\n");
				}
				var statusText = success
					? "PASSED"
					: "FAILED";
				LogPanel.Add(statusText + "\r\n");
				if (success)
				{
					LogPanel.Add("\r\n");
					LogPanel.Add($"DATA INFO\r\n");
					var dataInfo = await SqlInitHelper.SelectDataInfo(isPortable, connectionString);
					var table = SqlHelper.ConvertToTable(dataInfo);
					var csv = JocysCom.ClassLibrary.Files.CsvHelper.Write(table, true);
					LogPanel.Add(csv);
				}
			});
		}

		/*
		// Run the time-consuming operations asynchronously
		Task.Run(() =>
			{
				var ei = Global.Embeddings.Items.FirstOrDefault(x => x.Name == embeddingName);
				if (ei == null)
					return;
				var flags = GetFlags(ei);
		// Update the UI thread
		Dispatcher.CurrentDispatcher.Invoke(() =>
				{
					var items = property.ToArray();
					foreach (var item in items)
					{
						var flagName = flags.FirstOrDefault(x => x.Flag == (long)item.Key)?.FlagName ?? string.Empty;
		var description = Attributes.GetDescription(item.Key);
						if (!string.IsNullOrEmpty(flagName))
						{
							description += ": " + flagName;
						}

	item.Value = description;
					}
				});
			});

		*/

		#region Execute Method

		/// <summary>
		/// Stores cancellation tokens created on this control that can be stopped with the [Stop] button.
		/// </summary>
		ObservableCollection<CancellationTokenSource> cancellationTokenSources = new ObservableCollection<CancellationTokenSource>();

		/// <summary>
		/// Helps run cancellable methods of this form and logs results to the log panel.
		/// </summary>
		async Task ExecuteMethod(Func<CancellationToken, Task> action)
		{
			// Run the time-consuming operations asynchronously
			await Task.Run(async () =>
			{
				LogPanel.Clear();
				var source = new CancellationTokenSource();
				source.CancelAfter(TimeSpan.FromSeconds(600));
				ControlsHelper.AppInvoke(() =>
				{
					cancellationTokenSources.Add(source);
					Global.MainControl.InfoPanel.AddTask(source);
				});
				try
				{
					await action.Invoke(source.Token);
				}
				catch (Exception ex)
				{
					LogPanel.Add(ex.ToString());
					ControlsHelper.AppInvoke(() =>
					{
						Global.MainControl.InfoPanel.SetBodyError(ex.Message);
					});
				}
				finally
				{
					ControlsHelper.AppInvoke(() =>
					{
						cancellationTokenSources.Remove(source);
						Global.MainControl.InfoPanel.RemoveTask(source);
					});
				}
			});
		}

		#endregion

		#region ■ INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		#endregion

	}
}
