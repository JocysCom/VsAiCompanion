namespace JocysCom.VS.AiCompanion.Plugins.Core.VsFunctions
{
	/// <summary>
	/// Code error information.
	/// </summary>
	public class ErrorItem
	{
		/// <summary>Error level</summary>
		public ErrorLevel ErrorLevel { get; set; }
		/// <summary>Project.</summary>
		public string Project { get; set; }
		/// <summary>Error file.</summary>
		public string File { get; set; }
		/// <summary>Error line.</summary>
		public int Line { get; set; }
		/// <summary>Error column.</summary>
		public int Column { get; set; }
		/// <summary>Error description.</summary>
		public string Description { get; set; }
		/// <summary>Error categories.</summary>
		public string[] Category { get; set; }
		/// <summary>Document file related to the error.</summary>
		public DocItem DocumentFile { get; set; }

	}
}
