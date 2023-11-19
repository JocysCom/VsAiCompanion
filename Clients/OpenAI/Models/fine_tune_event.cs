namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
    public class fine_tune_event : list_paginated_fine_tuning_jobs_response
    {
        public int created_at { get; set; }

        public string level { get; set; }

        public string message { get; set; }

    }
}
