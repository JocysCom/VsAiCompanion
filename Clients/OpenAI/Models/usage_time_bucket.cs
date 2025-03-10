using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class usage_time_bucket : embedding_item
	{
		public int start_time { get; set; }

		public int end_time { get; set; }

		public List<object> result { get; set; }

	}
}
