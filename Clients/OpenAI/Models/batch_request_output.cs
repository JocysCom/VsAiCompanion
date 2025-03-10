using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class batch_request_output : audit_log_actor_service_account
	{
		public string custom_id { get; set; }

		public object response { get; set; }

		public object error { get; set; }

	}
}
