using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class chat_completion_response_message : chat_completion_stream_response_delta
	{
		public object audio { get; set; }

	}
}
