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
	public class FileHelper : IFileHelper
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
				if (deleteLines > 0)
				{
					long actualDeleteLines = Math.Min(deleteLines, allLines.Count - startLine + 1);
					allLines.RemoveRange((int)startLine - 1, (int)actualDeleteLines);
				}
				if (insertContents != null)
				{
					// Adjust for insert after deletion operations
					int adjustedStartLine = deleteLines > 0 ? (int)startLine - 1 : (int)startLine;
					allLines.InsertRange(adjustedStartLine, insertContents.Split(newLines, StringSplitOptions.None));
				}
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
					var buffer = new char[length];
					var readLength = reader.ReadBlock(buffer, 0, (int)Math.Min(length, fileStream.Length - offset));
					// If no characters were read, return null to indicate that the operation was not successful.
					if (readLength == 0)
						return null;
					result = new string(buffer, 0, readLength);
				}
				return result;
			}
			catch (Exception)
			{
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


		/// <summary>
		/// Detect file encoding and new line type.
		/// </summary>
		private void DetectFileProperties(string path, out Encoding encoding, out string newlineType)
		{
			// Fallback to default encoding.
			encoding = Encoding.Default;
			// Default to Environment NewLine.
			newlineType = Environment.NewLine;
			using (var reader = new StreamReader(path, detectEncodingFromByteOrderMarks: true))
			{
				encoding = reader.CurrentEncoding;
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
		}

	}
}
