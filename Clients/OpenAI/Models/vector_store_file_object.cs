using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class vector_store_file_object : vector_store_file_batch_object
	{
		public int usage_bytes { get; set; }

		public object last_error { get; set; }

		public object chunking_strategy { get; set; }

	}
}
