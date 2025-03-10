namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class vector_store_object : project
	{
		public int usage_bytes { get; set; }

		public object file_counts { get; set; }

		public vector_store_expiration_after expires_after { get; set; }

		public int? expires_at { get; set; }

		public int? last_active_at { get; set; }

		public chat_completion_modalities metadata { get; set; }

	}
}
