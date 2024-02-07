using System.ComponentModel;

namespace JocysCom.VS.AiCompanion.Engine
{
	public enum ToolCallApprovalProcess : int
	{
		[Description("User")]
		User = 0,
		[Description("Assistant and User")]
		UserWhenAssitantDenies,
		[Description("Assistant")]
		Assistant,
		[Description("Allow All")]
		AllowAll,
		[Description("Deny All")]
		DenyAll,
	}
}
