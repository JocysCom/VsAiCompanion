using JocysCom.ClassLibrary.Controls;
using JocysCom.ClassLibrary.Controls.UpdateControl;
using System;
using System.Windows;
using System.Windows.Controls;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	/// <summary>
	/// Interaction logic for UpdatesControl.xaml
	/// </summary>
	public partial class UpdatesControl : UserControl
	{
		public UpdatesControl()
		{
			InitializeComponent();
			if (ControlsHelper.IsDesignMode(this))
				return;
			PandocTabItem.Visibility = InitHelper.IsDebug
				? Visibility.Visible
				: Visibility.Collapsed;
			var appExeAssembly = System.Reflection.Assembly.GetEntryAssembly();
			if (Global.IsVsExtension)
			{
				MainTabItem.Visibility = Visibility.Collapsed;
			}
			else
			{
				var checker = Global.AiCompUpdateChecker;
				var control = new UpdateUserControl();
				checker.Settings.UpdateMissingDefaults(appExeAssembly);
				checker.DownloadTempFolder = AppHelper.GetTempFolderPath();
				checker.EnableReplace = true;
				checker.EnableRestart = true;
				checker.AddTask += UpdatesPanel_AddTask;
				checker.RemoveTask += UpdatesPanel_RemoveTask;
				// Bind data to control.
				control.Checker = checker;
				control.Settings = checker.Settings;
				// Add app update control to the app UI.
				MainTabItem.Content = control;
				mainUpdateUserControl = control;
				Global.AiCompUpdateTimeChecker.UpdateRequired += mainUuc_UpdateTimeControl_UpdateRequired;
			}
			if (InitHelper.IsDebug)
			{
				var checker = Global.PanDocUpdateChecker;
				var control = new UpdateUserControl();
				UpdateMissingPandocDefaults(checker.Settings);
				checker.DownloadTempFolder = AppHelper.GetTempFolderPath();
				checker.AddTask += UpdatesPanel_AddTask;
				checker.RemoveTask += UpdatesPanel_RemoveTask;
				// Bind data to control.
				control.Checker = checker;
				control.Settings = checker.Settings;
				// Add pandoc control to the app UI.
				PandocTabItem.Content = control;
				pandocUpdateUserControl = control;
				Global.PanDocUpdateTimeChecker.UpdateRequired += pandocUuc_UpdateTimeControl_UpdateRequired;
			}
		}

		UpdateUserControl mainUpdateUserControl;
		UpdateUserControl pandocUpdateUserControl;

		private async void mainUuc_UpdateTimeControl_UpdateRequired(object sender, EventArgs e)
		{

			// Start the update check process.
			await Global.AiCompUpdateChecker.StartUpdateCheckAsync();
			// Optionally, if you want to automatically install updates, call the install process.
			// await UpdateControl.StartUpdateInstallAsync();
		}

		private async void pandocUuc_UpdateTimeControl_UpdateRequired(object sender, EventArgs e)
		{
			// Start the update check process.
			await Global.PanDocUpdateChecker.StartUpdateCheckAsync();
			// Optionally, if you want to automatically install updates, call the install process.
			// await UpdateControl.StartUpdateInstallAsync();
		}


		public void UpdateMissingPandocDefaults(UpdateSettings us)
		{
			if (string.IsNullOrWhiteSpace(us.GitHubCompany))
				us.GitHubCompany = "jgm";
			if (string.IsNullOrWhiteSpace(us.GitHubProduct))
				us.GitHubProduct = "pandoc";
			if (string.IsNullOrWhiteSpace(us.GitHubAssetName))
				us.GitHubAssetName = "windows-x86_64.zip";
			if (string.IsNullOrWhiteSpace(us.FileNameInsideZip))
				us.FileNameInsideZip = "pandoc.exe";
			if (string.IsNullOrWhiteSpace(us.MinVersion))
				us.MinVersion = "3.1";
		}

		private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{

		}

		private void UpdatesPanel_AddTask(object sender, System.EventArgs e)
		{
			Global.MainControl.InfoPanel.AddTask(e);
		}

		private void UpdatesPanel_RemoveTask(object sender, System.EventArgs e)
		{
			Global.MainControl.InfoPanel.RemoveTask(e);
		}

		private void This_Loaded(object sender, System.Windows.RoutedEventArgs e)
		{
			if (ControlsHelper.AllowLoad(this))
			{
				AppHelper.InitHelp(this);
				UiPresetsManager.InitControl(this, true);
			}
		}
	}
}
