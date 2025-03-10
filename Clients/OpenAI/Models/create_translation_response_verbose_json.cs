using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class create_translation_response_verbose_json : create_translation_response_json
	{
		public string language { get; set; }

		public double duration { get; set; }

		public List<transcription_segment> segments { get; set; }

	}
}
