using JocysCom.ClassLibrary.Collections;
using JocysCom.Controls.UpdateControl.GitHub;
using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace JocysCom.ClassLibrary.Controls.UpdateControl
{
	/// <summary>
	/// UserControl that handles the update process by interacting with the UpdateChecker.
	/// </summary>
	public partial class UpdateUserControl : UserControl, INotifyPropertyChanged
	{
		public UpdateUserControl()
		{
			InitializeComponent();
			if (ControlsHelper.IsDesignMode(this))
				return;
			ExtraButtonsPanel.Visibility = InitHelper.IsDebug
				? Visibility.Visible
				: Visibility.Collapsed;
			LogPanel.ClearLogButton.Visibility = Visibility.Collapsed;
			UpdateButtons();
		}

		/// <summary>
		/// The Update Checker.
		/// </summary>
		public UpdateChecker Checker
		{
			get => _Checker;
			set
			{
				if (_Checker != null)
				{
					_Checker.LogAdded -= UpdateChecker_LogAdded;
					_Checker.PropertyChanged -= UpdateChecker_PropertyChanged;
					ReleaseComboBox.SelectionChanged -= ReleaseComboBox_SelectionChanged;
					ReleaseComboBox.ItemsSource = null;
				}
				_Checker = value;
				if (_Checker != null)
				{
					ReleaseComboBox.ItemsSource = _Checker.ReleaseList;
					ReleaseComboBox.SelectionChanged += ReleaseComboBox_SelectionChanged;
					_Checker.LogAdded += UpdateChecker_LogAdded;
					_Checker.PropertyChanged += UpdateChecker_PropertyChanged;
				}
				OnPropertyChanged();
			}
		}
		UpdateChecker _Checker;

		/// <summary>
		/// The UpdateSettings object that this control binds to.
		/// </summary>
		public UpdateSettings Settings
		{
			get => _Settings;
			set
			{
				_Settings = value;
				OnPropertyChanged();
			}
		}
		UpdateSettings _Settings;

		private void UpdateChecker_LogAdded(object sender, string e)
		{
			LogPanel.Add(e);
		}

		private void UpdateChecker_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(Checker.ReleaseList))
				UpdateButtons();
		}

		private void ReleaseComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			var item = ReleaseComboBox.SelectedItem as KeyValue<long, string>;
			if (item != null)
				Checker.ReleaseId = item.Key;
			UpdateButtons();
		}

		void UpdateButtons()
		{
			var checker = Checker;
			if (checker is null)
				return;
			var selectedRelease = checker.GetSelectedRelease();
			var selected = selectedRelease != null;
			InstallButton.IsEnabled = selected;
			DownloadButton.IsEnabled = selected;
			ExtractButton.IsEnabled = selected;
			CheckSignatureButton.IsEnabled = selected;
			CheckVersionButton.IsEnabled = selected;
			ReplaceFileButton.IsEnabled = selected;
			RestartButton.IsEnabled = selected;
			var currentVersion = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version;
			var changesText = "";
			if (checker.Releases?.Count > 0)
			{
				var asset = checker.GetSelectedAsset();
				var release = checker.GetSelectedRelease();
				// Get info about selected version.
				if (asset != null)
				{
					var latestVersion = Version.Parse(UpdateChecker.ExtractVersionFromName(checker.Releases[0].tag_name));
					var selectedVersion = Version.Parse(UpdateChecker.ExtractVersionFromName(release.tag_name));
					var totalDownloads = checker.Releases.Sum(x => x.assets.Sum(a => a.download_count));
					if (currentVersion.Revision == 0 && latestVersion.Revision == -1)
						currentVersion = new Version(currentVersion.Major, currentVersion.Minor, currentVersion.Build);
					if (currentVersion < latestVersion)
					{
						changesText += $"New updates found! Latest version: {latestVersion}\r\n";
						changesText += $"\r\n";
					}

					changesText += $"Current  version: {currentVersion}\r\n";
					changesText += $"\r\n";
					changesText += $"Selected version: {selectedVersion}\r\n";
					changesText += $"Download URL: {asset.browser_download_url}\r\n";
					changesText += $"File: {asset.name} ({JocysCom.ClassLibrary.IO.FileFinder.BytesToString(asset.size)})\r\n";
					changesText += $"Modified: {DateTime.Parse(asset.updated_at)}\r\n";
					changesText += $"\r\n";
				}
				// Get description of versions that are higher than the current version.
				var changes = checker.Releases.Where(x => currentVersion < Version.Parse(UpdateChecker.ExtractVersionFromName(x.tag_name))).ToList();
				changesText += changes.Count == 0
					? string.Join(Environment.NewLine, changes.Select(x => x.body))
					: selectedRelease?.body;
			}
			LogPanel.Clear();
			LogPanel.Add(changesText + "\r\n\r\n");
		}

		private async void InstallButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			await Checker.StartUpdateInstallAsync();
		}

		private async void CheckButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			LogPanel.Clear();
			await Checker.StartUpdateCheckAsync();
		}

		private async void DownloadButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			await Checker.Step2Download();
		}

		private void ExtractButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			Checker.Step3ExtractFile();
		}

		private void CheckSignatureButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			Checker.Step3CheckSignature();
		}

		private void CheckVersionButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			// Implement if necessary
		}

		private void ReplaceFileButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			Checker.Step5ReplaceFiles();
		}

		private void RestartButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			Checker.Step6RestartApp();
		}

		#region ■ INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged(string propertyName = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		#endregion
	}
}
