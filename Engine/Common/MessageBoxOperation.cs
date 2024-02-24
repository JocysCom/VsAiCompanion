using System.ComponentModel;

namespace JocysCom.VS.AiCompanion.Engine
{
	public enum MessageBoxOperation
	{
		[Description("None")]
		None,
		[Description("Clear Message")]
		ClearMessage,
		[Description("Reset Message")]
		ResetMessage,
	}
}
