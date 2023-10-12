using System.Collections.Generic;

namespace JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT
{
	public partial class fine_tuning_jobs_response : base_item
	{
		public List<fine_tuning_job> data { get; set; }
		public bool has_more { get; set; }
		public string @object { get; set; }
	}
}
