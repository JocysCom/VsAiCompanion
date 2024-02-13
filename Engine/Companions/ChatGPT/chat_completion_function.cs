namespace JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT
{
	public class chat_completion_function : base_item
	{
		public string id { get; set; }
		public string name { get; set; }
		public string description { get; set; }
		public base_item parameters { get; set; }

	}
}
