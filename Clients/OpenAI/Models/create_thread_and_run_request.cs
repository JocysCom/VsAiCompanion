using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class create_thread_and_run_request : assistant_object
	{
		public string assistant_id { get; set; }

		public create_thread_request thread { get; set; }

		public bool stream { get; set; }

		public int? max_prompt_tokens { get; set; }

		public int? max_completion_tokens { get; set; }

		public object truncation_strategy { get; set; }

		public object tool_choice { get; set; }

		public chat_completion_modalities parallel_tool_calls { get; set; }

	}
}
