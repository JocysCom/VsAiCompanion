using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
    public class list_paginated_fine_tuning_jobs_response : list_fine_tune_events_response
    {
        public bool has_more { get; set; }

    }
}
