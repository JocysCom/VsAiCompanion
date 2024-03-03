namespace JocysCom.VS.AiCompanion.Plugins.Core.UnifiedFormat
{
	/// <summary>
	/// Enumerates the types of operations that can be performed in a diff operation.
	/// These operations are key to understanding how one piece of text can be transformed into another.
	/// </summary>
	public enum OperationType
	{
		/// <summary>
		/// Delete operation: Specifies that the associated text should be removed from the original text.
		/// This operation subtracts text from the target document.
		/// </summary>
		DELETE,

		/// <summary>
		/// Insert operation: Indicates that the associated text should be inserted into the original text.
		/// This operation adds text to the target document.
		/// </summary>
		INSERT,

		/// <summary>
		/// Equal operation: Denotes that the associated text remains unchanged between the original and target texts.
		/// This operation signals no modification to the text at this segment.
		/// </summary>
		EQUAL
	}
}
