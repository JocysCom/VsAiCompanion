using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
    public class create_edit_response : list_paginated_fine_tuning_jobs_response
    {
        public List<object> choices { get; set; }

        public int created { get; set; }

        public completion_usage usage { get; set; }

    }
}
