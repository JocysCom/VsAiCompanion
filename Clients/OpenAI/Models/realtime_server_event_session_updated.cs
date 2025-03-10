namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class realtime_server_event_session_updated : realtime_server_event_input_audio_buffer_cleared
	{
		public realtime_session session { get; set; }

	}
}
