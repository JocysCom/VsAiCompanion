using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class default_project_error_response : add_upload_part_request
	{
		public int code { get; set; }

		public string message { get; set; }

	}
}
