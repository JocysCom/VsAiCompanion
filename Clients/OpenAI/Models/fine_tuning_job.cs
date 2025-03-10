using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class fine_tuning_job : create_fine_tuning_job_request
	{
		public string id { get; set; }

		public int created_at { get; set; }

		public object error { get; set; }

		public string fine_tuned_model { get; set; }

		public int? finished_at { get; set; }

		public string @object { get; set; }

		public string organization_id { get; set; }

		public List<string> result_files { get; set; }

		public string status { get; set; }

		public int? trained_tokens { get; set; }

		public int? estimated_finish { get; set; }

	}
}
