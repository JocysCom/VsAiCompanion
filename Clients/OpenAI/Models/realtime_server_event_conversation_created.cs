using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class realtime_server_event_conversation_created : realtime_server_event_input_audio_buffer_cleared
	{
		public object conversation { get; set; }

	}
}
