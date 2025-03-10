using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class realtime_server_event_response_function_call_arguments_done : realtime_server_event_response_audio_done
	{
		public string call_id { get; set; }

		public string arguments { get; set; }

	}
}
