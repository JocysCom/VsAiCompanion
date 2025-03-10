using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class create_chat_completion_request : create_completion_request
	{
		public List<chat_completion_modalities> messages { get; set; }

		public bool store { get; set; }

		public reasoning_effort reasoning_effort { get; set; }

		public chat_completion_modalities metadata { get; set; }

		public int? top_logprobs { get; set; }

		public int? max_completion_tokens { get; set; }

		public chat_completion_modalities modalities { get; set; }

		public object prediction { get; set; }

		public object audio { get; set; }

		public string service_tier { get; set; }

		public List<chat_completion_tool> tools { get; set; }

		public chat_completion_modalities tool_choice { get; set; }

		public chat_completion_modalities parallel_tool_calls { get; set; }

		public object function_call { get; set; }

		public List<chat_completion_functions> functions { get; set; }

	}
}
