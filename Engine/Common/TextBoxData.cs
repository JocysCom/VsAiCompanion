namespace JocysCom.VS.AiCompanion.Engine
{
	public class TextBoxData
	{
		public string Name { get; set; }
		public int SelectionStart { get; set; }
		public int SelectionLength { get; set; }
		// Used to check if text changed.
		public int TextLength { get; set; }
	}
}
