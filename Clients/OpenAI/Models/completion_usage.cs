namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class completion_usage : run_step_completion_usage
	{
		public object completion_tokens_details { get; set; }

		public object prompt_tokens_details { get; set; }

	}
}
