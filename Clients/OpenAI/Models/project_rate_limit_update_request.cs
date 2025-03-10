namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class project_rate_limit_update_request : add_upload_part_request
	{
		public int max_requests_per_1_minute { get; set; }

		public int max_tokens_per_1_minute { get; set; }

		public int max_images_per_1_minute { get; set; }

		public int max_audio_megabytes_per_1_minute { get; set; }

		public int max_requests_per_1_day { get; set; }

		public int batch_1_day_max_input_tokens { get; set; }

	}
}
