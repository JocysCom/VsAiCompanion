using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class transcription_word : add_upload_part_request
	{
		public string word { get; set; }

		public float start { get; set; }

		public float end { get; set; }

	}
}
