using System.ComponentModel;

namespace JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT
{
	public class chat_completion_function : base_item
	{
		[DefaultValue(null)]
		public string id { get; set; }
		[DefaultValue(null)]
		public string name { get; set; }
		[DefaultValue(null)]
		public string description { get; set; }
		[DefaultValue(null)]
		public base_item parameters { get; set; }

	}
}
