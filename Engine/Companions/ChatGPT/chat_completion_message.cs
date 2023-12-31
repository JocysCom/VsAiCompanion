﻿using System.ComponentModel;
using System.Text.Json.Serialization;

namespace JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT
{
	public class chat_completion_message: base_item
	{
		public chat_completion_message() { }

		public chat_completion_message(message_role role, string content)
		{
			this.role = role;
			this.content = content;
		}

		[JsonInclude]
		[JsonIgnore(Condition = JsonIgnoreCondition.Never)]
		public message_role role { get; set; }

		[JsonInclude]
		[JsonIgnore(Condition = JsonIgnoreCondition.Never)]
		public string content { get; set; }

		public string name { get; set; }

		[DefaultValue(function_call.none)]
		public function_call function_call { get; set; }

	}
}
