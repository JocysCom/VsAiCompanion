using System;

namespace JocysCom.VS.AiCompanion.Plugins.Core.UnifiedFormat
{
	/// <summary>
	/// Represents a single patch operation, which is a cohesive set of differences (diffs) applied to
	/// transform one piece of text into another. This structure is central to efficiently conveying updates
	/// between versions of a text document.
	/// </summary>
	public class TextPatch
	{
		/// <summary>
		/// An array of diff operations that collectively define the transformation required to patch the text.
		/// Each element in this array represents a single change operation to be applied in sequence.
		/// </summary>
		public DiffOperation[] Operations { get; set; } = Array.Empty<DiffOperation>();

		/// <summary>
		/// The 1-based starting position in the original text where the patching begins. The numbering starts
		/// from 1, following the conventional human counting system, marking the beginning of the text segment to be patched.
		/// </summary>
		public int OriginalStart { get; set; }

		/// <summary>
		/// The 1-based starting position in the target text where the result of the patching operation begins to apply.
		/// This helps align the patch operations correctly with the target text.
		/// </summary>
		public int TargetStart { get; set; }

		/// <summary>
		/// The total number of characters from the original text that are encompassed by the patch. This length covers
		/// the exact segment within the original text that is affected by the applied patch.
		/// </summary>
		public int OriginalLength { get; set; }

		/// <summary>
		/// The total number of characters in the target or resultant text after the patch has been applied. This reflects
		/// the length of the text segment resulting from the transformation.
		/// </summary>
		public int TargetLength { get; set; }
	}
}
