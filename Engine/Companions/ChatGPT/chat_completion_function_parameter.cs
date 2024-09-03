using System.Collections.Generic;
using System.ComponentModel;

namespace JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT
{
	public class chat_completion_function_parameter : base_item
	{
		[DefaultValue(null)] public string type { get; set; }
		[DefaultValue(null)] public Dictionary<string, object> properties { get; set; }
		[DefaultValue(null)] public List<string> required { get; set; }

	}
}
