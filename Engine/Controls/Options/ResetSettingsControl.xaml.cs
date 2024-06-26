using JocysCom.ClassLibrary.Controls;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace JocysCom.VS.AiCompanion.Engine.Controls.Options
{
	/// <summary>
	/// Interaction logic for VaultItemControl.xaml
	/// </summary>
	public partial class ResetSettingsControl : UserControl, INotifyPropertyChanged
	{
		public ResetSettingsControl()
		{
			InitializeComponent();
			if (ControlsHelper.IsDesignMode(this))
				return;
		}




		private void ResetApplicationSettingsButton_Click(object sender, RoutedEventArgs e)
		{
			if (!AppHelper.AllowReset("Application Settings, but not Services and Models"))
				return;
			SettingsSourceManager.ResetAppSettings();
		}

		private void ResetTemplatesButton_Click(object sender, RoutedEventArgs e)
		{
			if (!AppHelper.AllowReset("Task Tempaltes"))
				return;
			SettingsSourceManager.ResetTemplates();
		}

		private void ResetPromptingButton_Click(object sender, RoutedEventArgs e)
		{
			if (!AppHelper.AllowReset("Prompting Templates"))
				return;
			Global.PromptItems.ResetToDefault();
			Global.PromptItems.Save();
			Global.TriggerPromptingUpdated();
		}

		private void ResetVoicesButton_Click(object sender, RoutedEventArgs e)
		{
			if (!AppHelper.AllowReset("Avatar Voices"))
				return;
			Global.Voices.ResetToDefault();
			Global.Voices.Save();
			Global.TriggerVoicesUpdated();
		}

		/// <summary>
		/// Use to make screenshots.
		/// </summary>
		private void ResetUIButton_Click(object sender, RoutedEventArgs e)
		{
			var items = Global.AppSettings.PanelSettingsList.ToArray();
			foreach (var item in items)
				ClassLibrary.Runtime.Attributes.ResetPropertiesToDefault(item);
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

		private void ResetListsButton_Click(object sender, RoutedEventArgs e)
		{
			if (!AppHelper.AllowReset("Lists"))
				return;
			SettingsSourceManager.ResetLists();

		}

		private void ResetEmbeddingsButton_Click(object sender, RoutedEventArgs e)
		{
			if (!AppHelper.AllowReset("Embeddings"))
				return;
			SettingsSourceManager.ResetEmbeddings();

		}

		private void ResetServicesButton_Click(object sender, RoutedEventArgs e)
		{
			if (!AppHelper.AllowReset("Services and Models"))
				return;
			SettingsSourceManager.ResetServicesAndModels();
			Global.RaiseOnAiServicesUpdated();
			Global.RaiseOnAiModelsUpdated();
		}

		private void This_Loaded(object sender, RoutedEventArgs e)
		{
			if (ControlsHelper.IsDesignMode(this))
				return;
			AppHelper.AddHelp(ResetUIButton, Engine.Resources.MainResources.main_Reset_UI_Settings_ToolTip);
		}

		#region ■ INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		#endregion

	}
}
