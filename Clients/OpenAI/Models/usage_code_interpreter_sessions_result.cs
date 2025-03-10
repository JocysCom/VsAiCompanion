using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class usage_code_interpreter_sessions_result : list_models_response
	{
		public int num_sessions { get; set; }

		public string project_id { get; set; }

	}
}
