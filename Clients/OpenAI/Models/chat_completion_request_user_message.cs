using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class chat_completion_request_user_message : prediction_content
	{
		public string role { get; set; }

		public string name { get; set; }

	}
}
