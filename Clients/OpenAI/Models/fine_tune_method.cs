namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class fine_tune_method : assistant_tools_file_search
	{
		public fine_tune_supervised_method supervised { get; set; }

		public fine_tune_supervised_method dpo { get; set; }

	}
}
