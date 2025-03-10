using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class usage_images_result : usage_audio_speeches_result
	{
		public int images { get; set; }

		public string source { get; set; }

		public string size { get; set; }

	}
}
