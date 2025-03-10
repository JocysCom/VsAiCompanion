using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class audit_log_actor_session : add_upload_part_request
	{
		public audit_log_actor_user user { get; set; }

		public string ip_address { get; set; }

	}
}
