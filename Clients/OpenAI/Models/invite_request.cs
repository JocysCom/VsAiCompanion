using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class invite_request : audit_log_actor_user
	{
		public string role { get; set; }

		public List<object> projects { get; set; }

	}
}
