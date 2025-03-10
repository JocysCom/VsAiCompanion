using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class realtime_server_event_response_output_item_done : realtime_client_event_response_cancel
	{
		public int output_index { get; set; }

		public realtime_conversation_item_with_reference item { get; set; }

	}
}
