using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class assistant_object : modify_assistant_request
	{
		public string id { get; set; }

		public int created_at { get; set; }

	}
}
