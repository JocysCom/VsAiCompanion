using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
    public class list_runs_response : list_paginated_fine_tuning_jobs_response
    {
        public string first_id { get; set; }

        public string last_id { get; set; }

    }
}
