using JocysCom.ClassLibrary.IO;
using System;
using System.Collections.Concurrent;
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
			string detectedNewlineType = GetNewLineType(text);
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

		/// <summary>
		/// Get new line type.
		/// </summary>
		public static string GetNewLineType(string text)
		{
			using (var reader = new StringReader(text))
			{
				var newLine = GetNewLineType(reader);
				reader.Close();
				return newLine;
			}
		}

		/// <summary>
		/// Get new line type.
		/// </summary>
		public static string GetNewLineType(TextReader reader)
		{
			var newlineType = Environment.NewLine;
			char[] buffer = new char[1];
			char currentChar;
			char? lastChar = null;
			while (reader.Read(buffer, 0, 1) > 0)
			{
				currentChar = buffer[0];
				if (lastChar == '\r')
				{
					if (currentChar == '\n')
						// Windows (CRLF)
						return "\r\n";
					// Older Macs (CR)
					return "\r";
				}
				else if (currentChar == '\n')
				{
					// Unix/Linux (LF)
					return "\n";
				}
				lastChar = currentChar;
			}
			// Handling the case where the file ends with \r as the very last character.
			if (lastChar == '\r')
			{
				// Older Macs (CR)
				newlineType = "\r";
			}
			return newlineType;
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
				newlineType = GetNewLineType(reader);
			}
		}

		#endregion

		#region File Finder

		/// <summary>
		///  Find files.
		/// </summary>
		public List<FileInfo> FindFiles(
			string path, string searchPattern, bool allDirectories = false,
			string includePatterns = null, string excludePatterns = null,
			bool useGitIgnore = false)
		{
			// Setup file finder.
			var fileFinder = new FileFinder();
			var IncludePatterns = GetIgnoreFromText(includePatterns);
			var ExcludePatterns = GetIgnoreFromText(excludePatterns);
			fileFinder.IsIgnored = (string parentPath, string filePath, long fileLength) =>
			{
				var relativePath = GetRelativePathToIgnore(parentPath, filePath);
				if (IncludePatterns != null && IsIgnored(relativePath, IncludePatterns) == false)
					return true;
				if (ExcludePatterns != null && IsIgnored(relativePath, ExcludePatterns) == true)
					return true;
				if (useGitIgnore && IsIgnored(filePath, true, true))
					return true;
				return false;
			};
			// Do search.
			var files = fileFinder.GetFiles(searchPattern, allDirectories, new[] { path });
			return files;
		}

		#endregion

		#region Ignore Files

		/// <summary>
		/// Creates an <see cref="Ignore.Ignore"/> object from the given text.
		/// </summary>
		/// <param name="text">The text content to parse into ignore rules.</param>
		/// <returns>
		/// An <see cref="Ignore.Ignore"/> object containing the parsed rules, or <c>null</c> if no valid rules were found.
		/// </returns>
		public Ignore.Ignore GetIgnoreFromText(string text)
		{
			if (text == null)
				return null;
			var ignore = new Ignore.Ignore();
			var lines = text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
			var containsRules = false;
			foreach (var line in lines)
			{
				if (string.IsNullOrWhiteSpace(line))
					continue;
				if (line.TrimStart().StartsWith("#"))
					continue;
				ignore.Add(line);
				containsRules = true;
			}
			return containsRules ? ignore : null;
		}

		/// <summary>
		/// Creates an <see cref="Ignore.Ignore"/> object from the specified file.
		/// </summary>
		/// <param name="path">The path to the file containing ignore rules.</param>
		/// <returns>
		/// An <see cref="Ignore.Ignore"/> object containing the parsed rules, or <c>null</c> if the file does not exist or contains no valid rules.
		/// </returns>
		public Ignore.Ignore GetIgnoreFromFile(string path)
		{
			var fi = new FileInfo(path);
			if (!fi.Exists)
				return null;
			var text = System.IO.File.ReadAllText(path);
			return GetIgnoreFromText(text);
		}

		/// <summary>
		/// Cache for <see cref="Ignore.Ignore"/> objects to improve performance.
		/// </summary>
		/// <remarks>Cache allows for this class to work 20 times faster.</remarks>
		private ConcurrentDictionary<string, Ignore.Ignore> IgnoreCache { get; } = new ConcurrentDictionary<string, Ignore.Ignore>();

		/// <summary>
		/// Retrieves an <see cref="Ignore.Ignore"/> object from the specified folder, optionally using a cache.
		/// </summary>
		/// <param name="path">The folder path to retrieve the ignore object from.</param>
		/// <param name="cache">If <c>true</c>, the cache will be used to retrieve the ignore object.</param>
		/// <returns>
		/// An <see cref="Ignore.Ignore"/> object retrieved from the folder.
		/// </returns>
		private Ignore.Ignore GetIgnoreFromFolder(string path, bool cache = true)
		{
			var ignore = cache
				? IgnoreCache.GetOrAdd(path, x => _GetIgnoreFromFolder(path))
				: _GetIgnoreFromFolder(path);
			return ignore;
		}

		private Ignore.Ignore _GetIgnoreFromFolder(string path)
		{
			var ignoreFullName = Path.Combine(path, ".gitignore");
			var ignore = GetIgnoreFromFile(ignoreFullName);
			return ignore;
		}

		/// <summary>
		/// Retrieves <see cref="Ignore.Ignore"/> objects by searching for <c>.gitignore</c> files in the specified folder and optionally its parent directories.
		/// </summary>
		/// <param name="path">The folder path to start the search.</param>
		/// <param name="searchParentDirectories">
		/// If <c>true</c>, searches all parent directories for <c>.gitignore</c> files; otherwise, only searches the specified folder.
		/// </param>
		/// <param name="cache">If <c>true</c>, the cache will be used to retrieve the ignore object.</param>
		/// <returns>
		/// A list of <see cref="Ignore.Ignore"/> objects found in the folder and optionally its parent directories.
		/// </returns>
		public Dictionary<string, Ignore.Ignore> GetIgnoresFromDirectory(string path, bool searchParentDirectories = false, bool cache = true)
		{
			var list = new Dictionary<string, Ignore.Ignore>();
			var di = new DirectoryInfo(path);
			while (di != null && di.Exists)
			{
				var ignore = GetIgnoreFromFolder(di.FullName, cache);
				if (ignore != null)
					list.Add(di.FullName, ignore);
				if (!searchParentDirectories)
					break;
				di = di.Parent;
			}
			return list;
		}

		/// <summary>
		/// Returns true if file is ignored.
		/// </summary>
		/// <param name="filePath">Full path to the file.</param>
		/// <param name="searchParentDirectories">If `true`, searches all parent directories for `.gitignore` files; otherwise, only searches the file folder.</param>
		/// <param name="cache">If `true`, the cache will be used to retrieve the ignore object.</param>
		public bool IsIgnored(string filePath, bool searchParentDirectories = false, bool cache = true)
		{
			var directoryPath = System.IO.Path.GetDirectoryName(filePath);
			var ignoreKvs = GetIgnoresFromDirectory(directoryPath, searchParentDirectories, cache);
			foreach (var ignoreKv in ignoreKvs)
			{
				var ignorePath = ignoreKv.Key;
				var relativeFilePath = GetRelativePathToIgnore(ignorePath, filePath);
				if (IsIgnored(relativeFilePath, ignoreKv.Value))
					return true;
			}
			return false;
		}


		/// <summary>
		/// Returns true if file is ignored.
		/// </summary>
		/// <param name="relativeFilePath">relative path to the file.</param>
		/// <param name="ignore">Ignore object.</param>
		public bool IsIgnored(string relativeFilePath, Ignore.Ignore ignore)
		{
			var currentPath = relativeFilePath;
			while (!string.IsNullOrEmpty(currentPath))
			{
				// Check if the current path or any of its parent directories are ignored.
				// Note: Must use `Contains`, because it won't ignore exact path.
				var ignored = ignore.OriginalRules.Contains(currentPath) || ignore.IsIgnored(currentPath);
				if (ignored)
					return true;
				// Move up to the parent directory.
				var parentPath = Path.GetDirectoryName(currentPath)?.Replace("\\", "/");
				// If we've reached the root directory, exit the loop.
				if (string.IsNullOrEmpty(parentPath) || parentPath == currentPath)
					break;
				currentPath = parentPath;
			}
			return false;
		}

		/// <summary>
		/// Get path relative to ignore,
		/// </summary>
		/// <param name="ignorePath"></param>
		/// <param name="filePath"></param>
		public string GetRelativePathToIgnore(string ignorePath, string filePath)
		{
			// Make sure relative to path have folder separator to indicate that it is folder.
			if (!ignorePath.EndsWith("\\"))
				ignorePath = ignorePath + "\\";
			var relativePath = PathHelper.GetRelativePath(ignorePath, filePath, false)
			// Ignore file use `/` folder separaror.
			.Replace("\\", "/");
			// Add slash to indicate that path is root to gitignore.
			if (!relativePath.StartsWith("/"))
				relativePath = $"/{relativePath}";
			return relativePath;
		}


		#endregion

	}
}
