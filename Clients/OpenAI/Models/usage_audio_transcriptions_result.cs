namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class usage_audio_transcriptions_result : costs_result
	{
		public int seconds { get; set; }

		public int num_model_requests { get; set; }

		public string user_id { get; set; }

		public string api_key_id { get; set; }

		public string model { get; set; }

	}
}
