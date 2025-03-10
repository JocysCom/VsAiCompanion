using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class fine_tuning_job_checkpoint : project
	{
		public string fine_tuned_model_checkpoint { get; set; }

		public int step_number { get; set; }

		public object metrics { get; set; }

		public string fine_tuning_job_id { get; set; }

	}
}
