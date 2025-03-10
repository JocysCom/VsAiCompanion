using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class create_moderation_response : audit_log_actor_service_account
	{
		public string model { get; set; }

		public List<object> results { get; set; }

	}
}
