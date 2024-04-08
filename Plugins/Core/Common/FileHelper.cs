using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace JocysCom.VS.AiCompanion.Plugins.Core
{
	/// <summary>
	/// File Helper
	/// </summary>
	public partial class FileHelper : IFileHelper
	{

		/// <inheritdoc/>
		public string ModifyTextFile(string path, long startLine, long deleteLines, string insertContents = null)
		{
			if (startLine < 1)
				throw new ArgumentOutOfRangeException(nameof(startLine), $"Argument '{nameof(startLine)}' must be greater than 0.");
			if (deleteLines < 0)
				throw new ArgumentOutOfRangeException(nameof(deleteLines), $"Argument '{nameof(deleteLines)}' must be non-negative.");

			Encoding detectedEncoding;
			string detectedNewlineType;
			var newLines = new[] { "\r\n", "\n", "\r" };
			DetectFileProperties(path, out detectedEncoding, out detectedNewlineType);
			try
			{
				var allLines = File.ReadAllText(path, detectedEncoding)
					   .Split(newLines, StringSplitOptions.None)
					   .ToList();
				_ModifyContents(allLines, startLine, deleteLines, insertContents);
				// Write modified lines back to the file with the detected newline type.
				File.WriteAllText(path, string.Join(detectedNewlineType, allLines), detectedEncoding);
				return "OK";
			}
			catch (Exception ex)
			{
				return ex.Message;
			}
		}

		/// <inheritdoc/>
		public string ReadTextFile(string path, long offset = 0, long length = long.MaxValue)
		{
			try
			{
				string result = null;
				using (var fileStream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
				using (var reader = new StreamReader(fileStream, detectEncodingFromByteOrderMarks: true))
				{
					// Check if the desired offset is beyond the length of the file.
					if (offset >= fileStream.Length)
						return null;

					reader.BaseStream.Seek(offset, SeekOrigin.Begin);

					// Calculate the actual number of characters we can read.
					// This gets the smaller value between the specified length and the remaining file length from the offset.
					var actualLength = Math.Min(length, fileStream.Length - offset);

					// Ensure actualLength fits within int.MaxValue, as new char[] expects an int, not long.
					actualLength = Math.Min(actualLength, int.MaxValue);

					var buffer = new char[actualLength];
					var readLength = reader.ReadBlock(buffer, 0, (int)actualLength);

					// If no characters were read, return null to indicate that the operation was not successful.
					if (readLength == 0)
						return null;

					result = new string(buffer, 0, readLength);
				}
				return result;
			}
			catch (Exception)
			{
				// Optionally, log the exception or handle it accordingly.
				return null;
			}
		}

		/// <inheritdoc/>
		public string ReadTextFileLines(string path, long line = 1, long count = long.MaxValue)
		{
			try
			{
				if (line < 1)
					throw new ArgumentOutOfRangeException(nameof(line), $"Argument '{nameof(line)}' must be greater than 0.");
				if (count < 1)
					throw new ArgumentOutOfRangeException(nameof(count), $"Argument '{nameof(count)}' must be non-negative.");

				Encoding detectedEncoding;
				string detectedNewlineType;
				DetectFileProperties(path, out detectedEncoding, out detectedNewlineType);
				var lines = new List<string>();
				using (var fileStream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
				using (var reader = new StreamReader(fileStream, detectEncodingFromByteOrderMarks: true))
				{
					// Skip lines until the target line is reached.
					for (long currentLine = 1; currentLine < line; currentLine++)
						if (reader.ReadLine() == null)
							throw new ArgumentOutOfRangeException("Line exceeds the file's total lines.");
					string value;
					// Read until the end or requested line count.
					while ((value = reader.ReadLine()) != null && count-- > 0)
						lines.Add(value);
				}
				var result = string.Join(detectedNewlineType, lines);
				return result;
			}
			catch (Exception)
			{
				return null;
			}
		}

		#region Helper Functions

		/// <summary>
		/// Modifies text content by supporting line deletion, insertion, or updating through a combination of both.
		/// </summary>
		/// <param name="text">The text to operate on.</param>
		/// <param name="startLine">
		/// The 1-based line number indicating where the operation begins.
		/// For insertion, this is the line where the new content will be added before.
		/// </param>
		/// <param name="deleteLines">
		/// The number of lines to delete starting from the line number specified by "startLine"
		/// Set to 0 for insertion operations where existing lines are not to be removed.
		/// To delete all lines from the start line, use the maximum value of the integer type.
		/// </param>
		/// <param name="insertContents">
		/// The content to insert. For deletion operations, this should be set to null.
		/// For update operations, this contains the new content replacing the deleted lines.
		/// </param>
		/// <returns>Modified text.</returns>
		/// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="startLine"/> is less than 1 or <paramref name="deleteLines"/> is negative.</exception>
		/// <example>
		/// Deleting lines:
		/// { text: "...text to modify...", startLine: 3, deleteLines: 2 }
		/// 
		/// Inserting lines:
		/// { text: "...text to modify...", startLine: 4, deleteLines: 0, insertContents: "new content\r\nto insert from line 4" }
		///
		/// Updating lines:
		/// { text: "...text to modify...", startLine: 4, deleteLines: 3, insertContents: "New content replacing lines 4-6" }
		/// </example>
		public string ModifyText(string text, long startLine, long deleteLines, string insertContents = null)
		{
			if (string.IsNullOrEmpty(text))
				return text;
			var newLines = new[] { "\r\n", "\n", "\r" };
			string detectedNewlineType;
			using (var reader = new StringReader(text))
				_DetectNewLineType(reader, out detectedNewlineType);
			var allLines = text
				   .Split(newLines, StringSplitOptions.None)
				   .ToList();
			_ModifyContents(allLines, startLine, deleteLines, insertContents);
			var result = string.Join(detectedNewlineType, allLines);
			return result;
		}

		private void _ModifyContents(List<string> contents, long startLine, long deleteLines, string insertContents = null)
		{
			var newLines = new[] { "\r\n", "\n", "\r" };
			if (deleteLines > 0)
			{
				long actualDeleteLines = Math.Min(deleteLines, contents.Count - startLine + 1);
				contents.RemoveRange((int)startLine - 1, (int)actualDeleteLines);
			}
			if (insertContents != null)
			{
				// Always adjust to insert before the specified line
				int adjustedStartLine = (int)startLine - 1;
				var linesToInsert = insertContents.Split(newLines, StringSplitOptions.None);
				if (deleteLines == 0)
				{
					// Prepend insertContents to the specified line when not deleting
					contents[adjustedStartLine] = string.Join("", linesToInsert) + contents[adjustedStartLine];
				}
				else
				{
					// Insert after deletion or when adding new lines
					contents.InsertRange(adjustedStartLine, linesToInsert);
				}
			}
		}


		private void _DetectNewLineType(TextReader reader, out string newlineType)
		{
			newlineType = Environment.NewLine;
			char[] buffer = new char[1];
			char currentChar;
			char? lastChar = null;
			while (reader.Read(buffer, 0, 1) > 0)
			{
				currentChar = buffer[0];
				if (lastChar == '\r')
				{
					if (currentChar == '\n')
					{
						// Windows (CRLF)
						newlineType = "\r\n";
						return;
					}
					// Older Macs (CR)
					newlineType = "\r";
					break;
				}
				else if (currentChar == '\n')
				{
					// Unix/Linux (LF)
					newlineType = "\n";
					return;
				}
				lastChar = currentChar;
			}
			// Handling the case where the file ends with \r as the very last character.
			if (lastChar == '\r')
			{
				// Older Macs (CR)
				newlineType = "\r";
			}
		}


		/// <summary>
		/// Detect file encoding and new line type.
		/// </summary>
		private void DetectFileProperties(string path, out Encoding encoding, out string newlineType)
		{
			encoding = Encoding.Default;
			using (var reader = new StreamReader(path, detectEncodingFromByteOrderMarks: true))
			{
				encoding = reader.CurrentEncoding;
				_DetectNewLineType(reader, out newlineType);
			}
		}

		#endregion

	}
}
