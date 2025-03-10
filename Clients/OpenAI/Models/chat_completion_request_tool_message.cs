using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class chat_completion_request_tool_message : prediction_content
	{
		public string role { get; set; }

		public string tool_call_id { get; set; }

	}
}
