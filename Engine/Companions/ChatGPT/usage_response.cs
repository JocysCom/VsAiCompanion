namespace JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT
{
	public partial class usage_response: base_item
	{
		public int prompt_tokens { get; set; }
		public int completion_tokens { get; set; }
		public int total_tokens { get; set; }

	}

}
