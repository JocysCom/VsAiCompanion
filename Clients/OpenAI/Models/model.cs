using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
    public class model : delete_assistant_file_response
    {
        public int created { get; set; }

        public string owned_by { get; set; }

    }
}
