using JocysCom.ClassLibrary.Data;
using JocysCom.ClassLibrary.Windows;
using JocysCom.VS.AiCompanion.Engine.Companions;
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
				var isUrl = Uri.TryCreate(filePath, UriKind.Absolute, out Uri uri) && uri.Scheme != Uri.UriSchemeFile;
				var value = match.Value;
				try
				{
					// Get content only if reference is URL or valid existing file.
					var getContent = isUrl ||
						(!filePath.Intersect(Path.GetInvalidPathChars()).Any() && File.Exists(filePath));
					if (getContent)
					{
						// Read the file content
						value = GetFileWithCodeBlockContent(filePath);
					}
				}
				catch (Exception ex)
				{
					value = $"/* Error reading file '{filePath}': {ex.Message} */";
				}
				result.Remove(match.Index, match.Length);
				result.Insert(match.Index, value);
			}
			return result.ToString();
		}


		/// <summary>
		/// Handles dropping files into a TextBox with various formatting options.
		/// </summary>
		/// <param name="textBox">The TextBox receiving the files</param>
		/// <param name="files">Array of file paths being dropped</param>
		public static void DropFiles(TextBox textBox, string[] files)
		{
			if (textBox == null || files == null || files.Length == 0)
				return;

			// Determine option based on modifier keys
			var option = FilePasteOption.None;
			var isCtrlDown = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
			var isAltDown = Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt);

			if (isAltDown)
				option = FilePasteOption.AsReferences;
			else if (isCtrlDown)
				option = FilePasteOption.WithContent;
			else
			{
				option = FilePasteOption.AsList;
				// Check if instructions already exist in the text
				var currentLine = GetLineFromCaret(textBox);
				var instructionsExist = textBox.Text.Contains(Resources.MainResources.main_TextBox_Drop_Files_Instructions);

				// If instructions already exist or the current line starts with a list marker, use simple format
				if (instructionsExist || currentLine.TrimStart().StartsWith("-"))
					option = FilePasteOption.None;
			}

			// Generate the content
			string content = GenerateContentFromFiles(files, option);

			// Insert the content at the appropriate position
			var startIndex = textBox.CaretIndex;
			if (!IsCaretAtLineStart(textBox))
			{
				var insertionIndex = textBox.Text.IndexOf(Environment.NewLine, startIndex);
				startIndex = insertionIndex != -1
					? insertionIndex + Environment.NewLine.Length
					: textBox.Text.Length;
			}

			textBox.Text = textBox.Text.Insert(startIndex, content);
			textBox.CaretIndex = startIndex + content.Length;
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

				// Add the Opening event handler to update menu states
				textBox.ContextMenu.Opened += (s, e) => UpdatePasteMenuItems(textBox.ContextMenu);
			}

			// Remove existing paste items to avoid duplicates when refreshing
			var existingPasteItems = textBox.ContextMenu.Items.OfType<MenuItem>()
				.Where(item => item.Command == ApplicationCommands.Paste ||
							  (item.Header != null && item.Header.ToString().StartsWith("Paste", StringComparison.OrdinalIgnoreCase)))
				.ToList();

			foreach (var item in existingPasteItems)
			{
				textBox.ContextMenu.Items.Remove(item);
			}

			// Standard paste
			MenuItem standardPasteItem = new MenuItem
			{
				Command = ApplicationCommands.Paste,
				Header = "Paste",
				Tag = FilePasteOption.None
			};
			textBox.ContextMenu.Items.Add(standardPasteItem);

			// Paste files as list
			MenuItem pasteAsListItem = new MenuItem
			{
				Header = "Paste Files with Instructions",
				ToolTip = "Paste file paths and instructions",
				Tag = FilePasteOption.AsList
			};
			pasteAsListItem.Click += (s, e) => PasteFilesWithOption(textBox, FilePasteOption.AsList);
			textBox.ContextMenu.Items.Add(pasteAsListItem);

			// Paste files as references
			MenuItem pasteAsReferencesItem = new MenuItem
			{
				Header = "Paste Files as References",
				ToolTip = "Paste file paths as reference strings",
				Tag = FilePasteOption.AsReferences
			};
			pasteAsReferencesItem.Click += (s, e) => PasteFilesWithOption(textBox, FilePasteOption.AsReferences);
			textBox.ContextMenu.Items.Add(pasteAsReferencesItem);

			// Paste files with content
			MenuItem pasteWithContentItem = new MenuItem
			{
				Header = "Paste Files with Content",
				ToolTip = "Paste file paths and content wrapped into markdown block",
				Tag = FilePasteOption.WithContent
			};
			pasteWithContentItem.Click += (s, e) => PasteFilesWithOption(textBox, FilePasteOption.WithContent);
			textBox.ContextMenu.Items.Add(pasteWithContentItem);

		}

		/// <summary>
		/// Updates the enabled state of paste menu items based on clipboard content
		/// </summary>
		private static void UpdatePasteMenuItems(ContextMenu menu)
		{
			bool hasFileDropList = Clipboard.ContainsFileDropList();
			foreach (var item in menu.Items.OfType<MenuItem>())
			{
				// Skip items that aren't paste-related
				if (item.Tag == null || !(item.Tag is FilePasteOption tag && tag != FilePasteOption.None))
					continue;
				// Enable paste file options only when clipboard contains files
				item.IsEnabled = hasFileDropList;
			}
		}

		/// <summary>
		/// Options for pasting files
		/// </summary>
		public enum FilePasteOption
		{
			/// <summary>Standard paste without special handling for files</summary>
			None,
			/// <summary>Paste file paths as reference strings</summary>
			AsReferences,
			/// <summary>Paste file paths and content wrapped into markdown block</summary>
			WithContent,
			/// <summary>Paste file instructions and file path list</summary>
			AsList
		}

		/// <summary>
		/// Generates content from file paths based on the specified paste option.
		/// </summary>
		/// <param name="files">Array of file paths to process</param>
		/// <param name="option">Option determining how files should be processed</param>
		/// <returns>Generated text content based on the option</returns>
		public static string GenerateContentFromFiles(string[] files, FilePasteOption option)
		{
			if (files == null || files.Length == 0)
				return string.Empty;

			var sb = new StringBuilder();

			switch (option)
			{
				case FilePasteOption.AsReferences:
					return GetFileReferences(files);

				case FilePasteOption.WithContent:
					return GetFileWithCodeBlockContent(files);

				case FilePasteOption.AsList:
					// Add instructions if needed
					sb.AppendLine("\r\n" + Resources.MainResources.main_TextBox_Drop_Files_Instructions);
					// Append file paths
					foreach (string file in files)
						sb.AppendLine($"- {file}");
					return sb.ToString();

				case FilePasteOption.None:
				default:
					// Simple list of file paths without formatting
					foreach (string file in files)
						sb.AppendLine(file);
					return sb.ToString();
			}
		}

		/// <summary>
		/// Pastes files from clipboard with specified option
		/// </summary>
		private static void PasteFilesWithOption(TextBox textBox, FilePasteOption option)
		{
			if (!Clipboard.ContainsFileDropList() || textBox == null)
				return;

			var filePaths = Clipboard.GetFileDropList().Cast<string>().ToArray();

			// Special case for AsList option to maintain consistency with drop behavior
			if (option == FilePasteOption.AsList)
			{
				DropFiles(textBox, filePaths);
				return;
			}

			// Generate content based on option
			string content = GenerateContentFromFiles(filePaths, option);

			if (!string.IsNullOrEmpty(content))
			{
				int selectionStart = textBox.SelectionStart;
				textBox.Text = textBox.Text.Remove(textBox.SelectionStart, textBox.SelectionLength);
				textBox.Text = textBox.Text.Insert(selectionStart, content);
				textBox.SelectionStart = selectionStart + content.Length;
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
			if (!(sender is TextBox textBox))
				return;

			// Handle file drop list
			if (Clipboard.ContainsFileDropList())
			{
				var filePaths = Clipboard.GetFileDropList().Cast<string>().ToArray();

				// Check if this is a standard paste operation from the command
				bool isStandardPaste = e.Command == ApplicationCommands.Paste &&
									  e.OriginalSource is TextBox &&
									  !(e.Parameter is FilePasteOption);

				if (isStandardPaste)
				{
					// For standard paste, use the None option (simple list)
					string content = GenerateContentFromFiles(filePaths, FilePasteOption.None);
					int selectionStart = textBox.SelectionStart;
					int selectionLength = textBox.SelectionLength;

					// Replace selected text with the file list
					textBox.Text = textBox.Text.Remove(selectionStart, selectionLength);
					textBox.Text = textBox.Text.Insert(selectionStart, content);
					textBox.SelectionStart = selectionStart + content.Length;
				}
				else
				{
					// For special paste commands, use the DropFiles method
					DropFiles(textBox, filePaths);
				}

				e.Handled = true;
				return;
			}

			// Handle image or text in clipboard
			string clipboardText = null;
			if (Clipboard.ContainsImage())
			{
				var tempFolderPath = Path.Combine(AppHelper.GetTempFolderPath(), nameof(Clipboard));
				clipboardText = ClipboardHelper.GetImageFromClipboard(tempFolderPath, true);
			}
			else if (Clipboard.ContainsText())
			{
				clipboardText = Clipboard.GetText();
			}

			if (!string.IsNullOrEmpty(clipboardText))
			{
				int selectionStart = textBox.SelectionStart;
				textBox.Text = textBox.Text.Remove(textBox.SelectionStart, textBox.SelectionLength);
				textBox.Text = textBox.Text.Insert(selectionStart, clipboardText);
				textBox.SelectionStart = selectionStart + clipboardText.Length;
				e.Handled = true;
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
