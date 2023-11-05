using System.Collections.Generic;

namespace JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT
{
	public partial class fine_tune_request : base_item
	{
		public fine_tune_request() { }

		public fine_tune_request(string training_file, string model)
		{
			this.training_file = training_file;
			this.model = model;
		}

		public string training_file { get; set; }
		public string validation_file { get; set; }
		public string model { get; set; }
		public int? n_epochs { get; set; }
		public int? batch_size { get; set; }
		public double? learning_rate_multiplier { get; set; }
		public double? prompt_loss_weight { get; set; }
		public bool? compute_classification_metrics { get; set; }
		public int? classification_n_classes { get; set; }
		public string classification_positive_class { get; set; }
		public List<double> classification_betas { get; set; }
		public string suffix { get; set; }
	}
}
