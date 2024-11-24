using JocysCom.ClassLibrary.Collections;
using JocysCom.ClassLibrary.ComponentModel;
using JocysCom.ClassLibrary.Network;
using JocysCom.ClassLibrary.Security;
using JocysCom.ClassLibrary.Windows;
using JocysCom.Controls.UpdateControl.GitHub;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace JocysCom.ClassLibrary.Controls.UpdateControl
{
	/// <summary>
	/// Class that handles the update logic by interacting with GitHub releases.
	/// Provides functionality to check for updates, download, extract, verify, and install updates.
	/// </summary>
	public partial class UpdateChecker : NotifyPropertyChanged
	{
		#region Properties

		public UpdateSettings Settings
		{
			get { return _Settings; }
			set { _Settings = value; OnPropertyChanged(nameof(Settings)); }
		}
		UpdateSettings _Settings = new UpdateSettings();

		public BindingList<KeyValue<long, string>> ReleaseList = new BindingList<KeyValue<long, string>>();
		public long? ReleaseId = null;

		public List<release> Releases;

		public bool InstallMode;

		public bool EnableReplace { get; set; }
		public bool EnableRestart { get; set; }

		public string DownloadTempFolder { get; set; } = Path.GetTempPath();

		/// <summary>
		/// Path to GitHub file on the local disk.
		/// </summary>
		public string DownloadTargetFile
			=> DownloadTempFolder + Settings.GitHubAssetName;

		/// <summary>New application file.</summary>
		public string UpdateNewFileFullName
			=> UacHelper.CurrentProcessFileName + ".tmp";

		/// <summary>Current application backup file.</summary>
		public string UpdateBakFileFullName
			=> UacHelper.CurrentProcessFileName + ".bak";

		#endregion

		private CancellationTokenSource cancellationTokenSource;

		public event EventHandler AddTask;
		public event EventHandler RemoveTask;

		public event EventHandler<string> LogAdded;

		public UpdateChecker()
		{
			cancellationTokenSource = new CancellationTokenSource();
		}

		#region Methods

		/// <summary>
		/// Initiates the update check process programmatically.
		/// </summary>
		public async Task StartUpdateCheckAsync()
		{
			InstallMode = false;
			await Step1CheckOnline();
		}

		/// <summary>
		/// Initiates the update and install process.
		/// </summary>
		public async Task StartUpdateInstallAsync()
		{
			InstallMode = true;
			cancellationTokenSource = new CancellationTokenSource();
			await Step2Download();
		}

		/// <summary>
		/// Checks for available updates by retrieving release information from GitHub.
		/// </summary>
		public async Task Step1CheckOnline()
		{
			var e = new EventArgs();
			AddTask?.Invoke(this, e);
			try
			{
				var client = new GitHubApiClient();
				var gitReleases = await client.GetGitHubReleasesAsync(Settings.GitHubCompany, Settings.GitHubProduct);
				Version minVersion;
				Version.TryParse(Settings.MinVersion, out minVersion);
				var filter = gitReleases.Where(x => !string.IsNullOrWhiteSpace(x.tag_name));
				filter = filter.Where(x => x.assets.Any(y => y.name.EndsWith(Settings.GitHubAssetName, StringComparison.OrdinalIgnoreCase)));
				filter = filter.Where(x => Version.TryParse(ExtractVersionFromName(x.tag_name), out _));
				filter = filter.Where(x => Settings.IncludePrerelease || x.prerelease == false);
				filter = filter.Where(x => minVersion <= Version.Parse(ExtractVersionFromName(x.tag_name)));
				filter = filter.OrderByDescending(x => Version.Parse(ExtractVersionFromName(x.tag_name)));
				// Check if version is skipped.
				var skippedVersions = Settings.SkippedVersions ?? new List<string>();
				filter = filter.Where(x => !skippedVersions.Contains(ExtractVersionFromName(x.tag_name)));
				Releases = gitReleases.ToList();
				ReleaseList.Clear();
				for (int i = 0; i < Releases.Count; i++)
				{
					var release = Releases[i];
					var name = i == 0 ? $"Latest Version {ExtractVersionFromName(release.tag_name)}" : ExtractVersionFromName(release.tag_name);
					ReleaseList.Add(new KeyValue<long, string>(release.id, name));
				}
				if (Releases.Count > 0)
				{
					ReleaseId = Releases[0].id;
				}
				else
				{
					AddLog("No updates found!\r\n");
				}
				OnPropertyChanged(nameof(ReleaseList));
			}
			catch (System.Exception ex)
			{
				cancellationTokenSource.Cancel();
				AddLog(ex.ToString());
			}
			RemoveTask?.Invoke(this, e);
		}

		public static string ExtractVersionFromName(string name)
		{
			var pattern = @"v?\d+(\.\d+)*";
			var regex = new Regex(pattern);
			var match = regex.Match(name);
			return match.Success
				? match.Value
				: "";
		}

		public async Task Step2Download()
		{
			AddLog($"Downloading File...\r\n");
			var asset = GetSelectedAsset();
			if (asset == null)
				return;
			oldProgress = 0;
			_downloader = new Downloader();
			_downloader.Params.SourceUrl = asset.browser_download_url;
			_downloader.Params.TargetFile = DownloadTargetFile;
			_downloader.Progress += _downloader_Progress;
			await _downloader.LoadAsync();
		}

		decimal oldProgress;
		object progressLock = new object();
		Downloader _downloader;

		private void _downloader_Progress(object sender, DownloaderEventArgs e)
		{
			lock (progressLock)
			{
				var dl = (Downloader)sender;
				var progress = Math.Round(100m * e.BytesReceived / e.TotalBytesToReceive, 1);
				if (oldProgress != progress || dl.Params.ResponseData != null)
				{
					oldProgress = progress;
					var mb = Math.Round(e.BytesReceived / 1024m / 1024m, 1);
					var isDone = e.BytesReceived == e.TotalBytesToReceive;
					if (isDone && _downloader.Params.ResponseData != null)
					{
						AddLog($"Saving File {DownloadTargetFile}... \r\n");
						System.IO.File.WriteAllBytes(DownloadTargetFile, dl.Params.ResponseData);
						AddLog(" Done\r\n");
						if (InstallMode)
						{
							Step3ExtractFile();
							Step3CheckSignature();
							Step5ReplaceFiles();
							Step6RestartApp();
						}
					}
				}
			}
		}

		public release GetSelectedRelease()
		{
			var releaseId = ReleaseId;
			if (releaseId == null)
				return null;
			var selectedRelease = Releases
				.Where(x => x.id == releaseId)
				.FirstOrDefault();
			return selectedRelease;
		}

		public asset GetSelectedAsset()
		{
			var selectedRelease = GetSelectedRelease();
			if (selectedRelease == null)
				return null;
			var asset = selectedRelease.assets
				.First(x => x.name.EndsWith(Settings.GitHubAssetName, StringComparison.OrdinalIgnoreCase));
			return asset;
		}

		/// <summary>
		/// Extracts the downloaded update package to obtain the new executable.
		/// </summary>
		/// <returns>True if extraction was successful; otherwise, false.</returns>
		public bool Step3ExtractFile()
		{
			if (InstallMode && cancellationTokenSource.Token.IsCancellationRequested)
				return false;
			var tmpName = UpdateNewFileFullName;
			AddLog("Extracting...\r\n");
			AddLog($"\tFile: {Settings.FileNameInsideZip}\r\n");
			AddLog($"\tFrom: {DownloadTargetFile}\r\n");
			AddLog($"\tTo: {tmpName}\r\n");
			try
			{
				JocysCom.ClassLibrary.Files.Zip.UnZipFile(DownloadTargetFile, Settings.FileNameInsideZip, tmpName);
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

		/// <summary>
		/// Checks if the downloaded executable is digitally signed and trusted.
		/// </summary>
		/// <returns>True if the signature is valid; otherwise, false.</returns>
		public bool Step3CheckSignature()
		{
			if (InstallMode && cancellationTokenSource.Token.IsCancellationRequested)
				return false;
			AddLog("Check Digital Signature...\r\n");
			X509Certificate2 certificate;
			Exception error;
			if (CertificateHelper.IsSignedAndTrusted(UpdateNewFileFullName, out certificate, out error))
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

		/// <summary>
		/// Replaces the current application files with the newly downloaded ones.
		/// </summary>
		/// <returns>True if replacement is successful; otherwise, false.</returns>
		public bool Step5ReplaceFiles()
		{
			if (!EnableReplace)
			{
				AddLog("EnableReplace = False\r\n");
				return false;
			}
			AddLog("Replacing Files...\r\n");
			var args = new string[] {
				$"/{nameof(UpdateProcessHelper.ReplaceFiles)}",
				$"/bakFile=\"{UpdateBakFileFullName}\"",
				$"/newFile=\"{UpdateNewFileFullName}\"",
				$"/exeFile=\"{UacHelper.CurrentProcessFileName}\"",
			};
			if (PermissionHelper.CanRenameFile(UacHelper.CurrentProcessFileName))
				UpdateProcessHelper.ProcessAdminCommands(args);
			else
				UpdateProcessHelper.RunElevated(args);
			return true;
		}

		/// <summary>
		/// Restarts the application after the update process is completed.
		/// </summary>
		/// <returns>True if the application is restarted; otherwise, false.</returns>
		public bool Step6RestartApp()
		{
			if (!EnableRestart)
			{
				AddLog("EnableRestart = False\r\n");
				return false;
			}
			AddLog("Restarting...\r\n");
			Task.Delay(1000).Wait();
			var args = new string[] {
				$"/{nameof(UpdateProcessHelper.RestartApp)}",
				$"/exeFile=\"{ UacHelper.CurrentProcessFileName}\"",
			};
			UpdateProcessHelper.RunProcessAsync(args);
			System.Windows.Application.Current.Shutdown();
			return true;
		}

		protected void AddLog(string s)
		{
			LogAdded?.Invoke(this, s);
		}

		#endregion
	}
}
