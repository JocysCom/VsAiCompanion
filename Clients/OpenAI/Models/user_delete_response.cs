using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class user_delete_response : audit_log_actor_service_account
	{
		public string @object { get; set; }

		public bool deleted { get; set; }

	}
}
