namespace JocysCom.VS.AiCompanion.Plugins.Core
{

	/// <summary>
	/// Defines the access and execution levels for AI within applications,
	/// ranging from no permissions to full user-equivalent permissions.
	/// </summary>
	public enum RiskLevel : int
	{
		/// <summary>Unknown.</summary>
		Unknown,
		/// <summary>None: Read Internal. AI can only read data within the application.</summary>
		None,
		/// <summary>Low: Write Internal. AI can write or modify data within the application.</summary>
		Low,
		/// <summary>Medium: Read External. AI can read external data sources outside the application.</summary>
		Medium,
		/// <summary>High: Write External. AI can modify external data sources outside the application.</summary>
		High,
		/// <summary>Critical: Full Access. AI has full access, equivalent to a user, including running applications and scripts.</summary>
		Critical,
	}
}
