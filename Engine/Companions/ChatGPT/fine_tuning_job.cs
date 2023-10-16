using System.Collections.Generic;

namespace JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT
{
	public partial class fine_tuning_job : base_object
	{
		public error error { get; set; }
		public string fine_tuned_model { get; set; }
		public int? finished_at { get; set; }
		public hyperparameters hyperparameters { get; set; }
		public string model { get; set; }
		public string organization_id { get; set; }
		public List<string> result_files { get; set; }
		public int? trained_tokens { get; set; }
		public string training_file { get; set; }
		public string validation_file { get; set; }
	}
}
