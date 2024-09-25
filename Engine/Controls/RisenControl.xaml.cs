using JocysCom.ClassLibrary.Controls;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	/// <summary>
	/// Interaction logic for RisenControl.xaml
	/// </summary>
	public partial class RisenControl : UserControl
	{
		public RisenControl()
		{
			InitializeComponent();
		}

		private void This_Loaded(object sender, RoutedEventArgs e)
		{
			if (ControlsHelper.IsDesignMode(this))
				return;
			if (ControlsHelper.AllowLoad(this))
			{
				AppHelper.InitHelp(this);
				UiPresetsManager.InitControl(this, true);
			}
		}

		private string ConstructPrompt()
		{
			var role = RoleTextBox.Text.Trim();
			var instructions = InstructionsTextBox.Text.Trim();
			var steps = StepsTextBox.Text.Trim();
			var endGoal = EndGoalTextBox.Text.Trim();
			var narrowing = NarrowingTextBox.Text.Trim();
			var promptTemplate = (string)FindResource("prompt_Template");
			// Create a dictionary for placeholders and values
			var placeholders = new Dictionary<string, string>
			{
				{ "{Role}", !string.IsNullOrWhiteSpace(role) ? role : "Assistant" },
				{ "{Instructions}", instructions },
				{ "{Steps}", steps },
				{ "{EndGoal}", endGoal },
				{ "{Narrowing}", narrowing }
			};
			var prompt = promptTemplate;
			foreach (var placeholder in placeholders)
			{
				prompt = prompt.Replace(placeholder.Key, placeholder.Value);
			}
			return prompt;
		}

	}
}
