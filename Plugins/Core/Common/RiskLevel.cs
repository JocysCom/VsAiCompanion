namespace JocysCom.VS.AiCompanion.Plugins.Core
{

	/// <summary>Risk level</summary>
	public enum RiskLevel : int
	{
		/// <summary>Unknown.</summary>
		Unknown,
		/// <summary>None.</summary>
		None,
		/// <summary>Low. AI can read data.</summary>
		Low,
		/// <summary>Medium. AI can read and write or set data.</summary>
		Medium,
		/// <summary>High. AI can read, write and run scripts.</summary>
		High,
		/// <summary>Critical.</summary>
		Critical,
	}
}
