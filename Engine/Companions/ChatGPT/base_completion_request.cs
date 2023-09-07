using System.Collections.Generic;

namespace JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT
{
	public class base_completion_request : base_item
	{
		public string model { get; set; }
		public double? temperature { get; set; }
		public double? top_p { get; set; }
		public int? n { get; set; }
		public bool? stream { get; set; }
		public List<string> stop { get; set; }
		public int max_tokens { get; set; }
		public double? presence_penalty { get; set; }
		public double? frequency_penalty { get; set; }
		public object logit_bias { get; set; }
		public string user { get; set; }
	}
}
