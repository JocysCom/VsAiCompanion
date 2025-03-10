using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class realtime_server_event_input_audio_buffer_speech_started : realtime_server_event_conversation_item_deleted
	{
		public int audio_start_ms { get; set; }

	}
}
