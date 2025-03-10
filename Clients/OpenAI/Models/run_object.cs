using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class run_object : create_run_request
	{
		public string thread_id { get; set; }

		public string status { get; set; }

		public object required_action { get; set; }

		public object last_error { get; set; }

		public int? expires_at { get; set; }

		public int? started_at { get; set; }

		public int? cancelled_at { get; set; }

		public int? failed_at { get; set; }

		public int? completed_at { get; set; }

		public object incomplete_details { get; set; }

	}
}
