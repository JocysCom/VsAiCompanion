using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class chat_completion_stream_response_delta : chat_completion_request_function_message
	{
		public object function_call { get; set; }

		public List<run_step_delta_step_details_tool_calls_function_object> tool_calls { get; set; }

		public string refusal { get; set; }

	}
}
