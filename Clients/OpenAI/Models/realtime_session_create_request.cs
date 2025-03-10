using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class realtime_session_create_request : realtime_response_create_params
	{
		public string input_audio_format { get; set; }

		public object input_audio_transcription { get; set; }

		public object turn_detection { get; set; }

	}
}
