namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class static_chunking_strategy : add_upload_part_request
	{
		public int max_chunk_size_tokens { get; set; }

		public int chunk_overlap_tokens { get; set; }

	}
}
