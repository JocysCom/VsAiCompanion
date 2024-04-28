using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT
{
	public class chat_completion_message : base_item
	{
		public chat_completion_message() { }

		public chat_completion_message(message_role role, object content)
		{
			this.role = role;
			this.content = content;
		}

		[JsonInclude]
		[JsonIgnore(Condition = JsonIgnoreCondition.Never)]
		public message_role role { get; set; }

		[JsonInclude]
		[JsonIgnore(Condition = JsonIgnoreCondition.Never)]
		public object content { get; set; }

		public string name { get; set; }
		public List<chat_completion_message_tool_call> tool_calls { get; set; } = new List<chat_completion_message_tool_call>();

	}
}
