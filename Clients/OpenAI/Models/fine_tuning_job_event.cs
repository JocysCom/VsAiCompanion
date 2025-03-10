namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class fine_tuning_job_event : project
	{
		public string level { get; set; }

		public string message { get; set; }

		public string type { get; set; }

		public object data { get; set; }

	}
}
