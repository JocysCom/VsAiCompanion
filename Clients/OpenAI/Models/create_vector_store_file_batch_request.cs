using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class create_vector_store_file_batch_request : add_upload_part_request
	{
		public List<string> file_ids { get; set; }

		public chat_completion_modalities chunking_strategy { get; set; }

	}
}
