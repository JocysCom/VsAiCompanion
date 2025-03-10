namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class realtime_server_event_conversation_item_input_audio_transcription_completed : realtime_server_event_conversation_item_deleted
	{
		public int content_index { get; set; }

		public string transcript { get; set; }

	}
}
