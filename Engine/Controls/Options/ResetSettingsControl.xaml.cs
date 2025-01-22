using JocysCom.ClassLibrary.Controls;
using JocysCom.VS.AiCompanion.Engine.Settings;
using System.ComponentModel;
using System.Linq;
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

		private void ResetAllSettingsButton_Click(object sender, RoutedEventArgs e)
		{
			SettingsSourceManager.ResetAllSettings(true);
		}

		private void ResetApplicationSettingsButton_Click(object sender, RoutedEventArgs e)
		{
			if (!AppHelper.AllowReset("Application Settings"))
				return;
			SettingsSourceManager.ResetAppSettings();
		}

		private void ResetTasksButton_Click(object sender, RoutedEventArgs e)
		{
			if (!AppHelper.AllowReset("Tasks"))
				return;
			SettingsSourceManager.ResetTasks();
		}

		private void ResetTemplatesButton_Click(object sender, RoutedEventArgs e)
		{
			if (!AppHelper.AllowReset("Tempaltes"))
				return;
			SettingsSourceManager.ResetTemplates();
		}

		private void ResetPromptsButton_Click(object sender, RoutedEventArgs e)
		{
			if (!AppHelper.AllowReset("Prompts"))
				return;
			SettingsSourceManager.ResetPrompts();
			Global.Prompts.Save();
			Global.TriggerPromptingUpdated();
		}

		private void ResetResetsButton_Click(object sender, RoutedEventArgs e)
		{
			if (!AppHelper.AllowReset("Resets"))
				return;
			SettingsSourceManager.ResetResets();
			Global.Resets.Save();
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
			SettingsSourceManager.ResetUI();
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

		private void ResetUiPresetsButton_Click(object sender, RoutedEventArgs e)
		{
			if (!AppHelper.AllowReset("UI Presets"))
				return;
			SettingsSourceManager.ResetUiPresets();
		}

		private void ResetServicesButton_Click(object sender, RoutedEventArgs e)
		{
			if (!AppHelper.AllowReset("Services and Models"))
				return;
			SettingsSourceManager.ResetServicesAndModels();
			Global.RaiseOnAiServicesUpdated();
			Global.RaiseOnAiModelsUpdated();
		}

		private async void This_Loaded(object sender, RoutedEventArgs e)
		{
			if (ControlsHelper.IsDesignMode(this))
				return;
			if (ControlsHelper.AllowLoad(this))
			{
				AppHelper.InitHelp(this);
				UiPresetsManager.InitControl(this, true);
				UpdateInstructionsPanel.ValueType = typeof(UpdateInstruction);
				await UpdateInstructionsPanel.BindData(Global.Resets.Items.FirstOrDefault());
			}
		}

		private void ResetSettingsWithInstructions_Click(object sender, RoutedEventArgs e)
		{
			SettingsSourceManager.ResetWithInstructions(true);
		}

		#region ■ INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		#endregion

	}
}
