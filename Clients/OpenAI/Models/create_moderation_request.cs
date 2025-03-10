using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class create_moderation_request : add_upload_part_request
	{
		public object input { get; set; }

		public object model { get; set; }

	}
}
