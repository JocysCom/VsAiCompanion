using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class realtime_response : realtime_response_create_params
	{
		public string id { get; set; }

		public string @object { get; set; }

		public string status { get; set; }

		public object status_details { get; set; }

		public List<realtime_conversation_item_with_reference> output { get; set; }

		public object usage { get; set; }

		public string conversation_id { get; set; }

		public object max_output_tokens { get; set; }

	}
}
