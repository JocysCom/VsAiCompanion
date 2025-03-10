using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class audit_log : audit_log_actor_api_key
	{
		public int effective_at { get; set; }

		public object project { get; set; }

		public audit_log_actor actor { get; set; }

		public object api_key_created { get; set; }

		public object api_key_updated { get; set; }

		public object api_key_deleted { get; set; }

		public object invite_sent { get; set; }

		public object invite_accepted { get; set; }

		public object invite_deleted { get; set; }

		public object login_failed { get; set; }

		public object logout_failed { get; set; }

		public object organization_updated { get; set; }

		public object project_created { get; set; }

		public object project_updated { get; set; }

		public object project_archived { get; set; }

		public object rate_limit_updated { get; set; }

		public object rate_limit_deleted { get; set; }

		public object service_account_created { get; set; }

		public object service_account_updated { get; set; }

		public object service_account_deleted { get; set; }

		public object user_added { get; set; }

		public object user_updated { get; set; }

		public object user_deleted { get; set; }

	}
}
