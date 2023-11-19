namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
    public class create_embedding_response : list_paginated_fine_tuning_jobs_response
    {
        public string model { get; set; }

        public object usage { get; set; }

    }
}
