using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
    public class create_fine_tune_request : create_fine_tuning_job_request
    {
        public int? batch_size { get; set; }

        public List<double> classification_betas { get; set; }

        public int? classification_n_classes { get; set; }

        public string classification_positive_class { get; set; }

        public bool compute_classification_metrics { get; set; }

        public double? learning_rate_multiplier { get; set; }

        public double? prompt_loss_weight { get; set; }

    }
}
