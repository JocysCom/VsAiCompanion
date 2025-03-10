namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class update_vector_store_request : modify_run_request
	{
		public string name { get; set; }

		public object expires_after { get; set; }

	}
}
