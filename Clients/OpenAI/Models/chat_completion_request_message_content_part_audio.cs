using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class chat_completion_request_message_content_part_audio : response_format_text
	{
		public object input_audio { get; set; }

	}
}
