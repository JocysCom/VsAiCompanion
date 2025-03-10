namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class chat_completion_tool : response_format_text
	{
		public function_object function { get; set; }

	}
}
