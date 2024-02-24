using System.ComponentModel;

namespace JocysCom.VS.AiCompanion.Engine
{
	public enum ToolCallApprovalProcess : int
	{
		[Description("User")]
		User = 0,
		[Description("User When AI Denies")]
		UserWhenAssitantDenies,
		[Description("AI Assitant")]
		Assistant,
		[Description("Allow All")]
		AllowAll,
		[Description("Deny All")]
		DenyAll,
	}
}
