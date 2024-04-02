using JocysCom.ClassLibrary.Controls;
using System;
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
			UpdateSpellCheck();
		}

		private void AppSettings_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				case nameof(AppData.AllowOnlyOneCopy):
					// Make sure that AllowOnlyOneCopy setting applies immediatelly.
					Global.AppData.Save();
					break;
				case nameof(AppData.IsSpellCheckEnabled):
					UpdateSpellCheck();
					break;
				default:
					break;
			}
		}

		void UpdateSpellCheck()
		{
			var isEnabled = Global.AppSettings.IsSpellCheckEnabled;
			SpellCheck.SetIsEnabled(ContextDataTitleTextBox, isEnabled);
			SpellCheck.SetIsEnabled(ContextFileTitleTextBox, isEnabled);
			SpellCheck.SetIsEnabled(ContextChatInstructionsTextBox, isEnabled);
			SpellCheck.SetIsEnabled(ContextChatTitleTextBox, isEnabled);
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
			SettingsSourceManager.ResetAppSettings();
		}

		private void ResetTemplatesButton_Click(object sender, RoutedEventArgs e)
		{
			var text = $"Do you want to reset the templates? Please note that this will delete all custom templates!";
			var caption = $"{Global.Info.Product} - Reset Templates";
			var result = MessageBox.Show(text, caption, MessageBoxButton.YesNo, MessageBoxImage.Warning);
			if (result != MessageBoxResult.Yes)
				return;
			SettingsSourceManager.ResetTemplates();
		}

		private void ResetPromptingButton_Click(object sender, RoutedEventArgs e)
		{
			var text = $"Do you want to reset the prompting templates? Please note that this will delete all custom prompting templates!";
			var caption = $"{Global.Info.Product} - Reset Prompting Templates";
			var result = MessageBox.Show(text, caption, MessageBoxButton.YesNo, MessageBoxImage.Warning);
			if (result != MessageBoxResult.Yes)
				return;
			Global.PromptItems.ResetToDefault();
			Global.PromptItems.Save();
			Global.TriggerPromptingUpdated();
		}

		private void This_Loaded(object sender, RoutedEventArgs e)
		{
			if (ControlsHelper.IsDesignMode(this))
				return;
			AppHelper.AddHelp(IsSpellCheckEnabledCheckBox, Engine.Resources.Resources.Enable_spell_check_for_the_chat_textbox);
			AppHelper.AddHelp(ResetUIButton, Engine.Resources.Resources.Reset_UI_Settings_ToolTip);
		}

		private void ApplySettingsButton_Click(object sender, RoutedEventArgs e)
		{
			var text = $"Do you want to reset settings? Please note that this will reset all services, models, templates and tasks!";
			var caption = $"{Global.Info.Product} - Reset Settings";
			var result = MessageBox.Show(text, caption, MessageBoxButton.YesNo, MessageBoxImage.Warning);
			if (result != MessageBoxResult.Yes)
				return;
			SettingsSourceManager.ResetSettings();
		}

		System.Windows.Forms.OpenFileDialog _OpenFileDialog = new System.Windows.Forms.OpenFileDialog();

		private void BrowseSettingsButton_Click(object sender, RoutedEventArgs e)
		{
			var dialog = _OpenFileDialog;
			dialog.SupportMultiDottedExtensions = true;
			DialogHelper.AddFilter(dialog, ".zip");
			DialogHelper.AddFilter(dialog);
			dialog.FilterIndex = 1;
			dialog.RestoreDirectory = true;
			dialog.Title = "Open " + JocysCom.ClassLibrary.Files.Mime.GetFileDescription(".zip");
			var result = dialog.ShowDialog();
			if (result != System.Windows.Forms.DialogResult.OK)
				return;
			Global.AppSettings.ConfigurationUrl = dialog.FileNames[0];
		}

		/// <summary>
		/// Use to make screenshots.
		/// </summary>
		private void ResetUIButton_Click(object sender, RoutedEventArgs e)
		{
			ClassLibrary.Runtime.Attributes.ResetPropertiesToDefault(Global.AppSettings.TaskData);
			ClassLibrary.Runtime.Attributes.ResetPropertiesToDefault(Global.AppSettings.TemplateData);
			ClassLibrary.Runtime.Attributes.ResetPropertiesToDefault(Global.AppSettings.FineTuningData);
			ClassLibrary.Runtime.Attributes.ResetPropertiesToDefault(Global.AppSettings.AssistantData);
			ClassLibrary.Runtime.Attributes.ResetPropertiesToDefault(Global.AppSettings.ListsData);
			ClassLibrary.Runtime.Attributes.ResetPropertiesToDefault(Global.AppSettings.EmbeddingsData);
			var ps = Global.AppSettings.StartPosition;
			if (!Global.IsVsExtension)
			{
				var window = ControlsHelper.GetParent<Window>(this);
				var w = Math.Max((double)WindowWidthUpDown.Value, window.MinWidth);
				var h = Math.Max((double)WindowHeightUpDown.Value, window.MinHeight);
				var content = (FrameworkElement)window.Content;
				// Get space taken by the window borders.
				var wSpace = window.ActualWidth - content.ActualWidth;
				var hSpace = window.ActualHeight - content.ActualHeight;
				var size = new Size(w + wSpace, h + hSpace);
				var newSize = PositionSettings.ConvertToDiu(size);
				ps.Left = Math.Round(ps.Left / 2 / 3 / 5) * 2 * 3 * 5;
				ps.Top = Math.Round(ps.Top / 2 / 3 / 5) * 2 * 3 * 5;
				ps.Width = newSize.Width;
				ps.Height = newSize.Height;
				ps.LoadPosition(window);
			}
		}

		/// <summary>
		/// Adjusts the provided dimension to the nearest perfect size for screenshots, 
		/// meeting the criteria of divisibility by 2, 3, 4, and 10.
		/// </summary>
		/// <param name="value">The original size of the screenshot dimension 
		/// (width or height) to be adjusted.</param>
		/// <returns>The adjusted size, meeting the criteria of being a multiple of 2, 3, 4, and 10 
		/// for optimal resizing quality.</returns>
		public static int AdjustForScreenshot(int value)
		{
			// The LCM of 2, 3, 4, and 10 to ensure scaling and quality criteria
			const int perfectDivisor = 60;
			// If the value already meets the perfect criteria then return.
			if (value % perfectDivisor == 0)
				return value;
			// Calculate the nearest higher multiple of 60
			int adjustedValue = ((value / perfectDivisor) + 1) * perfectDivisor;
			return adjustedValue;
		}

		private void AdjustUIButton_Click(object sender, RoutedEventArgs e)
		{
			WindowWidthUpDown.Value = AdjustForScreenshot((int)WindowWidthUpDown.Value);
			WindowHeightUpDown.Value = AdjustForScreenshot((int)WindowHeightUpDown.Value);
			ResetUIButton_Click(null, null);
		}
	}

}
