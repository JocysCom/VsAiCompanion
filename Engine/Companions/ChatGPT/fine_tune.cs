using System.Collections.Generic;

namespace JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT
{
	public partial class fine_tune : base_item
	{
		public string id { get; set; }
		public string @object { get; set; }
		public int created_at { get; set; }
		public int updated_at { get; set; }
		public string model { get; set; }
		public string fine_tuned_model { get; set; }
		public string organization_id { get; set; }
		public string status { get; set; }
		public object hyperparams { get; set; } = new object();
		public List<file> training_files { get; set; } = new List<file>();
		public List<file> validation_files { get; set; } = new List<file>();
		public List<file> result_files { get; set; } = new List<file>();
		public List<fine_tune_event> events { get; set; } = new List<fine_tune_event>();
	}
}
