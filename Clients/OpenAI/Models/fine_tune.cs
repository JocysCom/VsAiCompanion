using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
    public class fine_tune : file
    {
        public List<fine_tune_event> events { get; set; }

        public string fine_tuned_model { get; set; }

        public object hyperparams { get; set; }

        public string model { get; set; }

        public string organization_id { get; set; }

        public List<file> result_files { get; set; }

        public List<file> training_files { get; set; }

        public int updated_at { get; set; }

        public List<file> validation_files { get; set; }

    }
}
