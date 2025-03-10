using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class create_transcription_response_verbose_json : create_translation_response_verbose_json
	{
		public List<transcription_word> words { get; set; }

	}
}
