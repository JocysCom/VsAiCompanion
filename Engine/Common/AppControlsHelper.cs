using JocysCom.ClassLibrary.Data;
using JocysCom.ClassLibrary.Windows;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace JocysCom.VS.AiCompanion.Engine
{
	public class AppControlsHelper
	{

		#region TextBox: Allow Drop

		public static void AllowDrop(TextBox control, bool allow)
		{
			control.AllowDrop = allow;
			if (allow)
			{
				control.PreviewDragOver += TextBox_PreviewDragOver;
				control.Drop += TextBox_Drop;
			}
			else
			{
				control.PreviewDragOver -= TextBox_PreviewDragOver;
				control.Drop -= TextBox_Drop;
			}
		}

		private static void TextBox_PreviewDragOver(object sender, DragEventArgs e)
		{
			// If the data is file drop, then allow it
			if (e.Data.GetDataPresent(DataFormats.FileDrop))
				e.Handled = true;
		}

		private static void TextBox_Drop(object sender, DragEventArgs e)
		{
			if (e.Data.GetDataPresent(DataFormats.FileDrop))
			{
				var textBox = (TextBox)sender;
				// Get the dropped files
				var files = (string[])e.Data.GetData(DataFormats.FileDrop);
				DropFiles(textBox, files);
				// Mark the event as handled
				e.Handled = true;
			}
		}

		public static string GetFileWithCodeBlockContent(params string[] files)
		{
			var sb = new StringBuilder();
			var binaryFiles = new List<string>();
			// Append file content
			foreach (string file in files)
			{
				if (!File.Exists(file) || JocysCom.ClassLibrary.Files.Mime.IsBinary(file))
				{
					binaryFiles.Add(file);
					continue;
				}
				var fileContent = File.ReadAllText(file);
				var markdownCodeBlock = MarkdownHelper.CreateMarkdownCodeBlock(file, fileContent, null);
				sb.AppendLine($"\r\n`{file}`:\r\n");
				sb.AppendLine($"{markdownCodeBlock}");
			}
			if (binaryFiles.Any())
			{
				// Append file paths
				foreach (string file in files)
					sb.AppendLine($"- {file}");
			}
			return sb.ToString();
		}

		public static string GetFileReferences(params string[] file)
		{
			var content = string.Join(Environment.NewLine, file.Select(x => $"#file:'{x}'"));
			return content;
		}

		public static string ReplaceFileReferences(string inputText)
		{
			if (string.IsNullOrEmpty(inputText))
				return inputText;

			// Regex to match #file:'path' pattern
			// Uses a non-greedy match to handle multiple file references
			var regex = new Regex(@"#file:'([^']+)'");

			// Use a StringBuilder for efficient string manipulation
			var result = new StringBuilder(inputText);

			// Find all matches
			var matches = regex.Matches(inputText);

			// Process matches in reverse order to avoid index shifting problems
			// when replacing text of different lengths
			for (int i = matches.Count - 1; i >= 0; i--)
			{
				var match = matches[i];
				var filePath = match.Groups[1].Value;

				try
				{
					// Read the file content
					string fileContent = AppControlsHelper.GetFileWithCodeBlockContent(filePath);

					// Replace the #file reference with the file content
					result.Remove(match.Index, match.Length);
					result.Insert(match.Index, fileContent);
				}
				catch (Exception ex)
				{
					// Handle file reading errors
					string errorMessage = $"/* Error reading file '{filePath}': {ex.Message} */";
					result.Remove(match.Index, match.Length);
					result.Insert(match.Index, errorMessage);
				}
			}

			return result.ToString();
		}


		public static void DropFiles(TextBox textBox, string[] files)
		{
			var sb = new StringBuilder();
			var currentLine = GetLineFromCaret(textBox);
			var isCtrlDown =
				System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.LeftCtrl) ||
				System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.RightCtrl);
			var isAltDown =
				System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.LeftAlt) ||
				System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.RightAlt);
			// If user holds ALT key during drop then...
			if (isAltDown)
			{
				// Paste file paths as reference strings.
				var content = GetFileReferences(files);
				sb.Append(content);
			}
			// If user holds CTRL key during drop then...
			else if (isCtrlDown)
			{
				// Paste file paths and content wrapped into markdown block.
				var content = GetFileWithCodeBlockContent(files);
				sb.Append(content);
			}
			else
			{
				// Initialize the text to insert
				// Ensure instructions are added only once and not present in the current line
				var instructionsExist = textBox.Text.Contains(Resources.MainResources.main_TextBox_Drop_Files_Instructions);
				// Determine if instructions should be added
				if (!instructionsExist && !currentLine.TrimStart().StartsWith("-"))
					sb.AppendLine("\r\n" + Resources.MainResources.main_TextBox_Drop_Files_Instructions);
				// Append file paths
				foreach (string file in files)
					sb.AppendLine($"- {file}");
			}
			// Insert or append the text to the TextBox
			var startIndex = textBox.CaretIndex;
			if (!IsCaretAtLineStart(textBox))
			{
				var insertionIndex = textBox.Text.IndexOf(Environment.NewLine, startIndex);
				startIndex = insertionIndex != -1
					? insertionIndex + Environment.NewLine.Length
					: textBox.Text.Length;
			}
			textBox.Text = textBox.Text.Insert(startIndex, sb.ToString());
			// Update cursor position
			textBox.CaretIndex = startIndex + sb.Length;
		}

		#endregion

		#region TextBox: Allow Paste

		private static readonly Dictionary<TextBox, CommandBinding> PasteCommandBindings = new Dictionary<TextBox, CommandBinding>();

		public static void AllowPasteFiles(TextBox textBox, bool allow)
		{
			if (textBox == null)
				throw new ArgumentNullException(nameof(textBox));
			if (allow)
			{
				if (PasteCommandBindings.ContainsKey(textBox))
					return;
				CommandBinding pasteBinding = new CommandBinding(ApplicationCommands.Paste, OnPasteFiles, OnCanPasteFiles);
				textBox.CommandBindings.Add(pasteBinding);
				EnsurePasteMenuItem(textBox);
				PasteCommandBindings[textBox] = pasteBinding;
			}
			else
			{
				if (!PasteCommandBindings.TryGetValue(textBox, out CommandBinding pasteBinding))
					return;
				textBox.CommandBindings.Remove(pasteBinding);
				PasteCommandBindings.Remove(textBox);
			}
		}

		#region Paste Menu Item

		private static void EnsurePasteMenuItem(TextBox textBox)
		{
			if (textBox.ContextMenu == null)
			{
				// Initialize with default ContextMenu
				textBox.ContextMenu = new ContextMenu();

				// Add default "Cut" menu item
				MenuItem cutItem = new MenuItem
				{
					Command = ApplicationCommands.Cut,
					Header = "Cut"
				};
				textBox.ContextMenu.Items.Add(cutItem);

				// Add default "Copy" menu item
				MenuItem copyItem = new MenuItem
				{
					Command = ApplicationCommands.Copy,
					Header = "Copy"
				};
				textBox.ContextMenu.Items.Add(copyItem);
			}

			// Check if any Paste menu items already exist
			bool hasPaste = textBox.ContextMenu.Items.OfType<MenuItem>().Any(item =>
				item.Command == ApplicationCommands.Paste || item.Header.ToString().Equals("Paste", StringComparison.OrdinalIgnoreCase));

			// Remove existing paste items to avoid duplicates when refreshing
			var existingPasteItems = textBox.ContextMenu.Items.OfType<MenuItem>()
				.Where(item => item.Command == ApplicationCommands.Paste ||
							  (item.Header != null && item.Header.ToString().StartsWith("Paste", StringComparison.OrdinalIgnoreCase)))
				.ToList();

			foreach (var item in existingPasteItems)
			{
				textBox.ContextMenu.Items.Remove(item);
			}

			// Create a submenu for paste options
			MenuItem pasteMenu = new MenuItem { Header = "Paste" };

			// Regular paste
			MenuItem standardPasteItem = new MenuItem
			{
				Command = ApplicationCommands.Paste,
				Header = "Standard Paste"
			};
			pasteMenu.Items.Add(standardPasteItem);

			// Separator
			pasteMenu.Items.Add(new Separator());

			// Paste files as references
			MenuItem pasteAsReferencesItem = new MenuItem
			{
				Header = "Paste Files as References",
				ToolTip = "Paste file paths as reference strings"
			};
			pasteAsReferencesItem.Click += (s, e) => PasteFilesWithOption(textBox, PasteOption.AsReferences);
			pasteMenu.Items.Add(pasteAsReferencesItem);

			// Paste files with content
			MenuItem pasteWithContentItem = new MenuItem
			{
				Header = "Paste Files with Content",
				ToolTip = "Paste file paths and content wrapped into markdown block"
			};
			pasteWithContentItem.Click += (s, e) => PasteFilesWithOption(textBox, PasteOption.WithContent);
			pasteMenu.Items.Add(pasteWithContentItem);

			// Paste files as list
			MenuItem pasteAsListItem = new MenuItem
			{
				Header = "Paste Files as List with Instructions",
				ToolTip = "Paste file instructions and file path list"
			};
			pasteAsListItem.Click += (s, e) => PasteFilesWithOption(textBox, PasteOption.AsList);
			pasteMenu.Items.Add(pasteAsListItem);

			// Add the paste submenu to the context menu
			textBox.ContextMenu.Items.Add(pasteMenu);
		}

		/// <summary>
		/// Options for pasting files
		/// </summary>
		private enum PasteOption
		{
			/// <summary>Paste file paths as reference strings</summary>
			AsReferences,
			/// <summary>Paste file paths and content wrapped into markdown block</summary>
			WithContent,
			/// <summary>Paste file instructions and file path list</summary>
			AsList
		}

		/// <summary>
		/// Pastes files from clipboard with specified option
		/// </summary>
		private static void PasteFilesWithOption(TextBox textBox, PasteOption option)
		{
			if (!Clipboard.ContainsFileDropList())
				return;

			var filePaths = Clipboard.GetFileDropList().Cast<string>().ToArray();

			string textToInsert;
			switch (option)
			{
				case PasteOption.AsReferences:
					textToInsert = GetFileReferences(filePaths);
					break;
				case PasteOption.WithContent:
					textToInsert = GetFileWithCodeBlockContent(filePaths);
					break;
				case PasteOption.AsList:
				default:
					// Simulate default drop behavior for consistency
					DropFiles(textBox, filePaths);
					return;
			}

			if (!string.IsNullOrEmpty(textToInsert))
			{
				int selectionStart = textBox.SelectionStart;
				textBox.Text = textBox.Text.Remove(textBox.SelectionStart, textBox.SelectionLength);
				textBox.Text = textBox.Text.Insert(selectionStart, textToInsert);
				textBox.SelectionStart = selectionStart + textToInsert.Length;
			}
		}

		#endregion


		private static void OnCanPasteFiles(object sender, CanExecuteRoutedEventArgs e)
		{
			if (sender is TextBox textBox)
			{
				e.CanExecute = Clipboard.ContainsText() || Clipboard.ContainsFileDropList() || Clipboard.ContainsImage();
				// Do not set e.Handled to true to allow other commands to process
			}
		}

		private static void OnPasteFiles(object sender, ExecutedRoutedEventArgs e)
		{
			if (sender is TextBox textBox)
			{
				if (Clipboard.ContainsFileDropList())
				{
					// Use the default paste behavior (AsList) when using keyboard shortcut
					var filePaths = Clipboard.GetFileDropList().Cast<string>().ToArray();
					DropFiles(textBox, filePaths);
					e.Handled = true;
					return;
				}

				string textToInsert = null;
				if (Clipboard.ContainsImage())
				{
					var tempFolderPath = Path.Combine(AppHelper.GetTempFolderPath(), nameof(Clipboard));
					textToInsert = ClipboardHelper.GetImageFromClipboard(tempFolderPath, true);
				}
				else if (Clipboard.ContainsText())
				{
					textToInsert = Clipboard.GetText();
				}

				if (!string.IsNullOrEmpty(textToInsert))
				{
					int selectionStart = textBox.SelectionStart;
					textBox.Text = textBox.Text.Remove(textBox.SelectionStart, textBox.SelectionLength);
					textBox.Text = textBox.Text.Insert(selectionStart, textToInsert);
					textBox.SelectionStart = selectionStart + textToInsert.Length;
					e.Handled = true;
				}
			}
		}

		#endregion

		private static bool IsCaretAtLineStart(TextBox textBox)
		{
			var lineIndex = textBox.GetLineIndexFromCharacterIndex(textBox.CaretIndex);
			var lineStartIndex = textBox.GetCharacterIndexFromLineIndex(lineIndex);
			return textBox.CaretIndex == lineStartIndex;
		}

		private static string GetLineFromCaret(TextBox textBox)
		{
			var lineIndex = textBox.GetLineIndexFromCharacterIndex(textBox.CaretIndex);
			var start = textBox.GetCharacterIndexFromLineIndex(lineIndex);
			int length;
			if (lineIndex < textBox.LineCount - 1)
			{
				var end = textBox.GetCharacterIndexFromLineIndex(lineIndex + 1);
				length = end - start;
			}
			else
			{
				length = textBox.Text.Length - start;
			}
			return textBox.Text.Substring(start, length);
		}

		#region Export/ Import

		public static System.Windows.Forms.OpenFileDialog ImportOpenFileDialog { get; } = new System.Windows.Forms.OpenFileDialog();

		public static List<T> Import<T>(string path)
		{
			var dialog = ImportOpenFileDialog;
			dialog.SupportMultiDottedExtensions = true;
			dialog.DefaultExt = "*.csv";
			dialog.Filter = "CSV Data (*.csv)|*.csv|JSON Data (*.json)|*.json|XML Data (*.XML)|*.xml|All files (*.*)|*.*";
			dialog.FilterIndex = 1;
			dialog.RestoreDirectory = true;
			dialog.Title = "Import Data File";
			var result = dialog.ShowDialog();
			if (result != System.Windows.Forms.DialogResult.OK)
				return null;
			var fi = new FileInfo(dialog.FileName);
			var content = System.IO.File.ReadAllText(fi.FullName);
			List<T> data;
			switch (fi.Extension.ToUpper())
			{
				case ".JSON":
					data = JocysCom.ClassLibrary.Runtime.Serializer.DeserializeFromJson<List<T>>(content);
					break;
				case ".XML":
					data = JocysCom.ClassLibrary.Runtime.Serializer.DeserializeFromXmlString<List<T>>(content);
					break;
				default:
					// Import as CSV.
					var table = ClassLibrary.Files.CsvHelper.Read(fi.FullName, true);
					data = SqlHelper.ConvertToList<T>(table);
					break;
			}
			return data;
		}

		public static System.Windows.Forms.SaveFileDialog ExportSaveFileDialog { get; } = new System.Windows.Forms.SaveFileDialog();

		public static void Export<T>(IEnumerable<T> data)
		{
			var dialog = ExportSaveFileDialog;
			dialog.DefaultExt = "*.csv";
			dialog.Filter = "CSV Data (*.csv)|*.csv|JSON Data (*.json)|*.json|XML Data (*.XML)|*.xml|All files (*.*)|*.*";
			dialog.FilterIndex = 1;
			dialog.RestoreDirectory = true;
			//if (string.IsNullOrEmpty(dialog.FileName))
			//	dialog.FileName = path;
			//if (string.IsNullOrEmpty(dialog.InitialDirectory)) dialog.InitialDirectory = ;
			dialog.Title = "Export Data File";
			var result = dialog.ShowDialog();
			if (result != System.Windows.Forms.DialogResult.OK)
				return;
			var fi = new FileInfo(dialog.FileName);
			string content;
			switch (fi.Extension.ToUpper())
			{
				case ".JSON":
					content = JocysCom.ClassLibrary.Runtime.Serializer.SerializeToJson(data);
					break;
				case ".XML":
					content = JocysCom.ClassLibrary.Runtime.Serializer.SerializeToXmlString(data);
					break;
				default:
					// Export as CSV.
					var table = SqlHelper.ConvertToTable(data);
					content = JocysCom.ClassLibrary.Files.CsvHelper.Write(table);
					break;
			}
			var bytes = System.Text.Encoding.UTF8.GetBytes(content);
			JocysCom.ClassLibrary.Configuration.SettingsHelper.WriteIfDifferent(dialog.FileName, bytes);
		}

		#endregion


		public static ConcurrentDictionary<int, Func<bool>> OnEnterActions { get; } = new ConcurrentDictionary<int, Func<bool>>();

		public static void UseEnterToSend(TextBox control, Func<bool> action)
		{
			int hashCode = control.GetHashCode();
			if (action == null)
			{
				control.PreviewKeyDown -= TextBox_PreviewKeyDown;
				control.PreviewKeyUp -= TextBox_PreviewKeyUp;
				OnEnterActions.TryRemove(hashCode, out _);
			}
			else
			{
				OnEnterActions.TryAdd(hashCode, action);
				control.PreviewKeyDown += TextBox_PreviewKeyDown;
				control.PreviewKeyUp += TextBox_PreviewKeyUp;
			}
		}

		private static void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Enter && !Keyboard.IsKeyDown(Key.LeftShift) && !Keyboard.IsKeyDown(Key.RightShift))
			{
				// Prevent new line added to the message.
				e.Handled = true;
			}
		}

		private static void TextBox_PreviewKeyUp(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Enter && !Keyboard.IsKeyDown(Key.LeftShift) && !Keyboard.IsKeyDown(Key.RightShift))
			{
				Func<bool> action;
				int hashCode = sender.GetHashCode();
				if (OnEnterActions.TryGetValue(hashCode, out action))
					action?.Invoke();
				// Prevent new line added to the message.
				e.Handled = true;
			}
		}



	}
}
