using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class create_chat_completion_stream_response : create_completion_response
	{
		public string service_tier { get; set; }

	}
}
