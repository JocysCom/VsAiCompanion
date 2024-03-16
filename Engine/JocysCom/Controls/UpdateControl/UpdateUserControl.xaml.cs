using JocysCom.ClassLibrary.Collections;
using JocysCom.ClassLibrary.Network;
using JocysCom.Controls.UpdateControl.GitHub;
using JocysCom.VS.AiCompanion.Engine;
using JocysCom.WebSites.Engine.Security;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.AccessControl;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text;
using System.Threading;
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
			cancellationTokenSource = new CancellationTokenSource();
			ReleaseComboBox.ItemsSource = ReleaseList;
		}

		CancellationTokenSource cancellationTokenSource;

		public string GitHubCompany { get; set; }
		public string GitHubProduct { get; set; }

		/// <summary>
		/// GitHub executing asset name + ".zip".
		/// </summary>
		public string GitHubAssetName { get; set; }

		/// <summary>
		/// Executable inside the zip
		/// </summary>
		public string FileNameInsideZip { get; set; }

		/// <summary>
		/// Executable file name to update.
		/// </summary>
		public string UpdateFileFullName { get; set; }


		public event EventHandler AddTask;
		public event EventHandler RemoveTask;

		/// <summary>
		/// Path to GitHub file on the local disk.
		/// </summary>
		public string DownloadTargetFile
			=> Path.GetTempPath() + GitHubAssetName;

		/// <summary>New application file.</summary>
		public string UpdateFileTempFullName
			=> UpdateFileFullName + ".tmp";

		/// <summary>Current application backup file.</summary>
		public string UpdateFileBackFullName
			=> UpdateFileFullName + ".bak";


		bool InstallMode;

		private async void InstallButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			InstallMode = true;
			cancellationTokenSource = new CancellationTokenSource();
			await Step2Download();
		}



		private async void CheckButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			InstallMode = false;
			await Step1CheckOnline();
		}

		public BindingList<KeyValue<long, string>> ReleaseList = new BindingList<KeyValue<long, string>>();
		public long? ReleaseId = null;

		List<release> Releases;

		public async Task Step1CheckOnline()
		{
			var e = new EventArgs();
			AddTask?.Invoke(this, e);
			try
			{
				var client = new GitHubApiClient();
				var releases = await client.GetGitHubReleasesAsync(GitHubCompany, GitHubProduct);
				Releases = releases
					.Where(x => !string.IsNullOrWhiteSpace(x.name))
					.Where(x => x.assets.Any(y => GitHubAssetName.Equals(y.name, System.StringComparison.OrdinalIgnoreCase)))
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
				cancellationTokenSource.Cancel();
				AddLog(ex.ToString());
			}
			RemoveTask?.Invoke(this, e);
		}

		private async void DownloadButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			InstallMode = false;
			await Step2Download();
		}

		Downloader _downloader;

		public async Task Step2Download()
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
				.First(x => GitHubAssetName.Equals(x.name, System.StringComparison.OrdinalIgnoreCase));
			oldProgress = 0;
			_downloader = new Downloader();
			_downloader.Params.SourceUrl = asset.browser_download_url;
			_downloader.Params.TargetFile = DownloadTargetFile;
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
					Dispatcher.Invoke(() =>
					{
						var mb = Math.Round(e.BytesReceived / 1024m / 1024m, 1);
						StatusPanel.Text = string.Format("Download... {0}% - {1} MB", progress, mb);
						var isDone = e.BytesReceived == e.TotalBytesToReceive;
						if (isDone && _downloader.Params.ResponseData != null)
						{
							AddLog($"Saving File {DownloadTargetFile}... \r\n");
							System.IO.File.WriteAllBytes(DownloadTargetFile, dl.Params.ResponseData);
							AddLog(" Done\r\n");
							if (InstallMode)
								Step3ExtractFile();
						}
					});
				}
			}
		}

		public void AddLog(string s)
		{
			LogTextBox.AppendText(s);
		}

		private void ExtractButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			InstallMode = false;
			Step3ExtractFile();
		}

		bool Step3ExtractFile()
		{
			if (InstallMode && cancellationTokenSource.Token.IsCancellationRequested)
				return false;
			var tmpName = UpdateFileFullName + ".tmp";
			AddLog("Extracting...\r\n");
			AddLog($"\tFile: {FileNameInsideZip}\r\n");
			AddLog($"\tFrom: {DownloadTargetFile}\r\n");
			AddLog($"\tTo: {tmpName}\r\n");
			try
			{
				JocysCom.ClassLibrary.Files.Zip.UnZipFile(DownloadTargetFile, FileNameInsideZip, tmpName);
			}
			catch (Exception ex)
			{
				AddLog($"\tException: {ex.Message}\r\n");
				cancellationTokenSource.Cancel();
				return false;
			}
			AddLog("...Done\r\n");
			return true;
		}

		private void CheckSignatureButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			InstallMode = false;
			Step3CheckSignature(UpdateFileTempFullName);
		}

		bool Step3CheckSignature(string updateFileName)
		{
			if (InstallMode && cancellationTokenSource.Token.IsCancellationRequested)
				return false;
			AddLog("Check Digital Signature...\r\n");
			X509Certificate2 certificate;
			Exception error;
			if (CertificateHelper.IsSignedAndTrusted(updateFileName, out certificate, out error))
			{
				AddLog($"\tSubject:    {certificate.Subject}\r\n");
				AddLog($"\tIssuer:     {certificate.Issuer}\r\n");
				AddLog($"\tExpires:    {certificate.NotAfter}\r\n");
				AddLog($"\tThumbprint: {certificate.Thumbprint}\r\n");
			}
			else
			{
				var errMessage = error == null
					? "\tFailed" : string.Format(" Failed: {0}", error.Message);
				AddLog(errMessage + "\r\n");
				return false;
			}
			AddLog("...Done.\r\n");
			return true;
		}

		private void CheckVersionButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			InstallMode = false;
			Step4CheckVersion();
		}

		bool Step4CheckVersion()
		{
			if (InstallMode && cancellationTokenSource.Token.IsCancellationRequested)
				return false;
			var processFi = System.Diagnostics.FileVersionInfo.GetVersionInfo(UpdateFileFullName);
			var updatedFi = System.Diagnostics.FileVersionInfo.GetVersionInfo(UpdateFileTempFullName);
			var processVersion = new Version(processFi.FileVersion);
			var updatedVersion = new Version(updatedFi.FileVersion);
			AddLog($"Current version: {processVersion}\r\n");
			AddLog($"Updated version: {updatedVersion}\r\n");
			if (processVersion == updatedVersion)
			{
				AddLog("Versions are the same. Skip Update\r\n");
				cancellationTokenSource.Cancel();
				return false;
			}
			if (processVersion > updatedVersion)
			{
				AddLog("Remote version is older. Skip Update.\r\n");
				if (CheckVersionCheckBox.IsChecked == true)
					cancellationTokenSource.Cancel();
				return false;
			}
			return true;
		}

		private void ReplaceFileButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			InstallMode = false;
			Step5ReplaceFiles();
		}

		bool Step5ReplaceFiles()
		{
			if (!HasRightsToModify(UpdateFileFullName))
			{
				// Continue update as elevated process.
				AdminCommands.RunElevated(AdminCommand.UpdaterRenameFiles, UpdateFileFullName);
				return true;
			}
			if (InstallMode && cancellationTokenSource.Token.IsCancellationRequested)
				return false;
			AddLog($"Removing UpdateFileBackFullName...\r\n");
			try
			{
				// Delete current application backup.
				if (File.Exists(UpdateFileBackFullName))
					File.Delete(UpdateFileBackFullName);
			}
			catch (Exception ex)
			{
				AddLog($"\tException: {ex.Message}\r\n");
				cancellationTokenSource.Cancel();
				return false;
			}
			AddLog("...Done.\r\n");
			// Change the currently running executable so it can be overwritten.
			AddLog($"Replacing with new file...\r\n");
			try
			{
				File.Move(UpdateFileFullName, UpdateFileBackFullName);
				File.Copy(UpdateFileTempFullName, UpdateFileFullName);
			}
			catch (Exception ex)
			{
				AddLog($"\tException: {ex.Message}\r\n");
				cancellationTokenSource.Cancel();
				return false;
			}
			AddLog("...Done.\r\n");
			return true;
		}

		private void RestartButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			InstallMode = false;
			Step6Restart();
		}

		bool Step6Restart()
		{
			if (InstallMode && cancellationTokenSource.Token.IsCancellationRequested)
				return false;
			var batchCommands = new StringBuilder();
			var tempBatchFile = Path.GetTempFileName() + ".bat";
			var tempVbsFile = Path.GetTempFileName() + ".vbs";
			// Prepare the batch script
			batchCommands.AppendLine("@echo off");
			batchCommands.AppendLine("timeout /t 5 /nobreak > NUL");
			batchCommands.AppendLine($"start \"\" \"{UpdateFileFullName}\"");
			batchCommands.AppendLine($"del \"{tempBatchFile}\"");
			File.WriteAllText(tempBatchFile, batchCommands.ToString());
			// Prepare the VBScript
			var vbsCommands = $"CreateObject(\"Wscript.Shell\").Run \"\"\"{tempBatchFile}\"\"\", 0, False";
			File.WriteAllText(tempVbsFile, vbsCommands);
			// Execute the VBScript, which runs the batch file invisibly
			Process.Start("wscript.exe", $"\"{tempVbsFile}\"");
			// Shutdown the current application instance
			System.Windows.Application.Current.Shutdown();
			return true;
		}

		private void ReleaseComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{

		}

		#region Permission Helper

		public bool HasRightsToModify(string fileFullName)
		{
			var rights = FileSystemRights.Modify;
			var users = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
			// Check if users in non elevated mode have rights to modify the file.
			var hasRights = JocysCom.ClassLibrary.Security.PermissionHelper.HasRights(fileFullName, rights, users, false);
			return hasRights;
			//if (!hasRights && JocysCom.ClassLibrary.Win32.WinAPI.IsElevated())
		}

		#endregion

		#region ■ INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		#endregion

	}
}
