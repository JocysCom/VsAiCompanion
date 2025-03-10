using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class embedding_item : list_models_response
	{
		public int index { get; set; }

		public List<double> embedding { get; set; }

	}
}
