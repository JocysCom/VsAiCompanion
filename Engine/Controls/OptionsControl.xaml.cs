using JocysCom.ClassLibrary.Controls;
using System;
using System.Linq;
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
			Global.AppSettings.PropertyChanged += AppSettings_PropertyChanged;
			StartWithWindowsStateBox.ItemsSource = Enum.GetValues(typeof(WindowState));
			SettingsFolderTextBox.Text = Global.AppData.XmlFile.Directory.FullName;
		}

		private void AppSettings_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			// Make sure that AllowOnlyOneCopy setting applies immediatelly.
			if (e.PropertyName == nameof(AppData.AllowOnlyOneCopy))
				Global.AppData.Save();
		}

		private void OpenButton_Click(object sender, RoutedEventArgs e)
			=> ControlsHelper.OpenUrl(Global.AppData.XmlFile.Directory.FullName);

		private void ResetApplicationSettingsButton_Click(object sender, RoutedEventArgs e)
		{
			var text = $"Do you want to reset the application settings?";
			var caption = $"{Global.Info.Product} - Reset Application Settings";
			var result = MessageBox.Show(text, caption, MessageBoxButton.YesNo, MessageBoxImage.Question);
			if (result != MessageBoxResult.Yes)
				return;
			Global.ResetAppSettings();
		}

		private void ResetTemplatesButton_Click(object sender, RoutedEventArgs e)
		{
			var text = $"Do you want to reset the templates? Please note that this will delete all custom templates!";
			var caption = $"{Global.Info.Product} - Reset Templates";
			var result = MessageBox.Show(text, caption, MessageBoxButton.YesNo, MessageBoxImage.Warning);
			if (result != MessageBoxResult.Yes)
				return;
			Global.ResetTemplates();
		}
	}

}
