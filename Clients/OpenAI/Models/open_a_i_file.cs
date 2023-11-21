using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
    public class open_a_i_file : fine_tuning_job_event
    {
        public int bytes { get; set; }

        public string filename { get; set; }

        public string purpose { get; set; }

        public string status { get; set; }

        public string status_details { get; set; }

    }
}
