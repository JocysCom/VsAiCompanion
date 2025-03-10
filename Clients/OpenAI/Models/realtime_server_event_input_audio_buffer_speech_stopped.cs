using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class realtime_server_event_input_audio_buffer_speech_stopped : realtime_server_event_conversation_item_deleted
	{
		public int audio_end_ms { get; set; }

	}
}
