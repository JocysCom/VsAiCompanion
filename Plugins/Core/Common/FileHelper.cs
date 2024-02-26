using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace JocysCom.VS.AiCompanion.Plugins.Core
{
	/// <summary>
	/// File Helper
	/// </summary>
	public class FileHelper : IFileHelper
	{
		/// <inheritdoc/>
		public bool WriteTextFile(string path, string contents, long line, long column, string mode)
		{
			if (line < 1 || column < 1) throw new ArgumentOutOfRangeException("Line and column must be greater than 0.");
			if (mode != "insert" && mode != "overwrite")
				throw new ArgumentException("Mode must be either 'insert' or 'overwrite'.");
			// Read all lines of the file if it exists, otherwise, initialize an empty list.
			var allLines = File.Exists(path) ? File.ReadAllLines(path).ToList() : new List<string>();
			// Ensure the list has enough lines to start writing at the specified line.
			while (allLines.Count < line)
				allLines.Add("");  // Add empty lines as needed.
								   // Handling the specified column, ensuring the target line is long enough.
			string targetLine = allLines[(int)line - 1];
			while (targetLine.Length < column - 1)
				targetLine += " ";  // Add spaces as needed to reach the target column.
			if (mode == "overwrite")
			{
				// In case of overwrite, replace the content starting from the column.
				// If the contents to overwrite are longer than the original line part, it will extend the line.
				string beforeColumn = targetLine.Substring(0, (int)column - 1);
				string afterColumn = targetLine.Length > column + contents.Length - 1
					? targetLine.Substring((int)(column + contents.Length - 1))
					: "";
				allLines[(int)line - 1] = beforeColumn + contents + afterColumn;
			}
			else if (mode == "insert")
			{
				// In case of insert, insert the contents at the specified column, pushing existing content to the right.
				string beforeColumn = targetLine.Substring(0, (int)column - 1);
				string afterColumn = targetLine.Substring((int)column - 1);
				allLines[(int)line - 1] = beforeColumn + contents + afterColumn;
			}
			// Write the modified lines back to the file.
			File.WriteAllLines(path, allLines);
			return true;
		}

		/// <inheritdoc/>
		public string ReadTextFile(string path, long line, long column, long length)
		{
			if (line < 1 || column < 1)
				throw new ArgumentOutOfRangeException("Line and column must be greater than 0.");
			if (length < 0)
				throw new ArgumentOutOfRangeException("Length must be non-negative.");
			string result = null;
			using (var fileStream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
			using (var reader = new StreamReader(fileStream))
			{
				// Skip lines until the target line is reached.
				for (long currentLine = 1; currentLine < line; currentLine++)
					if (reader.ReadLine() == null)
						throw new ArgumentOutOfRangeException("Line exceeds the file's total lines.");
				// Read target line
				var lineContent = reader.ReadLine();
				if (lineContent == null)
					throw new ArgumentOutOfRangeException("Line exceeds the file's total lines.");
				// Check if the column exceeds the line's length.
				if (column > lineContent.Length)
					throw new ArgumentOutOfRangeException("Column exceeds the line's length.");
				// Calculate the actual length to read, considering the column position and requested length.
				long actualLength = Math.Min(length, lineContent.Length - column + 1);
				// Extract the specified substring from the line.
				result = lineContent.Substring((int)column - 1, (int)actualLength);
			}
			return result;
		}

	}
}
