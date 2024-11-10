using JocysCom.ClassLibrary.Controls;
using JocysCom.ClassLibrary.Controls.UpdateControl;
using System;
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

			var appExeAssembly = System.Reflection.Assembly.GetEntryAssembly();
			if (Global.IsVsExtension)
			{
				MainTabItem.Visibility = System.Windows.Visibility.Collapsed;
			}
			else
			{
				var mainUuc = new UpdateUserControl();
				var us = Global.AppSettings.UpdateSettings;
				us.UpdateMissingDefaults(appExeAssembly);
				//mainUuc.Checker = Global.AiCompUpdateChecker;

				mainUuc.DownloadTempFolder = AppHelper.GetTempFolderPath();
				mainUuc.EnableReplace = true;
				mainUuc.EnableRestart = true;
				mainUuc.Settings = us;
				//mainUuc.Checker.AddTask += UpdatesPanel_AddTask;
				mainUuc.RemoveTask += UpdatesPanel_RemoveTask;
				MainTabItem.Content = mainUuc;
				mainUpdateUserControl = mainUuc;
				Global.AiCompUpdateTimeChecker.UpdateRequired += mainUuc_UpdateTimeControl_UpdateRequired;
			}
			if (InitHelper.IsDebug)
			{
				var pandocUuc = new UpdateUserControl();
				var pdUs = Global.AppSettings.PandocUpdateSettings;
				UpdateMissingPandocDefaults(pdUs);
				pandocUuc.DownloadTempFolder = AppHelper.GetTempFolderPath();
				pandocUuc.Settings = pdUs;
				pandocUuc.AddTask += UpdatesPanel_AddTask;
				pandocUuc.RemoveTask += UpdatesPanel_RemoveTask;
				PandocTabItem.Content = pandocUuc;
				pandocUpdateUserControl = pandocUuc;
				Global.PanDocUpdateTimeChecker.UpdateRequired += pandocUuc_UpdateTimeControl_UpdateRequired;
			}
		}

		UpdateUserControl mainUpdateUserControl;
		UpdateUserControl pandocUpdateUserControl;

		private async void mainUuc_UpdateTimeControl_UpdateRequired(object sender, EventArgs e)
		{
			// Start the update check process.
			await mainUpdateUserControl.StartUpdateCheckAsync();
			// Optionally, if you want to automatically install updates, call the install process.
			// await UpdateControl.StartUpdateInstallAsync();
		}

		private async void pandocUuc_UpdateTimeControl_UpdateRequired(object sender, EventArgs e)
		{
			// Start the update check process.
			await pandocUpdateUserControl.StartUpdateCheckAsync();
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
