namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class vector_store_expiration_after : add_upload_part_request
	{
		public string anchor { get; set; }

		public int days { get; set; }

	}
}
