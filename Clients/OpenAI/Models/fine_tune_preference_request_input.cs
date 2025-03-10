using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class fine_tune_preference_request_input : create_moderation_request
	{
		public List<object> preferred_completion { get; set; }

		public List<object> non_preferred_completion { get; set; }

	}
}
