using DiffMatchPatch;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using System;
using System.IO;

namespace JocysCom.VS.AiCompanion.Shared
{

	public static class DiffHelper
	{
		/// <summary>
		/// Updates a file by applying a series of changes described in a 'diff' format. 
		/// This method is ideal for updating files efficiently when only changes are known,
		/// especially useful when bandwidth or storage is limited.
		/// </summary>
		/// <param name="filePath">The path to the file that needs to be updated.</param>
		/// <param name="changes">The string representation of the changes to apply.</param>
		/// <returns>'OK' if the operation was successful; otherwise, an error message.</returns>
		public static string PatchFile(string path, string changes)
		{
			if (!File.Exists(path))
				return "File not found";
			try
			{
				// Initialize diff-match-patch handler
				var dmp = new diff_match_patch();
				var originalContent = File.ReadAllText(path);
				var patchedContent = PatchContents(originalContent, changes);
				// Write the patched content back to the file
				File.WriteAllText(path, patchedContent);
				return "OK";
			}
			catch (Exception ex)
			{
				// Appropriately log or handle exceptions here
				return ex.Message;
			}
		}

		/// <summary>
		/// Updates a string by applying a series of changes described in a 'diff' format. 
		/// This method is ideal for updating files efficiently when only changes are known,
		/// especially useful when bandwidth or storage is limited.
		/// </summary>
		/// <param name="contents">The string that needs to be updated.</param>
		/// <param name="changes">The string representation of the changes to apply.</param>
		/// <returns>'OK' if the operation was successful; otherwise, an error message.</returns>
		public static string PatchContents(string contents, string changes)
		{
			// Initialize diff-match-patch handler
			var dmp = new diff_match_patch();
			// Parse the serialized patches
			var patches = dmp.patch_fromText(changes);
			// Apply patches to the input content
			var result = dmp.patch_apply(patches, contents);
			// Result of patch application is the first element, cast appropriately
			return (string)result[0];
		}

		/// <summary>
		/// Compares two files and returns a textual representation of the changes.
		/// </summary>
		/// <param name="originalFilePath">The path to the original file.</param>
		/// <param name="modifiedFilePath">The path to the modified file.</param>
		/// <returns>A string detailing the changes made from the original file to the modified file.</returns>
		public static string CompareFilesAndReturnChanges(string originalFilePath, string modifiedFilePath)
		{
			// Read the contents of both files
			string originalText = File.ReadAllText(originalFilePath);
			string modifiedText = File.ReadAllText(modifiedFilePath);
			// Create an instance of the inline diff builder
			var diffBuilder = new InlineDiffBuilder(new DiffPlex.Differ());
			// Generate the diff model
			var diff = diffBuilder.BuildDiffModel(originalText, modifiedText);
			// Use a StringBuilder to accumulate the results
			var result = new System.Text.StringBuilder();
			// Iterate through each line of the diff model
			foreach (var line in diff.Lines)
			{
				switch (line.Type)
				{
					case ChangeType.Inserted:
						result.AppendLine($"+ {line.Text}");
						break;
					case ChangeType.Deleted:
						result.AppendLine($"- {line.Text}");
						break;
					default:
						// Optionally include unchanged lines with a prefix, or omit this part to exclude unchanged lines
						// result.AppendLine($"  {line.Text}");
						break;
				}
			}
			return result.ToString();
		}

	}
}
