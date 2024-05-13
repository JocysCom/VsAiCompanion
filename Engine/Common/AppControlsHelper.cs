using JocysCom.ClassLibrary.Data;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace JocysCom.VS.AiCompanion.Engine
{
	public class AppControlsHelper
	{

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
				var textBox = sender as TextBox;
				// Get the dropped files
				var files = (string[])e.Data.GetData(DataFormats.FileDrop);
				// Initialize the text to insert
				StringBuilder sb = new StringBuilder();
				// Ensure instructions are added only once and not present in the current line
				var instructionsExist = textBox.Text.Contains(Resources.MainResources.main_TextBox_Drop_Files_Instructions);
				var currentLine = GetLineFromCaret(textBox);
				// Determine if instructions should be added
				if (!instructionsExist && !currentLine.TrimStart().StartsWith("-"))
					sb.AppendLine("\r\n" + Resources.MainResources.main_TextBox_Drop_Files_Instructions);
				// Append file paths
				foreach (string file in files)
					sb.AppendLine($"- {file}");
				// Insert or append the text to the TextBox
				var startIndex = textBox.CaretIndex;
				if (!IsCaretAtLineStart(textBox))
				{
					var insertionIndex = textBox.Text.IndexOf(Environment.NewLine, textBox.CaretIndex);
					startIndex = insertionIndex != -1
						? insertionIndex + Environment.NewLine.Length
						: textBox.Text.Length;
				}
				textBox.Text = textBox.Text.Insert(startIndex, sb.ToString());
				// Update cursor position
				textBox.CaretIndex += sb.Length;
				// Mark the event as handled
				e.Handled = true;
			}
		}

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
