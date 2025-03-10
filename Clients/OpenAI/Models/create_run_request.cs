using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class create_run_request : create_thread_and_run_request
	{
		public string additional_instructions { get; set; }

		public List<create_message_request> additional_messages { get; set; }

	}
}
