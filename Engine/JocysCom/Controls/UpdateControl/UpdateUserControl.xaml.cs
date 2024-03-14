using JocysCom.ClassLibrary.Collections;
using JocysCom.ClassLibrary.Network;
using JocysCom.Controls.UpdateControl.GitHub;
using JocysCom.VS.AiCompanion.Engine;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace JocysCom.ClassLibrary.Controls.UpdateControl
{
	/// <summary>
	/// Interaction logic for UpdateWindow.xaml
	/// </summary>
	/// <remarks>Make sure to set the Owner property to be disposed properly after closing.</remarks>
	public partial class UpdateUserControl : UserControl, INotifyPropertyChanged
	{
		public UpdateUserControl()
		{
			InitializeComponent();
			if (ControlsHelper.IsDesignMode(this))
				return;
			//LogPanel.LogGridScrollUp = false;
			//var process = System.Diagnostics.Process.GetCurrentProcess();
			//processFileName = process.MainModule.FileName;
			ReleaseComboBox.ItemsSource = ReleaseList;
		}

		public string GitHubCompany { get; set; }
		public string GitHubProduct { get; set; }
		public string AssetName { get; set; }

		public event EventHandler AddTask;
		public event EventHandler RemoveTask;


		private async void CheckButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			await Step1ChekOnline();
		}

		public BindingList<KeyValue<long, string>> ReleaseList = new BindingList<KeyValue<long, string>>();
		public long? ReleaseId = null;

		List<release> Releases;

		public async Task Step1ChekOnline()
		{
			var e = new EventArgs();
			AddTask?.Invoke(this, e);
			try
			{
				var client = new GitHubApiClient();
				var releases = await client.GetGitHubReleasesAsync(GitHubCompany, GitHubProduct);
				Releases = releases
					.Where(x => !string.IsNullOrWhiteSpace(x.name))
					.Where(x => x.assets.Any(y => AssetName.Equals(y.name, System.StringComparison.OrdinalIgnoreCase)))
					.Where(x => System.Version.TryParse(x.name, out _))
					.OrderByDescending(x => System.Version.Parse(x.name))
					.ToList();
				ReleaseList.Clear();
				for (int i = 0; i < releases.Count; i++)
				{
					var release = releases[i];
					var name = i == 0 ? $"Latest Version {release.name}" : release.name;
					ReleaseList.Add(new KeyValue<long, string>(release.id, name));
				}
				if (releases.Count > 0)
					ReleaseComboBox.SelectedIndex = 0;
				OnPropertyChanged(nameof(ReleaseList));
				//LogPanel.Text = JsonSerializer.Serialize(releases);
			}
			catch (System.Exception ex)
			{
				LogPanel.Text = ex.ToString();
			}
			RemoveTask?.Invoke(this, e);
		}


		Downloader _downloader;

		public async Task Step2DownloadAndExtract()
		{
			var item = ReleaseComboBox.SelectedItem as KeyValue<long, string>;
			var releaseId = item?.Key;
			if (releaseId == null)
				return;
			var selectedRelease = Releases
				.Where(x => x.id == releaseId)
				.FirstOrDefault();
			if (selectedRelease == null)
				return;
			var asset = selectedRelease.assets
				.First(x => AssetName.Equals(x.name, System.StringComparison.OrdinalIgnoreCase));
			oldProgress = 0;
			_downloader = new Downloader();
			_downloader.Params.Url = asset.browser_download_url;
			_downloader.Params.TargetFile = AssetName;
			_downloader.Progress += _downloader_Progress;
			await _downloader.LoadAsync();
		}

		decimal oldProgress;
		object progressLock = new object();

		private void _downloader_Progress(object sender, DownloaderEventArgs e)
		{
			lock (progressLock)
			{
				var dl = (Downloader)sender;
				var progress = Math.Round(100m * e.BytesReceived / e.TotalBytesToReceive, 1);
				if (oldProgress != progress || dl.Params.ResponseData != null)
				{
					oldProgress = progress;
					Global.MainControl.Dispatcher.Invoke(() =>
					{
						var mb = Math.Round(e.BytesReceived / 1024m / 1024m, 1);
						StatusPanel.Text = string.Format("Download... {0}% - {1} MB", progress, mb);
						var isDone = e.BytesReceived == e.TotalBytesToReceive;
						if (isDone && _downloader.Params.ResponseData != null)
						{
							AddLog($"Saving File {dl.Params.TargetFile}... \r\n");
							System.IO.File.WriteAllBytes(dl.Params.TargetFile, dl.Params.ResponseData);
							AddLog(" Done");
							//Step3AExtractFiles(zipFileName);
						}
					});
				}
			}
		}

		public void AddLog(string s)
		{
			LogPanel.AppendText(s);
		}

		/*

		public void OpenDialog()
		{
			Global.CloudClient.TasksTimer.BeforeRemove += TasksTimer_BeforeRemove;
		}


		public void CloseDialog()
		{
			Global.CloudClient.TasksTimer.BeforeRemove -= TasksTimer_BeforeRemove;
		}

		bool CancelUpdate;

		private void CloseButton_Click(object sender, EventArgs e)
		{
			CancelUpdate = true;
			var item = CheckUpateItem;
			item.Retries = 0;
		}

		private void CheckButton_Click(object sender, EventArgs e)
		{
			Step1ChekOnline();
		}

		CloudItem CheckUpateItem;

	

		private void TasksTimer_BeforeRemove(object sender, QueueTimerEventArgs e)
		{
			var item = e.Item as CloudItem;
			// If check online task failed then...
			if (Equals(CheckUpateItem, item) && !e.Keep)
			{
				CurrentLogItem.Message += " Failed";
				if (item.Error != null)
					CurrentLogItem.Message += ": " + item.Error.Message;
			}
		}


		

		void Step3AExtractFiles(string zipFileName)
		{
			if (CancelUpdate)
				return;
			var name = System.IO.Path.GetFileName(processFileName);
			string updateFileName = processFileName + ".tmp";
			JocysCom.ClassLibrary.Files.Zip.UnZipFile(zipFileName, "x360ce.exe", updateFileName);
			Step3CheckSignature(updateFileName);
		}

		void Step3CheckSignature(string updateFileName)
		{
			if (CancelUpdate)
				return;
			if (CheckDigitalSignatureCheckBox.IsChecked == true)
			{
				CurrentLogItem = LogPanel.Add("Check Digital Signature...");
				X509Certificate2 certificate;
				Exception error;
				if (!CertificateHelper.IsSignedAndTrusted(updateFileName, out certificate, out error))
				{
					var errMessage = error == null
						? " Failed" : string.Format(" Failed: {0}", error.Message);
					CurrentLogItem.Message += errMessage;
					return;
				}
			}
			Step4CheckVersion(updateFileName);
		}

		void Step4CheckVersion(string updatedFileName)
		{
			if (CancelUpdate)
				return;
			if (CheckVersionCheckBox.IsChecked == true)
			{
				var processFi = System.Diagnostics.FileVersionInfo.GetVersionInfo(processFileName);
				var updatedFi = System.Diagnostics.FileVersionInfo.GetVersionInfo(updatedFileName);
				var processVersion = new Version(processFi.FileVersion);
				var updatedVersion = new Version(updatedFi.FileVersion);
				LogPanel.Add("Current version: {0}", processVersion);
				LogPanel.Add("Updated version: {0}", updatedVersion);
				if (processVersion == updatedVersion)
				{
					LogPanel.Add("Versions are the same. Skip Update");
					return;
				}
				if (processVersion > updatedVersion)
				{
					LogPanel.Add("Remote version is older. Skip Update.");
					return;
				}
			}
			Step5ReplaceFiles(updatedFileName);
		}

		void Step5ReplaceFiles(string updateFileName)
		{
			if (CancelUpdate)
				return;
			// Change the currently running executable so it can be overwritten.
			string bak = processFileName + ".bak";
			CurrentLogItem = LogPanel.Add("Renaming running process...");
			try
			{
				if (System.IO.File.Exists(bak))
					System.IO.File.Delete(bak);
			}
			catch (Exception ex)
			{
				CurrentLogItem.Message += " Failed: " + ex.Message;
				return;
			}
			System.IO.File.Move(processFileName, bak);
			System.IO.File.Copy(updateFileName, processFileName);
			CurrentLogItem.Message += " Done";
			Step6Restart();
		}

		void Step6Restart()
		{
			if (CancelUpdate)
				return;
			var process = System.Diagnostics.Process.GetCurrentProcess();
			CurrentLogItem = LogPanel.Add("Restarting...");
			System.Windows.Forms.Application.Restart();
		}

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{

		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			if (ControlsHelper.IsDesignMode(this))
				return;
			CancelUpdate = false;
			LogPanel.Items.Clear();
			// Center message box window in application.
			if (Owner == null)
				ControlsHelper.CenterWindowOnApplication(this);
		}

		*/

		#region ■ INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		#endregion

		private void ReleaseComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{

		}

		private async void InstallButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			await Step2DownloadAndExtract();
		}
	}
}
