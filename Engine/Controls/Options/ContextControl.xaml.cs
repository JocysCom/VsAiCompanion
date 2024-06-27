using JocysCom.ClassLibrary.Controls;
using System.Windows.Controls;

namespace JocysCom.VS.AiCompanion.Engine.Controls.Options
{
	/// <summary>
	/// Interaction logic for ContextControl.xaml
	/// </summary>
	public partial class ContextControl : UserControl
	{
		public ContextControl()
		{
			InitializeComponent();
			if (ControlsHelper.IsDesignMode(this))
				return;
			Global.AppSettings.PropertyChanged += AppSettings_PropertyChanged;
			UpdateSpellCheck();
		}

		private void AppSettings_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
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

		private void This_Loaded(object sender, System.Windows.RoutedEventArgs e)
		{
			if (ControlsHelper.AllowLoad(this))
			{
				AppHelper.InitHelp(this);
			}
		}
	}
}
