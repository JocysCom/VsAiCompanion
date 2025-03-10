namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class costs_result : usage_code_interpreter_sessions_result
	{
		public object amount { get; set; }

		public string line_item { get; set; }

	}
}
