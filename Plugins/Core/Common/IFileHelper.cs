using System;

namespace JocysCom.VS.AiCompanion.Plugins.Core
{
	/// <summary>
	/// File Helper.
	/// </summary>
	public interface IFileHelper
	{

		/// <summary>
		/// Modifies text content within a file by supporting line deletion, insertion, or updating through a combination of both.
		/// </summary>
		/// <param name="path">The full path to the text file to operate on.</param>
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
		/// The content to insert into the file. For deletion operations, this should be set to null.
		/// For update operations, this contains the new content replacing the deleted lines.
		/// </param>
		/// <returns>A string indicating the outcome of the operation. Returns "OK" if successful.</returns>
		/// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="startLine"/> is less than 1 or <paramref name="deleteLines"/> is negative.</exception>
		/// <example>
		/// Deleting lines:
		/// { path: "path/to/file.txt", startLine: 3, deleteLines: 2 }
		/// 
		/// Inserting lines:
		/// { path: "path/to/file.txt", startLine: 4, deleteLines: 0, insertContents: "new content\r\nto insert from line 4" }
		///
		/// Updating lines:
		/// { path: "path/to/file.txt", startLine: 4, deleteLines: 3, insertContents: "New content replacing lines 4-6" }
		/// </example>
		[RiskLevel(RiskLevel.High)]
		string ModifyTextFile(string path, long startLine, long deleteLines, string insertContents = null);

		/// <summary>
		/// Reads text content from a file on the user's computer.
		/// </summary>
		/// <param name="path">The file to read from.</param>
		/// <param name="offset">offset to start reading from. Value is 0-based. Default: 0.</param>
		/// <param name="length">The number of characters to read. Defaults to the remaining length of the file if not specified.</param>
		/// <returns>Null if the operation was not successful.</returns>
		[RiskLevel(RiskLevel.High)]
		string ReadTextFile(string path, long offset = 0, long length = long.MaxValue);

		/// <summary>
		/// Reads text content from a file on the user's computer.
		/// </summary>
		/// <param name="path">The file to read from.</param>
		/// <param name="line">Line number to start reading from. Value is 1-based. Default: 1.</param>
		/// <param name="count">The number of lines to read. Defaults to the remaining length of the lines if not specified.</param>
		/// <returns>Null if the operation was not successful.</returns>
		[RiskLevel(RiskLevel.High)]
		string ReadTextFileLines(string path, long line = 1, long count = long.MaxValue);

	}
}
