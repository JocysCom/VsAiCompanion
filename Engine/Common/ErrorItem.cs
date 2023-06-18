namespace JocysCom.VS.AiCompanion.Engine
{
	public class ErrorItem
	{
		public string Description { get; set; }
		public string File { get; set; }
		public int Line { get; set; }
		public int Column { get; set; }
		public string[] Category { get; set; }
	}
}
