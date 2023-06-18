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
			StartWithWindowsStateBox.ItemsSource = Enum.GetValues(typeof(WindowState));
			SettingsFolderTextBox.Text = Global.AppData.XmlFile.Directory.FullName;
			OpenAiApiSecretKeyPasswordBox.Password = Global.AppSettings.OpenAiSettings.ApiSecretKey;
			OpenAiApiSecretKeyPasswordBox.PasswordChanged += ChatGptSecretKeyPasswordBox_PasswordChanged;
			OpenAiApiOrganizationPasswordBox.Password = Global.AppSettings.OpenAiSettings.ApiOrganizationId;
			OpenAiApiOrganizationPasswordBox.PasswordChanged += OpenAiApiOrganizationPasswordBox_PasswordChanged;
		}

		public Companions.ChatGPT.Settings OpenAiSettings => Global.AppSettings.OpenAiSettings;

		private void ChatGptSecretKeyPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
			=> Global.AppSettings.OpenAiSettings.ApiSecretKey = OpenAiApiSecretKeyPasswordBox.Password;

		private void OpenAiApiOrganizationPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
			=> Global.AppSettings.OpenAiSettings.ApiOrganizationId = OpenAiApiOrganizationPasswordBox.Password;

		private void OpenButton_Click(object sender, RoutedEventArgs e)
			=> ControlsHelper.OpenUrl(Global.AppData.XmlFile.Directory.FullName);

		private void ResetApplicationSettingsButton_Click(object sender, RoutedEventArgs e)
		{
			var text = $"Do you want to reset the application settings?";
			var caption = $"{Global.Info.Product} - Reset Application Settings";
			var result = MessageBox.Show(text, caption, MessageBoxButton.YesNo, MessageBoxImage.Question);
			if (result != MessageBoxResult.Yes)
				return;
			var settings = Global.AppSettings;
			var exclude = new string[] { nameof(AppData.OpenAiSettings) };
			JocysCom.ClassLibrary.Runtime.Attributes.ResetPropertiesToDefault(settings, false, exclude);
		}

		private void ResetTemplatesButton_Click(object sender, RoutedEventArgs e)
		{
			var text = $"Do you want to reset the templates? Please note that this will delete all custom templates!";
			var caption = $"{Global.Info.Product} - Reset Templates";
			var result = MessageBox.Show(text, caption, MessageBoxButton.YesNo, MessageBoxImage.Warning);
			if (result != MessageBoxResult.Yes)
				return;
			var items = Global.Templates.Items.ToArray();
			foreach (var item in items)
				Global.Templates.DeleteItem(item);
			Global.Templates.Load();
		}
	}

}
