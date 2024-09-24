using System.ComponentModel;

namespace JocysCom.VS.AiCompanion.Engine
{
	public enum ApiServiceType
	{
		[Description("")]
		None,
		[Description("OpenAI")]
		OpenAI,
		[Description("Microsoft Azure")]
		Azure,
		[Description("AI Plugin")]
		AiPlugin,
	}
}
