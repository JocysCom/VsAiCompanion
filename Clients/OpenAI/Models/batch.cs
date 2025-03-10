namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class @batch : run_step_object
	{
		public string endpoint { get; set; }

		public object errors { get; set; }

		public string input_file_id { get; set; }

		public string completion_window { get; set; }

		public string output_file_id { get; set; }

		public string error_file_id { get; set; }

		public int in_progress_at { get; set; }

		public int finalizing_at { get; set; }

		public int cancelling_at { get; set; }

		public object request_counts { get; set; }

	}
}
