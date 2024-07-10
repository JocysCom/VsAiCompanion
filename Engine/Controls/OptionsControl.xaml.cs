using JocysCom.ClassLibrary;
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
			UpdateMicrosoftControls();
			Global.AppSettings.PropertyChanged += AppSettings_PropertyChanged;
		}

		private async void AppSettings_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(AppData.EnableMicrosoftAccount))
				await Helper.Delay(UpdateMicrosoftControls);
		}

		void UpdateMicrosoftControls()
		{
			MicrosoftAccountsTabItem.Visibility = Global.AppSettings.EnableMicrosoftAccount
				? Visibility.Visible
				: Visibility.Collapsed;
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
				UiPresetsManager.InitControl(this, true, new FrameworkElement[] { MicrosoftAccountsTabItem });
			}
		}
	}

}
