using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class modify_thread_request : modify_run_request
	{
		public object tool_resources { get; set; }

	}
}
