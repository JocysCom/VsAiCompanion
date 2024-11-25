using JocysCom.VS.AiCompanion.Extension.Controls;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Shell;

namespace JocysCom.VS.AiCompanion.Extension
{
	[VisualStudioContribution]
	public class MainWindowWindow : ToolWindowPane // Change IWindowContent to ToolWindowPane
	{
		private readonly SplashScreenControl _control;

		public MainWindowWindow()
		{
			_control = new SplashScreenControl();
			this.Content = _control; // Set the content of the ToolWindowPane
		}

	}

}
