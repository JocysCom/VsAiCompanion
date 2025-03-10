using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class create_transcription_request : create_translation_request
	{
		public string language { get; set; }

		public List<string> timestamp_granularities { get; set; }

	}
}
