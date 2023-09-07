namespace JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT
{
	public class base_completion_response: base_item
	{
		public string id { get; set; }
		public string @object { get; set; }
		public int created { get; set; }
		public string model { get; set; }
		public usage usage { get; set; }

	}
}
