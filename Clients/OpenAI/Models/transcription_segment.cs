using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class transcription_segment : transcription_word
	{
		public int id { get; set; }

		public int seek { get; set; }

		public string text { get; set; }

		public List<int> tokens { get; set; }

		public float temperature { get; set; }

		public float avg_logprob { get; set; }

		public float compression_ratio { get; set; }

		public float no_speech_prob { get; set; }

	}
}
