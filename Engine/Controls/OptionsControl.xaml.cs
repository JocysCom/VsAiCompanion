using JocysCom.ClassLibrary.Controls;
using System.Windows;
using System.Windows.Controls;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{

	public partial class OptionsControl : UserControl
	{
		public OptionsControl()
		{
			InitializeComponent();
			if (ControlsHelper.IsDesignMode(this))
				return;
			SettingsFolderTextBox.Text = Global.AppData.XmlFile.Directory.FullName;
		}

		private void OpenButton_Click(object sender, RoutedEventArgs e)
			=> ControlsHelper.OpenUrl(Global.AppData.XmlFile.Directory.FullName);

		private void OptionsTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			Global.RaiseOnTabControlSelectionChanged(sender, e);
		}

		private void This_Loaded(object sender, RoutedEventArgs e)
		{
			if (ControlsHelper.AllowLoad(this))
			{
				AppHelper.InitHelp(this);
				UiPresetsManager.InitControl(this, true);
			}
		}
	}

}
