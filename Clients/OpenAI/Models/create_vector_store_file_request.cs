using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class create_vector_store_file_request : add_upload_part_request
	{
		public string file_id { get; set; }

		public chat_completion_modalities chunking_strategy { get; set; }

	}
}
