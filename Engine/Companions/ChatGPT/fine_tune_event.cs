namespace JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT
{
	public partial class fine_tune_event: base_item
	{
		public string @object { get; set; }
		public int created_at { get; set; }
		public string level { get; set; }
		public string message { get; set; }
	}

}
