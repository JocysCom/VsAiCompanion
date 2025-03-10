using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class realtime_response_create_params : create_speech_request
	{
		public List<string> modalities { get; set; }

		public string instructions { get; set; }

		public string output_audio_format { get; set; }

		public List<object> tools { get; set; }

		public string tool_choice { get; set; }

		public double temperature { get; set; }

		public object max_response_output_tokens { get; set; }

		public object conversation { get; set; }

		public chat_completion_modalities metadata { get; set; }

	}
}
