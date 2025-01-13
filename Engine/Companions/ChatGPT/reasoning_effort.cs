using System.ComponentModel;

namespace JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT
{
	public enum reasoning_effort
	{
		[Description("Low")]
		low = -1,
		[Description("Medium")]
		medium = 0,
		[Description("High")]
		high = 1,
	}
}
