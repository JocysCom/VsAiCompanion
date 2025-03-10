using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class create_vector_store_request : update_vector_store_request
	{
		public List<string> file_ids { get; set; }

		public object chunking_strategy { get; set; }

	}
}
