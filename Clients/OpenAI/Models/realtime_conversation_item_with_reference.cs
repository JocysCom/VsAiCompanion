using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class realtime_conversation_item_with_reference : invite
	{
		public string type { get; set; }

		public List<object> content { get; set; }

		public string call_id { get; set; }

		public string arguments { get; set; }

		public string output { get; set; }

	}
}
