using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
    public class thread_object : delete_assistant_file_response
    {
        public int created_at { get; set; }

        public object metadata { get; set; }

    }
}
