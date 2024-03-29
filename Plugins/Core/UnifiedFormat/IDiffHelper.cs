﻿namespace JocysCom.VS.AiCompanion.Plugins.Core.UnifiedFormat
{
	/// <summary>
	/// Diff helper.
	/// </summary>
	public interface IDiffHelper
	{
		/// <summary>
		/// Compares two files and returns a textual representation of the changes by using
		/// Eugene W.Myers AnO(ND) Difference Algorithm implemented by Google's The Diff Match and Patch library.
		/// This format is focused on efficient text manipulation, supporting insertions, deletions, and modifications,
		/// especially useful when bandwidth or storage is limited.
		/// </summary>
		/// <param name="originalFileFullName">The path to the original file.</param>
		/// <param name="modifiedFileFullName">The path to the modified file.</param>
		/// <returns>Unified diff string detailing the changes made from the original file to the modified file.</returns>
		[RiskLevel(RiskLevel.Medium)]
		string CompareFilesAndReturnChanges(string originalFileFullName, string modifiedFileFullName);

		/// <summary>
		/// Compares two contents and returns a textual representation of the changes by using
		/// Eugene W.Myers AnO(ND) Difference Algorithm implemented by Google's The Diff Match and Patch library.
		/// This format is focused on efficient text manipulation, supporting insertions, deletions, and modifications,
		/// especially useful when bandwidth or storage is limited.
		/// </summary>
		/// <param name="originalText">Original content.</param>
		/// <param name="modifiedText">Modified content.</param>
		/// <returns>Unified diff string detailing the changes made from the original content to the modified content.</returns>
		[RiskLevel(RiskLevel.Low)]
		string CompareContentsAndReturnChanges(string originalText, string modifiedText);

		/// <summary>
		/// Updates the content of the file by applying a series of changes. 
		/// These changes must be represented using the unified diff format, which is focused on efficient text manipulation.
		/// This format supports insertions, deletions, and modifications, particularly useful when bandwidth or storage is limited.
		/// The unified diff format is derived from the Myers Diff Algorithm, as implemented by Google's Diff Match and Patch library.
		/// </summary>
		/// <param name="fileFullName">The path to the file that needs to be updated.</param>
		/// <param name="unifiedDiff">Unified diff string representing the changes to apply,
		/// adhering to the Eugene W.Myers AnO(ND) Difference Algorithm implemented by Google's The Diff Match and Patch library.</param>
		/// <returns>'OK' if the operation was successful; otherwise, an error message.</returns>
		/// <example>
		/// The GNU Unified Diff Format example:
		/// 
		/// @@ -OriginalStart,OriginalLength +TargetStart,TargetLength @@
		/// -TextToDelete
		/// +TextToInsert
		/// 
		/// Placeholders:
		/// - OriginalStart is the 1-based starting line number of the original text segment.
		/// - OriginalLength is the number of lines in the original text segment affected by the patch.
		/// - TargetStart is the 1-based starting line number in the transformed (target) text where changes begin.
		/// - TargetLength is the number of lines in the transformed text resulting from the patch.
		/// - TextToDelete is the original text to be removed.
		/// - TextToInsert is the new text to be added.
		/// </example>
		[RiskLevel(RiskLevel.High)]
		string ModifyFile(string fileFullName, string unifiedDiff);

		/// <summary>
		/// Updates the content of the file by applying a series of changes. 
		/// These changes must be represented using the unified diff format, which is focused on efficient text manipulation.
		/// This format supports insertions, deletions, and modifications, particularly useful when bandwidth or storage is limited.
		/// The unified diff format is derived from the Myers Diff Algorithm, as implemented by Google's Diff Match and Patch library.
		/// </summary>
		/// <param name="contents">The contents that needs to be updated.</param>
		/// <param name="unifiedDiff">Unified diff string representing the changes to apply,
		/// adhering to the Eugene W.Myers AnO(ND) Difference Algorithm implemented by The Diff Match and Patch library.</param>
		/// <returns>'OK' if the operation was successful; otherwise, an error message.</returns>
		/// <example>
		/// The GNU Unified Diff Format example:
		/// 
		/// @@ -OriginalStart,OriginalLength +TargetStart,TargetLength @@
		/// -TextToDelete
		/// +TextToInsert
		/// 
		/// Placeholders:
		/// - OriginalStart is the 1-based starting line number of the original text segment.
		/// - OriginalLength is the number of lines in the original text segment affected by the patch.
		/// - TargetStart is the 1-based starting line number in the transformed (target) text where changes begin.
		/// - TargetLength is the number of lines in the transformed text resulting from the patch.
		/// - TextToDelete is the original text to be removed.
		/// - TextToInsert is the new text to be added.
		/// </example>
		[RiskLevel(RiskLevel.High)]
		string ModifyContents(string contents, string unifiedDiff);

		/*

		/// <summary>
		/// Updates the content of the text by applying a series of changes. 
		/// These changes must be represented using the unified diff format, which is focused on efficient text manipulation.
		/// This format supports insertions, deletions, and modifications, particularly useful when bandwidth or storage is limited.
		/// The unified diff format is derived from the Myers Diff Algorithm, as implemented by Google's Diff Match and Patch library.
		/// </summary>
		/// <param name="contents">The text contents that needs to be updated.</param>
		/// <param name="textPatches">Unified diff string representing the changes to apply,
		/// adhering to the Eugene W.Myers AnO(ND) Difference Algorithm implemented by The Diff Match and Patch library.</param>
		/// <returns>'OK' if the operation was successful; otherwise, an error message.</returns>
		/// <example>
		/// The GNU Unified Diff Format example:
		/// 
		/// @@ -OriginalStart,OriginalLength +TargetStart,TargetLength @@
		/// -TextToDelete
		/// +TextToInsert
		/// 
		/// Placeholders:
		/// - OriginalStart is the 1-based starting line number of the original text segment.
		/// - OriginalLength is the number of lines in the original text segment affected by the patch.
		/// - TargetStart is the 1-based starting line number in the transformed (target) text where changes begin.
		/// - TargetLength is the number of lines in the transformed text resulting from the patch.
		/// - TextToDelete is the original text to be removed.
		/// - TextToInsert is the new text to be added.
		/// </example>
		[RiskLevel(RiskLevel.Low)]
		string PatchText(string contents, TextPatch[] textPatches);

		*/

	}
}
