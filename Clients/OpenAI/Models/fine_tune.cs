using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
    public class fine_tune : open_a_i_file
    {
        public List<fine_tune_event> events { get; set; }

        public string fine_tuned_model { get; set; }

        public object hyperparams { get; set; }

        public string model { get; set; }

        public string organization_id { get; set; }

        public List<open_a_i_file> result_files { get; set; }

        public List<open_a_i_file> training_files { get; set; }

        public int updated_at { get; set; }

        public List<open_a_i_file> validation_files { get; set; }

    }
}
