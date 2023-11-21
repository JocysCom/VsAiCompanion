using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
    public class assistant_file_object : delete_assistant_file_response
    {
        public int created_at { get; set; }

        public string assistant_id { get; set; }

    }
}
