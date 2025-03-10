using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class create_embedding_response : list_models_response
	{
		public string model { get; set; }

		public object usage { get; set; }

	}
}
