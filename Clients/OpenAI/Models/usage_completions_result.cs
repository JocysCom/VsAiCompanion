using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class usage_completions_result : usage_moderations_result
	{
		public int input_cached_tokens { get; set; }

		public int output_tokens { get; set; }

		public int input_audio_tokens { get; set; }

		public int output_audio_tokens { get; set; }

		public bool batch { get; set; }

	}
}
