using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using JocysCom.ClassLibrary.Configuration;
using JocysCom.ClassLibrary.Controls;
using JocysCom.ClassLibrary.Controls.IssuesControl;

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
			SeverityConverter = new SeverityToImageConverter();
			var ai = new AssemblyInfo(typeof(MainControl).Assembly);
			InfoPanel.DefaultHead = ai.GetTitle(true, false, true, false, false);
			InfoPanel.DefaultBody = ai.Description;
			InfoPanel.Reset();
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
				if (e.AddedItems[0] == TasksTabItem && TasksPanel.ItemPanel.ChatPanel.Visibility != Visibility.Visible)
				{
					// Delay to allow XAML UI to render.
					await Task.Delay(200);
					// Show Web Browser now, to allow data loading and rendering.
					TasksPanel.ItemPanel.ChatPanel.Visibility = Visibility.Visible;
				}
				if (e.AddedItems[0] == TemplatesTabItem && TemplatesPanel.ItemPanel.ChatPanel.Visibility != Visibility.Visible)
				{
					// Delay to allow XAML UI to render.
					await Task.Delay(200);
					// Show Web Browser now, to allow data loading and rendering.
					TemplatesPanel.ItemPanel.ChatPanel.Visibility = Visibility.Visible;
				}
			}
		}
	}
}
