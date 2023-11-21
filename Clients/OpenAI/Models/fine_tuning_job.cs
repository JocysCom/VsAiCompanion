using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
    public class fine_tuning_job : fine_tune
    {
        public int? finished_at { get; set; }

        public object hyperparameters { get; set; }

        public int? trained_tokens { get; set; }

        public string training_file { get; set; }

        public string validation_file { get; set; }

    }
}
