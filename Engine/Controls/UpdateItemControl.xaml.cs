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
	public partial class UpdateItemControl : UserControl
	{
		public UpdateItemControl()
		{
			InitializeComponent();
			if (ControlsHelper.IsDesignMode(this))
				return;
			var debugVisibility = InitHelper.IsDebug
				? Visibility.Visible
				: Visibility.Collapsed;
			UpdateTimePanel.Visibility = debugVisibility;
		}

		public void InitMain()
		{
			var appExeAssembly = System.Reflection.Assembly.GetEntryAssembly();
			var timeChecker = Global.MainCompUpdateTimeChecker;
			var checker = Global.MainCompUpdateChecker;
			checker.Settings.UpdateMissingDefaults(appExeAssembly);
			checker.DownloadTempFolder = AppHelper.GetTempFolderPath();
			checker.EnableReplace = true;
			checker.EnableRestart = true;
			timeChecker.UpdateRequired += mainUuc_UpdateTimeControl_UpdateRequired;
			BindData(timeChecker, checker);
		}

		public void InitPandoc()
		{
			var timeChecker = Global.PanDocUpdateTimeChecker;
			var checker = Global.PanDocUpdateChecker;
			checker.DownloadTempFolder = AppHelper.GetTempFolderPath();
			timeChecker.UpdateRequired += pandocUuc_UpdateTimeControl_UpdateRequired;
			BindData(timeChecker, checker);
		}

		public void BindData(UpdateTimeChecker timeChecker, UpdateChecker checker)
		{
			checker.AddTask += UpdatesPanel_AddTask;
			checker.RemoveTask += UpdatesPanel_RemoveTask;
			// Bind data to control.
			UpdateTimePanel.Settings = timeChecker.Settings;
			// Bind data to control.
			UpdatePanel.Checker = checker;
			UpdatePanel.Settings = checker.Settings;
		}

		private async void mainUuc_UpdateTimeControl_UpdateRequired(object sender, EventArgs e)
		{

			// Start the update check process.
			await Global.MainCompUpdateChecker.StartUpdateCheckAsync();
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
