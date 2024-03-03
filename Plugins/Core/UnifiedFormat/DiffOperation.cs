namespace JocysCom.VS.AiCompanion.Plugins.Core.UnifiedFormat
{
	/// <summary>
	/// Represents a single difference operation between two sets of text.
	/// </summary>
	public class DiffOperation
	{
		/// <summary>
		/// The type of operation performed: INSERT (to add text), DELETE (to remove text), or EQUAL (indicating no change).
		/// </summary>
		public OperationType Operation { get; set; }

		/// <summary>
		/// The text content associated with this operation. For INSERT, this is the text to be added;
		/// for DELETE, it's the text to be removed; and for EQUAL, it's the text that remains unchanged.
		/// </summary>
		public string TextContent { get; set; }
	}
}
