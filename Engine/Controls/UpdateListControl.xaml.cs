using JocysCom.ClassLibrary.Controls;
using System.Windows;
using System.Windows.Controls;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	/// <summary>
	/// Interaction logic for UpdatesControl.xaml
	/// </summary>
	public partial class UpdateListControl : UserControl
	{
		public UpdateListControl()
		{
			InitializeComponent();
			if (ControlsHelper.IsDesignMode(this))
				return;
			PandocTabItem.Visibility = InitHelper.IsDebug
				? Visibility.Visible
				: Visibility.Collapsed;
			if (!Global.IsVsExtension)
			{
				MainUpdateItemPanel.InitMain();
				if (InitHelper.IsDebug)
					MainUpdateItemPanel.InitPandoc();
			}
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
