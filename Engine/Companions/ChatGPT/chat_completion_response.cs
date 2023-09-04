using System.Collections.Generic;

namespace JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT
{
	internal class chat_completion_response: base_item
	{
		public string id { get; set; }

		public string @object { get; set; }

		public int created { get; set; }

		public string model { get; set; }

		public List<choice> choices { get; set; } = new List<choice>();

		public usage usage { get; set; }


	}
}
