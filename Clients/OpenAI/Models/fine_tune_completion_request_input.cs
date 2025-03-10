using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class fine_tune_completion_request_input : add_upload_part_request
	{
		public string prompt { get; set; }

		public string completion { get; set; }

	}
}
