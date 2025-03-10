namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class realtime_server_event_response_function_call_arguments_delta : realtime_server_event_response_audio_done
	{
		public string call_id { get; set; }

		public string delta { get; set; }

	}
}
