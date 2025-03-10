namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class run_step_delta_object : audit_log_actor_service_account
	{
		public string @object { get; set; }

		public object delta { get; set; }

	}
}
