using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class realtime_server_event_response_audio_done : realtime_server_event_conversation_item_input_audio_transcription_completed
	{
		public string response_id { get; set; }

		public int output_index { get; set; }

	}
}
