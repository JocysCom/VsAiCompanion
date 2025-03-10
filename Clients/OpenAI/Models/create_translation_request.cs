namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class create_translation_request : create_embedding_response
	{
		public string @file { get; set; }

		public string prompt { get; set; }

		public audio_response_format response_format { get; set; }

		public double temperature { get; set; }

	}
}
