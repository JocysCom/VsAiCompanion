using DiffMatchPatch;
using System;
using System.IO;

namespace JocysCom.VS.AiCompanion.Plugins.Core
{

	/// <summary>
	/// Diff helper.
	/// </summary>
	public class DiffHelper : IDiffHelper
	{

		/// <inheritdoc/>
		public string ApplyContentsChanges(string contents, string unifiedDiff)
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
		public string ApplyFileChanges(string fileFullName, string unifiedDiff)
		{
			if (!File.Exists(fileFullName))
				return "File not found";
			try
			{
				// Initialize diff-match-patch handler
				var dmp = new diff_match_patch();
				var originalContent = File.ReadAllText(fileFullName);
				var patchedContent = ApplyContentsChanges(originalContent, unifiedDiff);
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
		public string CompareContentsAndReturnChanges(string originalText, string modifiedText)
		{
			var dmp = new diff_match_patch();
			var patches = dmp.patch_make(originalText, modifiedText);
			var changes = dmp.patch_toText(patches);
			return changes;
		}

	}
}
