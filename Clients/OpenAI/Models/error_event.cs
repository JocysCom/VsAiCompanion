using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class error_event : add_upload_part_request
	{
		public string @event { get; set; }

	}
}
