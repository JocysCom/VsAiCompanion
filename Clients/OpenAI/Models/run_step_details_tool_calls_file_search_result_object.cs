using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class run_step_details_tool_calls_file_search_result_object : chat_completion_request_function_message
	{
		public string file_id { get; set; }

		public string file_name { get; set; }

		public double score { get; set; }

	}
}
