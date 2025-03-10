using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class create_upload_request : create_file_request
	{
		public string filename { get; set; }

		public int bytes { get; set; }

		public string mime_type { get; set; }

	}
}
