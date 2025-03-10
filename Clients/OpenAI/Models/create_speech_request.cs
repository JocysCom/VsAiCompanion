using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class create_speech_request : create_moderation_request
	{
		public string voice { get; set; }

		public string response_format { get; set; }

		public double speed { get; set; }

	}
}
