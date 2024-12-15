using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.Configuration;
using JocysCom.ClassLibrary.Controls;
using JocysCom.ClassLibrary.Controls.IssuesControl;
using JocysCom.ClassLibrary.Controls.Themes;
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace JocysCom.VS.AiCompanion.Engine
{
	/// <summary>
	/// Interaction logic for MainControl.
	/// </summary>
	public partial class MainControl : UserControl
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="MainControl"/> class.
		/// </summary>
		public MainControl()
		{
			InitializeComponent();
			UpdateAuthIconPanel();
			if (ControlsHelper.IsDesignMode(this))
				return;
			// Override AppUserData property in replacements.
			AssemblyInfo.Entry.AppUserData = Global.AppData.XmlFile.Directory.FullName;
			SeverityConverter = new SeverityToImageConverter();
			UpdateInfoPanelDefaults();
			var debugVisibility = InitHelper.IsDebug
				? Visibility.Visible
				: Visibility.Collapsed;
			UiThemeLabel.Visibility = debugVisibility;
			UiThemeComboBox.Visibility = debugVisibility;
			// Temporary: Hide "Assistants" feature for release users.
			TeamsTabItem.Visibility = debugVisibility;
			UpdatesTabItem.Visibility = !Global.IsVsExtension
				? Visibility.Visible
				: Visibility.Collapsed;
			Application.Current.MainWindow.Closing += MainWindow_Closing;
			// Subscribe to the application-wide Activated and Deactivated events
			Application.Current.Deactivated += Current_Deactivated;
			UpdateMicrosoftControls();
			Global.AppSettings.PropertyChanged += AppSettings_PropertyChanged;
			InfoForm.MonitorEnabled = Global.AppSettings.EnableShowFormInfo;
			ErrorsTabItem.Visibility = Global.AppSettings.ShowErrorsPanel ? Visibility.Visible : Visibility.Collapsed;
			TutorialHelper.SetupTutorialHelper(this);
			InfoPanel.BusyCount.MouseDown += BusyCount_MouseDown;
		}

		public ThemeType[] UiThemes { get; set; } = (ThemeType[])Enum.GetValues(typeof(ThemeType));

		public void UpdateInfoPanelDefaults()
		{
			var ai = new AssemblyInfo(typeof(MainControl).Assembly);
			InfoPanel.DefaultHead = AppHelper.GetNullIfWhiteSpace(Global.AppSettings.OverrideInfoDefaultHead) ?? ai.GetTitle(true, false, true, false, false);
			InfoPanel.DefaultBody = AppHelper.GetNullIfWhiteSpace(Global.AppSettings.OverrideInfoDefaultBody) ?? ai.Description;
			InfoPanel.Reset();
		}

		private void MainWindow_Closing(object sender, CancelEventArgs e)
		{
			Global.IsMainWindowClosing = true;
		}

		private void BusyCount_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			Global.MoveToWindowToggle();
		}

		private async void AppSettings_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(AppData.EnableShowFormInfo))
				InfoForm.MonitorEnabled = Global.AppSettings.EnableShowFormInfo;
			if (e.PropertyName == nameof(AppData.ShowErrorsPanel))
				ErrorsTabItem.Visibility = Global.AppSettings.ShowErrorsPanel ? Visibility.Visible : Visibility.Collapsed;
			if (e.PropertyName == nameof(AppData.EnableMicrosoftAccount))
				await Helper.Debounce(UpdateMicrosoftControls);
		}

		void UpdateMicrosoftControls()
		{
			AuthIconPanel.Visibility = Global.AppSettings.EnableMicrosoftAccount
			? Visibility.Visible
			: Visibility.Collapsed;
		}


		private void Current_Deactivated(object sender, System.EventArgs e)
		{
			if (!InitHelper.IsDebug)
				Global.SaveSettings();
		}

		private void This_Unloaded(object sender, RoutedEventArgs e)
		{
		}

		private async void This_Loaded(object sender, RoutedEventArgs e)
		{
			if (ControlsHelper.IsDesignMode(this))
				return;
			InfoPanel.RightIcon.MouseDoubleClick += RightIcon_MouseDoubleClick;
			if (ControlsHelper.AllowLoad(this))
			{
				Global.RaiseOnMainControlLoaded();
				Global.MainControl.MainTabControl.SelectedItem = Global.MainControl.TemplatesPanel;
				Global.MainControl.MainTabControl.SelectedItem = Global.MainControl.TasksPanel;
				AppHelper.InitHelp(this);
				UiPresetsManager.InitControl(this, true, new FrameworkElement[] { AuthIconPanel });
				UiPresetsManager.InitControl(InfoPanel);
				try
				{
					await Security.MicrosoftResourceManager.Current.RefreshProfileImage();
				}
				catch (Exception ex)
				{
					Global.MainControl.InfoPanel.SetBodyError(ex.Message);
				}
			}
		}

		private async void RightIcon_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			await Task.Delay(500);
			//if (Global.IsIncompleteSettings())
			//return;
			//var client = new Companions.ChatGPT.Client(Global.AppSettings.OpenAiSettings.BaseUrl);
			//var usage = await client.GetUsageAsync();
			//InfoPanel.SetBodyInfo($"Usage: Total Tokens = {usage}, Total Cost = {usage.AdditionalProperties["current_usage_usd"]:C}");
		}

		SeverityToImageConverter SeverityConverter;

		private async void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (ControlsHelper.IsDesignMode(this))
				return;
			if (e.AddedItems.Count > 0)
			{
				// Workaround: Make ChatPanel visible, which will trigger rendering and freezing XAML UI for moment.
				if (e.AddedItems[0] == TasksTabItem && TasksPanel.TemplateItemPanel.ChatPanel.Visibility != Visibility.Visible)
				{
					// Delay to allow XAML UI to render.
					await Task.Delay(200);
					// Show Web Browser now, to allow data loading and rendering.
					TasksPanel.TemplateItemPanel.ChatPanel.Visibility = Visibility.Visible;
				}
				if (e.AddedItems[0] == TemplatesTabItem && TemplatesPanel.TemplateItemPanel.ChatPanel.Visibility != Visibility.Visible)
				{
					// Delay to allow XAML UI to render.
					await Task.Delay(200);
					// Show Web Browser now, to allow data loading and rendering.
					TemplatesPanel.TemplateItemPanel.ChatPanel.Visibility = Visibility.Visible;
				}
			}
			Global.RaiseOnTabControlSelectionChanged(sender, e);
		}

		private void InfoPanel_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			UpdateAuthIconPanel();
		}

		void UpdateAuthIconPanel()
		{
			AuthIconPanel.Width = InfoPanel.ActualHeight;
			AuthIconPanel.Height = InfoPanel.ActualHeight;
		}

	}
}
