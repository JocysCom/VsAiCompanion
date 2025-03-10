using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class realtime_server_event_rate_limits_updated : realtime_server_event_input_audio_buffer_cleared
	{
		public List<object> rate_limits { get; set; }

	}
}
