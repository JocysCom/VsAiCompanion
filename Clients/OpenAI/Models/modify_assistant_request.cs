using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class modify_assistant_request : create_transcription_request
	{
		public reasoning_effort reasoning_effort { get; set; }

		public string name { get; set; }

		public string description { get; set; }

		public string instructions { get; set; }

		public List<object> tools { get; set; }

		public object tool_resources { get; set; }

		public chat_completion_modalities metadata { get; set; }

		public double? top_p { get; set; }

	}
}
