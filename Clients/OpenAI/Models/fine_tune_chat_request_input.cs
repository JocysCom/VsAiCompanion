using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class fine_tune_chat_request_input : create_thread_request
	{
		public List<chat_completion_tool> tools { get; set; }

		public chat_completion_modalities parallel_tool_calls { get; set; }

		public List<chat_completion_functions> functions { get; set; }

	}
}
