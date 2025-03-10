using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class realtime_server_event_response_text_delta : realtime_server_event_response_audio_done
	{
		public string delta { get; set; }

	}
}
