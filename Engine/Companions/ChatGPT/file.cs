using System;

namespace JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT
{
	public class file : base_item
	{
		public string id { get; set; }
		public string @object { get; set; }
		public int bytes { get; set; }
		public DateTime created_at { get; set; }
		public string filename { get; set; }
		public string purpose { get; set; }
		public string status { get; set; }
		public string status_details { get; set; }
	}
}
