using JocysCom.ClassLibrary.Controls;
using JocysCom.VS.AiCompanion.Plugins.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
			DomainMaxRiskLevelRefresh();
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

		private void This_Loaded(object sender, RoutedEventArgs e)
		{
			if (ControlsHelper.IsDesignMode(this))
				return;
			AppHelper.AddHelp(IsSpellCheckEnabledCheckBox, Engine.Resources.Resources.Enable_spell_check_for_the_chat_textbox);
			AppHelper.AddHelp(ResetUIButton, Engine.Resources.Resources.Reset_UI_Settings_ToolTip);
		}

		System.Windows.Forms.OpenFileDialog _OpenFileDialog;

		private void BrowseSettingsButton_Click(object sender, RoutedEventArgs e)
		{
			var path = JocysCom.ClassLibrary.Configuration.AssemblyInfo.ExpandPath(Global.AppSettings.ConfigurationUrl);
			if (_OpenFileDialog == null)
			{
				_OpenFileDialog = new System.Windows.Forms.OpenFileDialog();
				_OpenFileDialog.SupportMultiDottedExtensions = true;
				_OpenFileDialog.FileName = path;
				DialogHelper.AddFilter(_OpenFileDialog, ".zip");
				DialogHelper.AddFilter(_OpenFileDialog);
				_OpenFileDialog.FilterIndex = 1;
				_OpenFileDialog.RestoreDirectory = true;
			}
			var dialog = _OpenFileDialog;
			dialog.Title = "Open " + JocysCom.ClassLibrary.Files.Mime.GetFileDescription(".zip");
			var result = dialog.ShowDialog();
			if (result != System.Windows.Forms.DialogResult.OK)
				return;
			path = dialog.FileNames[0];
			path = JocysCom.ClassLibrary.Configuration.AssemblyInfo.ParameterizePath(path, true);
			Global.AppSettings.ConfigurationUrl = path;
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
				//var pixRect = PositionSettings.GetPixelsBoundaryRectangle(this);
				//var pixRectWin = PositionSettings.GetPixelsBoundaryRectangle(window);
				var w = Math.Max((double)WindowWidthUpDown.Value, window.MinWidth);
				var h = Math.Max((double)WindowHeightUpDown.Value, window.MinHeight);
				var content = (FrameworkElement)window.Content;
				// Get space taken by the window borders.
				var wSpace = window.ActualWidth - content.ActualWidth;
				var hSpace = window.ActualHeight - content.ActualHeight;
				//var tPad = pixRect.Top - pixRectWin.Top;
				//var lPad = pixRect.Left - pixRectWin.Left;
				//var padPoint = new Point(tPad, lPad);
				var size = new Size(w + wSpace, h + hSpace);
				var point = new Point(window.Left, window.Top);
				var newSize = PositionSettings.ConvertToDiu(size);
				var newPoint = PositionSettings.ConvertToDiu(point);
				//var newPadPoint = PositionSettings.ConvertToDiu(padPoint);
				ps.Left = (int)(newPoint.X / 2 / 3 / 5) * 2 * 3 * 5;
				ps.Top = (int)(newPoint.Y / 2 / 3 / 5) * 2 * 3 * 5;
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

		bool AllowReset(string name, string more = "")
		{
			var text = $"Do you want to reset the {name}?";
			text += string.IsNullOrEmpty(more)
				? $"Please note that this will delete all custom {name}!"
				: more;
			var caption = $"{Global.Info.Product} - Reset {name}";
			var result = MessageBox.Show(text, caption, MessageBoxButton.YesNo, MessageBoxImage.Warning);
			return result == MessageBoxResult.Yes;
		}

		private void ResetApplicationSettingsButton_Click(object sender, RoutedEventArgs e)
		{
			if (!AllowReset("Application Settings, but not Services and Models"))
				return;
			SettingsSourceManager.ResetAppSettings();
		}

		private void ResetTemplatesButton_Click(object sender, RoutedEventArgs e)
		{
			if (!AllowReset("Task Tempaltes"))
				return;
			SettingsSourceManager.ResetTemplates();
		}

		private void ResetPromptingButton_Click(object sender, RoutedEventArgs e)
		{
			if (!AllowReset("Prompting Templates"))
				return;
			Global.PromptItems.ResetToDefault();
			Global.PromptItems.Save();
			Global.TriggerPromptingUpdated();
		}

		private void ApplySettingsButton_Click(object sender, RoutedEventArgs e)
		{
			if (!AllowReset("All Settings", "Please note that this will reset all services, models, templates and tasks!"))
				return;
			SettingsSourceManager.ResetSettings();
		}

		private void ResetListsButton_Click(object sender, RoutedEventArgs e)
		{
			if (!AllowReset("Lists"))
				return;
			SettingsSourceManager.ResetLists();

		}

		private void ResetEmbeddingsButton_Click(object sender, RoutedEventArgs e)
		{
			if (!AllowReset("Embeddings"))
				return;
			SettingsSourceManager.ResetEmbeddings();

		}

		public Dictionary<RiskLevel, string> MaxRiskLevels
		=> ClassLibrary.Runtime.Attributes.GetDictionary(
			((RiskLevel[])Enum.GetValues(typeof(RiskLevel))).Except(new[] { RiskLevel.Unknown }).ToArray());


		private void DomainMaxRiskLevelRefresh(bool cache = true)
		{
			DomainMaxRiskLevelValueLabel.Content = "...";
			var visibility = DomainHelper.IsApplicationRunningOnDomain()
				? Visibility.Visible
				: Visibility.Collapsed;
			DomainMaxRiskLevelRefreshButton.Visibility = visibility;
			DomainMaxRiskLevelNameLabel.Visibility = visibility;
			DomainMaxRiskLevelValueLabel.Visibility = visibility;
			_ = Task.Run(() =>
			{
				var domainMaxRiskLevel = DomainHelper.GetDomainUserMaxRiskLevel(cache);
				var level = domainMaxRiskLevel?.ToString() ?? "N/A";
				Dispatcher.Invoke(() =>
				{
					DomainMaxRiskLevelValueLabel.Content = $"{level}";
				});
			});


		}

		private void DomainMaxRiskLevelRefreshButton_Click(object sender, RoutedEventArgs e)
		{
			DomainMaxRiskLevelRefresh(false);
		}
	}

}
