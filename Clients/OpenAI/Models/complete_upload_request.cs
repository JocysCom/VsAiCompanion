using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class complete_upload_request : add_upload_part_request
	{
		public List<string> part_ids { get; set; }

		public string md5 { get; set; }

	}
}
