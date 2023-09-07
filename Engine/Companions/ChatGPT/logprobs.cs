using System.Collections.Generic;

namespace JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT
{
	public class logprobs: base_item
	{
		public List<string> tokens { get; set; }

		public List<double> token_logprobs { get; set; }

		public List<object> top_logprobs { get; set; }

		public List<int> text_offset { get; set; }

	}
}
