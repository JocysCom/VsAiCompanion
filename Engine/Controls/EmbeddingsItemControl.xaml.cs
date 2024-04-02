using JocysCom.ClassLibrary.Configuration;
using JocysCom.ClassLibrary.Controls;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Linq;
using System.Threading.Tasks;
using JocysCom.VS.AiCompanion.DataClient;
using System;
using System.IO;
using JocysCom.ClassLibrary.IO;
using LiteDB;
using System.Windows;
using JocysCom.ClassLibrary;






#if NETFRAMEWORK
using System.Data.SQLite;
using System.Data.SqlClient;
#else
using Microsoft.Data.Sqlite;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
#endif

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
#if NETFRAMEWORK
			Microsoft.Data.ConnectionUI.DataConnectionDialog dcd;
			dcd = new Microsoft.Data.ConnectionUI.DataConnectionDialog();
			//Adds all the standard supported databases
			//DataSource.AddStandardDataSources(dcd);
			//allows you to add datasources, if you want to specify which will be supported 
			dcd.DataSources.Add(Microsoft.Data.ConnectionUI.DataSource.SqlDataSource);
			dcd.SetSelectedDataProvider(Microsoft.Data.ConnectionUI.DataSource.SqlDataSource, Microsoft.Data.ConnectionUI.DataProvider.SqlDataProvider);
			try
			{
				dcd.ConnectionString = Item.Target ?? "";
			}
			catch (System.Exception)
			{
			}
			Microsoft.Data.ConnectionUI.DataConnectionDialog.Show(dcd);
			if (dcd.DialogResult == System.Windows.Forms.DialogResult.OK)
			{
				Item.Target = dcd.ConnectionString;
			}
			//OnPropertyChanged(nameof(FilteredConnectionString));
#else
			Microsoft.SqlServer.Management.ConnectionUI.DataConnectionDialog dcd;
			dcd = new Microsoft.SqlServer.Management.ConnectionUI.DataConnectionDialog();
			// Add all the standard supported databases.
			Microsoft.SqlServer.Management.ConnectionUI.DataSource.AddStandardDataSources(dcd);
			//Add custom data sources.
			dcd.DataSources.Add(Microsoft.SqlServer.Management.ConnectionUI.DataSource.SqlDataSource);
			dcd.SetSelectedDataProvider(Microsoft.SqlServer.Management.ConnectionUI.DataSource.SqlDataSource, Microsoft.SqlServer.Management.ConnectionUI.DataProvider.SqlDataProvider);
			try
			{
				dcd.ConnectionString = Item.Target ?? "";
			}
			catch (System.Exception)
			{
			}
			Microsoft.SqlServer.Management.ConnectionUI.DataConnectionDialog.Show(dcd);
			if (dcd.DialogResult == System.Windows.Forms.DialogResult.OK)
			{
				Item.Target = dcd.ConnectionString;
			}
#endif
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
			await eh.SearchEmbeddings(Item, Item.Message, Item.Skip, Item.Take);
			if (eh.FileParts == null)
			{
				LogTextBox.Text += "\r\nSearch returned no results.";
				return;
			}
			foreach (var filPart in eh?.FileParts)
			{
				LogTextBox.Text += eh.Log;
				var file = eh.Files.Where(x => x.Id == filPart.Id).FirstOrDefault();
				LogTextBox.Text += $"\r\n{file?.Url}";
				var text = JocysCom.ClassLibrary.Text.Helper.IdentText(filPart.Text);
				LogTextBox.Text += "\r\n" + text + "\r\n\r\n";
			}
		}

		private void CreateButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			MainTabControl.SelectedItem = LogTabPage;
			var path = AssemblyInfo.ExpandPath(Item.Target);
			InitSqlDatabase(path);
		}

		public void InitSqlDatabase(string stringOrPath)
		{
			if (EmbeddingHelper.IsFilePath(stringOrPath))
				SqliteHelper.InitSqlLiteDatabase(stringOrPath);

		}

		FileProcessor fp;
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
		//IScanner _Scanner;
		object AddAndUpdateLock = new object();

		async void ScanTask(object state)
		{

			var item = (EmbeddingsItem)state;
			var paths = Array.Empty<string>();
			var source = AssemblyInfo.ExpandPath(item.Source);
			var target = AssemblyInfo.ExpandPath(item.Target);
			if (fp != null)
			{
				fp.IsStopping = true;
				fp.Progress -= _Scanner_Progress;
			}
			if (db != null)
			{
				db.Dispose();
			}
			db = EmbeddingHelper.NewEmbeddingsContext(target);
			// Mark all files as starting to process.
			var tempState = ProgressStatus.Started;
			await EmbeddingHelper.SetFileState(
				db, Item.EmbeddingGroupName, Item.EmbeddingGroupFlag, tempState);
			fp = new FileProcessor();
			fp.ProcessItem = _Scanner_ProcessItem;
			fp.Progress += _Scanner_Progress;
			Dispatcher.Invoke(new Action(() =>
			{
				MainTabControl.SelectedItem = LogTabPage;
				try
				{
					paths = new[] { source };
					if (EmbeddingHelper.IsFilePath(target))
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
			await fp.Scan(paths, "*.txt");
			// Cleanup.
			var noErrors =
				fp.ProcessItemStates[ProgressStatus.Exception] == 0 &&
				fp.ProcessItemStates[ProgressStatus.Failed] == 0 &&
				fp.ProcessItemStates[ProgressStatus.Canceled] == 0;
			// If cancellation was not requested and no errors then...
			if (!fp.Cancellation.Token.IsCancellationRequested && noErrors)
			{
				// Delete unprocessed files.
				await EmbeddingHelper.DeleteByState(
					db,
					Item.EmbeddingGroupName, Item.EmbeddingGroupFlag, tempState);

			}
		}

		private async Task<ProgressStatus> _Scanner_ProcessItem(FileProcessor fp, ClassLibrary.ProgressEventArgs e)
		{
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
			var p = fp;
			if (p != null)
			{
				fp.Cancellation.Cancel();
				p.IsStopping = true;
			}
		}

	}
}
