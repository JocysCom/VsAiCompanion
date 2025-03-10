using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class message_object : realtime_conversation_item_with_reference
	{
		public string thread_id { get; set; }

		public object incomplete_details { get; set; }

		public int? completed_at { get; set; }

		public int? incomplete_at { get; set; }

		public string assistant_id { get; set; }

		public string run_id { get; set; }

		public List<object> attachments { get; set; }

		public chat_completion_modalities metadata { get; set; }

	}
}
