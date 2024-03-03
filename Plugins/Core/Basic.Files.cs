using JocysCom.VS.AiCompanion.Plugins.Core.UnifiedFormat;
using JocysCom.VS.AiCompanion.Plugins.Core.VsFunctions;

namespace JocysCom.VS.AiCompanion.Plugins.Core
{
	public partial class Basic
	{
		#region File Operations

		/// <summary>
		/// Read file information and contents.
		/// </summary>
		/// <param name="path">The file to read from.</param>
		[RiskLevel(RiskLevel.Medium)]
		public static DocItem ReadFile(string path)
		{
			var di = new DocItem(null, path);
			di.LoadData();
			return di;
		}

		/// <summary>
		/// Write file text content on user computer.
		/// </summary>
		/// <param name="path">The file to write to.</param>
		/// <param name="contents">The string to write to the file.</param>
		/// <returns>True if the operation was successful.</returns>
		[RiskLevel(RiskLevel.High)]
		public static bool WriteFile(string path, string contents)
		{
			System.IO.File.WriteAllText(path, contents);
			return true;
		}

		#endregion

		#region IDiffHelper

		DiffHelper diffHelper = new DiffHelper();

		/// <inheritdoc/>
		public string CompareFilesAndReturnChanges(string originalFileFullName, string modifiedFileFullName)
			=> diffHelper.CompareFilesAndReturnChanges(originalFileFullName, modifiedFileFullName);

		/// <inheritdoc/>
		public string CompareContentsAndReturnChanges(string originalText, string modifiedText)
			=> diffHelper.CompareContentsAndReturnChanges(originalText, modifiedText);

		/// <inheritdoc/>
		public string ModifyFile(string fullFileName, string unifiedDiff)
			=> diffHelper.ModifyFile(fullFileName, unifiedDiff);


		/// <inheritdoc/>
		public string ModifyContents(string contents, string unifiedDiff)
			=> diffHelper.ModifyContents(contents, unifiedDiff);

		/*

		/// <inheritdoc/>
		public string PatchText(string contents, TextPatch[] textPatches)
			=> diffHelper.PatchText(contents, textPatches);

		*/

		#endregion

		#region IFileHelper

		FileHelper fileHelper = new FileHelper();

		/// <inheritdoc/>
		public string ModifyTextFile(string path, long startLine, long deleteLines, string insertContents = null)
			=> fileHelper.ModifyTextFile(path, startLine, deleteLines, insertContents);

		/// <inheritdoc/>
		public string ReadTextFile(string path, long offset = 0, long length = long.MaxValue)
			=> fileHelper.ReadTextFile(path, offset, length);

		/// <inheritdoc/>
		public string ReadTextFileLines(string path, long line = 1, long count = long.MaxValue)
			=> fileHelper.ReadTextFileLines(path, line, count);

		#endregion

	}
}
