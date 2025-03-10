using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class create_completion_request : create_image_edit_request
	{
		public int? best_of { get; set; }

		public bool echo { get; set; }

		public double? frequency_penalty { get; set; }

		public object logit_bias { get; set; }

		public int? logprobs { get; set; }

		public int? max_tokens { get; set; }

		public double? presence_penalty { get; set; }

		public long? seed { get; set; }

		public object stop { get; set; }

		public bool stream { get; set; }

		public chat_completion_stream_options stream_options { get; set; }

		public string suffix { get; set; }

		public double? temperature { get; set; }

		public double? top_p { get; set; }

	}
}
