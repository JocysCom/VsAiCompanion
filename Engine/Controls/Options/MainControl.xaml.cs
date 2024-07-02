using Hompus.VideoInputDevices;
using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.Controls;
using JocysCom.VS.AiCompanion.Plugins.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace JocysCom.VS.AiCompanion.Engine.Controls.Options
{
	/// <summary>
	/// Interaction logic for Main.xaml
	/// </summary>
	public partial class MainControl : UserControl, INotifyPropertyChanged
	{
		public MainControl()
		{
			InitializeComponent();
			if (ControlsHelper.IsDesignMode(this))
				return;
			Global.AppSettings.PropertyChanged += AppSettings_PropertyChanged;
			StartWithWindowsStateBox.ItemsSource = Enum.GetValues(typeof(WindowState));
			DomainMaxRiskLevelRefresh();
			var debugVisibility = InitHelper.IsDebug
				? Visibility.Visible
				: Visibility.Collapsed;
			MultimediaGroupBox.Visibility = debugVisibility;
			EnableShowFormInfoCheckBox.Visibility = debugVisibility;
		}

		private async void AppSettings_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				case nameof(AppData.AllowOnlyOneCopy):
					// Make sure that AllowOnlyOneCopy setting applies immediatelly.
					Global.AppData.Save();
					break;
				case nameof(AppData.OverrideInfoDefaultHead):
				case nameof(AppData.OverrideInfoDefaultBody):
					await Helper.Delay(Global.MainControl.UpdateInfoPanelDefaults);
					break;
				default:
					break;
			}
		}

		private void ApplySettingsButton_Click(object sender, RoutedEventArgs e)
		{
			SettingsSourceManager.ResetSettings(true);
		}

		private void This_Loaded(object sender, RoutedEventArgs e)
		{
			if (ControlsHelper.IsDesignMode(this))
				return;
			AppHelper.AddHelp(IsSpellCheckEnabledCheckBox, Engine.Resources.MainResources.main_Enable_Spell_Check_Help);
			if (ControlsHelper.AllowLoad(this))
			{
				UpdateVideoInputDevices();
				AppHelper.InitHelp(this);
				UiPresetsManager.InitControl(this, true);
			}
		}

		System.Windows.Forms.OpenFileDialog _OpenFileDialog;

		private void BrowseSettingsButton_Click(object sender, RoutedEventArgs e)
		{
			var path = JocysCom.ClassLibrary.Configuration.AssemblyInfo.ExpandPath(Global.AppSettings.ConfigurationUrl);
			if (_OpenFileDialog == null)
			{
				_OpenFileDialog = new System.Windows.Forms.OpenFileDialog();
				_OpenFileDialog.SupportMultiDottedExtensions = true;
				_OpenFileDialog.FileName = path;
				DialogHelper.AddFilter(_OpenFileDialog, ".zip");
				DialogHelper.AddFilter(_OpenFileDialog);
				_OpenFileDialog.FilterIndex = 1;
				_OpenFileDialog.RestoreDirectory = true;
			}
			var dialog = _OpenFileDialog;
			dialog.Title = "Open " + JocysCom.ClassLibrary.Files.Mime.GetFileDescription(".zip");
			var result = dialog.ShowDialog();
			if (result != System.Windows.Forms.DialogResult.OK)
				return;
			path = dialog.FileNames[0];
			path = JocysCom.ClassLibrary.Configuration.AssemblyInfo.ParameterizePath(path, true);
			Global.AppSettings.ConfigurationUrl = path;
		}

		public Dictionary<RiskLevel, string> MaxRiskLevels
		=> ClassLibrary.Runtime.Attributes.GetDictionary(
			((RiskLevel[])Enum.GetValues(typeof(RiskLevel))).Except(new[] { RiskLevel.Unknown }).ToArray());


		private void DomainMaxRiskLevelRefresh(bool cache = true)
		{
			DomainMaxRiskLevelValueLabel.Content = "...";
			var isDomainUser = JocysCom.ClassLibrary.Security.PermissionHelper.IsDomainUser();
			var visibility = isDomainUser
				? Visibility.Visible
				: Visibility.Collapsed;
			DomainMaxRiskLevelRefreshButton.Visibility = visibility;
			DomainMaxRiskLevelNameLabel.Visibility = visibility;
			DomainMaxRiskLevelValueLabel.Visibility = visibility;
			_ = Task.Run(() =>
			{
				var domainMaxRiskLevel = DomainHelper.GetDomainUserMaxRiskLevel(cache);
				var level = domainMaxRiskLevel?.ToString() ?? "N/A";
				ControlsHelper.AppInvoke(() =>
				{
					DomainMaxRiskLevelValueLabel.Content = $"{level}";
				});
			});


		}

		private void DomainMaxRiskLevelRefreshButton_Click(object sender, RoutedEventArgs e)
		{
			DomainMaxRiskLevelRefresh(false);
		}

		#region Group: Multimedia

		public ObservableCollection<string> VideoInputDevices { get; } = new ObservableCollection<string>();

		public void UpdateVideoInputDevices()
		{
			var enumarator = new SystemDeviceEnumerator();
			var devices = enumarator.ListVideoInputDevice().Select(x => x.Value).ToList();
			ClassLibrary.Collections.CollectionsHelper.Synchronize(devices, VideoInputDevices);
			OnPropertyChanged(nameof(VideoInputDevices));
		}

		private void VideoInputDevicesRefreshButton_Click(object sender, RoutedEventArgs e)
		{
			UpdateVideoInputDevices();
		}

		#endregion


		#region ■ INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		#endregion

	}

}
