using JocysCom.ClassLibrary.Controls;
using System;
using System.Windows;
using System.Windows.Controls;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	/// <summary>
	/// Interaction logic for CodeBlockControl.xaml
	/// </summary>
	public partial class CodeBlockControl : UserControl
	{
		public CodeBlockControl()
		{
			InitializeComponent();
			if (ControlsHelper.IsDesignMode(this))
				return;
			MarkdownLanguageNameComboBox.ItemsSource = MarkdownHelper.MarkdownLanguageNames.Split(',');
		}

		private void This_Loaded(object sender, RoutedEventArgs e)
		{
			if (ControlsHelper.AllowLoad(this))
			{
				var codeButtons = ControlsHelper.GetAll<Button>(CodeButtonsPanel);
				foreach (var codeButton in codeButtons)
				{
					var languageDisplayName = codeButton.ToolTip;
					codeButton.ToolTip = $"Paste {languageDisplayName} code block";
					AppHelper.AddHelp(codeButton,
						$"Wrap selection into `{languageDisplayName}` code block. Hold CTRL to paste from your clipboard as an `{languageDisplayName}` code block."
					);
				}
				AppHelper.InitHelp(this);
				UiPresetsManager.InitControl(this, true);
			}
		}

		public Func<TextBox> GetFocused;

		private void CodeButton_Click(object sender, RoutedEventArgs e)
		{
			var isCtrlDown =
				System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.LeftCtrl) ||
				System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.RightCtrl);
			var button = (Button)sender;
			var language = button.Tag as string;
			if (language == "Custom")
				language = MarkdownLanguageNameComboBox.SelectedItem as string ?? "";
			if (string.IsNullOrEmpty(language))
				return;
			var box = GetFocused();
			var caretIndex = box.CaretIndex;
			var clipboardText = isCtrlDown
				? JocysCom.ClassLibrary.Text.Helper.RemoveIdent(Global.GetClipboard()?.ContentData ?? "")
				: $"{box.SelectedText}";
			var prefix = "";
			// Add new line if caret is not on the new line.
			if (caretIndex > 0 && box.Text[caretIndex - 1] != '\n')
				prefix += $"\r\n";
			// Add new line if caret is not at the end of the line.
			var suffix = "";
			if (caretIndex < box.Text.Length && box.Text[caretIndex] != '\r')
				suffix += $"\r\n";
			var block = MarkdownHelper.CreateMarkdownCodeBlock(clipboardText, language);
			var text = $"{prefix}{block}{suffix}";
			AppHelper.InsertText(box, text, true, false);
			var newIndex = string.IsNullOrEmpty(clipboardText)
				? caretIndex + prefix.Length
				: caretIndex + text.Length;
			AppHelper.SetCaret(box, newIndex);
		}

	}
}
