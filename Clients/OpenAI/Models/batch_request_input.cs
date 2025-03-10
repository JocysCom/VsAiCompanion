namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class batch_request_input : add_upload_part_request
	{
		public string custom_id { get; set; }

		public string method { get; set; }

		public string url { get; set; }

	}
}
