using System;

namespace JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT
{
	public class base_object : base_item
	{
		public string id { get; set; }
		public string @object { get; set; }
		public DateTime created_at { get; set; }
		public string status { get; set; }
	}
}
