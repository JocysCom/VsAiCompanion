using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class thread_object : upload_part
	{
		public object tool_resources { get; set; }

		public chat_completion_modalities metadata { get; set; }

	}
}
