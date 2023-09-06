namespace JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT
{
	public class text_choice: base_item
	{
		public string text { get; set; }

		public int index { get; set; }

		public logprobs logprobs { get; set; }

		public choice_finish_reason? finish_reason { get; set; }

	}
}
