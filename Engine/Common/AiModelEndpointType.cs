using System.ComponentModel;

namespace JocysCom.VS.AiCompanion.Engine
{
	public enum AiModelEndpointType
	{
		[Description("Auto-detect endpoint type")]
		Auto = 0,

		[Description("OpenAI Chat Completion endpoint")]
		OpenAI_Chat = 1,

		[Description("OpenAI Response endpoint (for o3-pro)")]
		OpenAI_Response = 2
	}
}
