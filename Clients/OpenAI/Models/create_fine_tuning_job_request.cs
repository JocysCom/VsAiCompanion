using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
    public class create_fine_tuning_job_request : create_moderation_request
    {
        public string training_file { get; set; }

        public object hyperparameters { get; set; }

        public string suffix { get; set; }

        public string validation_file { get; set; }

    }
}
