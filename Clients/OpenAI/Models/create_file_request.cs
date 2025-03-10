namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class create_file_request : add_upload_part_request
	{
		public string @file { get; set; }

		public string purpose { get; set; }

	}
}
