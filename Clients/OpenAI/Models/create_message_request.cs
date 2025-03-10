using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class create_message_request : chat_completion_request_function_message
	{
		public List<object> attachments { get; set; }

		public chat_completion_modalities metadata { get; set; }

	}
}
