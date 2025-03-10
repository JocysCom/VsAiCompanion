using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class submit_tool_outputs_run_request : add_upload_part_request
	{
		public List<object> tool_outputs { get; set; }

		public bool stream { get; set; }

	}
}
