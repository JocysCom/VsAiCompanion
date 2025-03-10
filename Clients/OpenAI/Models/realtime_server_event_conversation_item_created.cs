namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class realtime_server_event_conversation_item_created : realtime_client_event_input_audio_buffer_append
	{
		public string previous_item_id { get; set; }

		public realtime_conversation_item_with_reference item { get; set; }

	}
}
