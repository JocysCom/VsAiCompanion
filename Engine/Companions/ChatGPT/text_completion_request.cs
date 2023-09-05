namespace JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT
{
	public class text_completion_request: base_completion_request
	{
		public string prompt { get; set; }
		public string suffix { get; set; }
		public int? logprobs { get; set; }
		public bool? echo { get; set; }
		public int? best_of { get; set; }
	}
}
