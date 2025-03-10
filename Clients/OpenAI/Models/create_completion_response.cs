using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class create_completion_response : create_embedding_response
	{
		public string id { get; set; }

		public List<object> choices { get; set; }

		public int created { get; set; }

		public string system_fingerprint { get; set; }

	}
}
