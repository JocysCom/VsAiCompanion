﻿using JocysCom.ClassLibrary.Configuration;
using JocysCom.ClassLibrary.Controls;
using JocysCom.VS.AiCompanion.Engine.Settings;
using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Windows;

namespace JocysCom.VS.AiCompanion.Engine
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		public MainWindow()
		{
			if (ControlsHelper.IsDesignMode(this))
				return;
			Global.GetEnvironmentProperties = AppHelper.GetEnvironmentProperties;
			Plugins.Core.ScreenshotHelper.GetTempFolderPath = AppHelper.GetTempFolderPath;
			ControlsHelper.InitInvokeContext();
			Topmost = Global.AppSettings.AppAlwaysOnTop;
			Global.AppSettings.PropertyChanged += AppSettings_PropertyChanged;
			InitializeComponent();
			//MainPanel.InfoPanel.BodyMaxLength = int.MaxValue;
			Global.MainControl = MainPanel;
			var ai = new AssemblyInfo(typeof(MainControl).Assembly);
			Title = ai.GetTitle(true, false, true, false, false);
			SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;
		}

		private void SystemEvents_DisplaySettingsChanged(object sender, EventArgs e)
		{
			// Adjust window position if it's outside the virtual screen bounds
			Global.AppSettings.StartPosition.SavePosition(this);
			Global.AppSettings.StartPosition.LoadPosition(this);
		}

		private void AppSettings_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(AppData.AppAlwaysOnTop))
				Topmost = Global.AppSettings.AppAlwaysOnTop;
		}

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			Global.AppSettings.StartPosition.SavePosition(this);
			Global.SaveSettings();
		}

		private void Window_SourceInitialized(object sender, System.EventArgs e)
		{
			var ps = Global.AppSettings.StartPosition;
			// Workaround: If Window position was completely minimized.
			if (ps.Height < 40)
				SettingsSourceManager.ResetUIMainWindow();
			Global.AppSettings.StartPosition.LoadPosition(this);
		}

		protected override void OnSourceInitialized(EventArgs e)
		{
			base.OnSourceInitialized(e);
			var source = (System.Windows.Interop.HwndSource)PresentationSource.FromVisual(this);
			source.AddHook(StartHelper.CustomWndProc);
		}
	}
}
