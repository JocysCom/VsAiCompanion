using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class vector_store_file_batch_object : project_service_account
	{
		public string vector_store_id { get; set; }

		public string status { get; set; }

		public object file_counts { get; set; }

	}
}
