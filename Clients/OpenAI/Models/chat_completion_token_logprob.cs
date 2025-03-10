using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class chat_completion_token_logprob : add_upload_part_request
	{
		public string token { get; set; }

		public double logprob { get; set; }

		public List<int> bytes { get; set; }

		public List<object> top_logprobs { get; set; }

	}
}
