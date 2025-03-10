using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class create_fine_tuning_job_request : batch_request_input
	{
		public object model { get; set; }

		public string training_file { get; set; }

		public object hyperparameters { get; set; }

		public string suffix { get; set; }

		public string validation_file { get; set; }

		public List<object> integrations { get; set; }

		public int? seed { get; set; }

	}
}
