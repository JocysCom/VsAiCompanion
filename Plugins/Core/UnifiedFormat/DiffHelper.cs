using DiffMatchPatch;
using System;
using System.IO;
using System.Linq;

namespace JocysCom.VS.AiCompanion.Plugins.Core.UnifiedFormat
{

	/// <summary>
	/// Diff helper.
	/// </summary>
	public class DiffHelper : IDiffHelper
	{

		/// <inheritdoc/>
		public string CompareContentsAndReturnChanges(string originalText, string modifiedText)
		{
			var dmp = new diff_match_patch();
			var patches = dmp.patch_make(originalText, modifiedText);
			var changes = dmp.patch_toText(patches);
			return changes;
		}

		/// <inheritdoc/>
		public string CompareFilesAndReturnChanges(string originalFileFullName, string modifiedFileFullName)
		{
			// Read the contents of both files
			var originalText = File.ReadAllText(originalFileFullName);
			var modifiedText = File.ReadAllText(modifiedFileFullName);
			// Create changes.
			var dmp = new diff_match_patch();
			var patches = dmp.patch_make(originalText, modifiedText);
			var changes = dmp.patch_toText(patches);
			return changes;
		}

		/// <inheritdoc/>
		public string ModifyContents(string contents, string unifiedDiff)
		{
			// Initialize diff-match-patch handler
			var dmp = new diff_match_patch();
			// Parse the serialized patches
			var patches = dmp.patch_fromText(unifiedDiff);
			// Apply patches to the input content
			var result = dmp.patch_apply(patches, contents);
			// Result of patch application is the first element, cast appropriately
			return (string)result[0];
		}

		/// <inheritdoc />
		public string ModifyFile(string fileFullName, string unifiedDiff)
		{
			if (!File.Exists(fileFullName))
				return "File not found";
			try
			{
				// Initialize diff-match-patch handler
				var dmp = new diff_match_patch();
				var originalContent = File.ReadAllText(fileFullName);
				var patchedContent = ModifyContents(originalContent, unifiedDiff);
				// Write the patched content back to the file
				File.WriteAllText(fileFullName, patchedContent);
				return "OK";
			}
			catch (Exception ex)
			{
				// Appropriately log or handle exceptions here
				return ex.Message;
			}
		}


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
		/// @@ -{OriginalStart},{OriginalLength} +{TargetStart},{TargetLength} @@
		/// -{TextToDelete}
		/// +{TextToInsert}
		/// 
		/// Placeholders:
		/// - {OriginalStart} is the 1-based starting line number of the original text segment.
		/// - {OriginalLength} is the number of lines in the original text segment affected by the patch.
		/// - {TargetStart} is the 1-based starting line number in the transformed (target) text where changes begin.
		/// - {TargetLength} is the number of lines in the transformed text resulting from the patch.
		/// - {TextToDelete} is the original text to be removed.
		/// - {TextToInsert} is the new text to be added.
		/// </example>
		public string PatchText(string contents, TextPatch[] textPatches)
		{
			var patches = textPatches.Select(x => new Patch()
			{
				diffs = x.Operations
					.Select(o => new Diff((Operation)o.Operation, o.TextContent))
					.ToList(),
				start1 = x.OriginalStart,
				length1 = x.OriginalLength,
				start2 = x.TargetStart,
				length2 = x.TargetLength
			}).ToList();
			// Initialize diff-match-patch handler
			var dmp = new diff_match_patch();
			// Apply patches to the input content
			var result = dmp.patch_apply(patches.ToList(), contents);
			// Result of patch application is the first element, cast appropriately
			return (string)result[0];
		}

	}
}
