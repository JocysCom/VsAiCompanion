using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class realtime_server_event_input_audio_buffer_committed : realtime_server_event_conversation_item_deleted
	{
		public string previous_item_id { get; set; }

	}
}
