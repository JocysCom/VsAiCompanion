using System.Collections.Generic;
using System.ComponentModel;

namespace JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT
{
	public partial class chat_completion_request : base_completion_request

	{
		public List<chat_completion_message> messages { get; set; } = new List<chat_completion_message>();
		public List<chat_completion_tool> tools { get; set; }

		[DefaultValue(tool_choice.auto)]
		public tool_choice tool_choice { get; set; }
	}
}
