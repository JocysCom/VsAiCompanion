namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class usage_vector_stores_result : list_models_response
	{
		public int usage_bytes { get; set; }

		public string project_id { get; set; }

	}
}
