using JocysCom.ClassLibrary.Controls;
using System.Windows.Controls;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	/// <summary>
	/// Interaction logic for PluginItemControl.xaml
	/// </summary>
	public partial class PluginItemControl : UserControl
	{
		public PluginItemControl()
		{
			InitializeComponent();
			if (ControlsHelper.IsDesignMode(this))
				return;
		}

		private void This_Loaded(object sender, System.Windows.RoutedEventArgs e)
		{
			if (ControlsHelper.AllowLoad(this))
			{
				AppHelper.InitHelp(this);
			}
		}
	}
}
