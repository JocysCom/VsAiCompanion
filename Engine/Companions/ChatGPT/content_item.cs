namespace JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT
{
	public partial class content_item : base_item
	{
		public cotent_item_type @type { get; set; }
		public string text { get; set; }
		public image_url image_url { get; set; }
	}
}
