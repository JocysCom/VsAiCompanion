using JocysCom.ClassLibrary.Configuration;
using JocysCom.ClassLibrary.Controls;
using JocysCom.ClassLibrary.Controls.IssuesControl;
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
			if (ControlsHelper.IsDesignMode(this))
				return;
			SeverityConverter = new SeverityToImageConverter();
			var ai = new AssemblyInfo(typeof(MainControl).Assembly);
			InfoPanel.DefaultHead = ai.GetTitle(true, false, true, false, false);
			InfoPanel.DefaultBody = ai.Description;
			InfoPanel.Reset();
			// Temporary: Hide "Assistants" feature for release users.
			AssistantsTabItem.Visibility = InitHelper.IsDebug
				? Visibility.Visible
				: Visibility.Collapsed;
			UpdatesTabItem.Visibility = InitHelper.IsDebug && !Global.IsVsExtension
				? Visibility.Visible
				: Visibility.Collapsed;
			UpdatesPanel.GitHubCompany = "JocysCom";
			UpdatesPanel.GitHubProduct = "VsAiCompanion";
			UpdatesPanel.GitHubAssetName = "JocysCom.VS.AiCompanion.App.zip";
			UpdatesPanel.FileNameInsideZip = "JocysCom.VS.AiCompanion.App.exe";
			UpdatesPanel.UpdateFileFullName = new AssemblyInfo().AssemblyPath;
			UpdatesPanel.AddTask += UpdatesPanel_AddTask;
			UpdatesPanel.RemoveTask += UpdatesPanel_RemoveTask;
			// Subscribe to the application-wide Activated and Deactivated events
			Application.Current.Deactivated += Current_Deactivated;
		}

		private void Current_Deactivated(object sender, System.EventArgs e)
		{
			Global.SaveSettings();
		}

		private void UpdatesPanel_AddTask(object sender, System.EventArgs e)
		{
			Global.MainControl.InfoPanel.AddTask(e);
		}

		private void UpdatesPanel_RemoveTask(object sender, System.EventArgs e)
		{
			Global.MainControl.InfoPanel.RemoveTask(e);
		}

		private void MainWindowPanel_Unloaded(object sender, RoutedEventArgs e)
		{
		}

		private void MainWindowPanel_Loaded(object sender, RoutedEventArgs e)
		{
			InfoPanel.RightIcon.MouseDoubleClick += RightIcon_MouseDoubleClick;
			if (ControlsHelper.AllowLoad(this))
			{
				Global.MainControl.MainTabControl.SelectedItem = Global.MainControl.TemplatesPanel;
				Global.MainControl.MainTabControl.SelectedItem = Global.MainControl.TasksPanel;
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
		}
	}
}
