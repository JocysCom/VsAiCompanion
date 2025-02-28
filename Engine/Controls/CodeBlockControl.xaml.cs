using JocysCom.ClassLibrary.Controls;
using JocysCom.VS.AiCompanion.Plugins.Core;
using NPOI.HSSF.Record.Aggregates;
using SharpVectors.Converters;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Resources;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	/// <summary>
	/// Interaction logic for CodeBlockControl.xaml.
	/// This control dynamically generates code block buttons from a configurable list of language definitions.
	/// </summary>
	public partial class CodeBlockControl : UserControl
	{
		/// <summary>
		/// Gets or sets the list of languages used to dynamically generate code block buttons.
		/// Each item contains a language abbreviation (Key), a color (Value), a tooltip (Comment), and a markdown tag (Tags).
		/// </summary>
		public System.Collections.Generic.List<ListItem> LanguageItems { get; set; }

		/// <summary>
		/// Provides a delegate that returns the currently focused TextBox.
		/// </summary>
		public Func<TextBox> GetFocused;

		/// <summary>
		/// Initializes a new instance of the <see cref="CodeBlockControl"/> class.
		/// In design mode, initialization is halted. Otherwise, the markdown language selector and default language items are set up.
		/// </summary>
		public CodeBlockControl()
		{
			InitializeComponent();
			if (ControlsHelper.IsDesignMode(this))
				return;

			// Initialize the ComboBox with a comma–separated list of markdown language names.
			MarkdownLanguageNameComboBox.ItemsSource = MarkdownHelper.MarkdownLanguageNames.Split(',');

			// Set default language items.
			LanguageItems = new System.Collections.Generic.List<ListItem>
			{
				new ListItem { Key = "C#", Value = "C#;#60AF1F", Comment = "Paste C# Code", Tags = "CSharp" },
				new ListItem { Key = "SQL", Value = "SQL;#FF9900", Comment = "Paste SQL Code", Tags = "SQL" },
				new ListItem { Key = "XML", Value = "XML;#3366CC", Comment = "Paste XML Code", Tags = "XML" },
				new ListItem { Key = "PowerShell", Value = "PS;#B30086", Comment = "Paste PowerShell Code", Tags = "PowerShell" },
				new ListItem { Key = "TypeScript", Value = "TS;#3178C6", Comment = "Paste TypeScript Code", Tags = "TypeScript" },
				new ListItem { Key = "JavaScript", Value = "JS;#C0AF3F", Comment = "Paste JavaScript Code", Tags = "JavaScript" },
				new ListItem { Key = "Log", Value = "LOG;#999999", Comment = "Paste Log Code", Tags = "Log" },
				new ListItem { Key = "Markdown", Value = "MD;#2d2d2d", Comment = "Paste Markdown Code", Tags = "Markdown" },
				new ListItem { Key = "Text", Value = "TXT;#000000", Comment = "Paste Text Code", Tags = "Text" }
			};
		}

		/// <summary>
		/// Handles the Loaded event of the control.
		/// Dynamically generates code block buttons based on the configured language list.
		/// </summary>
		/// <param name="sender">The source of the event.</param>
		/// <param name="e">The event data.</param>
		private void This_Loaded(object sender, RoutedEventArgs e)
		{
			if (ControlsHelper.AllowLoad(this))
			{
				// Preserve the ComboBox (assumed to be the first child) and clear any other children.
				ExtraButtonsPanel.Children.Clear();
				// Dynamically create a button for each language item.
				foreach (var languageItem in LanguageItems)
				{
					var button = new Button
					{
						Tag = languageItem.Tags,
						ToolTip = languageItem.Comment,
						Margin = new Thickness(3, 3, 0, 3)
					};
					button.Click += CodeButton_Click;

					var iconText = languageItem.Value.Split(';')[0];
					var iconBack = languageItem.Value.Split(';')[1];
					// Create an icon using the SVG template (simulated here via a helper).
					var iconControl = new ContentControl
					{
						Margin = new Thickness(0, 0, 0, 0),
						Focusable = false,
						Content = CreateSvgIcon(iconText, iconBack)
					};
					button.Content = iconControl;

					// Add context help for the button.
					AppHelper.AddHelp(button, $"Wrap selection into `{iconText}` code block. Hold CTRL to paste from your clipboard as an `{iconBack}` code block.");
					ExtraButtonsPanel.Children.Add(button);
				}

				AppHelper.InitHelp(this);
				UiPresetsManager.InitControl(this, true);
			}
		}

		/// <summary>
		/// Event handler for button click events.
		/// Wraps the selected text (or clipboard content if CTRL is pressed) into a markdown code block of the specified language.
		/// </summary>
		/// <param name="sender">The button that was clicked.</param>
		/// <param name="e">The event data.</param>
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
			// Add a new line if the caret is not already on a new line.
			if (caretIndex > 0 && box.Text[caretIndex - 1] != '\n')
				prefix += "\r\n";
			// Add a new line if the caret is not at the beginning of a line.
			var suffix = "";
			if (caretIndex < box.Text.Length && box.Text[caretIndex] != '\r')
				suffix += "\r\n";
			var block = MarkdownHelper.CreateMarkdownCodeBlock(clipboardText, language);
			var text = $"{prefix}{block}{suffix}";
			AppHelper.InsertText(box, text, true, false);
			var newIndex = string.IsNullOrEmpty(clipboardText)
			? caretIndex + prefix.Length
			: caretIndex + text.Length;
			AppHelper.SetCaret(box, newIndex);
		}

		/// <summary>
		/// Creates a UI element based on an SVG template resource.
		/// The SVG template is loaded from the project's resources and updated with the provided text and background color.
		/// Uses an SVG rendering library (e.g., SharpVectors.Wpf) to display the SVG.
		/// </summary>
		/// <param name="text">
		/// The text to display on the icon (typically the language abbreviation). This will replace the default template text.
		/// </param>
		/// <param name="backgroundColor">
		/// The background color for the icon in hexadecimal format. This will replace the default template background color.
		/// </param>
		/// <returns>
		/// A UIElement representing the SVG icon. If the resource fails to load, a fallback TextBlock is returned.
		/// </returns>
		private UIElement CreateSvgIcon(string text, string backgroundColor)
		{
			// Verify that the SVG resource (code_template.svg) is correctly added to your project with the proper Build Action (typically “Resource”).
			var uri = AppHelper.GetResourceUri("Resources/Icons/Templates/code_template.svg");
			StreamResourceInfo resourceInfo = Application.GetResourceStream(uri);
			if (resourceInfo != null)
			{
				// Read the resource SVG template.
				string svgContent;
				using (StreamReader reader = new StreamReader(resourceInfo.Stream))
				{
					svgContent = reader.ReadToEnd();
				}

				// Update the SVG template content: replace the default text and background color with provided values.
				// (Note: The actual replacement strings must match those in your SVG template.)
				svgContent = svgContent.Replace("C#", text);
				svgContent = svgContent.Replace("#60AF1F", backgroundColor);

				// Parse the updated SVG content into a UIElement using an SVG rendering control.
				// The following uses the SvgViewbox control from SharpVectors.Wpf.
				try
				{
					// Create a temporary MemoryStream from the updated SVG content.
					using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(svgContent)))
					{
						// The SvgViewbox will load the SVG from the stream.
						var svgViewbox = new SvgViewbox
						{
							Stretch = Stretch.Uniform,
							StreamSource = stream
						};
						return svgViewbox;
					}
				}
				catch (Exception ex)
				{
					// In case of render failure, fall back to a simple TextBlock.
					System.Diagnostics.Debug.WriteLine($"Error rendering SVG: {ex.Message}");
				}
			}

			// Fallback: Simulate the icon with a styled TextBlock.
			return new TextBlock
			{
				Text = text,
				Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(backgroundColor)),
				Foreground = Brushes.White,
				FontWeight = FontWeights.Bold,
				FontSize = 16,
				TextAlignment = TextAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center,
				HorizontalAlignment = HorizontalAlignment.Center,
				Padding = new Thickness(5)
			};
		}
	}
}

