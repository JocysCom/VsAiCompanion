namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class chat_completion_functions : project_update_request
	{
		public string description { get; set; }

		public chat_completion_modalities parameters { get; set; }

	}
}
