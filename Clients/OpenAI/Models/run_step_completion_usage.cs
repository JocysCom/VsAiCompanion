namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class run_step_completion_usage : add_upload_part_request
	{
		public int completion_tokens { get; set; }

		public int prompt_tokens { get; set; }

		public int total_tokens { get; set; }

	}
}
